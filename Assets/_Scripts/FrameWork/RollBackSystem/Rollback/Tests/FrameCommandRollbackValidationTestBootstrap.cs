using ECSFrameWork;
using System;

namespace FrameWork.RollBackSystem
{
    /// <summary>
    /// FrameCommand 本地 Rollback 验证。
    /// </summary>
    public static class FrameCommandRollbackValidationTestBootstrap
    {
        private const int PlayerID = 1;
        private const int Seed = 20260817;
        private const int CorrectionFrame = 120;
        private const int PostRollbackFrameCount = 60;
        private const float TickLength = 1f / 60f;
        private const int ExpectedFinalCommandValue = 1111110;

        /// <summary>
        /// 验证普通逻辑帧中 BeforeTick 与 AfterTick 的执行时序。
        /// </summary>
        public static void RunFrameCommandTimingTestStatic()
        {
            const int commandFrame = 10;
            using var env = CreateEnvironment(false);

            var before = new IncrementCounterFrameCommand(commandFrame, env.Player, 10);
            var after = new IncrementCounterFrameCommand(commandFrame, env.Player, 100);
            env.CommandBuffer.AddCommand(before, SimulationFrameCommandTiming.BeforeTick);
            env.CommandBuffer.AddCommand(after, SimulationFrameCommandTiming.AfterTick);

            for (int frame = 1; frame <= commandFrame; frame++) DriveFrame(env, frame, CreateInput(frame), false);

            AssertCounter(env, 110, commandFrame, "Timing EndFrame");
            AssertObserved(env, 10, commandFrame, commandFrame, "Timing EndFrame");
            Expect(before.ExecuteCount == 1, $"FrameCommand Timing BeforeTick Error: ExpectedExecuteCount=1, Actual={before.ExecuteCount}");
            Expect(after.ExecuteCount == 1, $"FrameCommand Timing AfterTick Error: ExpectedExecuteCount=1, Actual={after.ExecuteCount}");

            DriveFrame(env, commandFrame + 1, CreateInput(commandFrame + 1), false);
            AssertCounter(env, 110, commandFrame + 1, "Timing NextFrame");
            AssertObserved(env, 110, commandFrame + 1, commandFrame + 1, "Timing NextFrame");
        }

