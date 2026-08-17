using ECSFrameWork;
using System;

namespace FrameWork.RollBackSystem
{
    /// <summary>
    /// 验证部分玩家输入缺失后使用预测输入，并在权威输入晚到时触发多人 Rollback 收敛。
    /// </summary>
    public static class PartialInputRollbackValidationTestBootstrap
    {
        private const int Player1ID = 1;
        private const int Player2ID = 2;
        private const uint Seed = 20260817u;
        private const int CorrectionFrame = 120;
        private const int PostRollbackFrameCount = 60;
        private const float TickLength = 1f / 60f;

        /// <summary>
        /// Player2 在 CorrectionFrame 输入缺失，使用 LastKnown 预测，延迟若干帧后收到权威输入并回滚。
        /// </summary>
        public static void RunPartialInputRollbackTestStatic(int authoritativeDelay)
        {
            if (authoritativeDelay <= 0) throw new ArgumentOutOfRangeException(nameof(authoritativeDelay));

            int receiveFrame = CorrectionFrame + authoritativeDelay;
            int endFrame = receiveFrame + PostRollbackFrameCount;

            using var reference = CreateEnvironment(true);
            using var predicted = CreateEnvironment(true);

            var assembler = new FrameInputAssembler(new LastKnownPlayerInputPredictionPolicy());
            assembler.RegisterPlayer(Player1ID);
            assembler.RegisterPlayer(Player2ID);

            FrameInputSet authoritativeCorrection = default;
            bool predictionObserved = false;
            bool rollbackCompleted = false;

            for (int frame = 1; frame <= endFrame; frame++)
            {
                FrameInputSet authoritative = CreateAuthoritativeFrameInputSet(frame);
                if (frame == CorrectionFrame) authoritativeCorrection = authoritative;

                var accumulator = new FrameInputAccumulator(frame);

                PlayerInputSnapshot player1 = CreatePlayerInput(frame, Player1ID);
                accumulator.TryAddInput(in player1);

                if (frame != CorrectionFrame)
                {
                    PlayerInputSnapshot player2 = CreatePlayerInput(frame, Player2ID);
                    accumulator.TryAddInput(in player2);
                }

                FrameInputAssemblyResult assembled = assembler.Assemble(accumulator);

                if (frame == CorrectionFrame)
                {
                    Expect(!assembled.IsPredicted(Player1ID),
                        $"PartialInput Prediction Error: Frame={frame}, Player1 Must Be Real");

                    Expect(assembled.IsPredicted(Player2ID),
                        $"PartialInput Prediction Error: Frame={frame}, Player2 Must Be Predicted");

                    Expect(assembled.PredictedCount == 1,
                        $"PartialInput PredictionCount Error: Frame={frame}, Expected=1, Actual={assembled.PredictedCount}");

                    predictionObserved = true;
                }

                DriveFrame(reference, frame, authoritative, true);
                DriveFrame(predicted, frame, assembled.InputSet, true);

                if (frame != receiveFrame) continue;

                AssertPlayerEqual(reference, predicted, reference.Player1, predicted.Player1, frame, $"PreRollback Delay={authoritativeDelay} Player1");
                ExpectPlayerStateDifferent(reference, predicted, reference.Player2, predicted.Player2, frame, authoritativeDelay);

                Expect(authoritativeCorrection.TryGetInput(Player2ID, out PlayerInputSnapshot delayedPlayer2),
                    $"PartialInput Authoritative Player2 Missing Error: Frame={CorrectionFrame}");

                assembler.ObserveAuthoritativeInput(in delayedPlayer2);
                predicted.Coordinator.ReceiveAuthoritativeInput(CorrectionFrame, authoritativeCorrection);

                Expect(predicted.Coordinator.CurrentFrame == frame,
                    $"PartialInput Rollback CurrentFrame Error: Delay={authoritativeDelay}, Expected={frame}, Actual={predicted.Coordinator.CurrentFrame}");

                AssertWorldEqual(reference, predicted, frame, $"AfterRollback Delay={authoritativeDelay}");
                rollbackCompleted = true;
            }

            Expect(predictionObserved,
                $"PartialInput Prediction Execution Error: Delay={authoritativeDelay}, Prediction Was Not Observed");

            Expect(rollbackCompleted,
                $"PartialInput Rollback Execution Error: Delay={authoritativeDelay}, Rollback Was Not Triggered");

            AssertWorldEqual(reference, predicted, endFrame, $"Final Delay={authoritativeDelay}");
        }

