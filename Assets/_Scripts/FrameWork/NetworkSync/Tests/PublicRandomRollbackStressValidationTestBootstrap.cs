using ECSFrameWork;
using FrameWork.NetworkSync;
using FrameWork.RollBackSystem;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using UnityEngine;

namespace FrameWork.NetworkSync.Tests
{
    /// <summary>
    /// Unity 公网随机输入延迟、Prediction、Authority Correction 与重复 Rollback 压力验证。
    /// </summary>
    public static class PublicRandomRollbackStressValidationTestBootstrap
    {
        private const string ServerAddress = "8.137.83.229";
        private const int ServerPort = 28015;
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
        private const float TickLength = 1f / 60f;
        private const double FinalFlushTimeoutSeconds = 15.0;

        /// <summary>
        /// 连续 2000 帧运行公网随机延迟 P2 输入，通过 Ubuntu Authority 反复修正预测历史并最终收敛。
        /// </summary>
        public static IEnumerator Run()
        {
            var serverEndPoint = new IPEndPoint(IPAddress.Parse(ServerAddress), ServerPort);
            LocalNetworkInputClient client1 = null, client2 = null;
            TestEnvironment reference = null, predicted = null;
            var stopwatch = Stopwatch.StartNew();

            try
            {
                reference = CreateEnvironment(true);
                predicted = CreateEnvironment(true);

                var assembler = new FrameInputAssembler(new LastKnownPlayerInputPredictionPolicy());
                Expect(assembler.RegisterPlayer(Player1ID), "PublicRandomRollbackStressValidationTestBootstrap Run Error: Failed To Register Player1");
                Expect(assembler.RegisterPlayer(Player2ID), "PublicRandomRollbackStressValidationTestBootstrap Run Error: Failed To Register Player2");

                var authorityDriver = new NetworkAuthorityRollbackDriver(assembler, predicted.Coordinator);
                var frameComparer = new FrameInputSetComparer();
                var playerComparer = new PlayerInputSnapshotComparer();

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
                int client2AuthorityDrainedCount = 0;
                int convergenceCheckpointCount = 0;

                int p2OutOfOrderSendCount = 0;
                int highestSentP2Frame = 0;

                UnityEngine.Debug.Log(
                    $"PublicRandomRollbackStressValidationTestBootstrap Run Log: [PUBLIC RANDOM ROLLBACK STRESS] " +
                    $"Server={serverEndPoint}, Session=0x{SessionId:X8}, Seed={Seed}, Frames={TotalFrames}, MaxInputDelay={MaxInputDelayFrames}, " +
                    $"Player1Local={client1.LocalEndPoint}, Player2Local={client2.LocalEndPoint}");

                for (int frame = 1; frame <= TotalFrames; frame++)
                {
                    PlayerInputSnapshot player1 = CreatePlayerInput(frame, Player1ID);
                    PlayerInputSnapshot player2 = CreatePlayerInput(frame, Player2ID);

                    var authoritative = new FrameInputSet(frame, new[] { player1, player2 });
                    authoritativeHistory[frame] = authoritative;

                    int delay = NextRange(ref delayRandomState, 0, MaxInputDelayFrames);

                    if (delay == 0) zeroDelayInputCount++;
                    else delayedInputCount++;

                    if (delay > maxObservedDelay) maxObservedDelay = delay;

                    int p2SendFrame = frame + delay;
                    delayedP2Inputs[p2SendFrame] ??= new List<PlayerInputSnapshot>();
                    delayedP2Inputs[p2SendFrame].Add(player2);

                    // 客户端当前只拥有 P1，本帧 P2 必须由 Prediction Layer 产生。
                    var accumulator = new FrameInputAccumulator(frame);
                    Expect(accumulator.TryAddInput(in player1),
                        $"PublicRandomRollbackStressValidationTestBootstrap Run Error: P1 Input Add Failed, Frame={frame}");

                    FrameInputAssemblyResult assembly = assembler.Assemble(accumulator);
                    FrameInputSet predictedInput = assembly.InputSet;

                    Expect(assembly.IsPredicted(Player2ID),
                        $"PublicRandomRollbackStressValidationTestBootstrap Run Error: Prediction Coverage, Frame={frame}, P2 Must Be Predicted");

                    Expect(!assembly.IsPredicted(Player1ID),
                        $"PublicRandomRollbackStressValidationTestBootstrap Run Error: Prediction Coverage, Frame={frame}, P1 Must Be Real");

                    predictedFrameCount++;

                    Expect(predictedInput.TryGetInput(Player2ID, out PlayerInputSnapshot predictedP2),
                        $"PublicRandomRollbackStressValidationTestBootstrap Run Error: Predicted P2 Missing, Frame={frame}");

                    if (playerComparer.IsEqual(predictedP2, player2)) correctPredictionCount++;

                    bool frameMismatch = !frameComparer.IsEqual(predictedInput, authoritative);
                    frameMispredicted[frame] = frameMismatch;

                    if (frameMismatch)
                    {
                        mispredictedFrameCount++;
                        unresolvedMispredictedFrames++;
                    }

                    // P1 始终实时发送。
                    client1.SendInput(in player1);

                    // 发送当前网络帧到期的 P2 历史输入。
                    SendDuePlayer2Inputs(
                        delayedP2Inputs[frame],
                        client2,
                        ref highestSentP2Frame,
                        ref p2OutOfOrderSendCount);

                    // Reference 永远使用完整真实 Authority。
                    DriveFrame(reference, frame, authoritative, true);

                    // Predicted 使用真实 FrameInputAssembler 产出的完整预测帧。
                    DriveFrame(predicted, frame, predictedInput, true);

                    // 先排空当前已经抵达的公网 Authority。
                    PumpClient1Authorities(
                        client1,
                        authorityDriver,
                        authoritativeHistory,
                        frameMispredicted,
                        authorityReceived,
                        reference,
                        predicted,
                        frame,
                        ref authorityReceivedCount,
                        ref mismatchAuthorityCorrectionCount,
                        ref unresolvedMispredictedFrames,
                        ref convergenceCheckpointCount);

                    PumpClient2Authorities(
                        client2,
                        ref client2AuthorityDrainedCount);

                    ThrowIfClientRejected(client1);
                    ThrowIfClientRejected(client2);

                    // 每个逻辑帧至少让 Unity/OS 网络栈获得一次真实时间推进。
                    yield return null;

                    PumpClient1Authorities(
                        client1,
                        authorityDriver,
                        authoritativeHistory,
                        frameMispredicted,
                        authorityReceived,
                        reference,
                        predicted,
                        frame,
                        ref authorityReceivedCount,
                        ref mismatchAuthorityCorrectionCount,
                        ref unresolvedMispredictedFrames,
                        ref convergenceCheckpointCount);

                    PumpClient2Authorities(
                        client2,
                        ref client2AuthorityDrainedCount);

                    ThrowIfClientRejected(client1);
                    ThrowIfClientRejected(client2);

                    if (frame % 250 == 0)
                    {
                        UnityEngine.Debug.Log(
                            $"PublicRandomRollbackStressValidationTestBootstrap Run Log: " +
                            $"Frame={frame}/{TotalFrames}, Authorities={authorityReceivedCount}, " +
                            $"Mispredicted={mispredictedFrameCount}, Corrections={mismatchAuthorityCorrectionCount}, " +
                            $"OutstandingMismatch={unresolvedMispredictedFrames}, OutOfOrderAuthority={authorityDriver.OutOfOrderAuthorityCount}");
                    }
                }

                // 最后 6 个逻辑帧可能仍有 P2 输入尚未达到计划发送时间。
                // World 保持停在 2000，只推进“网络时间”。
                for (int networkFrame = TotalFrames + 1; networkFrame <= TotalFrames + MaxInputDelayFrames; networkFrame++)
                {
                    SendDuePlayer2Inputs(
                        delayedP2Inputs[networkFrame],
                        client2,
                        ref highestSentP2Frame,
                        ref p2OutOfOrderSendCount);

                    PumpClient1Authorities(
                        client1,
                        authorityDriver,
                        authoritativeHistory,
                        frameMispredicted,
                        authorityReceived,
                        reference,
                        predicted,
                        TotalFrames,
                        ref authorityReceivedCount,
                        ref mismatchAuthorityCorrectionCount,
                        ref unresolvedMispredictedFrames,
                        ref convergenceCheckpointCount);

                    PumpClient2Authorities(
                        client2,
                        ref client2AuthorityDrainedCount);

                    ThrowIfClientRejected(client1);
                    ThrowIfClientRejected(client2);

                    yield return null;

                    PumpClient1Authorities(
                        client1,
                        authorityDriver,
                        authoritativeHistory,
                        frameMispredicted,
                        authorityReceived,
                        reference,
                        predicted,
                        TotalFrames,
                        ref authorityReceivedCount,
                        ref mismatchAuthorityCorrectionCount,
                        ref unresolvedMispredictedFrames,
                        ref convergenceCheckpointCount);

                    PumpClient2Authorities(
                        client2,
                        ref client2AuthorityDrainedCount);

                    ThrowIfClientRejected(client1);
                    ThrowIfClientRejected(client2);
                }

                // 所有输入都已经发出，等待公网中尚未返回的 Authority。
                double deadline = Time.realtimeSinceStartupAsDouble + FinalFlushTimeoutSeconds;

                while (authorityReceivedCount < TotalFrames || client2AuthorityDrainedCount < TotalFrames)
                {
                    PumpClient1Authorities(
                        client1,
                        authorityDriver,
                        authoritativeHistory,
                        frameMispredicted,
                        authorityReceived,
                        reference,
                        predicted,
                        TotalFrames,
                        ref authorityReceivedCount,
                        ref mismatchAuthorityCorrectionCount,
                        ref unresolvedMispredictedFrames,
                        ref convergenceCheckpointCount);

                    PumpClient2Authorities(
                        client2,
                        ref client2AuthorityDrainedCount);

                    ThrowIfClientRejected(client1);
                    ThrowIfClientRejected(client2);

                    if (authorityReceivedCount == TotalFrames && client2AuthorityDrainedCount == TotalFrames) break;

                    if (Time.realtimeSinceStartupAsDouble >= deadline)
                    {
                        throw new TimeoutException(
                            $"PublicRandomRollbackStressValidationTestBootstrap Run Error: Final Authority Flush Timeout, " +
                            $"Client1Authorities={authorityReceivedCount}/{TotalFrames}, Client2Authorities={client2AuthorityDrainedCount}/{TotalFrames}, " +
                            $"OutstandingMismatch={unresolvedMispredictedFrames}, DriverApplied={authorityDriver.AppliedAuthorityCount}, " +
                            $"P1=[{GetClientState(client1)}], P2=[{GetClientState(client2)}]");
                    }

                    yield return null;
                }

                Expect(client1.LastSentSequence == (uint)TotalFrames,
                    $"PublicRandomRollbackStressValidationTestBootstrap Run Error: Client1 Sequence Expected={TotalFrames}, Actual={client1.LastSentSequence}");

                Expect(client2.LastSentSequence == (uint)TotalFrames,
                    $"PublicRandomRollbackStressValidationTestBootstrap Run Error: Client2 Sequence Expected={TotalFrames}, Actual={client2.LastSentSequence}");

                Expect(authorityReceivedCount == TotalFrames,
                    $"PublicRandomRollbackStressValidationTestBootstrap Run Error: Authority Receive Count Expected={TotalFrames}, Actual={authorityReceivedCount}");

                Expect(client2AuthorityDrainedCount == TotalFrames,
                    $"PublicRandomRollbackStressValidationTestBootstrap Run Error: Client2 Broadcast Count Expected={TotalFrames}, Actual={client2AuthorityDrainedCount}");

                Expect(authorityDriver.AppliedAuthorityCount == TotalFrames,
                    $"PublicRandomRollbackStressValidationTestBootstrap Run Error: Authority Driver Count Expected={TotalFrames}, Actual={authorityDriver.AppliedAuthorityCount}");

                Expect(unresolvedMispredictedFrames == 0,
                    $"PublicRandomRollbackStressValidationTestBootstrap Run Error: Unresolved Prediction Count={unresolvedMispredictedFrames}");

                Expect(delayedInputCount >= MinDelayedInputCount,
                    $"PublicRandomRollbackStressValidationTestBootstrap Run Error: Delay Coverage Expected>={MinDelayedInputCount}, Actual={delayedInputCount}");

                Expect(maxObservedDelay == MaxInputDelayFrames,
                    $"PublicRandomRollbackStressValidationTestBootstrap Run Error: Max Delay Expected={MaxInputDelayFrames}, Actual={maxObservedDelay}");

                Expect(mispredictedFrameCount >= MinMispredictedFrameCount,
                    $"PublicRandomRollbackStressValidationTestBootstrap Run Error: Misprediction Coverage Expected>={MinMispredictedFrameCount}, Actual={mispredictedFrameCount}");

                Expect(mismatchAuthorityCorrectionCount == mispredictedFrameCount,
                    $"PublicRandomRollbackStressValidationTestBootstrap Run Error: Correction Coverage Mispredicted={mispredictedFrameCount}, Corrections={mismatchAuthorityCorrectionCount}");

                Expect(p2OutOfOrderSendCount >= MinOutOfOrderP2SendCount,
                    $"PublicRandomRollbackStressValidationTestBootstrap Run Error: P2 OutOfOrder Coverage Expected>={MinOutOfOrderP2SendCount}, Actual={p2OutOfOrderSendCount}");

                Expect(authorityDriver.OutOfOrderAuthorityCount >= MinOutOfOrderAuthorityCount,
                    $"PublicRandomRollbackStressValidationTestBootstrap Run Error: Authority OutOfOrder Coverage Expected>={MinOutOfOrderAuthorityCount}, Actual={authorityDriver.OutOfOrderAuthorityCount}");

                Expect(correctPredictionCount > 0,
                    "PublicRandomRollbackStressValidationTestBootstrap Run Error: No Correct Predictions");

                Expect(convergenceCheckpointCount > 0,
                    "PublicRandomRollbackStressValidationTestBootstrap Run Error: No Convergence Checkpoint Was Observed");

                AssertWorldEqual(reference, predicted, TotalFrames, "Public Random Final");

                uint finalReferenceChecksum = WorldChecksumCalculator.Calculate(reference.World);
                uint finalPredictedChecksum = WorldChecksumCalculator.Calculate(predicted.World);

                stopwatch.Stop();

                var report = new PublicRandomRollbackStressReport(
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
                    client2AuthorityDrainedCount,
                    convergenceCheckpointCount,
                    finalReferenceChecksum,
                    finalPredictedChecksum,
                    stopwatch.Elapsed.TotalMilliseconds);

                UnityEngine.Debug.Log(report.ToDisplayString());
            }
            finally
            {
                client1?.Dispose();
                client2?.Dispose();
                reference?.Dispose();
                predicted?.Dispose();
            }
        }