        /// <summary>
        /// 验证 FrameCommand 在不同 Rollback 深度下被正确重放并最终收敛到 Reference World。
        /// </summary>
        public static void RunFrameCommandRollbackReplayTestStatic(int rollbackDepth)
        {
            Expect(rollbackDepth > 0, $"FrameCommand Rollback Depth Error: Value={rollbackDepth}");

            int receiveFrame = CorrectionFrame + rollbackDepth;
            int endFrame = receiveFrame + PostRollbackFrameCount;
            bool rollbackCompleted = false;

            using var reference = CreateEnvironment(true);
            using var predicted = CreateEnvironment(true);
            FrameCommandScenario referenceScenario = ScheduleScenario(reference);
            FrameCommandScenario predictedScenario = ScheduleScenario(predicted);
            PlayerInputSnapshot authoritativeCorrection = default;

            for (int frame = 1; frame <= endFrame; frame++)
            {
                PlayerInputSnapshot authoritative = CreateInput(frame);
                PlayerInputSnapshot local = authoritative;

                if (frame == CorrectionFrame)
                {
                    authoritativeCorrection = authoritative;
                    local = CreateWrongPrediction(authoritative);
                }

                DriveFrame(reference, frame, authoritative, true);
                DriveFrame(predicted, frame, local, true);

                if (frame == receiveFrame)
                {
                    ExpectStateDifferent(reference, predicted, frame);
                    predicted.Coordinator.ReceiveAuthoritativeInput(CorrectionFrame, authoritativeCorrection);

                    Expect(predicted.Coordinator.CurrentFrame == frame, $"FrameCommand Rollback CurrentFrame Error: Depth={rollbackDepth}, Expected={frame}, Actual={predicted.Coordinator.CurrentFrame}");
                    AssertStateEqual(reference, predicted, frame, $"AfterRollback Depth={rollbackDepth}");

                    Expect(referenceScenario.CorrectionBefore.ExecuteCount == 1, $"FrameCommand Reference Before ReplayCount Error: Depth={rollbackDepth}, Expected=1, Actual={referenceScenario.CorrectionBefore.ExecuteCount}");
                    Expect(referenceScenario.CorrectionAfter.ExecuteCount == 1, $"FrameCommand Reference After ReplayCount Error: Depth={rollbackDepth}, Expected=1, Actual={referenceScenario.CorrectionAfter.ExecuteCount}");
                    Expect(predictedScenario.CorrectionBefore.ExecuteCount == 2, $"FrameCommand Predicted Before ReplayCount Error: Depth={rollbackDepth}, Expected=2, Actual={predictedScenario.CorrectionBefore.ExecuteCount}");
                    Expect(predictedScenario.CorrectionAfter.ExecuteCount == 2, $"FrameCommand Predicted After ReplayCount Error: Depth={rollbackDepth}, Expected=2, Actual={predictedScenario.CorrectionAfter.ExecuteCount}");

                    rollbackCompleted = true;
                    continue;
                }

                if (rollbackCompleted) AssertStateEqual(reference, predicted, frame, $"PostRollback Depth={rollbackDepth}");
            }

            Expect(rollbackCompleted, $"FrameCommand Rollback Execution Error: Depth={rollbackDepth}, Rollback Was Not Triggered");
            AssertStateEqual(reference, predicted, endFrame, $"Final Depth={rollbackDepth}");
            AssertCounter(reference, ExpectedFinalCommandValue, endFrame, $"Reference Final Depth={rollbackDepth}");
            AssertCounter(predicted, ExpectedFinalCommandValue, endFrame, $"Predicted Final Depth={rollbackDepth}");
            AssertObserved(reference, ExpectedFinalCommandValue, endFrame, endFrame, $"Reference Final Depth={rollbackDepth}");
            AssertObserved(predicted, ExpectedFinalCommandValue, endFrame, endFrame, $"Predicted Final Depth={rollbackDepth}");
            AssertFinalExecutionCounts(referenceScenario, predictedScenario, receiveFrame, rollbackDepth);
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

            var observation = new FrameCommandObservation();
            world.AddSystem(new InputMoveSystem());
            world.AddSystem(new FrameCommandProbeSystem(player, observation));
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

            var environment = new TestEnvironment(world, player, coordinator, commandBuffer, commandApplier, snapshotBuffer, observation);
            if (saveInitialSnapshot) coordinator.SaveSnapshot();
            return environment;
        }

        private static FrameCommandScenario ScheduleScenario(TestEnvironment env)
        {
            var correctionBefore = AddIncrementCommand(env, CorrectionFrame, SimulationFrameCommandTiming.BeforeTick, 10);
            var correctionAfter = AddIncrementCommand(env, CorrectionFrame, SimulationFrameCommandTiming.AfterTick, 100);
            var frame122Before = AddIncrementCommand(env, 122, SimulationFrameCommandTiming.BeforeTick, 1000);
            var frame122After = AddIncrementCommand(env, 122, SimulationFrameCommandTiming.AfterTick, 10000);
            var frame130Before = AddIncrementCommand(env, 130, SimulationFrameCommandTiming.BeforeTick, 100000);
            var frame130After = AddIncrementCommand(env, 130, SimulationFrameCommandTiming.AfterTick, 1000000);

            return new FrameCommandScenario(correctionBefore, correctionAfter, frame122Before, frame122After, frame130Before, frame130After);
        }

        private static IncrementCounterFrameCommand AddIncrementCommand(TestEnvironment env, int frame, SimulationFrameCommandTiming timing, int delta)
        {
            var command = new IncrementCounterFrameCommand(frame, env.Player, delta);
            env.CommandBuffer.AddCommand(command, timing);
            return command;
        }

        private static void DriveFrame(TestEnvironment env, int frame, PlayerInputSnapshot input, bool saveSnapshot)
        {
            RollbackStepResult result = env.Coordinator.TryStep(frame, input);
            Expect(result.Succeeded, $"FrameCommand DriveFrame TryStep Error: Frame={frame}, Kind={result.FailureKind}, Message={result.Message}");

            var context = new SimulationContext(frame, TickLength, false);
            env.CommandApplier.ApplyCommandsToWorld(frame, SimulationFrameCommandTiming.BeforeTick);
            env.World.Tick(in context);
            env.CommandApplier.ApplyCommandsToWorld(frame, SimulationFrameCommandTiming.AfterTick);

            if (saveSnapshot) env.Coordinator.SaveSnapshot();
        }

