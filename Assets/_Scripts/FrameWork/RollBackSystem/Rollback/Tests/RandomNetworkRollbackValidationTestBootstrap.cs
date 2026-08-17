using ECSFrameWork;
using System;
using System.Collections.Generic;

namespace FrameWork.RollBackSystem
{
    /// <summary>
    /// 随机延迟、乱序、重复包、预测与权威修正组合压力验证。
    /// </summary>
    public static class RandomNetworkRollbackValidationTestBootstrap
    {
        private const int PlayerCount = 4;
        private const int TotalFrames = 10000;
        private const int MaxNetworkDelayFrames = 6;
        private const int DuplicateChancePercent = 5;

        private const int MinPredictedPlayerInputs = 20000;
        private const int MinMispredictedPlayerInputs = 10000;
        private const int MinMispredictedFrames = 5000;
        private const int MinRollbackCount = 5000;
        private const int MinDuplicatePackets = 1000;
        private const int MinOutOfOrderPackets = 1000;

        private const uint Seed = 20260817u;
        private const float TickLength = 1f / 60f;

        /// <summary>
        /// 在确定性 Fake Network 下运行四玩家 10000 帧网络预测与 Rollback 压力测试。
        /// </summary>
        public static RandomNetworkSimulationReport RunRandomNetworkStressTestStatic()
        {
            using var reference = CreateEnvironment(true);
            using var predicted = CreateEnvironment(true);

            var assembler = new FrameInputAssembler(new LastKnownPlayerInputPredictionPolicy());
            for (int playerID = 1; playerID <= PlayerCount; playerID++) assembler.RegisterPlayer(playerID);

            var network = new DeterministicInputNetworkSimulator(
                Seed ^ 0xD1B54A35u,
                MaxNetworkDelayFrames,
                DuplicateChancePercent);

            var frameComparer = new FrameInputSetComparer();
            var playerComparer = new PlayerInputSnapshotComparer();

            var authoritativeHistory = new FrameInputSet[TotalFrames + 1];
            var frameMispredicted = new bool[TotalFrames + 1];
            var authoritySubmitted = new bool[TotalFrames + 1];
            var latestAuthorityFrameByPlayer = new int[PlayerCount + 1];

            var authorityAccumulators = new Dictionary<int, FrameInputAccumulator>();
            var deliveredPackets = new List<FakeInputPacket>(PlayerCount * 2);
            var completedAuthorityFrames = new List<int>(16);

            int authoritySubmittedCount = 0;
            int predictedFrameCount = 0;
            int predictedPlayerInputCount = 0;
            int correctPredictedPlayerInputCount = 0;
            int mispredictedPlayerInputCount = 0;
            int mispredictedFrameCount = 0;
            int rollbackCount = 0;
            int unresolvedMispredictedFrames = 0;
            int maxPredictionAge = 0;
            int convergenceCheckpointCount = 0;

            for (int frame = 1; frame <= TotalFrames; frame++)
            {
                FrameInputSet authoritative = CreateAuthoritativeFrameInputSet(frame);
                authoritativeHistory[frame] = authoritative;
                network.ScheduleFrame(authoritative);

                completedAuthorityFrames.Clear();
                network.Deliver(frame, deliveredPackets);

                ProcessArrivals(
                    deliveredPackets,
                    assembler,
                    authorityAccumulators,
                    authoritySubmitted,
                    latestAuthorityFrameByPlayer,
                    completedAuthorityFrames);

                FrameInputAccumulator currentAccumulator = GetOrCreateAccumulator(authorityAccumulators, frame);
                FrameInputAssemblyResult assembly = assembler.Assemble(currentAccumulator);
                FrameInputSet predictedInput = assembly.InputSet;

                if (assembly.HasPrediction)
                {
                    predictedFrameCount++;

                    for (int i = 0; i < assembly.PredictedCount; i++)
                    {
                        int playerID = assembly.GetPredictedPlayerIDAt(i);
                        predictedPlayerInputCount++;

                        int lastKnownFrame = latestAuthorityFrameByPlayer[playerID];
                        int predictionAge = lastKnownFrame <= 0 ? frame : frame - lastKnownFrame;
                        if (predictionAge > maxPredictionAge) maxPredictionAge = predictionAge;

                        Expect(predictedInput.TryGetInput(playerID, out PlayerInputSnapshot predictedPlayerInput),
                            $"RandomNetwork Predicted Input Missing Error: Frame={frame}, PlayerID={playerID}");

                        Expect(authoritative.TryGetInput(playerID, out PlayerInputSnapshot authoritativePlayerInput),
                            $"RandomNetwork Authority Input Missing Error: Frame={frame}, PlayerID={playerID}");

                        if (playerComparer.IsEqual(predictedPlayerInput, authoritativePlayerInput))
                            correctPredictedPlayerInputCount++;
                        else mispredictedPlayerInputCount++;
                    }
                }

                bool frameMismatch = !frameComparer.IsEqual(predictedInput, authoritative);
                frameMispredicted[frame] = frameMismatch;

                if (frameMismatch)
                {
                    mispredictedFrameCount++;
                    unresolvedMispredictedFrames++;
                }

                DriveFrame(reference, frame, authoritative, true);
                DriveFrame(predicted, frame, predictedInput, true);

                bool hadOutstandingMismatch = unresolvedMispredictedFrames > 0;

                ProcessCompletedAuthorities(
                    frame,
                    predicted,
                    authoritativeHistory,
                    frameMispredicted,
                    authoritySubmitted,
                    authorityAccumulators,
                    completedAuthorityFrames,
                    frameComparer,
                    ref authoritySubmittedCount,
                    ref rollbackCount,
                    ref unresolvedMispredictedFrames);

                if (hadOutstandingMismatch && unresolvedMispredictedFrames == 0)
                {
                    AssertWorldEqual(reference, predicted, frame, $"Convergence Frame={frame}");
                    convergenceCheckpointCount++;
                }
                else if (unresolvedMispredictedFrames == 0 && frame % 1000 == 0)
                {
                    AssertWorldEqual(reference, predicted, frame, $"StableCheckpoint Frame={frame}");
                    convergenceCheckpointCount++;
                }
            }

            // 所有逻辑帧已经模拟完，但最后几帧的网络包可能仍在路上。
            // 此处只继续推进 Fake Network 时间，不推进 World.CurrentFrame。
            for (int networkFrame = TotalFrames + 1; networkFrame <= network.LastScheduledArrivalFrame; networkFrame++)
            {
                completedAuthorityFrames.Clear();
                network.Deliver(networkFrame, deliveredPackets);

                ProcessArrivals(
                    deliveredPackets,
                    assembler,
                    authorityAccumulators,
                    authoritySubmitted,
                    latestAuthorityFrameByPlayer,
                    completedAuthorityFrames);

                bool hadOutstandingMismatch = unresolvedMispredictedFrames > 0;

                ProcessCompletedAuthorities(
                    TotalFrames,
                    predicted,
                    authoritativeHistory,
                    frameMispredicted,
                    authoritySubmitted,
                    authorityAccumulators,
                    completedAuthorityFrames,
                    frameComparer,
                    ref authoritySubmittedCount,
                    ref rollbackCount,
                    ref unresolvedMispredictedFrames);

                if (hadOutstandingMismatch && unresolvedMispredictedFrames == 0)
                {
                    AssertWorldEqual(reference, predicted, TotalFrames, $"FlushConvergence NetworkFrame={networkFrame}");
                    convergenceCheckpointCount++;
                }
            }

            Expect(!network.HasPendingPackets,
                $"RandomNetwork PendingPacket Error: LastScheduledArrivalFrame={network.LastScheduledArrivalFrame}");

            Expect(authoritySubmittedCount == TotalFrames,
                $"RandomNetwork AuthoritySubmittedCount Error: Expected={TotalFrames}, Actual={authoritySubmittedCount}");

            Expect(unresolvedMispredictedFrames == 0,
                $"RandomNetwork UnresolvedMismatch Error: Actual={unresolvedMispredictedFrames}");

            Expect(predictedPlayerInputCount >= MinPredictedPlayerInputs,
                $"RandomNetwork PredictionCoverage Error: Expected>={MinPredictedPlayerInputs}, Actual={predictedPlayerInputCount}");

            Expect(mispredictedPlayerInputCount >= MinMispredictedPlayerInputs,
                $"RandomNetwork MispredictedInputCoverage Error: Expected>={MinMispredictedPlayerInputs}, Actual={mispredictedPlayerInputCount}");

            Expect(mispredictedFrameCount >= MinMispredictedFrames,
                $"RandomNetwork MispredictedFrameCoverage Error: Expected>={MinMispredictedFrames}, Actual={mispredictedFrameCount}");

            Expect(rollbackCount >= MinRollbackCount,
                $"RandomNetwork RollbackCoverage Error: Expected>={MinRollbackCount}, Actual={rollbackCount}");

            Expect(network.DuplicatePacketCount >= MinDuplicatePackets,
                $"RandomNetwork DuplicateCoverage Error: Expected>={MinDuplicatePackets}, Actual={network.DuplicatePacketCount}");

            Expect(network.OutOfOrderUniquePacketCount >= MinOutOfOrderPackets,
                $"RandomNetwork OutOfOrderCoverage Error: Expected>={MinOutOfOrderPackets}, Actual={network.OutOfOrderUniquePacketCount}");

            Expect(network.DelayedUniquePacketCount > 0,
                "RandomNetwork DelayCoverage Error: No Delayed Packets");

            Expect(correctPredictedPlayerInputCount > 0,
                "RandomNetwork CorrectPredictionCoverage Error: No Correct Predictions");

            Expect(maxPredictionAge >= 2,
                $"RandomNetwork PredictionAgeCoverage Error: Expected>=2, Actual={maxPredictionAge}");

            AssertWorldEqual(reference, predicted, TotalFrames, "RandomNetwork Final");

            return new RandomNetworkSimulationReport(
                Seed,
                PlayerCount,
                TotalFrames,
                MaxNetworkDelayFrames,
                network.UniquePacketCount,
                network.DelayedUniquePacketCount,
                network.DuplicatePacketCount,
                network.DeliveredPacketCount,
                network.DeliveredDuplicatePacketCount,
                network.OutOfOrderUniquePacketCount,
                predictedFrameCount,
                predictedPlayerInputCount,
                correctPredictedPlayerInputCount,
                mispredictedPlayerInputCount,
                mispredictedFrameCount,
                rollbackCount,
                maxPredictionAge,
                authoritySubmittedCount,
                convergenceCheckpointCount);
        }