        private static void SendDuePlayer2Inputs(
            List<PlayerInputSnapshot> dueInputs,
            LocalNetworkInputClient client,
            ref int highestSentFrame,
            ref int outOfOrderSendCount)
        {
            if (dueInputs == null) return;

            for (int i = 0; i < dueInputs.Count; i++)
            {
                PlayerInputSnapshot input = dueInputs[i];

                if (highestSentFrame > 0 && input.frameNumber < highestSentFrame)
                    outOfOrderSendCount++;

                if (input.frameNumber > highestSentFrame)
                    highestSentFrame = input.frameNumber;

                client.SendInput(in input);
            }
        }

        private static void PumpClient1Authorities(
            LocalNetworkInputClient client,
            NetworkAuthorityRollbackDriver authorityDriver,
            FrameInputSet[] authoritativeHistory,
            bool[] frameMispredicted,
            bool[] authorityReceived,
            TestEnvironment reference,
            TestEnvironment predicted,
            int currentFrame,
            ref int authorityReceivedCount,
            ref int mismatchAuthorityCorrectionCount,
            ref int unresolvedMispredictedFrames,
            ref int convergenceCheckpointCount)
        {
            while (client.TryReceiveAuthority(out ServerAuthorityFramePacket authority))
            {
                int authorityFrame = authority.InputSet.frameNumber;

                Expect(authorityFrame > 0 && authorityFrame <= TotalFrames,
                    $"PublicRandomRollbackStressValidationTestBootstrap Pump Error: Invalid Authority Frame={authorityFrame}");

                Expect(authorityFrame <= currentFrame,
                    $"PublicRandomRollbackStressValidationTestBootstrap Pump Error: Future Authority Frame={authorityFrame}, CurrentFrame={currentFrame}");

                Expect(!authorityReceived[authorityFrame],
                    $"PublicRandomRollbackStressValidationTestBootstrap Pump Error: Duplicate Authority Frame={authorityFrame}");

                AssertFrameInputSetBitExact(
                    authoritativeHistory[authorityFrame],
                    authority.InputSet,
                    authorityFrame);

                bool hadOutstandingMismatch = unresolvedMispredictedFrames > 0;
                int frameBefore = predicted.Coordinator.CurrentFrame;

                authorityDriver.Apply(in authority);

                int frameAfter = predicted.Coordinator.CurrentFrame;

                Expect(frameAfter == frameBefore,
                    $"PublicRandomRollbackStressValidationTestBootstrap Pump Error: CurrentFrame Changed, AuthorityFrame={authorityFrame}, Before={frameBefore}, After={frameAfter}");

                authorityReceived[authorityFrame] = true;
                authorityReceivedCount++;

                if (frameMispredicted[authorityFrame])
                {
                    mismatchAuthorityCorrectionCount++;
                    unresolvedMispredictedFrames--;

                    Expect(unresolvedMispredictedFrames >= 0,
                        $"PublicRandomRollbackStressValidationTestBootstrap Pump Error: Unresolved Prediction Underflow, AuthorityFrame={authorityFrame}");
                }

                if (hadOutstandingMismatch && unresolvedMispredictedFrames == 0)
                {
                    AssertWorldEqual(
                        reference,
                        predicted,
                        currentFrame,
                        $"Public Random Convergence AuthorityFrame={authorityFrame}");

                    convergenceCheckpointCount++;
                }
            }
        }

