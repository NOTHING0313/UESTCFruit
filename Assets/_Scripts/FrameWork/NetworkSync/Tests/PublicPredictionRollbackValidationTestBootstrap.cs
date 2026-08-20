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
    /// Unity 公网 Prediction + Authority + Rollback 完整生产链路验证入口。
    /// </summary>
    public static class PublicPredictionRollbackValidationTestBootstrap
    {
        private const string ServerAddress = "8.137.83.229";
        private const int ServerPort = 28015;
        private const uint SessionId = 0x11223344u;
        private const int Player1ID = 1;
        private const int Player2ID = 2;
        private const uint Seed = 20260817u;
        private const int CorrectionFrame = 120;
        private const int AuthorityDelayFrames = 6;
        private const int ReceiveFrame = CorrectionFrame + AuthorityDelayFrames;
        private const int PostRollbackFrames = 60;
        private const int EndFrame = ReceiveFrame + PostRollbackFrames;
        private const float TickLength = 1f / 60f;
        private const double TimeoutSeconds = 3.0;

        /// <summary>
        /// F120 缺失 P2，由 FrameInputAssembler 真实预测；F126 将 P2 F120 发送至公网服务器并通过 NetworkAuthorityRollbackDriver 修正。
        /// </summary>
        public static IEnumerator Run()
        {
            var serverEndPoint = new IPEndPoint(IPAddress.Parse(ServerAddress), ServerPort);
            LocalNetworkInputClient client1 = null, client2 = null;
            TestEnvironment reference = null, predicted = null;
            var stopwatch = Stopwatch.StartNew();

            bool predictionObserved = false;
            bool divergenceObserved = false;
            bool publicAuthorityObserved = false;
            bool rollbackConverged = false;
            PlayerInputSnapshot delayedPlayer2Input = default;
            FrameInputSet authoritativeCorrection = default;
            FrameInputSet predictedCorrection = default;
            uint authoritySequence = 0;
            int currentFrameBeforeCorrection = 0, currentFrameAfterCorrection = 0;
            uint preCorrectionReferenceChecksum = 0, preCorrectionPredictedChecksum = 0;
            uint finalReferenceChecksum = 0, finalPredictedChecksum = 0;

            try
            {
                reference = CreateEnvironment(true);
                predicted = CreateEnvironment(true);

                var assembler = new FrameInputAssembler(new LastKnownPlayerInputPredictionPolicy());
                Expect(assembler.RegisterPlayer(Player1ID), "PublicPredictionRollbackValidationTestBootstrap Run Error: Failed To Register Player1");
                Expect(assembler.RegisterPlayer(Player2ID), "PublicPredictionRollbackValidationTestBootstrap Run Error: Failed To Register Player2");

                var authorityDriver = new NetworkAuthorityRollbackDriver(assembler, predicted.Coordinator);

                client1 = new LocalNetworkInputClient(
                    new UdpTransportConfig("0.0.0.0", 0, NetworkProtocolConstants.MaxDatagramSize),
                    serverEndPoint,
                    SessionId,
                    Player1ID);

                client2 = new LocalNetworkInputClient(
                    new UdpTransportConfig("0.0.0.0", 0, NetworkProtocolConstants.MaxDatagramSize),
                    serverEndPoint,
                    SessionId,
                    Player2ID);

                UnityEngine.Debug.Log(
                    $"PublicPredictionRollbackValidationTestBootstrap Run Log: [UNITY PUBLIC PREDICTION ROLLBACK] " +
                    $"Server={serverEndPoint}, Session=0x{SessionId:X8}, Players=2, " +
                    $"MismatchFrame={CorrectionFrame}, ReceiveFrame={ReceiveFrame}, AuthorityDelay={AuthorityDelayFrames}, " +
                    $"Player1Local={client1.LocalEndPoint}, Player2Local={client2.LocalEndPoint}");

                for (int frame = 1; frame <= EndFrame; frame++)
                {
                    PlayerInputSnapshot player1 = CreatePlayerInput(frame, Player1ID);
                    PlayerInputSnapshot player2 = CreatePlayerInput(frame, Player2ID);

                    var authoritative = new FrameInputSet(frame, new[] { player1, player2 });
                    if (frame == CorrectionFrame) authoritativeCorrection = authoritative;

                    var accumulator = new FrameInputAccumulator(frame);
                    Expect(accumulator.TryAddInput(in player1),
                        $"PublicPredictionRollbackValidationTestBootstrap Run Error: P1 Input Add Failed, Frame={frame}");

                    if (frame != CorrectionFrame)
                    {
                        Expect(accumulator.TryAddInput(in player2),
                            $"PublicPredictionRollbackValidationTestBootstrap Run Error: P2 Input Add Failed, Frame={frame}");
                    }
                    else delayedPlayer2Input = player2;

                    FrameInputAssemblyResult assembly = assembler.Assemble(accumulator);

                    if (frame == CorrectionFrame)
                    {
                        predictedCorrection = assembly.InputSet;

                        Expect(!assembly.IsPredicted(Player1ID),
                            $"PublicPredictionRollbackValidationTestBootstrap Run Error: Category=Prediction, Frame={frame}, P1 Must Be Real");

                        Expect(assembly.IsPredicted(Player2ID),
                            $"PublicPredictionRollbackValidationTestBootstrap Run Error: Category=Prediction, Frame={frame}, P2 Must Be Predicted");

                        Expect(assembly.PredictedCount == 1,
                            $"PublicPredictionRollbackValidationTestBootstrap Run Error: Category=PredictionCount, Frame={frame}, Expected=1, Actual={assembly.PredictedCount}");

                        Expect(predictedCorrection.TryGetInput(Player2ID, out PlayerInputSnapshot predictedP2),
                            $"PublicPredictionRollbackValidationTestBootstrap Run Error: Category=PredictedP2Missing, Frame={frame}");

                        Expect(authoritativeCorrection.TryGetInput(Player2ID, out PlayerInputSnapshot authoritativeP2),
                            $"PublicPredictionRollbackValidationTestBootstrap Run Error: Category=AuthorityP2Missing, Frame={frame}");

                        ExpectFloatBits(-1f, predictedP2.moveX, frame, "Prediction", "P2.MoveX");
                        ExpectFloatBits(1f, authoritativeP2.moveX, frame, "Authority", "P2.MoveX");

                        Expect(FloatBits(predictedP2.moveX) != FloatBits(authoritativeP2.moveX),
                            $"PublicPredictionRollbackValidationTestBootstrap Run Error: Category=PredictionAccidentallyMatched, Frame={frame}");

                        predictionObserved = true;

                        client1.SendInput(in player1);

                        UnityEngine.Debug.Log(
                            $"PublicPredictionRollbackValidationTestBootstrap Run Log: " +
                            $"Frame={frame}, Event=PredictionCreatedAndP1AuthoritySent, " +
                            $"PredictedP2MoveX={predictedP2.moveX}, AuthoritativeP2MoveX={authoritativeP2.moveX}, PredictedCount={assembly.PredictedCount}");
                    }

                    DriveFrame(reference, frame, authoritative, true);
                    DriveFrame(predicted, frame, assembly.InputSet, true);

                    if (frame < CorrectionFrame)
                    {
                        AssertWorldEqual(reference, predicted, frame, "PrePrediction");
                        continue;
                    }

                    if (frame != ReceiveFrame)
                    {
                        if (rollbackConverged) AssertWorldEqual(reference, predicted, frame, "PostRollback");
                        continue;
                    }

                    AssertPlayerEqual(
                        reference,
                        predicted,
                        reference.Players[0],
                        predicted.Players[0],
                        frame,
                        "PreRollback P1");

                    ExpectPlayerStateDifferent(
                        reference,
                        predicted,
                        reference.Players[1],
                        predicted.Players[1],
                        frame);

                    preCorrectionReferenceChecksum = WorldChecksumCalculator.Calculate(reference.World);
                    preCorrectionPredictedChecksum = WorldChecksumCalculator.Calculate(predicted.World);

                    Expect(preCorrectionReferenceChecksum != preCorrectionPredictedChecksum,
                        $"PublicPredictionRollbackValidationTestBootstrap Run Error: Category=PreCorrectionChecksumDidNotDiverge, Frame={frame}, Checksum=0x{preCorrectionReferenceChecksum:X8}");

                    divergenceObserved = true;
                    currentFrameBeforeCorrection = predicted.Coordinator.CurrentFrame;

                    Expect(currentFrameBeforeCorrection == ReceiveFrame,
                        $"PublicPredictionRollbackValidationTestBootstrap Run Error: Category=CurrentFrameBeforeCorrection, Expected={ReceiveFrame}, Actual={currentFrameBeforeCorrection}");

                    client2.SendInput(in delayedPlayer2Input);

                    UnityEngine.Debug.Log(
                        $"PublicPredictionRollbackValidationTestBootstrap Run Log: " +
                        $"Frame={frame}, Event=DelayedP2AuthoritySent, AuthorityFrame={CorrectionFrame}, Delay={AuthorityDelayFrames}");

                    ServerAuthorityFramePacket packet = default;
                    double deadline = Time.realtimeSinceStartupAsDouble + TimeoutSeconds;

                    while (!publicAuthorityObserved)
                    {
                        if (client1.TryReceiveAuthority(out ServerAuthorityFramePacket authority))
                        {
                            if (authority.InputSet.frameNumber < CorrectionFrame) continue;

                            if (authority.InputSet.frameNumber > CorrectionFrame)
                            {
                                throw new InvalidOperationException(
                                    $"PublicPredictionRollbackValidationTestBootstrap Run Error: Category=UnexpectedFutureAuthority, " +
                                    $"ExpectedFrame={CorrectionFrame}, ActualFrame={authority.InputSet.frameNumber}, Sequence={authority.Sequence}");
                            }

                            packet = authority;
                            publicAuthorityObserved = true;
                            break;
                        }

                        if (client1.LastRejectReason != NetworkInputExchangeRejectReason.None)
                        {
                            throw new InvalidOperationException(
                                $"PublicPredictionRollbackValidationTestBootstrap Run Error: Category=ClientReject, PlayerID={client1.PlayerID}, " +
                                $"Reason={client1.LastRejectReason}, Decode={client1.LastDecodeError}");
                        }

                        if (Time.realtimeSinceStartupAsDouble >= deadline)
                        {
                            throw new TimeoutException(
                                $"PublicPredictionRollbackValidationTestBootstrap Run Error: Category=AuthorityTimeout, " +
                                $"ExpectedFrame={CorrectionFrame}, Server={serverEndPoint}, " +
                                $"Player1Local={client1.LocalEndPoint}, Player2Local={client2.LocalEndPoint}, " +
                                $"Player1State=[{GetClientState(client1)}], Player2State=[{GetClientState(client2)}]");
                        }

                        yield return null;
                    }

                    ValidateAuthority(in packet, in authoritativeCorrection);
                    authoritySequence = packet.Sequence;

                    int appliedBefore = authorityDriver.AppliedAuthorityCount;
                    authorityDriver.Apply(in packet);
                    int appliedAfter = authorityDriver.AppliedAuthorityCount;

                    Expect(appliedAfter == appliedBefore + 1,
                        $"PublicPredictionRollbackValidationTestBootstrap Run Error: Category=AuthorityDriverApplyCount, Before={appliedBefore}, After={appliedAfter}");

                    Expect(authorityDriver.LastAuthorityFrame == CorrectionFrame,
                        $"PublicPredictionRollbackValidationTestBootstrap Run Error: Category=AuthorityDriverLastFrame, Expected={CorrectionFrame}, Actual={authorityDriver.LastAuthorityFrame}");

                    Expect(authorityDriver.OutOfOrderAuthorityCount == 0,
                        $"PublicPredictionRollbackValidationTestBootstrap Run Error: Category=UnexpectedOutOfOrderAuthority, Count={authorityDriver.OutOfOrderAuthorityCount}");

                    currentFrameAfterCorrection = predicted.Coordinator.CurrentFrame;

                    Expect(currentFrameAfterCorrection == currentFrameBeforeCorrection,
                        $"PublicPredictionRollbackValidationTestBootstrap Run Error: Category=CurrentFrameRegressed, Before={currentFrameBeforeCorrection}, After={currentFrameAfterCorrection}");

                    AssertWorldEqual(reference, predicted, frame, "AfterPublicAuthorityRollback");
                    rollbackConverged = true;

                    UnityEngine.Debug.Log(
                        $"PublicPredictionRollbackValidationTestBootstrap Run Log: " +
                        $"Frame={frame}, Event=NetworkAuthorityRollbackDriverApplied, AuthorityFrame={packet.InputSet.frameNumber}, " +
                        $"AuthoritySequence={authoritySequence}, CurrentFrameBefore={currentFrameBeforeCorrection}, " +
                        $"CurrentFrameAfter={currentFrameAfterCorrection}, PreReferenceChecksum=0x{preCorrectionReferenceChecksum:X8}, " +
                        $"PrePredictedChecksum=0x{preCorrectionPredictedChecksum:X8}, Result=Converged");
                }

                Expect(predictionObserved,
                    "PublicPredictionRollbackValidationTestBootstrap Run Error: Prediction Was Not Observed");

                Expect(divergenceObserved,
                    "PublicPredictionRollbackValidationTestBootstrap Run Error: Divergence Was Not Observed");

                Expect(publicAuthorityObserved,
                    "PublicPredictionRollbackValidationTestBootstrap Run Error: Public Authority Was Not Observed");

                Expect(rollbackConverged,
                    "PublicPredictionRollbackValidationTestBootstrap Run Error: Rollback Did Not Converge");

                AssertWorldEqual(reference, predicted, EndFrame, "Final");

                finalReferenceChecksum = WorldChecksumCalculator.Calculate(reference.World);
                finalPredictedChecksum = WorldChecksumCalculator.Calculate(predicted.World);

                Expect(finalReferenceChecksum == finalPredictedChecksum,
                    $"PublicPredictionRollbackValidationTestBootstrap Run Error: Category=FinalChecksum, Reference=0x{finalReferenceChecksum:X8}, Predicted=0x{finalPredictedChecksum:X8}");

                stopwatch.Stop();

                UnityEngine.Debug.Log(
                    $"PublicPredictionRollbackValidationTestBootstrap Run Log: [UNITY PUBLIC PREDICTION ROLLBACK] " +
                    $"Server={serverEndPoint}, Session=0x{SessionId:X8}, Players=2, MismatchFrame={CorrectionFrame}, " +
                    $"AuthorityDelay={AuthorityDelayFrames}, PredictionObserved={predictionObserved}, DivergenceObserved={divergenceObserved}, " +
                    $"PublicAuthorityObserved={publicAuthorityObserved}, AuthoritySequence={authoritySequence}, " +
                    $"CurrentFrameBefore={currentFrameBeforeCorrection}, CurrentFrameAfter={currentFrameAfterCorrection}, " +
                    $"PreReferenceChecksum=0x{preCorrectionReferenceChecksum:X8}, PrePredictedChecksum=0x{preCorrectionPredictedChecksum:X8}, " +
                    $"FinalReferenceChecksum=0x{finalReferenceChecksum:X8}, FinalPredictedChecksum=0x{finalPredictedChecksum:X8}, " +
                    $"FinalFrame={predicted.Coordinator.CurrentFrame}, Elapsed={stopwatch.Elapsed.TotalMilliseconds:F2}ms, Result=PASS");
            }
            finally
            {
                client1?.Dispose();
                client2?.Dispose();
                reference?.Dispose();
                predicted?.Dispose();
            }
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

        private static void DriveFrame(TestEnvironment env, int frame, FrameInputSet input, bool saveSnapshot)
        {
            RollbackStepResult result = env.Coordinator.TryStep(frame, input);

            Expect(result.Succeeded,
                $"PublicPredictionRollbackValidationTestBootstrap DriveFrame Error: Frame={frame}, Kind={result.FailureKind}, Message={result.Message}");

            var context = new SimulationContext(frame, TickLength, false);

            env.CommandApplier.ApplyCommandsToWorld(frame, SimulationFrameCommandTiming.BeforeTick);
            env.World.Tick(in context);
            env.CommandApplier.ApplyCommandsToWorld(frame, SimulationFrameCommandTiming.AfterTick);

            if (saveSnapshot) env.Coordinator.SaveSnapshot();
        }

        private static PlayerInputSnapshot CreatePlayerInput(int frame, int playerID)
        {
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

        private static void ValidateAuthority(in ServerAuthorityFramePacket packet, in FrameInputSet expected)
        {
            Expect(packet.SessionId == SessionId,
                $"PublicPredictionRollbackValidationTestBootstrap ValidateAuthority Error: Category=SessionMismatch, Expected=0x{SessionId:X8}, Actual=0x{packet.SessionId:X8}");

            Expect(packet.Sequence != 0,
                "PublicPredictionRollbackValidationTestBootstrap ValidateAuthority Error: Category=InvalidSequence, Sequence=0");

            Expect(packet.InputSet.IsCreated,
                "PublicPredictionRollbackValidationTestBootstrap ValidateAuthority Error: Authority InputSet Is Not Created");

            Expect(packet.InputSet.frameNumber == CorrectionFrame,
                $"PublicPredictionRollbackValidationTestBootstrap ValidateAuthority Error: Category=FrameMismatch, Expected={CorrectionFrame}, Actual={packet.InputSet.frameNumber}");

            Expect(packet.InputSet.Count == 2,
                $"PublicPredictionRollbackValidationTestBootstrap ValidateAuthority Error: Category=PlayerCount, Expected=2, Actual={packet.InputSet.Count}");

            for (int playerID = Player1ID; playerID <= Player2ID; playerID++)
            {
                Expect(expected.TryGetInput(playerID, out PlayerInputSnapshot expectedInput),
                    $"PublicPredictionRollbackValidationTestBootstrap ValidateAuthority Error: Expected Input Missing, PlayerID={playerID}");

                Expect(packet.InputSet.TryGetInput(playerID, out PlayerInputSnapshot actualInput),
                    $"PublicPredictionRollbackValidationTestBootstrap ValidateAuthority Error: Authority Input Missing, PlayerID={playerID}");

                AssertInputBitExact(in expectedInput, in actualInput, CorrectionFrame, playerID, "PublicAuthority");
            }
        }

        private static void AssertInputBitExact(in PlayerInputSnapshot expected, in PlayerInputSnapshot actual, int frame, int playerID, string stage)
        {
            Expect(expected.frameNumber == actual.frameNumber,
                $"{stage} FrameNumber Error: PlayerID={playerID}, Expected={expected.frameNumber}, Actual={actual.frameNumber}");

            Expect(expected.playerID == actual.playerID,
                $"{stage} PlayerID Error: Frame={frame}, Expected={expected.playerID}, Actual={actual.playerID}");

            ExpectFloatBits(expected.moveX, actual.moveX, frame, stage, $"P{playerID}.MoveX");
            ExpectFloatBits(expected.moveY, actual.moveY, frame, stage, $"P{playerID}.MoveY");
            ExpectFloatBits(expected.mouseX, actual.mouseX, frame, stage, $"P{playerID}.MouseX");
            ExpectFloatBits(expected.mouseY, actual.mouseY, frame, stage, $"P{playerID}.MouseY");
            ExpectFloatBits(expected.mouseDeltaX, actual.mouseDeltaX, frame, stage, $"P{playerID}.MouseDeltaX");
            ExpectFloatBits(expected.mouseDeltaY, actual.mouseDeltaY, frame, stage, $"P{playerID}.MouseDeltaY");
            ExpectFloatBits(expected.scrollX, actual.scrollX, frame, stage, $"P{playerID}.ScrollX");
            ExpectFloatBits(expected.scrollY, actual.scrollY, frame, stage, $"P{playerID}.ScrollY");

            Expect(expected.pressedButtons == actual.pressedButtons,
                $"{stage} PressedButtons Error: Frame={frame}, PlayerID={playerID}, Expected={expected.pressedButtons}, Actual={actual.pressedButtons}");

            Expect(expected.heldButtons == actual.heldButtons,
                $"{stage} HeldButtons Error: Frame={frame}, PlayerID={playerID}, Expected={expected.heldButtons}, Actual={actual.heldButtons}");

            Expect(expected.releasedButtons == actual.releasedButtons,
                $"{stage} ReleasedButtons Error: Frame={frame}, PlayerID={playerID}, Expected={expected.releasedButtons}, Actual={actual.releasedButtons}");
        }

        private static void AssertWorldEqual(TestEnvironment reference, TestEnvironment predicted, int frame, string stage)
        {
            Expect(reference.Coordinator.CurrentFrame == predicted.Coordinator.CurrentFrame,
                $"{stage} CoordinatorFrame Error: Frame={frame}, Reference={reference.Coordinator.CurrentFrame}, Predicted={predicted.Coordinator.CurrentFrame}");

            Expect(reference.World.AliveEntityCount == predicted.World.AliveEntityCount,
                $"{stage} AliveEntityCount Error: Frame={frame}, Reference={reference.World.AliveEntityCount}, Predicted={predicted.World.AliveEntityCount}");

            Expect(reference.World.CreatedEntityCount == predicted.World.CreatedEntityCount,
                $"{stage} CreatedEntityCount Error: Frame={frame}, Reference={reference.World.CreatedEntityCount}, Predicted={predicted.World.CreatedEntityCount}");

            AssertPlayerEqual(reference, predicted, reference.Players[0], predicted.Players[0], frame, $"{stage} P1");
            AssertPlayerEqual(reference, predicted, reference.Players[1], predicted.Players[1], frame, $"{stage} P2");

            uint referenceChecksum = WorldChecksumCalculator.Calculate(reference.World);
            uint predictedChecksum = WorldChecksumCalculator.Calculate(predicted.World);

            Expect(referenceChecksum == predictedChecksum,
                $"{stage} Checksum Error: Frame={frame}, Reference=0x{referenceChecksum:X8}, Predicted=0x{predictedChecksum:X8}");
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

        private static void ExpectPlayerStateDifferent(TestEnvironment reference, TestEnvironment predicted, Entity referencePlayer, Entity predictedPlayer, int frame)
        {
            Expect(reference.World.TryGetComponent(referencePlayer, out PositionComponent positionA),
                $"PublicPredictionRollbackValidationTestBootstrap ExpectPlayerStateDifferent Error: Reference Position Missing, Frame={frame}");

            Expect(predicted.World.TryGetComponent(predictedPlayer, out PositionComponent positionB),
                $"PublicPredictionRollbackValidationTestBootstrap ExpectPlayerStateDifferent Error: Predicted Position Missing, Frame={frame}");

            bool different =
                FloatBits(positionA.x) != FloatBits(positionB.x) ||
                FloatBits(positionA.y) != FloatBits(positionB.y) ||
                FloatBits(positionA.z) != FloatBits(positionB.z);

            Expect(different,
                $"PublicPredictionRollbackValidationTestBootstrap ExpectPlayerStateDifferent Error: Frame={frame}, P2 Predicted State Did Not Diverge");
        }

        private static void ExpectFloatBits(float expected, float actual, int frame, string stage, string field)
        {
            int expectedBits = FloatBits(expected);
            int actualBits = FloatBits(actual);

            Expect(expectedBits == actualBits,
                $"{stage} {field} Error: Frame={frame}, Expected={expected}({expectedBits:X8}), Actual={actual}({actualBits:X8})");
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