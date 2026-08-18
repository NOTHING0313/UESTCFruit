using ECSFrameWork;
using FrameWork.RollBackSystem;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading;

namespace FrameWork.NetworkSync
{
    /// <summary>
    /// 双玩家真实 UDP 随机输入延迟、Prediction、Authority Correction 与 Rollback 压力验证。
    /// </summary>
    public static class RandomUdpRollbackStressValidationTestBootstrap
    {
        private const string LoopbackAddress = "127.0.0.1";
        private const uint SessionId = 0x11223344u;

        private const int Player1ID = 1;
        private const int Player2ID = 2;

        private const int TotalFrames = 2000;
        private const int MaxInputDelayFrames = 6;

        private const int MinDelayedInputCount = 1500;
        private const int MinMispredictedFrameCount = 1200;
        private const int MinOutOfOrderP2SendCount = 100;
        private const int MinOutOfOrderAuthorityCount = 100;

        private const uint Seed = 20260817u;
        private const int TimeoutMs = 2000;
        private const float TickLength = 1f / 60f;

        /// <summary>
        /// 连续 2000 帧随机延迟 P2 输入，通过真实 UDP Authority 反复修正预测历史并保持最终收敛。
        /// </summary>
        public static RandomUdpRollbackStressReport RunRandomUdpRollbackStressStatic()
        {
            using var reference = CreateEnvironment(true);
            using var predicted = CreateEnvironment(true);

            using var server = CreateServer();
            using var client1 = CreateClient(server.LocalEndPoint, Player1ID);
            using var client2 = CreateClient(server.LocalEndPoint, Player2ID);

            server.RegisterPlayer(Player1ID, client1.LocalEndPoint);
            server.RegisterPlayer(Player2ID, client2.LocalEndPoint);

            var assembler = new FrameInputAssembler(new LastKnownPlayerInputPredictionPolicy());
            assembler.RegisterPlayer(Player1ID);
            assembler.RegisterPlayer(Player2ID);

            var authorityDriver = new NetworkAuthorityRollbackDriver(
                assembler,
                predicted.Coordinator);

            var frameComparer = new FrameInputSetComparer();
            var playerComparer = new PlayerInputSnapshotComparer();

            var authoritativeHistory = new FrameInputSet[TotalFrames + 1];
            var frameMispredicted = new bool[TotalFrames + 1];
            var authorityReceived = new bool[TotalFrames + 1];

            var delayedP2Inputs = new List<PlayerInputSnapshot>[TotalFrames + MaxInputDelayFrames + 1];

            uint delayRandomState = Seed ^ 0xD1B54A35u;

            int zeroDelayInputCount = 0;
            int delayedInputCount = 0;
            int maxObservedDelay = 0;

            int predictedFrameCount = 0;
            int correctPredictionCount = 0;
            int mispredictedFrameCount = 0;
            int mismatchAuthorityCorrectionCount = 0;
            int unresolvedMispredictedFrames = 0;

            int authorityReceivedCount = 0;
            int convergenceCheckpointCount = 0;

            int p2OutOfOrderSendCount = 0;
            int highestSentP2Frame = 0;

            for (int frame = 1; frame <= TotalFrames; frame++)
            {
                PlayerInputSnapshot player1 = CreatePlayerInput(frame, Player1ID);
                PlayerInputSnapshot player2 = CreatePlayerInput(frame, Player2ID);

                FrameInputSet authoritative = new FrameInputSet(frame, new[]
                {
                    player1,
                    player2
                });

                authoritativeHistory[frame] = authoritative;

                int delay = NextRange(
                    ref delayRandomState,
                    0,
                    MaxInputDelayFrames);

                if (delay == 0) zeroDelayInputCount++;
                else delayedInputCount++;

                if (delay > maxObservedDelay) maxObservedDelay = delay;

                int p2SendFrame = frame + delay;

                delayedP2Inputs[p2SendFrame] ??= new List<PlayerInputSnapshot>();
                delayedP2Inputs[p2SendFrame].Add(player2);

                // Client1 当前只知道自己的真实输入。
                // P2 当前输入始终由 Prediction Layer 决定，之后由服务器 Authority 修正。
                var accumulator = new FrameInputAccumulator(frame);
                accumulator.TryAddInput(in player1);

                FrameInputAssemblyResult assembly = assembler.Assemble(accumulator);
                FrameInputSet predictedInput = assembly.InputSet;

                Expect(assembly.IsPredicted(Player2ID),
                    $"05B Prediction Coverage Error: Frame={frame}, P2 Must Be Predicted");

                Expect(!assembly.IsPredicted(Player1ID),
                    $"05B Prediction Coverage Error: Frame={frame}, P1 Must Be Real");

                predictedFrameCount++;

                Expect(predictedInput.TryGetInput(
                        Player2ID,
                        out PlayerInputSnapshot predictedP2),
                    $"05B Predicted P2 Missing Error: Frame={frame}");

                bool p2PredictionCorrect =
                    playerComparer.IsEqual(predictedP2, player2);

                if (p2PredictionCorrect) correctPredictionCount++;

                bool frameMismatch =
                    !frameComparer.IsEqual(predictedInput, authoritative);

                frameMispredicted[frame] = frameMismatch;

                if (frameMismatch)
                {
                    mispredictedFrameCount++;
                    unresolvedMispredictedFrames++;
                }

                // P1 始终立即走真实 UDP。
                client1.SendInput(in player1);

                List<PlayerInputSnapshot> dueP2Inputs =
                    delayedP2Inputs[frame];

                int dueP2Count = dueP2Inputs?.Count ?? 0;

                if (dueP2Inputs != null)
                {
                    for (int i = 0; i < dueP2Inputs.Count; i++)
                    {
                        PlayerInputSnapshot delayedInput = dueP2Inputs[i];

                        if (highestSentP2Frame > 0 &&
                           delayedInput.frameNumber < highestSentP2Frame)
                        {
                            p2OutOfOrderSendCount++;
                        }

                        if (delayedInput.frameNumber > highestSentP2Frame)
                            highestSentP2Frame = delayedInput.frameNumber;

                        client2.SendInput(in delayedInput);
                    }
                }

                // Reference 永远使用正确输入。
                DriveFrame(
                    reference,
                    frame,
                    authoritative,
                    true);

                // Predicted World 使用当前预测后的完整 FrameInputSet。
                DriveFrame(
                    predicted,
                    frame,
                    predictedInput,
                    true);

                int expectedServerDatagrams = 1 + dueP2Count;

                List<ServerAuthorityFramePacket> generatedAuthorities =
                    ProcessServerDatagrams(
                        server,
                        expectedServerDatagrams);

                Expect(
                    generatedAuthorities.Count == dueP2Count,
                    $"05B Server Authority Generation Error: Frame={frame}, Expected={dueP2Count}, Actual={generatedAuthorities.Count}");

                List<ServerAuthorityFramePacket> receivedAuthorities =
                    ReceiveAuthorities(
                        client1,
                        generatedAuthorities.Count);

                // Client2 当前没有 World，只负责排空 Server 广播。
                ReceiveAuthorities(
                    client2,
                    generatedAuthorities.Count);

                ApplyAuthorities(
                    receivedAuthorities,
                    authorityDriver,
                    authoritativeHistory,
                    frameMispredicted,
                    authorityReceived,
                    frameComparer,
                    reference,
                    predicted,
                    frame,
                    ref authorityReceivedCount,
                    ref mismatchAuthorityCorrectionCount,
                    ref unresolvedMispredictedFrames,
                    ref convergenceCheckpointCount);
            }

            // World 已停在 TotalFrames。
            // 最后最多还有 6 帧被延迟的 P2 Input 尚未发出。
            // 这里只继续网络时间，不继续 World.Tick。
            for (int networkFrame = TotalFrames + 1;
                networkFrame <= TotalFrames + MaxInputDelayFrames;
                networkFrame++)
            {
                List<PlayerInputSnapshot> dueP2Inputs =
                    delayedP2Inputs[networkFrame];

                int dueP2Count = dueP2Inputs?.Count ?? 0;

                if (dueP2Inputs != null)
                {
                    for (int i = 0; i < dueP2Inputs.Count; i++)
                    {
                        PlayerInputSnapshot delayedInput = dueP2Inputs[i];

                        if (highestSentP2Frame > 0 &&
                           delayedInput.frameNumber < highestSentP2Frame)
                        {
                            p2OutOfOrderSendCount++;
                        }

                        if (delayedInput.frameNumber > highestSentP2Frame)
                            highestSentP2Frame = delayedInput.frameNumber;

                        client2.SendInput(in delayedInput);
                    }
                }

                List<ServerAuthorityFramePacket> generatedAuthorities =
                    ProcessServerDatagrams(
                        server,
                        dueP2Count);

                Expect(
                    generatedAuthorities.Count == dueP2Count,
                    $"05B Flush Authority Generation Error: NetworkFrame={networkFrame}, Expected={dueP2Count}, Actual={generatedAuthorities.Count}");

                List<ServerAuthorityFramePacket> receivedAuthorities =
                    ReceiveAuthorities(
                        client1,
                        generatedAuthorities.Count);

                ReceiveAuthorities(
                    client2,
                    generatedAuthorities.Count);

                ApplyAuthorities(
                    receivedAuthorities,
                    authorityDriver,
                    authoritativeHistory,
                    frameMispredicted,
                    authorityReceived,
                    frameComparer,
                    reference,
                    predicted,
                    TotalFrames,
                    ref authorityReceivedCount,
                    ref mismatchAuthorityCorrectionCount,
                    ref unresolvedMispredictedFrames,
                    ref convergenceCheckpointCount);
            }

            Expect(server.ProcessedDatagramCount == TotalFrames * 2,
                $"05B Server Datagram Count Error: Expected={TotalFrames * 2}, Actual={server.ProcessedDatagramCount}");

            Expect(server.AuthorityFrameCount == TotalFrames,
                $"05B Server Authority Count Error: Expected={TotalFrames}, Actual={server.AuthorityFrameCount}");

            Expect(server.RejectedDatagramCount == 0,
                $"05B Server Reject Error: Count={server.RejectedDatagramCount}, Reason={server.LastRejectReason}, Decode={server.LastDecodeError}");

            Expect(client1.LastSentSequence == (uint)TotalFrames,
                $"05B Client1 Sequence Error: Expected={TotalFrames}, Actual={client1.LastSentSequence}");

            Expect(client2.LastSentSequence == (uint)TotalFrames,
                $"05B Client2 Sequence Error: Expected={TotalFrames}, Actual={client2.LastSentSequence}");

            Expect(authorityReceivedCount == TotalFrames,
                $"05B Authority Receive Count Error: Expected={TotalFrames}, Actual={authorityReceivedCount}");

            Expect(authorityDriver.AppliedAuthorityCount == TotalFrames,
                $"05B Authority Driver Count Error: Expected={TotalFrames}, Actual={authorityDriver.AppliedAuthorityCount}");

            Expect(unresolvedMispredictedFrames == 0,
                $"05B Unresolved Prediction Error: Actual={unresolvedMispredictedFrames}");

            Expect(delayedInputCount >= MinDelayedInputCount,
                $"05B Delay Coverage Error: Expected>={MinDelayedInputCount}, Actual={delayedInputCount}");

            Expect(maxObservedDelay == MaxInputDelayFrames,
                $"05B Max Delay Coverage Error: Expected={MaxInputDelayFrames}, Actual={maxObservedDelay}");

            Expect(mispredictedFrameCount >= MinMispredictedFrameCount,
                $"05B Misprediction Coverage Error: Expected>={MinMispredictedFrameCount}, Actual={mispredictedFrameCount}");

            Expect(mismatchAuthorityCorrectionCount == mispredictedFrameCount,
                $"05B Correction Coverage Error: Mispredicted={mispredictedFrameCount}, Corrections={mismatchAuthorityCorrectionCount}");

            Expect(p2OutOfOrderSendCount >= MinOutOfOrderP2SendCount,
                $"05B P2 OutOfOrder Coverage Error: Expected>={MinOutOfOrderP2SendCount}, Actual={p2OutOfOrderSendCount}");

            Expect(authorityDriver.OutOfOrderAuthorityCount >= MinOutOfOrderAuthorityCount,
                $"05B Authority OutOfOrder Coverage Error: Expected>={MinOutOfOrderAuthorityCount}, Actual={authorityDriver.OutOfOrderAuthorityCount}");

            Expect(correctPredictionCount > 0,
                "05B Correct Prediction Coverage Error: No Correct Predictions");

            AssertWorldEqual(
                reference,
                predicted,
                TotalFrames,
                "05B Final");

            return new RandomUdpRollbackStressReport(
                Seed,
                TotalFrames,
                MaxInputDelayFrames,
                zeroDelayInputCount,
                delayedInputCount,
                maxObservedDelay,
                predictedFrameCount,
                correctPredictionCount,
                mispredictedFrameCount,
                mismatchAuthorityCorrectionCount,
                p2OutOfOrderSendCount,
                authorityDriver.OutOfOrderAuthorityCount,
                authorityReceivedCount,
                convergenceCheckpointCount,
                server.ProcessedDatagramCount,
                server.AuthorityFrameCount);
        }

