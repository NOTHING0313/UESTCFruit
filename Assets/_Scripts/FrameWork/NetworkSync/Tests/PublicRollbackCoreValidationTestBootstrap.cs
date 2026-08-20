using ECSFrameWork;
using FrameWork.NetworkSync;
using FrameWork.RollBackSystem;
using System;
using System.Collections;
using System.Diagnostics;
using System.Net;
using UnityEngine;

namespace FrameWork.NetworkSync.Tests
{
    /// <summary>
    /// Unity 公网 Authority 驱动 RollbackCoordinator 回滚收敛验证入口。
    /// </summary>
    public static class PublicRollbackCoreValidationTestBootstrap
    {
        private const string ServerAddress = "8.137.83.229";
        private const int ServerPort = 28015;
        private const uint SessionId = 0x11223344u;
        private const int PlayerID = 2;
        private const int CorrectionFrame = 120;
        private const int ReceiveFrame = 126;
        private const int EndFrame = 150;
        private const float TickLength = 1f / 60f;
        private const double TimeoutSeconds = 3.0;

        /// <summary>
        /// F120 使用错误预测，F126 才将真实 P2 F120 发送给 Ubuntu，并使用公网 Authority 修正本地历史。
        /// </summary>
        public static IEnumerator Run()
        {
            var serverEndPoint = new IPEndPoint(IPAddress.Parse(ServerAddress), ServerPort);
            LocalNetworkInputClient player1Client = null;
            LocalNetworkInputClient player2Client = null;
            TestEnvironment reference = null;
            TestEnvironment predicted = null;
            var stopwatch = Stopwatch.StartNew();

            PlayerInputSnapshot player1AuthorityInput = CreatePlayer1NetworkInput(CorrectionFrame);
            PlayerInputSnapshot player2AuthorityInput = CreatePlayer2Input(CorrectionFrame);
            PlayerInputSnapshot player2PredictedInput = CreateWrongPrediction(CorrectionFrame);

            bool authorityReceived = false;
            bool preCorrectionDiverged = false;
            bool rollbackApplied = false;
            uint authoritySequence = 0;
            int currentFrameBeforeCorrection = 0;
            int currentFrameAfterCorrection = 0;

            try
            {
                reference = CreateEnvironment(true);
                predicted = CreateEnvironment(true);

                var config1 = new UdpTransportConfig("0.0.0.0", 0, NetworkProtocolConstants.MaxDatagramSize);
                var config2 = new UdpTransportConfig("0.0.0.0", 0, NetworkProtocolConstants.MaxDatagramSize);

                player1Client = new LocalNetworkInputClient(config1, serverEndPoint, SessionId, 1);
                player2Client = new LocalNetworkInputClient(config2, serverEndPoint, SessionId, 2);

                ExpectFloatBits(-1f, CreatePlayer2Input(CorrectionFrame - 1).moveX, CorrectionFrame - 1, "PredictionPrecondition", "P2.MoveX");
                ExpectFloatBits(1f, player2AuthorityInput.moveX, CorrectionFrame, "PredictionPrecondition", "AuthorityP2.MoveX");
                ExpectFloatBits(-1f, player2PredictedInput.moveX, CorrectionFrame, "PredictionPrecondition", "PredictedP2.MoveX");
                Expect(FloatBits(player2PredictedInput.moveX) != FloatBits(player2AuthorityInput.moveX),
                    $"PublicRollbackCoreValidationTestBootstrap Run Error: Category=PredictionDidNotMismatch, Frame={CorrectionFrame}");

                UnityEngine.Debug.Log(
                    $"PublicRollbackCoreValidationTestBootstrap Run Log: [UNITY PUBLIC ROLLBACK] " +
                    $"Server={serverEndPoint}, Session=0x{SessionId:X8}, PlayerID={PlayerID}, " +
                    $"MismatchFrame={CorrectionFrame}, ReceiveFrame={ReceiveFrame}, Delay={ReceiveFrame - CorrectionFrame}, " +
                    $"Player1Local={player1Client.LocalEndPoint}, Player2Local={player2Client.LocalEndPoint}");

                for (int frame = 1; frame <= EndFrame; frame++)
                {
                    PlayerInputSnapshot authoritative = CreatePlayer2Input(frame);
                    PlayerInputSnapshot local = frame == CorrectionFrame ? player2PredictedInput : authoritative;

                    DriveFrame(reference, frame, authoritative, true);
                    DriveFrame(predicted, frame, local, true);

                    if (frame == CorrectionFrame)
                    {
                        player1Client.SendInput(in player1AuthorityInput);

                        UnityEngine.Debug.Log(
                            $"PublicRollbackCoreValidationTestBootstrap Run Log: " +
                            $"Frame={frame}, Event=P1AuthoritySentAndP2AuthorityHeld, " +
                            $"PredictedP2MoveX={player2PredictedInput.moveX}, AuthoritativeP2MoveX={player2AuthorityInput.moveX}");
                    }

                    if (frame != ReceiveFrame) continue;

                    ExpectStateDifferent(reference, predicted, frame);
                    preCorrectionDiverged = true;

                    currentFrameBeforeCorrection = predicted.Coordinator.CurrentFrame;
                    Expect(currentFrameBeforeCorrection == ReceiveFrame,
                        $"PublicRollbackCoreValidationTestBootstrap Run Error: Category=UnexpectedCurrentFrameBeforeCorrection, Expected={ReceiveFrame}, Actual={currentFrameBeforeCorrection}");

                    player2Client.SendInput(in player2AuthorityInput);

                    UnityEngine.Debug.Log(
                        $"PublicRollbackCoreValidationTestBootstrap Run Log: " +
                        $"Frame={frame}, Event=DelayedP2AuthoritySent, AuthorityFrame={CorrectionFrame}, Delay={ReceiveFrame - CorrectionFrame}");

                    ServerAuthorityFramePacket authorityPacket = default;
                    double deadline = Time.realtimeSinceStartupAsDouble + TimeoutSeconds;

                    while (!authorityReceived)
                    {
                        if (player1Client.TryReceiveAuthority(out ServerAuthorityFramePacket packet))
                        {
                            if (packet.InputSet.frameNumber == CorrectionFrame)
                            {
                                authorityPacket = packet;
                                authorityReceived = true;
                                break;
                            }

                            UnityEngine.Debug.Log(
                                $"PublicRollbackCoreValidationTestBootstrap Run Log: " +
                                $"Event=IgnoredAuthority, AuthorityFrame={packet.InputSet.frameNumber}, Sequence={packet.Sequence}");
                        }

                        if (Time.realtimeSinceStartupAsDouble >= deadline)
                        {
                            throw new TimeoutException(
                                $"PublicRollbackCoreValidationTestBootstrap Run Error: Category=AuthorityTimeout, " +
                                $"Server={serverEndPoint}, ExpectedFrame={CorrectionFrame}, " +
                                $"Player1Local={player1Client.LocalEndPoint}, Player2Local={player2Client.LocalEndPoint}, " +
                                $"Player1State=[{GetClientState(player1Client)}], Player2State=[{GetClientState(player2Client)}]");
                        }

                        yield return null;
                    }

                    ValidateAuthority(in authorityPacket, in player1AuthorityInput, in player2AuthorityInput);
                    authoritySequence = authorityPacket.Sequence;

                    Expect(authorityPacket.InputSet.TryGetInput(PlayerID, out PlayerInputSnapshot publicAuthoritativeP2),
                        $"PublicRollbackCoreValidationTestBootstrap Run Error: Category=MissingP2Authority, Frame={CorrectionFrame}");

                    AssertInputBitExact(in player2AuthorityInput, in publicAuthoritativeP2, CorrectionFrame, PlayerID, "PublicAuthority");

                    predicted.Coordinator.ReceiveAuthoritativeInput(CorrectionFrame, publicAuthoritativeP2);
                    rollbackApplied = true;

                    currentFrameAfterCorrection = predicted.Coordinator.CurrentFrame;

                    Expect(currentFrameAfterCorrection == currentFrameBeforeCorrection,
                        $"PublicRollbackCoreValidationTestBootstrap Run Error: Category=CurrentFrameRegressed, Before={currentFrameBeforeCorrection}, After={currentFrameAfterCorrection}");

                    AssertStateEqual(reference, predicted, frame, "AfterPublicRollback");

                    UnityEngine.Debug.Log(
                        $"PublicRollbackCoreValidationTestBootstrap Run Log: " +
                        $"Frame={frame}, Event=PublicAuthorityCorrectionApplied, AuthoritySequence={authoritySequence}, " +
                        $"CurrentFrameBefore={currentFrameBeforeCorrection}, CurrentFrameAfter={currentFrameAfterCorrection}, Result=Converged");
                }

                AssertStateEqual(reference, predicted, EndFrame, "Final");

                Expect(authorityReceived, "PublicRollbackCoreValidationTestBootstrap Run Error: Public Authority Was Not Received");
                Expect(preCorrectionDiverged, "PublicRollbackCoreValidationTestBootstrap Run Error: Predicted World Never Diverged");
                Expect(rollbackApplied, "PublicRollbackCoreValidationTestBootstrap Run Error: Rollback Correction Was Not Applied");

                stopwatch.Stop();

                UnityEngine.Debug.Log(
                    $"PublicRollbackCoreValidationTestBootstrap Run Log: [UNITY PUBLIC ROLLBACK] " +
                    $"Server={serverEndPoint}, Session=0x{SessionId:X8}, PlayerID={PlayerID}, " +
                    $"MismatchFrame={CorrectionFrame}, ReceiveFrame={ReceiveFrame}, Delay={ReceiveFrame - CorrectionFrame}, " +
                    $"PredictedMoveX={player2PredictedInput.moveX}, AuthoritativeMoveX={player2AuthorityInput.moveX}, " +
                    $"AuthoritySequence={authoritySequence}, CurrentFrameBefore={currentFrameBeforeCorrection}, " +
                    $"CurrentFrameAfter={currentFrameAfterCorrection}, FinalFrame={predicted.Coordinator.CurrentFrame}, " +
                    $"Elapsed={stopwatch.Elapsed.TotalMilliseconds:F2}ms, Result=PASS");
            }
            finally
            {
                player1Client?.Dispose();
                player2Client?.Dispose();
                reference?.Dispose();
                predicted?.Dispose();
            }
        }