        private static void PumpClient2Authorities(LocalNetworkInputClient client, ref int drainedCount)
        {
            while (client.TryReceiveAuthority(out ServerAuthorityFramePacket authority))
            {
                int frame = authority.InputSet.frameNumber;

                Expect(frame > 0 && frame <= TotalFrames,
                    $"PublicRandomRollbackStressValidationTestBootstrap Client2 Drain Error: Invalid Authority Frame={frame}");

                drainedCount++;
            }

            Expect(drainedCount <= TotalFrames,
                $"PublicRandomRollbackStressValidationTestBootstrap Client2 Drain Error: Count={drainedCount}, Expected<={TotalFrames}");
        }

        private static void ThrowIfClientRejected(LocalNetworkInputClient client)
        {
            if (client.LastRejectReason == NetworkInputExchangeRejectReason.None) return;

            throw new InvalidOperationException(
                $"PublicRandomRollbackStressValidationTestBootstrap Client Reject Error: PlayerID={client.PlayerID}, " +
                $"Reason={client.LastRejectReason}, Decode={client.LastDecodeError}, Endpoint={client.LocalEndPoint}");
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
                $"PublicRandomRollbackStressValidationTestBootstrap DriveFrame Error: Frame={frame}, Kind={result.FailureKind}, Message={result.Message}");