        private static TestEnvironment CreateEnvironment(bool saveInitialSnapshot)
        {
            var world = new World { EnableSystemProfile = false };

            Entity player1 = world.CreateEntity();
            world.SetComponent(player1, new PlayerTagComponent());
            world.SetComponent(player1, new PlayerInputSnapshotComponent(0, Player1ID, 0f, 0f));
            world.SetComponent(player1, new MoveSpeedComponent(3.25f));
            world.SetComponent(player1, new VelocityComponent(0f, 0f, 0f));
            world.SetComponent(player1, new PositionComponent(-5f, 0f, 0f));

            Entity player2 = world.CreateEntity();
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
            rollbackAdapter.SetFrameCommandReplayBinding(new RollbackFrameCommandReplayBinding(commandBuffer, commandApplier));

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

            var environment = new TestEnvironment(world, player1, player2, coordinator, commandBuffer, commandApplier, snapshotBuffer);

            if (saveInitialSnapshot) coordinator.SaveSnapshot();
            return environment;
        }

        private static void DriveFrame(TestEnvironment env, int frame, FrameInputSet input, bool saveSnapshot)
        {
            RollbackStepResult result = env.Coordinator.TryStep(frame, input);

            Expect(result.Succeeded,
                $"PartialInput DriveFrame Error: Frame={frame}, Kind={result.FailureKind}, Message={result.Message}");

            var context = new SimulationContext(frame, TickLength, false);

            env.CommandApplier.ApplyCommandsToWorld(frame, SimulationFrameCommandTiming.BeforeTick);
            env.World.Tick(in context);
            env.CommandApplier.ApplyCommandsToWorld(frame, SimulationFrameCommandTiming.AfterTick);

            if (saveSnapshot) env.Coordinator.SaveSnapshot();
        }