        private static TestEnvironment CreateEnvironment(bool saveInitialSnapshot)
        {
            var world = new World { EnableSystemProfile = false };
            var players = new Entity[PlayerCount];
            var inputApplier = new FrameInputSetApplier();

            for (int i = 0; i < PlayerCount; i++)
            {
                int playerID = i + 1;
                Entity player = world.CreateEntity();
                players[i] = player;

                world.SetComponent(player, new PlayerTagComponent());
                world.SetComponent(player, new PlayerInputSnapshotComponent(0, playerID, 0f, 0f));
                world.SetComponent(player, new MoveSpeedComponent(2.5f + i * 0.25f));
                world.SetComponent(player, new VelocityComponent(0f, 0f, 0f));
                world.SetComponent(player, new PositionComponent(i * 4f, 0f, -i * 2f));

                inputApplier.RegisterPlayer(playerID, player);
            }

            world.AddSystem(new InputMoveSystem());
            world.AddSystem(new MovementSystem());

            var commandBuffer = new SimulationFrameCommandBuffer(512);
            var commandApplier = new SimulationFrameCommandApplier(world, commandBuffer, 512);

            var rollbackAdapter = new WorldRollbackAdapter<FrameInputSet>(world, world, inputApplier, null);
            rollbackAdapter.SetFrameCommandReplayBinding(
                new RollbackFrameCommandReplayBinding(commandBuffer, commandApplier));

            var snapshotBuffer = new SnapshotRingBuffer<EcsWorldSnapshot>(512);

            var coordinator = new RollbackCoordinator<FrameInputSet, EcsWorldSnapshot>(
                new InputBuffer<FrameInputSet>(),
                new AuthoritativeInputBuffer<FrameInputSet>(),
                snapshotBuffer,
                rollbackAdapter,
                new FrameInputSetComparer(),
                new ChecksumBuffer(),
                new AuthoritativeChecksumBuffer())
            {
                TickLength = TickLength
            };

            var environment = new TestEnvironment(
                world,
                players,
                coordinator,
                commandBuffer,
                commandApplier,
                snapshotBuffer);

            if (saveInitialSnapshot) coordinator.SaveSnapshot();
            return environment;
        }