        private static PlayerInputSnapshot CreateInput(int frame)
        {
            uint state = unchecked((uint)Seed) ^ unchecked((uint)frame * 0x9E3779B9u);
            state = NextRandom(state);
            float moveX = (int)(state % 3) - 1;
            state = NextRandom(state);
            float moveY = (int)(state % 3) - 1;

            Expect(moveX >= -1f && moveX <= 1f, $"FrameCommand CreateInput MoveX Error: Frame={frame}, Value={moveX}");
            Expect(moveY >= -1f && moveY <= 1f, $"FrameCommand CreateInput MoveY Error: Frame={frame}, Value={moveY}");

            return new PlayerInputSnapshot(frame, PlayerID)
            {
                moveX = moveX,
                moveY = moveY
            };
        }

        private static PlayerInputSnapshot CreateWrongPrediction(PlayerInputSnapshot authoritative)
        {
            PlayerInputSnapshot predicted = authoritative;
            predicted.moveX = authoritative.moveX == 1f ? -1f : 1f;
            predicted.moveY = authoritative.moveY == -1f ? 1f : -1f;
            return predicted;
        }

        private static uint NextRandom(uint value)
        {
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            return value;
        }

        private static void AssertStateEqual(TestEnvironment a, TestEnvironment b, int frame, string stage)
        {
            Expect(a.Coordinator.CurrentFrame == b.Coordinator.CurrentFrame, $"{stage} CoordinatorFrame Error: Frame={frame}, A={a.Coordinator.CurrentFrame}, B={b.Coordinator.CurrentFrame}");
            Expect(a.Player == b.Player, $"{stage} Entity Error: Frame={frame}, A={a.Player}, B={b.Player}");
            Expect(a.World.AliveEntityCount == b.World.AliveEntityCount, $"{stage} AliveEntityCount Error: Frame={frame}, A={a.World.AliveEntityCount}, B={b.World.AliveEntityCount}");
            Expect(a.World.CreatedEntityCount == b.World.CreatedEntityCount, $"{stage} CreatedEntityCount Error: Frame={frame}, A={a.World.CreatedEntityCount}, B={b.World.CreatedEntityCount}");
            Expect(a.World.RegisteredComponentTypeCount == b.World.RegisteredComponentTypeCount, $"{stage} ComponentTypeCount Error: Frame={frame}, A={a.World.RegisteredComponentTypeCount}, B={b.World.RegisteredComponentTypeCount}");
            Expect(a.World.SystemCount == b.World.SystemCount, $"{stage} SystemCount Error: Frame={frame}, A={a.World.SystemCount}, B={b.World.SystemCount}");

            Expect(a.World.TryGetComponent(a.Player, out PositionComponent positionA), $"{stage} PositionA Error: Frame={frame}, ComponentMissing");
            Expect(b.World.TryGetComponent(b.Player, out PositionComponent positionB), $"{stage} PositionB Error: Frame={frame}, ComponentMissing");
            ExpectFloatBits(positionA.x, positionB.x, frame, stage, "Position.X");
            ExpectFloatBits(positionA.y, positionB.y, frame, stage, "Position.Y");
            ExpectFloatBits(positionA.z, positionB.z, frame, stage, "Position.Z");

            Expect(a.World.TryGetComponent(a.Player, out VelocityComponent velocityA), $"{stage} VelocityA Error: Frame={frame}, ComponentMissing");
            Expect(b.World.TryGetComponent(b.Player, out VelocityComponent velocityB), $"{stage} VelocityB Error: Frame={frame}, ComponentMissing");
            ExpectFloatBits(velocityA.x, velocityB.x, frame, stage, "Velocity.X");
            ExpectFloatBits(velocityA.y, velocityB.y, frame, stage, "Velocity.Y");
            ExpectFloatBits(velocityA.z, velocityB.z, frame, stage, "Velocity.Z");

            Expect(a.World.TryGetComponent(a.Player, out MoveSpeedComponent speedA), $"{stage} MoveSpeedA Error: Frame={frame}, ComponentMissing");
            Expect(b.World.TryGetComponent(b.Player, out MoveSpeedComponent speedB), $"{stage} MoveSpeedB Error: Frame={frame}, ComponentMissing");
            ExpectFloatBits(speedA.value, speedB.value, frame, stage, "MoveSpeed");

            Expect(a.World.TryGetComponent(a.Player, out PlayerInputSnapshotComponent inputA), $"{stage} InputA Error: Frame={frame}, ComponentMissing");
            Expect(b.World.TryGetComponent(b.Player, out PlayerInputSnapshotComponent inputB), $"{stage} InputB Error: Frame={frame}, ComponentMissing");
            AssertInputEqual(inputA, inputB, frame, stage);

            ExpectFloatBits(a.Observation.value, b.Observation.value, frame, stage, "ObservedValue");
            Expect(a.Observation.frame == b.Observation.frame, $"{stage} ObservedFrame Error: Frame={frame}, A={a.Observation.frame}, B={b.Observation.frame}");
            Expect(a.World.HasComponent<PlayerTagComponent>(a.Player) == b.World.HasComponent<PlayerTagComponent>(b.Player), $"{stage} PlayerTag Error: Frame={frame}");
        }