        private static void ApplyAuthorities(
            List<ServerAuthorityFramePacket> authorities,
            NetworkAuthorityRollbackDriver authorityDriver,
            FrameInputSet[] authoritativeHistory,
            bool[] frameMispredicted,
            bool[] authorityReceived,
            FrameInputSetComparer comparer,
            TestEnvironment reference,
            TestEnvironment predicted,
            int currentFrame,
            ref int authorityReceivedCount,
            ref int mismatchAuthorityCorrectionCount,
            ref int unresolvedMispredictedFrames,
            ref int convergenceCheckpointCount)
        {
            for (int i = 0; i < authorities.Count; i++)
            {
                ServerAuthorityFramePacket authority =
                    authorities[i];

                int authorityFrame =
                    authority.InputSet.frameNumber;

                Expect(
                    authorityFrame > 0 && authorityFrame <= TotalFrames,
                    $"05B Authority Frame Error: Frame={authorityFrame}");

                Expect(
                    !authorityReceived[authorityFrame],
                    $"05B Duplicate Authority Error: Frame={authorityFrame}");

                Expect(
                    comparer.IsEqual(
                        authority.InputSet,
                        authoritativeHistory[authorityFrame]),
                    $"05B Authority Data Error: Frame={authorityFrame}");

                bool hadOutstandingMismatch =
                    unresolvedMispredictedFrames > 0;

                authorityDriver.Apply(in authority);

                authorityReceived[authorityFrame] = true;
                authorityReceivedCount++;

                if (frameMispredicted[authorityFrame])
                {
                    mismatchAuthorityCorrectionCount++;
                    unresolvedMispredictedFrames--;

                    Expect(
                        unresolvedMispredictedFrames >= 0,
                        $"05B Unresolved Prediction Underflow Error: Frame={authorityFrame}");
                }

                // 只有所有已发生的错误历史都已经被 Authority 修正，
                // Predicted World 才必须立即与 Reference 收敛。
                if (hadOutstandingMismatch &&
                   unresolvedMispredictedFrames == 0)
                {
                    AssertWorldEqual(
                        reference,
                        predicted,
                        currentFrame,
                        $"05B Convergence AuthorityFrame={authorityFrame}");

                    convergenceCheckpointCount++;
                }
            }
        }