        private static void ProcessArrivals(
            List<FakeInputPacket> packets,
            FrameInputAssembler assembler,
            Dictionary<int, FrameInputAccumulator> authorityAccumulators,
            bool[] authoritySubmitted,
            int[] latestAuthorityFrameByPlayer,
            List<int> completedAuthorityFrames)
        {
            for (int i = 0; i < packets.Count; i++)
            {
                FakeInputPacket packet = packets[i];
                PlayerInputSnapshot input = packet.Input;

                Expect(input.frameNumber > 0 && input.frameNumber <= TotalFrames,
                    $"RandomNetwork PacketFrame Error: InputFrame={input.frameNumber}, ArrivalFrame={packet.ArrivalFrame}");

                Expect(input.playerID >= 1 && input.playerID <= PlayerCount,
                    $"RandomNetwork PacketPlayer Error: Frame={input.frameNumber}, PlayerID={input.playerID}");

                assembler.ObserveAuthoritativeInput(in input);

                if (input.frameNumber > latestAuthorityFrameByPlayer[input.playerID])
                    latestAuthorityFrameByPlayer[input.playerID] = input.frameNumber;

                if (authoritySubmitted[input.frameNumber]) continue;

                FrameInputAccumulator accumulator = GetOrCreateAccumulator(authorityAccumulators, input.frameNumber);
                bool added = accumulator.TryAddInput(in input);

                if (added && accumulator.Count == PlayerCount)
                    completedAuthorityFrames.Add(input.frameNumber);
            }
        }