        private static TestEnvironment CreateEnvironment(bool saveInitialSnapshot)
        {
            var world = new World { EnableSystemProfile = false };
            Entity player = world.CreateEntity();

            world.SetComponent(player, new PlayerTagComponent());
            world.SetComponent(player, new PlayerInputSnapshotComponent(0, PlayerID, 0f, 0f));
            world.SetComponent(player, new MoveSpeedComponent(3.25f));
            world.SetComponent(player, new VelocityComponent(0f, 0f, 0f));
            world.SetComponent(player, new PositionComponent(1.25f, 0f, -2.5f));
            world.AddSystem(new InputMoveSystem());
            world.AddSystem(new MovementSystem());

            var inputApplier = new PlayerSnapshotInputApplier();
            inputApplier.RegisterPlayer(PlayerID, player);

            var commandBuffer = new SimulationFrameCommandBuffer(512);
            var commandApplier = new SimulationFrameCommandApplier(world, commandBuffer, 512);
            var rollbackAdapter = new WorldRollbackAdapter<PlayerInputSnapshot>(world, world, inputApplier, null);
            rollbackAdapter.SetFrameCommandReplayBinding(new RollbackFrameCommandReplayBinding(commandBuffer, commandApplier));

            var snapshotBuffer = new SnapshotRingBuffer<EcsWorldSnapshot>(512);
            var coordinator = new RollbackCoordinator<PlayerInputSnapshot, EcsWorldSnapshot>(
                new InputBuffer<PlayerInputSnapshot>(),
                new AuthoritativeInputBuffer<PlayerInputSnapshot>(),
                snapshotBuffer,
                rollbackAdapter,
                new PlayerInputSnapshotComparer(),
                new ChecksumBuffer(),
                new AuthoritativeChecksumBuffer())
            {
                TickLength = TickLength
            };

            var environment = new TestEnvironment(world, player, coordinator, commandBuffer, commandApplier, snapshotBuffer);
            if (saveInitialSnapshot) coordinator.SaveSnapshot();
            return environment;
        }