            var context = new SimulationContext(frame, TickLength, false);

            env.CommandApplier.ApplyCommandsToWorld(frame, SimulationFrameCommandTiming.BeforeTick);
            env.World.Tick(in context);
            env.CommandApplier.ApplyCommandsToWorld(frame, SimulationFrameCommandTiming.AfterTick);

            if (saveSnapshot) env.Coordinator.SaveSnapshot();
        }

        private static PlayerInputSnapshot CreatePlayerInput(int frame, int playerID)
        {
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

        private static int NextRange(ref uint state, int minInclusive, int maxInclusive)
        {
            state = NextRandom(state);
            return minInclusive + (int)(state % (uint)(maxInclusive - minInclusive + 1));
        }

        private static uint NextRandom(uint value)
        {
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            return value;
        }

        private static void AssertFrameInputSetBitExact(FrameInputSet expected, FrameInputSet actual, int frame)
        {
            Expect(expected.IsCreated && actual.IsCreated,
                $"PublicRandomRollbackStressValidationTestBootstrap Authority Data Error: Frame={frame}, InputSet Not Created");

            Expect(expected.frameNumber == actual.frameNumber,
                $"PublicRandomRollbackStressValidationTestBootstrap Authority Data Error: ExpectedFrame={expected.frameNumber}, ActualFrame={actual.frameNumber}");

            Expect(expected.Count == actual.Count,
                $"PublicRandomRollbackStressValidationTestBootstrap Authority Data Error: Frame={frame}, ExpectedCount={expected.Count}, ActualCount={actual.Count}");

            for (int playerID = Player1ID; playerID <= Player2ID; playerID++)
            {
                Expect(expected.TryGetInput(playerID, out PlayerInputSnapshot a),
                    $"PublicRandomRollbackStressValidationTestBootstrap Authority Data Error: Expected P{playerID} Missing, Frame={frame}");

                Expect(actual.TryGetInput(playerID, out PlayerInputSnapshot b),
                    $"PublicRandomRollbackStressValidationTestBootstrap Authority Data Error: Actual P{playerID} Missing, Frame={frame}");

                AssertPlayerInputBitExact(in a, in b, frame, playerID);
            }
        }

        private static void AssertPlayerInputBitExact(in PlayerInputSnapshot a, in PlayerInputSnapshot b, int frame, int playerID)
        {
            Expect(a.frameNumber == b.frameNumber,
                $"PublicRandomRollbackStressValidationTestBootstrap Authority Input Error: Frame={frame}, PlayerID={playerID}, Field=FrameNumber, A={a.frameNumber}, B={b.frameNumber}");

            Expect(a.playerID == b.playerID,
                $"PublicRandomRollbackStressValidationTestBootstrap Authority Input Error: Frame={frame}, PlayerID={playerID}, Field=PlayerID, A={a.playerID}, B={b.playerID}");

            ExpectFloatBits(a.moveX, b.moveX, frame, $"Authority P{playerID}", "MoveX");
            ExpectFloatBits(a.moveY, b.moveY, frame, $"Authority P{playerID}", "MoveY");
            ExpectFloatBits(a.mouseX, b.mouseX, frame, $"Authority P{playerID}", "MouseX");
            ExpectFloatBits(a.mouseY, b.mouseY, frame, $"Authority P{playerID}", "MouseY");
            ExpectFloatBits(a.mouseDeltaX, b.mouseDeltaX, frame, $"Authority P{playerID}", "MouseDeltaX");
            ExpectFloatBits(a.mouseDeltaY, b.mouseDeltaY, frame, $"Authority P{playerID}", "MouseDeltaY");
            ExpectFloatBits(a.scrollX, b.scrollX, frame, $"Authority P{playerID}", "ScrollX");
            ExpectFloatBits(a.scrollY, b.scrollY, frame, $"Authority P{playerID}", "ScrollY");

            Expect(a.pressedButtons == b.pressedButtons,
                $"PublicRandomRollbackStressValidationTestBootstrap Authority Input Error: Frame={frame}, PlayerID={playerID}, Field=PressedButtons");

            Expect(a.heldButtons == b.heldButtons,
                $"PublicRandomRollbackStressValidationTestBootstrap Authority Input Error: Frame={frame}, PlayerID={playerID}, Field=HeldButtons");

            Expect(a.releasedButtons == b.releasedButtons,
                $"PublicRandomRollbackStressValidationTestBootstrap Authority Input Error: Frame={frame}, PlayerID={playerID}, Field=ReleasedButtons");
        }

        private static void AssertWorldEqual(TestEnvironment reference, TestEnvironment predicted, int frame, string stage)
        {
            Expect(reference.Coordinator.CurrentFrame == predicted.Coordinator.CurrentFrame,
                $"{stage} CoordinatorFrame Error: Frame={frame}, Reference={reference.Coordinator.CurrentFrame}, Predicted={predicted.Coordinator.CurrentFrame}");

            Expect(reference.World.AliveEntityCount == predicted.World.AliveEntityCount,
                $"{stage} AliveEntityCount Error: Frame={frame}, Reference={reference.World.AliveEntityCount}, Predicted={predicted.World.AliveEntityCount}");

            Expect(reference.World.CreatedEntityCount == predicted.World.CreatedEntityCount,
                $"{stage} CreatedEntityCount Error: Frame={frame}, Reference={reference.World.CreatedEntityCount}, Predicted={predicted.World.CreatedEntityCount}");

            for (int i = 0; i < reference.Players.Length; i++)
            {
                AssertPlayerEqual(
                    reference,
                    predicted,
                    reference.Players[i],
                    predicted.Players[i],
                    frame,
                    $"{stage} P{i + 1}");
            }

            uint checksumA = WorldChecksumCalculator.Calculate(reference.World);
            uint checksumB = WorldChecksumCalculator.Calculate(predicted.World);

            Expect(checksumA == checksumB,
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

        private static void ExpectFloatBits(float a, float b, int frame, string stage, string field)
        {
            int bitsA = BitConverter.SingleToInt32Bits(a);
            int bitsB = BitConverter.SingleToInt32Bits(b);

            Expect(bitsA == bitsB,
                $"{stage} {field} Error: Frame={frame}, Reference={a}({bitsA:X8}), Predicted={b}({bitsB:X8})");
        }

        private static string GetClientState(LocalNetworkInputClient client)
            => $"LastSentSequence={client.LastSentSequence}, LastRejectReason={client.LastRejectReason}, LastDecodeError={client.LastDecodeError}";

        private static void Expect(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        /// <summary>
        /// 公网随机 Rollback 压力统计。
        /// </summary>
        public sealed class PublicRandomRollbackStressReport
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
            public readonly int Client2AuthorityDrainedCount;
            public readonly int ConvergenceCheckpointCount;
            public readonly uint FinalReferenceChecksum;
            public readonly uint FinalPredictedChecksum;
            public readonly double ElapsedMilliseconds;

            public PublicRandomRollbackStressReport(
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
                int client2AuthorityDrainedCount,
                int convergenceCheckpointCount,
                uint finalReferenceChecksum,
                uint finalPredictedChecksum,
                double elapsedMilliseconds)
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
                Client2AuthorityDrainedCount = client2AuthorityDrainedCount;
                ConvergenceCheckpointCount = convergenceCheckpointCount;
                FinalReferenceChecksum = finalReferenceChecksum;
                FinalPredictedChecksum = finalPredictedChecksum;
                ElapsedMilliseconds = elapsedMilliseconds;
            }

            public string ToDisplayString()
            {
                return
                    $"PublicRandomRollbackStressValidationTestBootstrap Run Log: [PUBLIC RANDOM ROLLBACK STRESS]\n" +
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
                    $"Client2 Authority Broadcasts = {Client2AuthorityDrainedCount}\n" +
                    $"Convergence Checkpoints      = {ConvergenceCheckpointCount}\n" +
                    $"Final Reference Checksum     = 0x{FinalReferenceChecksum:X8}\n" +
                    $"Final Predicted Checksum     = 0x{FinalPredictedChecksum:X8}\n" +
                    $"Elapsed                      = {ElapsedMilliseconds:F2} ms\n" +
                    $"Result                       = PASS";
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