        private static void AssertInputEqual(PlayerInputSnapshotComponent a, PlayerInputSnapshotComponent b, int frame, string stage)
        {
            Expect(a.inputFrame == b.inputFrame, $"{stage} InputFrame Error: Frame={frame}, A={a.inputFrame}, B={b.inputFrame}");
            Expect(a.playerID == b.playerID, $"{stage} PlayerID Error: Frame={frame}, A={a.playerID}, B={b.playerID}");
            ExpectFloatBits(a.moveX, b.moveX, frame, stage, "Input.MoveX");
            ExpectFloatBits(a.moveY, b.moveY, frame, stage, "Input.MoveY");
            ExpectFloatBits(a.mouseX, b.mouseX, frame, stage, "Input.MouseX");
            ExpectFloatBits(a.mouseY, b.mouseY, frame, stage, "Input.MouseY");
            ExpectFloatBits(a.mouseDeltaX, b.mouseDeltaX, frame, stage, "Input.MouseDeltaX");
            ExpectFloatBits(a.mouseDeltaY, b.mouseDeltaY, frame, stage, "Input.MouseDeltaY");
            ExpectFloatBits(a.scrollX, b.scrollX, frame, stage, "Input.ScrollX");
            ExpectFloatBits(a.scrollY, b.scrollY, frame, stage, "Input.ScrollY");
            Expect(a.pressedButtons == b.pressedButtons, $"{stage} PressedButtons Error: Frame={frame}, A={a.pressedButtons}, B={b.pressedButtons}");
            Expect(a.heldButtons == b.heldButtons, $"{stage} HeldButtons Error: Frame={frame}, A={a.heldButtons}, B={b.heldButtons}");
            Expect(a.releasedButtons == b.releasedButtons, $"{stage} ReleasedButtons Error: Frame={frame}, A={a.releasedButtons}, B={b.releasedButtons}");
        }

        private static void AssertCounter(TestEnvironment env, int expected, int frame, string stage)
        {
            Expect(env.World.TryGetComponent(env.Player, out PositionComponent position), $"{stage} Position Missing Error: Frame={frame}");
            ExpectFloatBits(position.y, expected, frame, stage, "Counter");
        }

        private static void AssertObserved(TestEnvironment env, int expectedValue, int expectedObservedFrame, int frame, string stage)
        {
            ExpectFloatBits(env.Observation.value, expectedValue, frame, stage, "ObservedValue");
            Expect(env.Observation.frame == expectedObservedFrame, $"{stage} ObservedFrame Error: Frame={frame}, Expected={expectedObservedFrame}, Actual={env.Observation.frame}");
        }

        private static void ExpectStateDifferent(TestEnvironment a, TestEnvironment b, int frame)
        {
            Expect(a.World.TryGetComponent(a.Player, out PositionComponent positionA), $"FrameCommand Rollback PreCheck PositionA Error: Frame={frame}");
            Expect(b.World.TryGetComponent(b.Player, out PositionComponent positionB), $"FrameCommand Rollback PreCheck PositionB Error: Frame={frame}");

            bool different = FloatBits(positionA.x) != FloatBits(positionB.x) || FloatBits(positionA.y) != FloatBits(positionB.y) || FloatBits(positionA.z) != FloatBits(positionB.z);
            Expect(different, $"FrameCommand Rollback PreCheck Error: Frame={frame}, Predicted World Did Not Diverge Before Authoritative Correction");
        }