        private static void DriveFrame(TestEnvironment environment, int frame, PlayerInputSnapshot input, bool saveSnapshot)
        {
            RollbackStepResult result = environment.Coordinator.TryStep(frame, input);

            Expect(result.Succeeded,
                $"PublicRollbackCoreValidationTestBootstrap DriveFrame Error: Frame={frame}, Kind={result.FailureKind}, Message={result.Message}");

            var context = new SimulationContext(frame, TickLength, false);

            environment.CommandApplier.ApplyCommandsToWorld(frame, SimulationFrameCommandTiming.BeforeTick);
            environment.World.Tick(in context);
            environment.CommandApplier.ApplyCommandsToWorld(frame, SimulationFrameCommandTiming.AfterTick);

            if (saveSnapshot) environment.Coordinator.SaveSnapshot();
        }

        private static PlayerInputSnapshot CreatePlayer1NetworkInput(int frame)
        {
            return new PlayerInputSnapshot(frame, 1)
            {
                moveX = 0.5f,
                moveY = -0.25f,
                mouseX = 12.5f,
                mouseY = -4.25f,
                mouseDeltaX = 0.125f,
                mouseDeltaY = -0.25f,
                scrollX = 0.5f,
                scrollY = -0.5f,
                pressedButtons = (InputButtonFlags)1UL,
                heldButtons = (InputButtonFlags)2UL,
                releasedButtons = (InputButtonFlags)4UL
            };
        }