        private static FrameInputSet CreateAuthoritativeFrameInputSet(int frame)
        {
            return new FrameInputSet(frame, new[]
            {
                CreatePlayerInput(frame,Player1ID),
                CreatePlayerInput(frame,Player2ID)
            });
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

            uint state = Seed ^ unchecked((uint)frame * 0x9E3779B9u) ^ unchecked((uint)playerID * 0x85EBCA6Bu);

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

        private static void AssertWorldEqual(TestEnvironment a, TestEnvironment b, int frame, string stage)
        {
            Expect(a.Coordinator.CurrentFrame == b.Coordinator.CurrentFrame,
                $"{stage} CoordinatorFrame Error: Frame={frame}, A={a.Coordinator.CurrentFrame}, B={b.Coordinator.CurrentFrame}");

            Expect(a.World.AliveEntityCount == b.World.AliveEntityCount,
                $"{stage} AliveEntityCount Error: Frame={frame}, A={a.World.AliveEntityCount}, B={b.World.AliveEntityCount}");

            Expect(a.World.CreatedEntityCount == b.World.CreatedEntityCount,
                $"{stage} CreatedEntityCount Error: Frame={frame}, A={a.World.CreatedEntityCount}, B={b.World.CreatedEntityCount}");

            AssertPlayerEqual(a, b, a.Player1, b.Player1, frame, $"{stage} Player1");
            AssertPlayerEqual(a, b, a.Player2, b.Player2, frame, $"{stage} Player2");

            uint checksumA = WorldChecksumCalculator.Calculate(a.World);
            uint checksumB = WorldChecksumCalculator.Calculate(b.World);

            Expect(checksumA == checksumB,
                $"{stage} Checksum Error: Frame={frame}, A=0x{checksumA:X8}, B=0x{checksumB:X8}");
        }

        private static void AssertPlayerEqual(TestEnvironment a, TestEnvironment b, Entity playerA, Entity playerB, int frame, string stage)
        {
            Expect(a.World.TryGetComponent(playerA, out PositionComponent positionA),
                $"{stage} PositionA Missing Error: Frame={frame}");

            Expect(b.World.TryGetComponent(playerB, out PositionComponent positionB),
                $"{stage} PositionB Missing Error: Frame={frame}");

            ExpectFloatBits(positionA.x, positionB.x, frame, stage, "Position.X");
            ExpectFloatBits(positionA.y, positionB.y, frame, stage, "Position.Y");
            ExpectFloatBits(positionA.z, positionB.z, frame, stage, "Position.Z");

            Expect(a.World.TryGetComponent(playerA, out VelocityComponent velocityA),
                $"{stage} VelocityA Missing Error: Frame={frame}");

            Expect(b.World.TryGetComponent(playerB, out VelocityComponent velocityB),
                $"{stage} VelocityB Missing Error: Frame={frame}");

            ExpectFloatBits(velocityA.x, velocityB.x, frame, stage, "Velocity.X");
            ExpectFloatBits(velocityA.y, velocityB.y, frame, stage, "Velocity.Y");
            ExpectFloatBits(velocityA.z, velocityB.z, frame, stage, "Velocity.Z");

            Expect(a.World.TryGetComponent(playerA, out PlayerInputSnapshotComponent inputA),
                $"{stage} InputA Missing Error: Frame={frame}");

            Expect(b.World.TryGetComponent(playerB, out PlayerInputSnapshotComponent inputB),
                $"{stage} InputB Missing Error: Frame={frame}");

            Expect(inputA.inputFrame == inputB.inputFrame,
                $"{stage} InputFrame Error: Frame={frame}, A={inputA.inputFrame}, B={inputB.inputFrame}");

            Expect(inputA.playerID == inputB.playerID,
                $"{stage} PlayerID Error: Frame={frame}, A={inputA.playerID}, B={inputB.playerID}");

            ExpectFloatBits(inputA.moveX, inputB.moveX, frame, stage, "Input.MoveX");
            ExpectFloatBits(inputA.moveY, inputB.moveY, frame, stage, "Input.MoveY");
        }

        private static void ExpectPlayerStateDifferent(TestEnvironment reference, TestEnvironment predicted, Entity referencePlayer, Entity predictedPlayer, int frame, int delay)
        {
            Expect(reference.World.TryGetComponent(referencePlayer, out PositionComponent positionA),
                $"PartialInput PreCheck Reference Position Missing Error: Frame={frame}");

            Expect(predicted.World.TryGetComponent(predictedPlayer, out PositionComponent positionB),
                $"PartialInput PreCheck Predicted Position Missing Error: Frame={frame}");

            bool different =
                FloatBits(positionA.x) != FloatBits(positionB.x) ||
                FloatBits(positionA.z) != FloatBits(positionB.z);

            Expect(different,
                $"PartialInput PreCheck Error: Frame={frame}, Delay={delay}, Player2 Predicted State Did Not Diverge");
        }

        private static void ExpectFloatBits(float a, float b, int frame, string stage, string field)
        {
            int bitsA = FloatBits(a);
            int bitsB = FloatBits(b);

            Expect(bitsA == bitsB,
                $"{stage} {field} Error: Frame={frame}, A={a}({bitsA:X8}), B={b}({bitsB:X8})");
        }

        private static int FloatBits(float value) => BitConverter.SingleToInt32Bits(value);

        private static void Expect(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private sealed class TestEnvironment : IDisposable
        {
            public readonly World World;
            public readonly Entity Player1;
            public readonly Entity Player2;
            public readonly RollbackCoordinator<FrameInputSet, EcsWorldSnapshot> Coordinator;
            public readonly SimulationFrameCommandBuffer CommandBuffer;
            public readonly SimulationFrameCommandApplier CommandApplier;
            public readonly SnapshotRingBuffer<EcsWorldSnapshot> SnapshotBuffer;

            public TestEnvironment(World world, Entity player1, Entity player2, RollbackCoordinator<FrameInputSet, EcsWorldSnapshot> coordinator, SimulationFrameCommandBuffer commandBuffer, SimulationFrameCommandApplier commandApplier, SnapshotRingBuffer<EcsWorldSnapshot> snapshotBuffer)
            {
                World = world;
                Player1 = player1;
                Player2 = player2;
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