        private static void AssertFinalExecutionCounts(FrameCommandScenario reference, FrameCommandScenario predicted, int receiveFrame, int rollbackDepth)
        {
            for (int i = 0; i < reference.Commands.Length; i++)
            {
                IncrementCounterFrameCommand referenceCommand = reference.Commands[i];
                IncrementCounterFrameCommand predictedCommand = predicted.Commands[i];
                int predictedExpected = predictedCommand.FrameNumber <= receiveFrame ? 2 : 1;

                Expect(referenceCommand.ExecuteCount == 1, $"FrameCommand Reference ExecuteCount Error: Depth={rollbackDepth}, CommandFrame={referenceCommand.FrameNumber}, Delta={referenceCommand.Delta}, Expected=1, Actual={referenceCommand.ExecuteCount}");
                Expect(predictedCommand.ExecuteCount == predictedExpected, $"FrameCommand Predicted ExecuteCount Error: Depth={rollbackDepth}, CommandFrame={predictedCommand.FrameNumber}, Delta={predictedCommand.Delta}, Expected={predictedExpected}, Actual={predictedCommand.ExecuteCount}");
            }
        }

        private static void ExpectFloatBits(float a, float b, int frame, string stage, string field)
        {
            int bitsA = FloatBits(a), bitsB = FloatBits(b);
            Expect(bitsA == bitsB, $"{stage} {field} Error: Frame={frame}, A={a}({bitsA:X8}), B={b}({bitsB:X8})");
        }

        private static int FloatBits(float value) => BitConverter.SingleToInt32Bits(value);

        private static void Expect(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private sealed class IncrementCounterFrameCommand : ISimulationFrameCommand
        {
            private readonly Entity _entity;

            public int FrameNumber { get; }
            public int Delta { get; }
            public int ExecuteCount { get; private set; }

            public IncrementCounterFrameCommand(int frameNumber, Entity entity, int delta)
            {
                FrameNumber = frameNumber;
                _entity = entity;
                Delta = delta;
            }

            public void Execute(World world)
            {
                ref PositionComponent position = ref world.GetComponent<PositionComponent>(_entity);
                position.y += Delta;
                ExecuteCount++;
            }
        }

        private sealed class FrameCommandProbeSystem : FixedStepSystemBase
        {
            private readonly Entity _entity;
            private readonly FrameCommandObservation _observation;

            public override SystemTickSequence sequence => SystemTickSequence.logic;

            public FrameCommandProbeSystem(Entity entity, FrameCommandObservation observation)
            {
                _entity = entity;
                _observation = observation;
            }

            public override void Tick(in SimulationContext context)
            {
                ref PositionComponent position = ref World.GetComponent<PositionComponent>(_entity);
                _observation.value = position.y;
                _observation.frame = context.frameNumber;
            }
        }

        private sealed class FrameCommandObservation
        {
            public float value;
            public int frame;
        }

        private sealed class FrameCommandScenario
        {
            public readonly IncrementCounterFrameCommand CorrectionBefore;
            public readonly IncrementCounterFrameCommand CorrectionAfter;
            public readonly IncrementCounterFrameCommand[] Commands;

            public FrameCommandScenario(IncrementCounterFrameCommand correctionBefore, IncrementCounterFrameCommand correctionAfter, IncrementCounterFrameCommand frame122Before, IncrementCounterFrameCommand frame122After, IncrementCounterFrameCommand frame130Before, IncrementCounterFrameCommand frame130After)
            {
                CorrectionBefore = correctionBefore;
                CorrectionAfter = correctionAfter;
                Commands = new[]
                {
                    correctionBefore,
                    correctionAfter,
                    frame122Before,
                    frame122After,
                    frame130Before,
                    frame130After
                };
            }
        }

        private sealed class TestEnvironment : IDisposable
        {
            public readonly World World;
            public readonly Entity Player;
            public readonly RollbackCoordinator<PlayerInputSnapshot, EcsWorldSnapshot> Coordinator;
            public readonly SimulationFrameCommandBuffer CommandBuffer;
            public readonly SimulationFrameCommandApplier CommandApplier;
            public readonly SnapshotRingBuffer<EcsWorldSnapshot> SnapshotBuffer;
            public readonly FrameCommandObservation Observation;

            public TestEnvironment(World world, Entity player, RollbackCoordinator<PlayerInputSnapshot, EcsWorldSnapshot> coordinator, SimulationFrameCommandBuffer commandBuffer, SimulationFrameCommandApplier commandApplier, SnapshotRingBuffer<EcsWorldSnapshot> snapshotBuffer, FrameCommandObservation observation)
            {
                World = world;
                Player = player;
                Coordinator = coordinator;
                CommandBuffer = commandBuffer;
                CommandApplier = commandApplier;
                SnapshotBuffer = snapshotBuffer;
                Observation = observation;
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