        private static PlayerInputSnapshot CreatePlayer2Input(int frame)
        {
            float moveX = 0f;

            if (frame == CorrectionFrame - 1) moveX = -1f;
            else if (frame == CorrectionFrame) moveX = 1f;

            return new PlayerInputSnapshot(frame, PlayerID)
            {
                moveX = moveX,
                moveY = 0f,
                mouseX = frame * 0.25f + PlayerID,
                mouseY = -frame * 0.125f - PlayerID,
                mouseDeltaX = 0f,
                mouseDeltaY = 0f,
                scrollX = 0f,
                scrollY = 0f,
                pressedButtons = 0,
                heldButtons = 0,
                releasedButtons = 0
            };
        }

        private static PlayerInputSnapshot CreateWrongPrediction(int frame)
        {
            PlayerInputSnapshot lastKnown = CreatePlayer2Input(frame - 1);

            return new PlayerInputSnapshot(frame, PlayerID)
            {
                moveX = lastKnown.moveX,
                moveY = lastKnown.moveY,
                mouseX = lastKnown.mouseX,
                mouseY = lastKnown.mouseY,
                mouseDeltaX = 0f,
                mouseDeltaY = 0f,
                scrollX = lastKnown.scrollX,
                scrollY = lastKnown.scrollY,
                pressedButtons = 0,
                heldButtons = lastKnown.heldButtons,
                releasedButtons = 0
            };
        }

        private static void ValidateAuthority(in ServerAuthorityFramePacket packet, in PlayerInputSnapshot expected1, in PlayerInputSnapshot expected2)
        {
            Expect(packet.SessionId == SessionId,
                $"PublicRollbackCoreValidationTestBootstrap ValidateAuthority Error: Category=SessionMismatch, Expected=0x{SessionId:X8}, Actual=0x{packet.SessionId:X8}");

            Expect(packet.Sequence != 0,
                "PublicRollbackCoreValidationTestBootstrap ValidateAuthority Error: Category=InvalidSequence, Sequence=0");

            FrameInputSet inputSet = packet.InputSet;

            Expect(inputSet.IsCreated,
                $"PublicRollbackCoreValidationTestBootstrap ValidateAuthority Error: Category=InputSetNotCreated, Frame={CorrectionFrame}");

            Expect(inputSet.frameNumber == CorrectionFrame,
                $"PublicRollbackCoreValidationTestBootstrap ValidateAuthority Error: Category=FrameMismatch, Expected={CorrectionFrame}, Actual={inputSet.frameNumber}");

            Expect(inputSet.Count == 2,
                $"PublicRollbackCoreValidationTestBootstrap ValidateAuthority Error: Category=PlayerCountMismatch, Expected=2, Actual={inputSet.Count}");

            Expect(inputSet.TryGetInput(1, out PlayerInputSnapshot actual1),
                $"PublicRollbackCoreValidationTestBootstrap ValidateAuthority Error: Category=MissingPlayer, PlayerID=1, Frame={CorrectionFrame}");

            Expect(inputSet.TryGetInput(2, out PlayerInputSnapshot actual2),
                $"PublicRollbackCoreValidationTestBootstrap ValidateAuthority Error: Category=MissingPlayer, PlayerID=2, Frame={CorrectionFrame}");

            AssertInputBitExact(in expected1, in actual1, CorrectionFrame, 1, "AuthorityP1");
            AssertInputBitExact(in expected2, in actual2, CorrectionFrame, 2, "AuthorityP2");
        }