        private static void ProcessCompletedAuthorities(
            int currentFrame,
            TestEnvironment predicted,
            FrameInputSet[] authoritativeHistory,
            bool[] frameMispredicted,
            bool[] authoritySubmitted,
            Dictionary<int, FrameInputAccumulator> authorityAccumulators,
            List<int> completedAuthorityFrames,
            FrameInputSetComparer comparer,
            ref int authoritySubmittedCount,
            ref int rollbackCount,
            ref int unresolvedMispredictedFrames)
        {
            for (int i = 0; i < completedAuthorityFrames.Count; i++)
            {
                int authorityFrame = completedAuthorityFrames[i];

                if (authoritySubmitted[authorityFrame]) continue;

                Expect(authorityFrame <= currentFrame,
                    $"RandomNetwork FutureAuthority Error: CurrentFrame={currentFrame}, AuthorityFrame={authorityFrame}");

                Expect(authorityAccumulators.TryGetValue(authorityFrame, out FrameInputAccumulator accumulator),
                    $"RandomNetwork AuthorityAccumulator Missing Error: Frame={authorityFrame}");

                FrameInputSet authoritative = BuildCompleteFrameInputSet(accumulator);

                Expect(comparer.IsEqual(authoritative, authoritativeHistory[authorityFrame]),
                    $"RandomNetwork AuthorityReassembly Error: Frame={authorityFrame}");

                predicted.Coordinator.ReceiveAuthoritativeInput(authorityFrame, authoritative);

                Expect(predicted.Coordinator.CurrentFrame == currentFrame,
                    $"RandomNetwork Rollback CurrentFrame Error: AuthorityFrame={authorityFrame}, Expected={currentFrame}, Actual={predicted.Coordinator.CurrentFrame}");

                authoritySubmitted[authorityFrame] = true;
                authoritySubmittedCount++;
                authorityAccumulators.Remove(authorityFrame);

                if (!frameMispredicted[authorityFrame]) continue;

                rollbackCount++;
                unresolvedMispredictedFrames--;

                Expect(unresolvedMispredictedFrames >= 0,
                    $"RandomNetwork UnresolvedMismatch Underflow Error: AuthorityFrame={authorityFrame}");
            }
        }

        private static FrameInputAccumulator GetOrCreateAccumulator(
            Dictionary<int, FrameInputAccumulator> accumulators,
            int frame)
        {
            if (accumulators.TryGetValue(frame, out FrameInputAccumulator accumulator))
                return accumulator;

            accumulator = new FrameInputAccumulator(frame);
            accumulators.Add(frame, accumulator);
            return accumulator;
        }