        private static TestEnvironment CreateEnvironment(bool saveInitialSnapshot)
        {
            var world = new World
            {
                EnableSystemProfile = false
            };

            var players = new Entity[2];

            Entity player1 = world.CreateEntity();
            players[0] = player1;

            world.SetComponent(
                player1,
                new PlayerTagComponent());

            world.SetComponent(
                player1,
                new PlayerInputSnapshotComponent(
                    0,
                    Player1ID,
                    0f,
                    0f));

            world.SetComponent(
                player1,
                new MoveSpeedComponent(3.25f));

            world.SetComponent(
                player1,
                new VelocityComponent(0f, 0f, 0f));

            world.SetComponent(
                player1,
                new PositionComponent(-5f, 0f, 0f));

            Entity player2 = world.CreateEntity();
            players[1] = player2;

            world.SetComponent(
                player2,
                new PlayerTagComponent());

            world.SetComponent(
                player2,
                new PlayerInputSnapshotComponent(
                    0,
                    Player2ID,
                    0f,
                    0f));

            world.SetComponent(
                player2,
                new MoveSpeedComponent(2.75f));

            world.SetComponent(
                player2,
                new VelocityComponent(0f, 0f, 0f));

            world.SetComponent(
                player2,
                new PositionComponent(5f, 0f, 0f));

            world.AddSystem(new InputMoveSystem());
            world.AddSystem(new MovementSystem());

            var inputApplier =
                new FrameInputSetApplier();

            inputApplier.RegisterPlayer(
                Player1ID,
                player1);

            inputApplier.RegisterPlayer(
                Player2ID,
                player2);

            var commandBuffer =
                new SimulationFrameCommandBuffer(512);

            var commandApplier =
                new SimulationFrameCommandApplier(
                    world,
                    commandBuffer,
                    512);

            var rollbackAdapter =
                new WorldRollbackAdapter<FrameInputSet>(
                    world,
                    world,
                    inputApplier,
                    null);

            rollbackAdapter.SetFrameCommandReplayBinding(
                new RollbackFrameCommandReplayBinding(
                    commandBuffer,
                    commandApplier));

            var snapshotBuffer =
                new SnapshotRingBuffer<EcsWorldSnapshot>(512);

            var coordinator =
                new RollbackCoordinator<
                    FrameInputSet,
                    EcsWorldSnapshot>(
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

            var environment =
                new TestEnvironment(
                    world,
                    players,
                    coordinator,
                    commandBuffer,
                    commandApplier,
                    snapshotBuffer);

            if (saveInitialSnapshot)
                coordinator.SaveSnapshot();

            return environment;
        }

        private static LocalNetworkInputServer CreateServer()
        {
            return new LocalNetworkInputServer(
                new UdpTransportConfig(
                    LoopbackAddress,
                    0,
                    NetworkProtocolConstants.MaxDatagramSize),
                SessionId);
        }

        private static LocalNetworkInputClient CreateClient(
            IPEndPoint serverEndPoint,
            int playerID)
        {
            return new LocalNetworkInputClient(
                new UdpTransportConfig(
                    LoopbackAddress,
                    0,
                    NetworkProtocolConstants.MaxDatagramSize),
                serverEndPoint,
                SessionId,
                playerID);
        }

        private static void DriveFrame(
            TestEnvironment env,
            int frame,
            FrameInputSet input,
            bool saveSnapshot)
        {
            RollbackStepResult result =
                env.Coordinator.TryStep(
                    frame,
                    input);

            Expect(
                result.Succeeded,
                $"05B DriveFrame Error: Frame={frame}, Kind={result.FailureKind}, Message={result.Message}");

            var context =
                new SimulationContext(
                    frame,
                    TickLength,
                    false);

            env.CommandApplier.ApplyCommandsToWorld(
                frame,
                SimulationFrameCommandTiming.BeforeTick);

            env.World.Tick(in context);

            env.CommandApplier.ApplyCommandsToWorld(
                frame,
                SimulationFrameCommandTiming.AfterTick);

            if (saveSnapshot)
                env.Coordinator.SaveSnapshot();
        }

        private static List<ServerAuthorityFramePacket> ProcessServerDatagrams(
            LocalNetworkInputServer server,
            int expectedDatagramCount)
        {
            var authorities =
                new List<ServerAuthorityFramePacket>(
                    Math.Max(1, expectedDatagramCount));

            if (expectedDatagramCount == 0)
                return authorities;

            int startProcessed =
                server.ProcessedDatagramCount;

            int targetProcessed =
                startProcessed + expectedDatagramCount;

            Stopwatch stopwatch =
                Stopwatch.StartNew();

            while (stopwatch.ElapsedMilliseconds < TimeoutMs)
            {
                int before =
                    server.ProcessedDatagramCount;

                if (server.TryProcessOneDatagram(
                    out ServerAuthorityFramePacket authority))
                {
                    authorities.Add(authority);
                }

                if (server.ProcessedDatagramCount >= targetProcessed)
                    return authorities;

                if (server.ProcessedDatagramCount == before)
                    Thread.Sleep(1);
            }

            throw new TimeoutException(
                $"05B Server Process Timeout: Expected={targetProcessed}, Actual={server.ProcessedDatagramCount}, Authority={server.AuthorityFrameCount}, Reject={server.RejectedDatagramCount}, Reason={server.LastRejectReason}, Decode={server.LastDecodeError}");
        }

        private static List<ServerAuthorityFramePacket> ReceiveAuthorities(
            LocalNetworkInputClient client,
            int expectedCount)
        {
            var authorities =
                new List<ServerAuthorityFramePacket>(expectedCount);

            if (expectedCount == 0)
                return authorities;

            Stopwatch stopwatch =
                Stopwatch.StartNew();

            while (stopwatch.ElapsedMilliseconds < TimeoutMs)
            {
                if (client.TryReceiveAuthority(
                    out ServerAuthorityFramePacket authority))
                {
                    authorities.Add(authority);

                    if (authorities.Count == expectedCount)
                        return authorities;

                    continue;
                }

                if (client.LastRejectReason !=
                   NetworkInputExchangeRejectReason.None)
                {
                    throw new InvalidOperationException(
                        $"05B Client Authority Reject Error: PlayerID={client.PlayerID}, Reason={client.LastRejectReason}, Decode={client.LastDecodeError}");
                }

                Thread.Sleep(1);
            }

            throw new TimeoutException(
                $"05B Client Authority Timeout: PlayerID={client.PlayerID}, Expected={expectedCount}, Actual={authorities.Count}, Endpoint={client.LocalEndPoint}");
        }

        private static PlayerInputSnapshot CreatePlayerInput(
            int frame,
            int playerID)
        {
            uint state =
                Seed ^
                unchecked((uint)frame * 0x9E3779B9u) ^
                unchecked((uint)playerID * 0x85EBCA6Bu);

            state = NextRandom(state);
            float moveX = (int)(state % 3u) - 1;

            state = NextRandom(state);
            float moveY = (int)(state % 3u) - 1;

            return new PlayerInputSnapshot(
                frame,
                playerID)
            {
                moveX = moveX,
                moveY = moveY
            };
        }

        private static int NextRange(
            ref uint state,
            int minInclusive,
            int maxInclusive)
        {
            state = NextRandom(state);

            return minInclusive +
                (int)(
                    state %
                    (uint)(
                        maxInclusive -
                        minInclusive +
                        1));
        }

        private static uint NextRandom(uint value)
        {
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            return value;
        }

        private static void AssertWorldEqual(
            TestEnvironment reference,
            TestEnvironment predicted,
            int frame,
            string stage)
        {
            Expect(
                reference.Coordinator.CurrentFrame ==
                predicted.Coordinator.CurrentFrame,
                $"{stage} CoordinatorFrame Error: Frame={frame}, Reference={reference.Coordinator.CurrentFrame}, Predicted={predicted.Coordinator.CurrentFrame}");

            Expect(
                reference.World.AliveEntityCount ==
                predicted.World.AliveEntityCount,
                $"{stage} AliveEntityCount Error: Frame={frame}, Reference={reference.World.AliveEntityCount}, Predicted={predicted.World.AliveEntityCount}");

            Expect(
                reference.World.CreatedEntityCount ==
                predicted.World.CreatedEntityCount,
                $"{stage} CreatedEntityCount Error: Frame={frame}, Reference={reference.World.CreatedEntityCount}, Predicted={predicted.World.CreatedEntityCount}");

            for (int i = 0; i < reference.Players.Length; i++)
            {
                int playerID = i + 1;

                AssertPlayerEqual(
                    reference,
                    predicted,
                    reference.Players[i],
                    predicted.Players[i],
                    frame,
                    $"{stage} P{playerID}");
            }

            uint checksumA =
                WorldChecksumCalculator.Calculate(
                    reference.World);

            uint checksumB =
                WorldChecksumCalculator.Calculate(
                    predicted.World);

            Expect(
                checksumA == checksumB,
                $"{stage} Checksum Error: Frame={frame}, Reference=0x{checksumA:X8}, Predicted=0x{checksumB:X8}");
        }

        private static void AssertPlayerEqual(
            TestEnvironment reference,
            TestEnvironment predicted,
            Entity referencePlayer,
            Entity predictedPlayer,
            int frame,
            string stage)
        {
            Expect(
                reference.World.TryGetComponent(
                    referencePlayer,
                    out PositionComponent positionA),
                $"{stage} Reference Position Missing Error: Frame={frame}");

            Expect(
                predicted.World.TryGetComponent(
                    predictedPlayer,
                    out PositionComponent positionB),
                $"{stage} Predicted Position Missing Error: Frame={frame}");

            ExpectFloatBits(
                positionA.x,
                positionB.x,
                frame,
                stage,
                "Position.X");

            ExpectFloatBits(
                positionA.y,
                positionB.y,
                frame,
                stage,
                "Position.Y");

            ExpectFloatBits(
                positionA.z,
                positionB.z,
                frame,
                stage,
                "Position.Z");

            Expect(
                reference.World.TryGetComponent(
                    referencePlayer,
                    out VelocityComponent velocityA),
                $"{stage} Reference Velocity Missing Error: Frame={frame}");

            Expect(
                predicted.World.TryGetComponent(
                    predictedPlayer,
                    out VelocityComponent velocityB),
                $"{stage} Predicted Velocity Missing Error: Frame={frame}");

            ExpectFloatBits(
                velocityA.x,
                velocityB.x,
                frame,
                stage,
                "Velocity.X");

            ExpectFloatBits(
                velocityA.y,
                velocityB.y,
                frame,
                stage,
                "Velocity.Y");

            ExpectFloatBits(
                velocityA.z,
                velocityB.z,
                frame,
                stage,
                "Velocity.Z");

            Expect(
                reference.World.TryGetComponent(
                    referencePlayer,
                    out PlayerInputSnapshotComponent inputA),
                $"{stage} Reference Input Missing Error: Frame={frame}");

            Expect(
                predicted.World.TryGetComponent(
                    predictedPlayer,
                    out PlayerInputSnapshotComponent inputB),
                $"{stage} Predicted Input Missing Error: Frame={frame}");

            Expect(
                inputA.inputFrame == inputB.inputFrame,
                $"{stage} InputFrame Error: Frame={frame}, Reference={inputA.inputFrame}, Predicted={inputB.inputFrame}");

            Expect(
                inputA.playerID == inputB.playerID,
                $"{stage} PlayerID Error: Frame={frame}, Reference={inputA.playerID}, Predicted={inputB.playerID}");

            ExpectFloatBits(
                inputA.moveX,
                inputB.moveX,
                frame,
                stage,
                "Input.MoveX");

            ExpectFloatBits(
                inputA.moveY,
                inputB.moveY,
                frame,
                stage,
                "Input.MoveY");
        }

        private static void ExpectFloatBits(
            float a,
            float b,
            int frame,
            string stage,
            string field)
        {
            int bitsA =
                BitConverter.SingleToInt32Bits(a);

            int bitsB =
                BitConverter.SingleToInt32Bits(b);

            Expect(
                bitsA == bitsB,
                $"{stage} {field} Error: Frame={frame}, Reference={a}({bitsA:X8}), Predicted={b}({bitsB:X8})");
        }

        private static void Expect(
            bool condition,
            string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        /// <summary>05B 随机真实 UDP 压力统计。</summary>
        public sealed class RandomUdpRollbackStressReport
        {
            public readonly uint Seed;
            public readonly int TotalFrames;
            public readonly int MaxInputDelayFrames;

            public readonly int ZeroDelayInputCount;
            public readonly int DelayedInputCount;
            public readonly int MaxObservedDelay;

            public readonly int PredictedFrameCount;
            public readonly int CorrectPredictionCount;
            public readonly int MispredictedFrameCount;
            public readonly int MismatchAuthorityCorrectionCount;

            public readonly int P2OutOfOrderSendCount;
            public readonly int OutOfOrderAuthorityCount;

            public readonly int AuthorityReceivedCount;
            public readonly int ConvergenceCheckpointCount;

            public readonly int ServerProcessedDatagramCount;
            public readonly int ServerAuthorityFrameCount;

            public RandomUdpRollbackStressReport(
                uint seed,
                int totalFrames,
                int maxInputDelayFrames,
                int zeroDelayInputCount,
                int delayedInputCount,
                int maxObservedDelay,
                int predictedFrameCount,
                int correctPredictionCount,
                int mispredictedFrameCount,
                int mismatchAuthorityCorrectionCount,
                int p2OutOfOrderSendCount,
                int outOfOrderAuthorityCount,
                int authorityReceivedCount,
                int convergenceCheckpointCount,
                int serverProcessedDatagramCount,
                int serverAuthorityFrameCount)
            {
                Seed = seed;
                TotalFrames = totalFrames;
                MaxInputDelayFrames = maxInputDelayFrames;
                ZeroDelayInputCount = zeroDelayInputCount;
                DelayedInputCount = delayedInputCount;
                MaxObservedDelay = maxObservedDelay;
                PredictedFrameCount = predictedFrameCount;
                CorrectPredictionCount = correctPredictionCount;
                MispredictedFrameCount = mispredictedFrameCount;
                MismatchAuthorityCorrectionCount = mismatchAuthorityCorrectionCount;
                P2OutOfOrderSendCount = p2OutOfOrderSendCount;
                OutOfOrderAuthorityCount = outOfOrderAuthorityCount;
                AuthorityReceivedCount = authorityReceivedCount;
                ConvergenceCheckpointCount = convergenceCheckpointCount;
                ServerProcessedDatagramCount = serverProcessedDatagramCount;
                ServerAuthorityFrameCount = serverAuthorityFrameCount;
            }

            public string ToDisplayString()
            {
                return
                    $"[RANDOM REAL UDP ROLLBACK STRESS]\n" +
                    $"Seed                         = {Seed}\n" +
                    $"Frames                       = {TotalFrames}\n" +
                    $"Max Input Delay              = {MaxInputDelayFrames}\n" +
                    $"Zero-delay P2 Inputs         = {ZeroDelayInputCount}\n" +
                    $"Delayed P2 Inputs            = {DelayedInputCount}\n" +
                    $"Max Observed Delay           = {MaxObservedDelay}\n" +
                    $"Predicted Frames             = {PredictedFrameCount}\n" +
                    $"Correct Predictions          = {CorrectPredictionCount}\n" +
                    $"Mispredicted Frames          = {MispredictedFrameCount}\n" +
                    $"Mismatch Corrections         = {MismatchAuthorityCorrectionCount}\n" +
                    $"Out-of-order P2 Sends        = {P2OutOfOrderSendCount}\n" +
                    $"Out-of-order Authorities     = {OutOfOrderAuthorityCount}\n" +
                    $"Authority Frames Received    = {AuthorityReceivedCount}\n" +
                    $"Convergence Checkpoints      = {ConvergenceCheckpointCount}\n" +
                    $"Server Datagrams             = {ServerProcessedDatagramCount}\n" +
                    $"Server Authority Frames      = {ServerAuthorityFrameCount}";
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