        private static void AssertStateEqual(TestEnvironment expected, TestEnvironment actual, int frame, string stage)
        {
            Expect(expected.Coordinator.CurrentFrame == actual.Coordinator.CurrentFrame,
                $"PublicRollbackCoreValidationTestBootstrap {stage} Error: Category=CoordinatorFrame, Frame={frame}, Expected={expected.Coordinator.CurrentFrame}, Actual={actual.Coordinator.CurrentFrame}");

            Expect(expected.World.AliveEntityCount == actual.World.AliveEntityCount,
                $"PublicRollbackCoreValidationTestBootstrap {stage} Error: Category=AliveEntityCount, Frame={frame}, Expected={expected.World.AliveEntityCount}, Actual={actual.World.AliveEntityCount}");

            Expect(expected.World.CreatedEntityCount == actual.World.CreatedEntityCount,
                $"PublicRollbackCoreValidationTestBootstrap {stage} Error: Category=CreatedEntityCount, Frame={frame}, Expected={expected.World.CreatedEntityCount}, Actual={actual.World.CreatedEntityCount}");

            Expect(expected.World.RegisteredComponentTypeCount == actual.World.RegisteredComponentTypeCount,
                $"PublicRollbackCoreValidationTestBootstrap {stage} Error: Category=ComponentTypeCount, Frame={frame}, Expected={expected.World.RegisteredComponentTypeCount}, Actual={actual.World.RegisteredComponentTypeCount}");

            Expect(expected.World.SystemCount == actual.World.SystemCount,
                $"PublicRollbackCoreValidationTestBootstrap {stage} Error: Category=SystemCount, Frame={frame}, Expected={expected.World.SystemCount}, Actual={actual.World.SystemCount}");

            Expect(expected.World.TryGetComponent(expected.Player, out PositionComponent expectedPosition),
                $"PublicRollbackCoreValidationTestBootstrap {stage} Error: PositionExpectedMissing, Frame={frame}");

            Expect(actual.World.TryGetComponent(actual.Player, out PositionComponent actualPosition),
                $"PublicRollbackCoreValidationTestBootstrap {stage} Error: PositionActualMissing, Frame={frame}");

            ExpectFloatBits(expectedPosition.x, actualPosition.x, frame, stage, "Position.X");
            ExpectFloatBits(expectedPosition.y, actualPosition.y, frame, stage, "Position.Y");
            ExpectFloatBits(expectedPosition.z, actualPosition.z, frame, stage, "Position.Z");

            Expect(expected.World.TryGetComponent(expected.Player, out VelocityComponent expectedVelocity),
                $"PublicRollbackCoreValidationTestBootstrap {stage} Error: VelocityExpectedMissing, Frame={frame}");

            Expect(actual.World.TryGetComponent(actual.Player, out VelocityComponent actualVelocity),
                $"PublicRollbackCoreValidationTestBootstrap {stage} Error: VelocityActualMissing, Frame={frame}");

            ExpectFloatBits(expectedVelocity.x, actualVelocity.x, frame, stage, "Velocity.X");
            ExpectFloatBits(expectedVelocity.y, actualVelocity.y, frame, stage, "Velocity.Y");
            ExpectFloatBits(expectedVelocity.z, actualVelocity.z, frame, stage, "Velocity.Z");

            Expect(expected.World.TryGetComponent(expected.Player, out MoveSpeedComponent expectedSpeed),
                $"PublicRollbackCoreValidationTestBootstrap {stage} Error: MoveSpeedExpectedMissing, Frame={frame}");

            Expect(actual.World.TryGetComponent(actual.Player, out MoveSpeedComponent actualSpeed),
                $"PublicRollbackCoreValidationTestBootstrap {stage} Error: MoveSpeedActualMissing, Frame={frame}");

            ExpectFloatBits(expectedSpeed.value, actualSpeed.value, frame, stage, "MoveSpeed");

            Expect(expected.World.TryGetComponent(expected.Player, out PlayerInputSnapshotComponent expectedInput),
                $"PublicRollbackCoreValidationTestBootstrap {stage} Error: InputExpectedMissing, Frame={frame}");

            Expect(actual.World.TryGetComponent(actual.Player, out PlayerInputSnapshotComponent actualInput),
                $"PublicRollbackCoreValidationTestBootstrap {stage} Error: InputActualMissing, Frame={frame}");

            AssertInputComponentBitExact(expectedInput, actualInput, frame, stage);
        }