        private static FrameInputSet BuildCompleteFrameInputSet(FrameInputAccumulator accumulator)
        {
            var inputs = new PlayerInputSnapshot[PlayerCount];

            for (int playerID = 1; playerID <= PlayerCount; playerID++)
            {
                Expect(accumulator.TryGetInput(playerID, out PlayerInputSnapshot input),
                    $"RandomNetwork CompleteAuthority MissingPlayer Error: Frame={accumulator.FrameNumber}, PlayerID={playerID}");

                inputs[playerID - 1] = input;
            }

            return new FrameInputSet(accumulator.FrameNumber, inputs);
        }

        private static FrameInputSet CreateAuthoritativeFrameInputSet(int frame)
        {
            var inputs = new PlayerInputSnapshot[PlayerCount];

            for (int i = 0; i < PlayerCount; i++)
                inputs[i] = CreatePlayerInput(frame, i + 1);

            return new FrameInputSet(frame, inputs);
        }

        private static PlayerInputSnapshot CreatePlayerInput(int frame, int playerID)
        {
            uint state = Seed ^
                unchecked((uint)frame * 0x9E3779B9u) ^
                unchecked((uint)playerID * 0x85EBCA6Bu);

            state = NextRandom(state);
            float moveX = (int)(state % 3u) - 1;

            state = NextRandom(state);
            float moveY = (int)(state % 3u) - 1;

            return new PlayerInputSnapshot(frame, playerID)
            {
                moveX = moveX,
                moveY = moveY
            };
        }

        private static uint NextRandom(uint value)
        {
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            return value;
        }

        private static void DriveFrame(TestEnvironment env, int frame, FrameInputSet input, bool saveSnapshot)
        {
            RollbackStepResult result = env.Coordinator.TryStep(frame, input);

            Expect(result.Succeeded,
                $"RandomNetwork DriveFrame Error: Frame={frame}, Kind={result.FailureKind}, Message={result.Message}");

            var context = new SimulationContext(frame, TickLength, false);

            env.CommandApplier.ApplyCommandsToWorld(
                frame,
                SimulationFrameCommandTiming.BeforeTick);

            env.World.Tick(in context);

            env.CommandApplier.ApplyCommandsToWorld(
                frame,
                SimulationFrameCommandTiming.AfterTick);

            if (saveSnapshot) env.Coordinator.SaveSnapshot();
        }

