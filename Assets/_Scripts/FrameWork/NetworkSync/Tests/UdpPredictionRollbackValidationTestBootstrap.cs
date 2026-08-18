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
    /// 双玩家真实本地 UDP Authority 驱动 Prediction + Rollback 的组合验证。
    /// </summary>
    public static class UdpPredictionRollbackValidationTestBootstrap
    {
        private const string LoopbackAddress = "127.0.0.1";
        private const uint SessionId = 0x11223344u;
        private const int Player1ID = 1;
        private const int Player2ID = 2;

        private const uint Seed = 20260817u;
        private const int CorrectionFrame = 120;
        private const int PostRollbackFrames = 60;
        private const int TimeoutMs = 2000;
        private const float TickLength = 1f / 60f;

        /// <summary>
        /// P2 在 F120 的服务器输入延迟 authoritativeDelay 帧，
        /// 客户端先使用 LastKnown Prediction，随后通过真实 UDP Authority 触发 Rollback。
        /// </summary>
        public static void RunUdpPredictionRollbackStatic(int authoritativeDelay)
        {
            if (authoritativeDelay <= 0) throw new ArgumentOutOfRangeException(nameof(authoritativeDelay));

            int receiveFrame = CorrectionFrame + authoritativeDelay;
            int endFrame = receiveFrame + PostRollbackFrames;

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

            var authorityDriver = new NetworkAuthorityRollbackDriver(assembler, predicted.Coordinator);

            PlayerInputSnapshot delayedPlayer2Input = default;
            bool predictionObserved = false;
            bool divergenceObserved = false;
            bool correctionAuthorityObserved = false;
            bool rollbackConverged = false;
            int totalServerAuthorityCount = 0;

            for (int frame = 1; frame <= endFrame; frame++)
            {
                PlayerInputSnapshot player1 = CreatePlayerInput(frame, Player1ID);
                PlayerInputSnapshot player2 = CreatePlayerInput(frame, Player2ID);

                FrameInputSet authoritative = new FrameInputSet(frame, new[]
                {
                    player1,
                    player2
                });

                var localAccumulator = new FrameInputAccumulator(frame);
                localAccumulator.TryAddInput(in player1);

                if (frame != CorrectionFrame)
                    localAccumulator.TryAddInput(in player2);
                else
                    delayedPlayer2Input = player2;

                FrameInputAssemblyResult assembly = assembler.Assemble(localAccumulator);

                if (frame == CorrectionFrame)
                {
                    Expect(assembly.IsPredicted(Player2ID),
                        $"05A Prediction Error: Frame={frame}, P2 Must Be Predicted");

                    Expect(!assembly.IsPredicted(Player1ID),
                        $"05A Prediction Error: Frame={frame}, P1 Must Be Real");

                    Expect(assembly.PredictedCount == 1,
                        $"05A Prediction Count Error: Frame={frame}, Expected=1, Actual={assembly.PredictedCount}");

                    Expect(
                        assembly.InputSet.TryGetInput(Player2ID, out PlayerInputSnapshot predictedP2),
                        $"05A Predicted P2 Missing Error: Frame={frame}");

                    Expect(
                        !new PlayerInputSnapshotComparer().IsEqual(predictedP2, player2),
                        $"05A Prediction Coverage Error: Frame={frame}, Predicted P2 Accidentally Equals Authority");

                    predictionObserved = true;
                }

                // 两个真实 UDP Client 向服务器发送自己的 Input。
                client1.SendInput(in player1);

                if (frame != CorrectionFrame)
                    client2.SendInput(in player2);

                // 到指定帧后再发送 F120 的旧 P2 输入。
                // 当前帧 P2 先发送，旧 F120 后发送，故意允许旧 Authority 晚于新 Authority。
                if (frame == receiveFrame)
                    client2.SendInput(in delayedPlayer2Input);

                DriveFrame(reference, frame, authoritative, true);
                DriveFrame(predicted, frame, assembly.InputSet, true);

                if (frame == receiveFrame)
                {
                    AssertPlayerEqual(
                        reference,
                        predicted,
                        reference.Players[0],
                        predicted.Players[0],
                        frame,
                        $"05A PreRollback Delay={authoritativeDelay} P1");

                    ExpectPlayerStateDifferent(
                        reference,
                        predicted,
                        reference.Players[1],
                        predicted.Players[1],
                        frame,
                        authoritativeDelay);

                    divergenceObserved = true;
                }

                int expectedDatagrams = frame == CorrectionFrame ? 1 : 2;
                if (frame == receiveFrame) expectedDatagrams++;

                List<ServerAuthorityFramePacket> generatedAuthorities =
                    ProcessServerDatagrams(server, expectedDatagrams);

                totalServerAuthorityCount += generatedAuthorities.Count;

                List<ServerAuthorityFramePacket> clientAuthorities =
                    ReceiveAuthorities(client1, generatedAuthorities.Count, authorityDriver);

                // Client2 当前不挂 World，只负责把广播包排空，防止测试期间 Socket Receive Buffer 堆积。
                ReceiveAuthorities(client2, generatedAuthorities.Count, null);

                for (int i = 0; i < clientAuthorities.Count; i++)
                {
                    if (clientAuthorities[i].InputSet.frameNumber == CorrectionFrame)
                        correctionAuthorityObserved = true;
                }

                if (frame == receiveFrame)
                {
                    Expect(correctionAuthorityObserved,
                        $"05A Correction Authority Error: Delay={authoritativeDelay}, F{CorrectionFrame} Authority Was Not Received");

                    AssertWorldEqual(
                        reference,
                        predicted,
                        frame,
                        $"05A AfterUdpRollback Delay={authoritativeDelay}");

                    rollbackConverged = true;
                }
                else if (rollbackConverged)
                {
                    AssertWorldEqual(
                        reference,
                        predicted,
                        frame,
                        $"05A PostRollback Delay={authoritativeDelay}");
                }
                else if (frame < CorrectionFrame)
                {
                    AssertWorldEqual(
                        reference,
                        predicted,
                        frame,
                        $"05A PrePrediction Frame={frame}");
                }
            }

            Expect(predictionObserved,
                $"05A Prediction Execution Error: Delay={authoritativeDelay}");

            Expect(divergenceObserved,
                $"05A Divergence Execution Error: Delay={authoritativeDelay}");

            Expect(correctionAuthorityObserved,
                $"05A Authority Execution Error: Delay={authoritativeDelay}");

            Expect(rollbackConverged,
                $"05A Rollback Convergence Error: Delay={authoritativeDelay}");

            Expect(server.RejectedDatagramCount == 0,
                $"05A Server Reject Error: Delay={authoritativeDelay}, Count={server.RejectedDatagramCount}, Reason={server.LastRejectReason}, Decode={server.LastDecodeError}");

            Expect(totalServerAuthorityCount == endFrame,
                $"05A Server Authority Count Error: Delay={authoritativeDelay}, Expected={endFrame}, Actual={totalServerAuthorityCount}");

            Expect(authorityDriver.AppliedAuthorityCount == endFrame,
                $"05A Client Authority Count Error: Delay={authoritativeDelay}, Expected={endFrame}, Actual={authorityDriver.AppliedAuthorityCount}");

            AssertWorldEqual(reference, predicted, endFrame, $"05A Final Delay={authoritativeDelay}");
        }

        private static TestEnvironment CreateEnvironment(bool saveInitialSnapshot)
        {
            var world = new World { EnableSystemProfile = false };
            var players = new Entity[2];

            Entity player1 = world.CreateEntity();
            players[0] = player1;

            world.SetComponent(player1, new PlayerTagComponent());
            world.SetComponent(player1, new PlayerInputSnapshotComponent(0, Player1ID, 0f, 0f));
            world.SetComponent(player1, new MoveSpeedComponent(3.25f));
            world.SetComponent(player1, new VelocityComponent(0f, 0f, 0f));
            world.SetComponent(player1, new PositionComponent(-5f, 0f, 0f));

            Entity player2 = world.CreateEntity();
            players[1] = player2;

            world.SetComponent(player2, new PlayerTagComponent());
            world.SetComponent(player2, new PlayerInputSnapshotComponent(0, Player2ID, 0f, 0f));
            world.SetComponent(player2, new MoveSpeedComponent(2.75f));
            world.SetComponent(player2, new VelocityComponent(0f, 0f, 0f));
            world.SetComponent(player2, new PositionComponent(5f, 0f, 0f));

            world.AddSystem(new InputMoveSystem());
            world.AddSystem(new MovementSystem());

            var inputApplier = new FrameInputSetApplier();
            inputApplier.RegisterPlayer(Player1ID, player1);
            inputApplier.RegisterPlayer(Player2ID, player2);

            var commandBuffer = new SimulationFrameCommandBuffer(512);
            var commandApplier = new SimulationFrameCommandApplier(world, commandBuffer, 512);

            var rollbackAdapter = new WorldRollbackAdapter<FrameInputSet>(
                world,
                world,
                inputApplier,
                null);

            rollbackAdapter.SetFrameCommandReplayBinding(
                new RollbackFrameCommandReplayBinding(
                    commandBuffer,
                    commandApplier));

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

        private static LocalNetworkInputServer CreateServer()
        {
            return new LocalNetworkInputServer(
                new UdpTransportConfig(
                    LoopbackAddress,
                    0,
                    NetworkProtocolConstants.MaxDatagramSize),
                SessionId);
        }

        private static LocalNetworkInputClient CreateClient(IPEndPoint serverEndPoint, int playerID)
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

        private static void DriveFrame(TestEnvironment env, int frame, FrameInputSet input, bool saveSnapshot)
        {
            RollbackStepResult result = env.Coordinator.TryStep(frame, input);

            Expect(result.Succeeded,
                $"05A DriveFrame Error: Frame={frame}, Kind={result.FailureKind}, Message={result.Message}");

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

        private static List<ServerAuthorityFramePacket> ProcessServerDatagrams(LocalNetworkInputServer server, int expectedDatagramCount)
        {
            int startProcessed = server.ProcessedDatagramCount;
            int targetProcessed = startProcessed + expectedDatagramCount;

            var authorities = new List<ServerAuthorityFramePacket>(2);
            Stopwatch stopwatch = Stopwatch.StartNew();

            while (stopwatch.ElapsedMilliseconds < TimeoutMs)
            {
                int before = server.ProcessedDatagramCount;

                if (server.TryProcessOneDatagram(out ServerAuthorityFramePacket authority))
                    authorities.Add(authority);

                if (server.ProcessedDatagramCount >= targetProcessed)
                    return authorities;

                if (server.ProcessedDatagramCount == before)
                    Thread.Sleep(1);
            }

            throw new TimeoutException(
                $"05A Server Process Timeout: ExpectedProcessed={targetProcessed}, Actual={server.ProcessedDatagramCount}, Authority={server.AuthorityFrameCount}, Reject={server.RejectedDatagramCount}, Reason={server.LastRejectReason}, Decode={server.LastDecodeError}");
        }

        private static List<ServerAuthorityFramePacket> ReceiveAuthorities(LocalNetworkInputClient client, int expectedCount, NetworkAuthorityRollbackDriver driver)
        {
            var authorities = new List<ServerAuthorityFramePacket>(expectedCount);
            if (expectedCount == 0) return authorities;

            Stopwatch stopwatch = Stopwatch.StartNew();

            while (stopwatch.ElapsedMilliseconds < TimeoutMs)
            {
                if (client.TryReceiveAuthority(out ServerAuthorityFramePacket authority))
                {
                    authorities.Add(authority);
                    driver?.Apply(in authority);

                    if (authorities.Count == expectedCount)
                        return authorities;

                    continue;
                }

                if (client.LastRejectReason != NetworkInputExchangeRejectReason.None)
                {
                    throw new InvalidOperationException(
                        $"05A Client Authority Reject Error: PlayerID={client.PlayerID}, Reason={client.LastRejectReason}, Decode={client.LastDecodeError}");
                }

                Thread.Sleep(1);
            }

            throw new TimeoutException(
                $"05A Client Authority Timeout: PlayerID={client.PlayerID}, Expected={expectedCount}, Actual={authorities.Count}, Endpoint={client.LocalEndPoint}");
        }

        private static PlayerInputSnapshot CreatePlayerInput(int frame, int playerID)
        {
            // 明确保证 F119 的 LastKnown 与 F120 Authority 相反。
            if (playerID == Player2ID && frame == CorrectionFrame - 1)
            {
                return new PlayerInputSnapshot(frame, playerID)
                {
                    moveX = -1f,
                    moveY = 0f
                };
            }

            if (playerID == Player2ID && frame == CorrectionFrame)
            {
                return new PlayerInputSnapshot(frame, playerID)
                {
                    moveX = 1f,
                    moveY = 0f
                };
            }

            uint state =
                Seed ^
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

        private static void AssertWorldEqual(TestEnvironment reference, TestEnvironment predicted, int frame, string stage)
        {
            Expect(reference.Coordinator.CurrentFrame == predicted.Coordinator.CurrentFrame,
                $"{stage} CoordinatorFrame Error: Frame={frame}, Reference={reference.Coordinator.CurrentFrame}, Predicted={predicted.Coordinator.CurrentFrame}");

            Expect(reference.World.AliveEntityCount == predicted.World.AliveEntityCount,
                $"{stage} AliveEntityCount Error: Frame={frame}, Reference={reference.World.AliveEntityCount}, Predicted={predicted.World.AliveEntityCount}");

            Expect(reference.World.CreatedEntityCount == predicted.World.CreatedEntityCount,
                $"{stage} CreatedEntityCount Error: Frame={frame}, Reference={reference.World.CreatedEntityCount}, Predicted={predicted.World.CreatedEntityCount}");

            AssertPlayerEqual(
                reference,
                predicted,
                reference.Players[0],
                predicted.Players[0],
                frame,
                $"{stage} P1");

            AssertPlayerEqual(
                reference,
                predicted,
                reference.Players[1],
                predicted.Players[1],
                frame,
                $"{stage} P2");

            uint checksumA = WorldChecksumCalculator.Calculate(reference.World);
            uint checksumB = WorldChecksumCalculator.Calculate(predicted.World);

            Expect(checksumA == checksumB,
                $"{stage} Checksum Error: Frame={frame}, Reference=0x{checksumA:X8}, Predicted=0x{checksumB:X8}");
        }

        private static void AssertPlayerEqual(TestEnvironment reference, TestEnvironment predicted, Entity referencePlayer, Entity predictedPlayer, int frame, string stage)
        {
            Expect(reference.World.TryGetComponent(referencePlayer, out PositionComponent positionA),
                $"{stage} Reference Position Missing Error: Frame={frame}");

            Expect(predicted.World.TryGetComponent(predictedPlayer, out PositionComponent positionB),
                $"{stage} Predicted Position Missing Error: Frame={frame}");

            ExpectFloatBits(positionA.x, positionB.x, frame, stage, "Position.X");
            ExpectFloatBits(positionA.y, positionB.y, frame, stage, "Position.Y");
            ExpectFloatBits(positionA.z, positionB.z, frame, stage, "Position.Z");

            Expect(reference.World.TryGetComponent(referencePlayer, out VelocityComponent velocityA),
                $"{stage} Reference Velocity Missing Error: Frame={frame}");

            Expect(predicted.World.TryGetComponent(predictedPlayer, out VelocityComponent velocityB),
                $"{stage} Predicted Velocity Missing Error: Frame={frame}");

            ExpectFloatBits(velocityA.x, velocityB.x, frame, stage, "Velocity.X");
            ExpectFloatBits(velocityA.y, velocityB.y, frame, stage, "Velocity.Y");
            ExpectFloatBits(velocityA.z, velocityB.z, frame, stage, "Velocity.Z");

            Expect(reference.World.TryGetComponent(referencePlayer, out PlayerInputSnapshotComponent inputA),
                $"{stage} Reference Input Missing Error: Frame={frame}");

            Expect(predicted.World.TryGetComponent(predictedPlayer, out PlayerInputSnapshotComponent inputB),
                $"{stage} Predicted Input Missing Error: Frame={frame}");

            Expect(inputA.inputFrame == inputB.inputFrame,
                $"{stage} InputFrame Error: Frame={frame}, Reference={inputA.inputFrame}, Predicted={inputB.inputFrame}");

            Expect(inputA.playerID == inputB.playerID,
                $"{stage} PlayerID Error: Frame={frame}, Reference={inputA.playerID}, Predicted={inputB.playerID}");

            ExpectFloatBits(inputA.moveX, inputB.moveX, frame, stage, "Input.MoveX");
            ExpectFloatBits(inputA.moveY, inputB.moveY, frame, stage, "Input.MoveY");
        }

        private static void ExpectPlayerStateDifferent(TestEnvironment reference, TestEnvironment predicted, Entity referencePlayer, Entity predictedPlayer, int frame, int delay)
        {
            Expect(reference.World.TryGetComponent(referencePlayer, out PositionComponent positionA),
                $"05A PreRollback Reference Position Missing Error: Frame={frame}");

            Expect(predicted.World.TryGetComponent(predictedPlayer, out PositionComponent positionB),
                $"05A PreRollback Predicted Position Missing Error: Frame={frame}");

            bool different =
                Bit(positionA.x) != Bit(positionB.x) ||
                Bit(positionA.z) != Bit(positionB.z);

            Expect(different,
                $"05A PreRollback Divergence Error: Frame={frame}, Delay={delay}, P2 Did Not Diverge");
        }

        private static void ExpectFloatBits(float a, float b, int frame, string stage, string field)
        {
            int bitsA = Bit(a);
            int bitsB = Bit(b);

            Expect(bitsA == bitsB,
                $"{stage} {field} Error: Frame={frame}, Reference={a}({bitsA:X8}), Predicted={b}({bitsB:X8})");
        }

        private static int Bit(float value) => BitConverter.SingleToInt32Bits(value);

        private static void Expect(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private sealed class TestEnvironment : IDisposable
        {
            public readonly World World;
            public readonly Entity[] Players;
            public readonly RollbackCoordinator<FrameInputSet, EcsWorldSnapshot> Coordinator;
            public readonly SimulationFrameCommandBuffer CommandBuffer;
            public readonly SimulationFrameCommandApplier CommandApplier;
            public readonly SnapshotRingBuffer<EcsWorldSnapshot> SnapshotBuffer;

            public TestEnvironment(World world, Entity[] players, RollbackCoordinator<FrameInputSet, EcsWorldSnapshot> coordinator, SimulationFrameCommandBuffer commandBuffer, SimulationFrameCommandApplier commandApplier, SnapshotRingBuffer<EcsWorldSnapshot> snapshotBuffer)
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