        private static void ExpectStateDifferent(TestEnvironment expected, TestEnvironment actual, int frame)
        {
            Expect(expected.World.TryGetComponent(expected.Player, out PositionComponent expectedPosition),
                $"PublicRollbackCoreValidationTestBootstrap ExpectStateDifferent Error: ReferencePositionMissing, Frame={frame}");

            Expect(actual.World.TryGetComponent(actual.Player, out PositionComponent actualPosition),
                $"PublicRollbackCoreValidationTestBootstrap ExpectStateDifferent Error: PredictedPositionMissing, Frame={frame}");

            bool different =
                FloatBits(expectedPosition.x) != FloatBits(actualPosition.x) ||
                FloatBits(expectedPosition.y) != FloatBits(actualPosition.y) ||
                FloatBits(expectedPosition.z) != FloatBits(actualPosition.z);

            Expect(different,
                $"PublicRollbackCoreValidationTestBootstrap ExpectStateDifferent Error: Frame={frame}, PredictedWorldDidNotDiverge");
        }

        private static void AssertInputComponentBitExact(PlayerInputSnapshotComponent expected, PlayerInputSnapshotComponent actual, int frame, string stage)
        {
            Expect(expected.inputFrame == actual.inputFrame,
                $"PublicRollbackCoreValidationTestBootstrap {stage} Error: Field=InputFrame, Frame={frame}, Expected={expected.inputFrame}, Actual={actual.inputFrame}");

            Expect(expected.playerID == actual.playerID,
                $"PublicRollbackCoreValidationTestBootstrap {stage} Error: Field=PlayerID, Frame={frame}, Expected={expected.playerID}, Actual={actual.playerID}");

            ExpectFloatBits(expected.moveX, actual.moveX, frame, stage, "Input.MoveX");
            ExpectFloatBits(expected.moveY, actual.moveY, frame, stage, "Input.MoveY");
            ExpectFloatBits(expected.mouseX, actual.mouseX, frame, stage, "Input.MouseX");
            ExpectFloatBits(expected.mouseY, actual.mouseY, frame, stage, "Input.MouseY");
            ExpectFloatBits(expected.mouseDeltaX, actual.mouseDeltaX, frame, stage, "Input.MouseDeltaX");
            ExpectFloatBits(expected.mouseDeltaY, actual.mouseDeltaY, frame, stage, "Input.MouseDeltaY");
            ExpectFloatBits(expected.scrollX, actual.scrollX, frame, stage, "Input.ScrollX");
            ExpectFloatBits(expected.scrollY, actual.scrollY, frame, stage, "Input.ScrollY");

            Expect(expected.pressedButtons == actual.pressedButtons,
                $"PublicRollbackCoreValidationTestBootstrap {stage} Error: Field=PressedButtons, Frame={frame}, Expected={expected.pressedButtons}, Actual={actual.pressedButtons}");

            Expect(expected.heldButtons == actual.heldButtons,
                $"PublicRollbackCoreValidationTestBootstrap {stage} Error: Field=HeldButtons, Frame={frame}, Expected={expected.heldButtons}, Actual={actual.heldButtons}");

            Expect(expected.releasedButtons == actual.releasedButtons,
                $"PublicRollbackCoreValidationTestBootstrap {stage} Error: Field=ReleasedButtons, Frame={frame}, Expected={expected.releasedButtons}, Actual={actual.releasedButtons}");
        }