        private static void AssertWorldEqual(
            TestEnvironment reference,
            TestEnvironment predicted,
            int frame,
            string stage)
        {
            Expect(reference.Coordinator.CurrentFrame == predicted.Coordinator.CurrentFrame,
                $"{stage} CoordinatorFrame Error: Frame={frame}, Reference={reference.Coordinator.CurrentFrame}, Predicted={predicted.Coordinator.CurrentFrame}");

            Expect(reference.World.AliveEntityCount == predicted.World.AliveEntityCount,
                $"{stage} AliveEntityCount Error: Frame={frame}, Reference={reference.World.AliveEntityCount}, Predicted={predicted.World.AliveEntityCount}");

            Expect(reference.World.CreatedEntityCount == predicted.World.CreatedEntityCount,
                $"{stage} CreatedEntityCount Error: Frame={frame}, Reference={reference.World.CreatedEntityCount}, Predicted={predicted.World.CreatedEntityCount}");

            Expect(reference.Players.Length == predicted.Players.Length,
                $"{stage} PlayerCount Error: Frame={frame}, Reference={reference.Players.Length}, Predicted={predicted.Players.Length}");

            for (int i = 0; i < reference.Players.Length; i++)
            {
                Entity playerA = reference.Players[i];
                Entity playerB = predicted.Players[i];
                int playerID = i + 1;

                Expect(playerA == playerB,
                    $"{stage} EntityIdentity Error: Frame={frame}, PlayerID={playerID}, Reference={playerA}, Predicted={playerB}");

                Expect(reference.World.TryGetComponent(playerA, out PositionComponent positionA),
                    $"{stage} Reference Position Missing Error: Frame={frame}, PlayerID={playerID}");

                Expect(predicted.World.TryGetComponent(playerB, out PositionComponent positionB),
                    $"{stage} Predicted Position Missing Error: Frame={frame}, PlayerID={playerID}");

                ExpectFloatBits(positionA.x, positionB.x, frame, stage, $"P{playerID}.Position.X");
                ExpectFloatBits(positionA.y, positionB.y, frame, stage, $"P{playerID}.Position.Y");
                ExpectFloatBits(positionA.z, positionB.z, frame, stage, $"P{playerID}.Position.Z");

                Expect(reference.World.TryGetComponent(playerA, out VelocityComponent velocityA),
                    $"{stage} Reference Velocity Missing Error: Frame={frame}, PlayerID={playerID}");

                Expect(predicted.World.TryGetComponent(playerB, out VelocityComponent velocityB),
                    $"{stage} Predicted Velocity Missing Error: Frame={frame}, PlayerID={playerID}");

                ExpectFloatBits(velocityA.x, velocityB.x, frame, stage, $"P{playerID}.Velocity.X");
                ExpectFloatBits(velocityA.y, velocityB.y, frame, stage, $"P{playerID}.Velocity.Y");
                ExpectFloatBits(velocityA.z, velocityB.z, frame, stage, $"P{playerID}.Velocity.Z");

                Expect(reference.World.TryGetComponent(playerA, out PlayerInputSnapshotComponent inputA),
                    $"{stage} Reference Input Missing Error: Frame={frame}, PlayerID={playerID}");

                Expect(predicted.World.TryGetComponent(playerB, out PlayerInputSnapshotComponent inputB),
                    $"{stage} Predicted Input Missing Error: Frame={frame}, PlayerID={playerID}");

                Expect(inputA.inputFrame == inputB.inputFrame,
                    $"{stage} InputFrame Error: Frame={frame}, PlayerID={playerID}, Reference={inputA.inputFrame}, Predicted={inputB.inputFrame}");

                Expect(inputA.playerID == inputB.playerID,
                    $"{stage} InputPlayerID Error: Frame={frame}, PlayerID={playerID}, Reference={inputA.playerID}, Predicted={inputB.playerID}");

                ExpectFloatBits(inputA.moveX, inputB.moveX, frame, stage, $"P{playerID}.Input.MoveX");
                ExpectFloatBits(inputA.moveY, inputB.moveY, frame, stage, $"P{playerID}.Input.MoveY");
            }

            uint checksumA = WorldChecksumCalculator.Calculate(reference.World);
            uint checksumB = WorldChecksumCalculator.Calculate(predicted.World);

            Expect(checksumA == checksumB,
                $"{stage} Checksum Error: Frame={frame}, Reference=0x{checksumA:X8}, Predicted=0x{checksumB:X8}");
        }

        private static void ExpectFloatBits(float a, float b, int frame, string stage, string field)
        {
            int bitsA = BitConverter.SingleToInt32Bits(a);
            int bitsB = BitConverter.SingleToInt32Bits(b);

            Expect(bitsA == bitsB,
                $"{stage} {field} Error: Frame={frame}, Reference={a}({bitsA:X8}), Predicted={b}({bitsB:X8})");
        }

        private static void Expect(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        /// <summary>Fake Network 压力测试统计结果。</summary>
        public sealed class RandomNetworkSimulationReport
        {
            public readonly uint Seed;
            public readonly int PlayerCount;
            public readonly int TotalFrames;
            public readonly int MaxNetworkDelayFrames;
            public readonly int UniquePacketCount;
            public readonly int DelayedUniquePacketCount;
            public readonly int DuplicatePacketCount;
            public readonly int DeliveredPacketCount;
            public readonly int DeliveredDuplicatePacketCount;
            public readonly int OutOfOrderPacketCount;
            public readonly int PredictedFrameCount;
            public readonly int PredictedPlayerInputCount;
            public readonly int CorrectPredictedPlayerInputCount;
            public readonly int MispredictedPlayerInputCount;
            public readonly int MispredictedFrameCount;
            public readonly int RollbackCount;
            public readonly int MaxPredictionAge;
            public readonly int AuthoritySubmittedCount;
            public readonly int ConvergenceCheckpointCount;

            public RandomNetworkSimulationReport(
                uint seed,
                int playerCount,
                int totalFrames,
                int maxNetworkDelayFrames,
                int uniquePacketCount,
                int delayedUniquePacketCount,
                int duplicatePacketCount,
                int deliveredPacketCount,
                int deliveredDuplicatePacketCount,
                int outOfOrderPacketCount,
                int predictedFrameCount,
                int predictedPlayerInputCount,
                int correctPredictedPlayerInputCount,
                int mispredictedPlayerInputCount,
                int mispredictedFrameCount,
                int rollbackCount,
                int maxPredictionAge,
                int authoritySubmittedCount,
                int convergenceCheckpointCount)
            {
                Seed = seed;
                PlayerCount = playerCount;
                TotalFrames = totalFrames;
                MaxNetworkDelayFrames = maxNetworkDelayFrames;
                UniquePacketCount = uniquePacketCount;
                DelayedUniquePacketCount = delayedUniquePacketCount;
                DuplicatePacketCount = duplicatePacketCount;
                DeliveredPacketCount = deliveredPacketCount;
                DeliveredDuplicatePacketCount = deliveredDuplicatePacketCount;
                OutOfOrderPacketCount = outOfOrderPacketCount;
                PredictedFrameCount = predictedFrameCount;
                PredictedPlayerInputCount = predictedPlayerInputCount;
                CorrectPredictedPlayerInputCount = correctPredictedPlayerInputCount;
                MispredictedPlayerInputCount = mispredictedPlayerInputCount;
                MispredictedFrameCount = mispredictedFrameCount;
                RollbackCount = rollbackCount;
                MaxPredictionAge = maxPredictionAge;
                AuthoritySubmittedCount = authoritySubmittedCount;
                ConvergenceCheckpointCount = convergenceCheckpointCount;
            }

            public string ToDisplayString()
            {
                return
                    $"[RANDOM NETWORK STRESS]\n" +
                    $"Seed                     = {Seed}\n" +
                    $"Players                   = {PlayerCount}\n" +
                    $"Frames                    = {TotalFrames}\n" +
                    $"Max Network Delay         = {MaxNetworkDelayFrames}\n" +
                    $"Unique Packets            = {UniquePacketCount}\n" +
                    $"Delayed Unique Packets    = {DelayedUniquePacketCount}\n" +
                    $"Duplicate Packets         = {DuplicatePacketCount}\n" +
                    $"Delivered Packets         = {DeliveredPacketCount}\n" +
                    $"Delivered Duplicates      = {DeliveredDuplicatePacketCount}\n" +
                    $"Out-of-order Packets      = {OutOfOrderPacketCount}\n" +
                    $"Frames With Prediction    = {PredictedFrameCount}\n" +
                    $"Predicted Player Inputs   = {PredictedPlayerInputCount}\n" +
                    $"Correct Predictions       = {CorrectPredictedPlayerInputCount}\n" +
                    $"Mispredicted Inputs       = {MispredictedPlayerInputCount}\n" +
                    $"Mispredicted Frames       = {MispredictedFrameCount}\n" +
                    $"Rollback Corrections      = {RollbackCount}\n" +
                    $"Max Prediction Age        = {MaxPredictionAge}\n" +
                    $"Authority Frames          = {AuthoritySubmittedCount}\n" +
                    $"Convergence Checkpoints   = {ConvergenceCheckpointCount}";
            }
        }

        private sealed class TestEnvironment : IDisposable
        {
            public readonly World World;
            public readonly Entity[] Players;
            public readonly RollbackCoordinator<FrameInputSet, EcsWorldSnapshot> Coordinator;
            public readonly SimulationFrameCommandBuffer CommandBuffer;
            public readonly SimulationFrameCommandApplier CommandApplier;
            public readonly SnapshotRingBuffer<EcsWorldSnapshot> SnapshotBuffer;

            public TestEnvironment(
                World world,
                Entity[] players,
                RollbackCoordinator<FrameInputSet, EcsWorldSnapshot> coordinator,
                SimulationFrameCommandBuffer commandBuffer,
                SimulationFrameCommandApplier commandApplier,
                SnapshotRingBuffer<EcsWorldSnapshot> snapshotBuffer)
            {
                World = world;
                Players = players;
                Coordinator = coordinator;
                CommandBuffer = commandBuffer;
                CommandApplier = commandApplier;
                SnapshotBuffer = snapshotBuffer;
            }

            public void Dispose()
            {
                SnapshotBuffer.Clear();
                CommandBuffer.Clear();
                World.Dispose();
            }
        }
    }
}