        private static void AssertInputBitExact(in PlayerInputSnapshot expected, in PlayerInputSnapshot actual, int frame, int playerID, string stage)
        {
            Expect(expected.frameNumber == actual.frameNumber,
                $"PublicRollbackCoreValidationTestBootstrap {stage} Error: PlayerID={playerID}, Field=FrameNumber, Expected={expected.frameNumber}, Actual={actual.frameNumber}");

            Expect(expected.playerID == actual.playerID,
                $"PublicRollbackCoreValidationTestBootstrap {stage} Error: PlayerID={playerID}, Field=PlayerID, Expected={expected.playerID}, Actual={actual.playerID}");

            ExpectFloatBits(expected.moveX, actual.moveX, frame, stage, "MoveX");
            ExpectFloatBits(expected.moveY, actual.moveY, frame, stage, "MoveY");
            ExpectFloatBits(expected.mouseX, actual.mouseX, frame, stage, "MouseX");
            ExpectFloatBits(expected.mouseY, actual.mouseY, frame, stage, "MouseY");
            ExpectFloatBits(expected.mouseDeltaX, actual.mouseDeltaX, frame, stage, "MouseDeltaX");
            ExpectFloatBits(expected.mouseDeltaY, actual.mouseDeltaY, frame, stage, "MouseDeltaY");
            ExpectFloatBits(expected.scrollX, actual.scrollX, frame, stage, "ScrollX");
            ExpectFloatBits(expected.scrollY, actual.scrollY, frame, stage, "ScrollY");

            Expect(expected.pressedButtons == actual.pressedButtons,
                $"PublicRollbackCoreValidationTestBootstrap {stage} Error: PlayerID={playerID}, Field=PressedButtons, Expected={expected.pressedButtons}, Actual={actual.pressedButtons}");

            Expect(expected.heldButtons == actual.heldButtons,
                $"PublicRollbackCoreValidationTestBootstrap {stage} Error: PlayerID={playerID}, Field=HeldButtons, Expected={expected.heldButtons}, Actual={actual.heldButtons}");

            Expect(expected.releasedButtons == actual.releasedButtons,
                $"PublicRollbackCoreValidationTestBootstrap {stage} Error: PlayerID={playerID}, Field=ReleasedButtons, Expected={expected.releasedButtons}, Actual={actual.releasedButtons}");
        }

        private static void ExpectFloatBits(float expected, float actual, int frame, string stage, string field)
        {
            int expectedBits = FloatBits(expected);
            int actualBits = FloatBits(actual);

            Expect(expectedBits == actualBits,
                $"PublicRollbackCoreValidationTestBootstrap {stage} Error: Frame={frame}, Field={field}, Expected={expected}({expectedBits:X8}), Actual={actual}({actualBits:X8})");
        }

        private static int FloatBits(float value) => BitConverter.SingleToInt32Bits(value);

        private static string GetClientState(LocalNetworkInputClient client)
            => $"LastSentSequence={client.LastSentSequence}, LastRejectReason={client.LastRejectReason}, LastDecodeError={client.LastDecodeError}";

        private static void Expect(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private sealed class TestEnvironment : IDisposable
        {
            public readonly World World;
            public readonly Entity Player;
            public readonly RollbackCoordinator<PlayerInputSnapshot, EcsWorldSnapshot> Coordinator;
            public readonly SimulationFrameCommandBuffer CommandBuffer;
            public readonly SimulationFrameCommandApplier CommandApplier;
            public readonly SnapshotRingBuffer<EcsWorldSnapshot> SnapshotBuffer;

            public TestEnvironment(World world, Entity player, RollbackCoordinator<PlayerInputSnapshot, EcsWorldSnapshot> coordinator, SimulationFrameCommandBuffer commandBuffer, SimulationFrameCommandApplier commandApplier, SnapshotRingBuffer<EcsWorldSnapshot> snapshotBuffer)
            {
                World = world;
                Player = player;
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