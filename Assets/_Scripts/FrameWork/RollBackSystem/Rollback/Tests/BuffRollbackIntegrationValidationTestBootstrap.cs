using BuffSystem;
using ECSFrameWork;
using System;

namespace FrameWork.RollBackSystem
{
    /// <summary>
    /// BuffSystem 与 Rollback Restore Listener 端到端验证。
    /// </summary>
    public static class BuffRollbackIntegrationValidationTestBootstrap
    {
        private const int PlayerID = 1;
        private const int BuffID = 990001;
        private const int CorrectionFrame = 2;
        private const int PostRollbackFrameCount = 60;
        private const float TickLength = 1f / 60f;

        /// <summary>
        /// 验证预测路径改变 Buff 后，真实 Rollback Restore 能通过 Listener 恢复 Buff 查询与 ECS 真状态。
        /// </summary>
        public static void RunBuffRollbackRestoreIntegrationTestStatic(int rollbackDepth)
        {
            Expect(rollbackDepth > 0, $"BuffRollback Depth Error: Value={rollbackDepth}");

            int receiveFrame = CorrectionFrame + rollbackDepth;
            int endFrame = receiveFrame + PostRollbackFrameCount;
            bool rollbackCompleted = false;

            using var reference = CreateEnvironment(true);
            using var predicted = CreateEnvironment(true);

            reference.BuffSystem.AddBuff(new AddBuffCommand(reference.Player, BuffID, reference.Player));
            predicted.BuffSystem.AddBuff(new AddBuffCommand(predicted.Player, BuffID, predicted.Player));

            var referenceCommand = new ConditionalRemoveBuffFrameCommand(CorrectionFrame, reference.Player, reference.BuffSystem);
            var predictedCommand = new ConditionalRemoveBuffFrameCommand(CorrectionFrame, predicted.Player, predicted.BuffSystem);
            reference.CommandBuffer.AddCommand(referenceCommand, SimulationFrameCommandTiming.BeforeTick);
            predicted.CommandBuffer.AddCommand(predictedCommand, SimulationFrameCommandTiming.BeforeTick);

            PlayerInputSnapshot authoritativeCorrection = default;

            for (int frame = 1; frame <= endFrame; frame++)
            {
                PlayerInputSnapshot authoritative = CreateAuthoritativeInput(frame);
                PlayerInputSnapshot local = authoritative;

                if (frame == CorrectionFrame)
                {
                    authoritativeCorrection = authoritative;
                    local = CreateWrongPrediction(authoritative);
                }

                DriveFrame(reference, frame, authoritative, true);
                DriveFrame(predicted, frame, local, true);

                if (frame == 1)
                {
                    AssertBuffPresent(reference, frame, "Reference Initial");
                    AssertBuffPresent(predicted, frame, "Predicted Initial");
                }

                if (frame == receiveFrame)
                {
                    AssertBuffPresent(reference, frame, $"Reference BeforeRollback Depth={rollbackDepth}");
                    AssertBuffAbsent(predicted, frame, $"Predicted BeforeRollback Depth={rollbackDepth}");

                    predicted.Coordinator.ReceiveAuthoritativeInput(CorrectionFrame, authoritativeCorrection);

                    Expect(predicted.Coordinator.CurrentFrame == frame,
                        $"BuffRollback CurrentFrame Error: Depth={rollbackDepth}, Expected={frame}, Actual={predicted.Coordinator.CurrentFrame}");

                    AssertEquivalent(reference, predicted, frame, $"AfterRollback Depth={rollbackDepth}");

                    Expect(referenceCommand.ExecuteCount == 1,
                        $"BuffRollback Reference Command ExecuteCount Error: Depth={rollbackDepth}, Expected=1, Actual={referenceCommand.ExecuteCount}");
                    Expect(predictedCommand.ExecuteCount == 2,
                        $"BuffRollback Predicted Command ExecuteCount Error: Depth={rollbackDepth}, Expected=2, Actual={predictedCommand.ExecuteCount}");
                    Expect(referenceCommand.RemoveCount == 0,
                        $"BuffRollback Reference RemoveCount Error: Depth={rollbackDepth}, Expected=0, Actual={referenceCommand.RemoveCount}");
                    Expect(predictedCommand.RemoveCount == 1,
                        $"BuffRollback Predicted RemoveCount Error: Depth={rollbackDepth}, Expected=1, Actual={predictedCommand.RemoveCount}");

                    rollbackCompleted = true;
                    continue;
                }

                if (rollbackCompleted) AssertEquivalent(reference, predicted, frame, $"PostRollback Depth={rollbackDepth}");
            }

            Expect(rollbackCompleted, $"BuffRollback Execution Error: Depth={rollbackDepth}, Rollback Was Not Triggered");
            AssertEquivalent(reference, predicted, endFrame, $"Final Depth={rollbackDepth}");
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

            var definitions = new BuffDefinitionRegistry();
            BuffDefinition definition = new BuffDefinition(
                BuffID,
                "RollbackValidationBuff",
                0,
                1,
                false,
                true,
                0,
                0,
                0,
                BuffTriggerType.Tick,
                BuffInstanceType.normal,
                NormalBuffStackPolicy.RefreshDuration,
                ParallelBuffStackUpPolicy.Append,
                ParallelBuffStackDownPolicy.RemoveEarliest,
                0);
            definitions.Register(in definition);

            var buffSystem = new BuffSystemCore(definitions);

            world.AddSystem(new InputMoveSystem());
            world.AddSystem(new MovementSystem());
            world.AddSystem(new TestBuffSystemBridge(buffSystem));

            var inputApplier = new PlayerSnapshotInputApplier();
            inputApplier.RegisterPlayer(PlayerID, player);

            var commandBuffer = new SimulationFrameCommandBuffer(512);
            var commandApplier = new SimulationFrameCommandApplier(world, commandBuffer, 512);
            var rollbackAdapter = new WorldRollbackAdapter<PlayerInputSnapshot>(world, world, inputApplier, null);
            rollbackAdapter.SetFrameCommandReplayBinding(new RollbackFrameCommandReplayBinding(commandBuffer, commandApplier));
            rollbackAdapter.AddRollbackRestoreListener(new BuffRollbackRestoreListener(buffSystem));

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

            var environment = new TestEnvironment(world, player, buffSystem, coordinator, commandBuffer, commandApplier, snapshotBuffer);
            if (saveInitialSnapshot) coordinator.SaveSnapshot();
            return environment;
        }

        private static void DriveFrame(TestEnvironment env, int frame, PlayerInputSnapshot input, bool saveSnapshot)
        {
            RollbackStepResult result = env.Coordinator.TryStep(frame, input);
            Expect(result.Succeeded, $"BuffRollback DriveFrame Error: Frame={frame}, Kind={result.FailureKind}, Message={result.Message}");

            var context = new SimulationContext(frame, TickLength, false);
            env.CommandApplier.ApplyCommandsToWorld(frame, SimulationFrameCommandTiming.BeforeTick);
            env.World.Tick(in context);
            env.CommandApplier.ApplyCommandsToWorld(frame, SimulationFrameCommandTiming.AfterTick);

            if (saveSnapshot) env.Coordinator.SaveSnapshot();
        }

        private static PlayerInputSnapshot CreateAuthoritativeInput(int frame)
        {
            if (frame == CorrectionFrame)
                return new PlayerInputSnapshot(frame, PlayerID) { moveX = -1f, moveY = 0f };

            uint state = unchecked((uint)20260817) ^ unchecked((uint)frame * 0x9E3779B9u);
            state = NextRandom(state);
            float moveX = (int)(state % 3) - 1;
            state = NextRandom(state);
            float moveY = (int)(state % 3) - 1;

            return new PlayerInputSnapshot(frame, PlayerID) { moveX = moveX, moveY = moveY };
        }

        private static PlayerInputSnapshot CreateWrongPrediction(PlayerInputSnapshot authoritative)
        {
            PlayerInputSnapshot predicted = authoritative;
            predicted.moveX = 1f;
            return predicted;
        }

        private static uint NextRandom(uint value)
        {
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            return value;
        }

        private static void AssertEquivalent(TestEnvironment a, TestEnvironment b, int frame, string stage)
        {
            AssertBuffPresent(a, frame, $"{stage} A");
            AssertBuffPresent(b, frame, $"{stage} B");

            bool hasA = a.BuffSystem.TryGetBuff(a.Player, BuffID, a.Player, out BuffViewData buffA);
            bool hasB = b.BuffSystem.TryGetBuff(b.Player, BuffID, b.Player, out BuffViewData buffB);

            Expect(hasA && hasB, $"{stage} Buff Presence Error: Frame={frame}, A={hasA}, B={hasB}");
            Expect(buffA.Stack == buffB.Stack, $"{stage} Stack Error: Frame={frame}, A={buffA.Stack}, B={buffB.Stack}");
            Expect(buffA.RemainingFrames == buffB.RemainingFrames, $"{stage} RemainingFrames Error: Frame={frame}, A={buffA.RemainingFrames}, B={buffB.RemainingFrames}");
            Expect(buffA.RuntimeHandle == buffB.RuntimeHandle, $"{stage} RuntimeHandle Error: Frame={frame}, A={buffA.RuntimeHandle}, B={buffB.RuntimeHandle}");

            uint checksumA = WorldChecksumCalculator.Calculate(a.World);
            uint checksumB = WorldChecksumCalculator.Calculate(b.World);
            Expect(checksumA == checksumB, $"{stage} Checksum Error: Frame={frame}, A={checksumA}, B={checksumB}");
        }

        private static void AssertBuffPresent(TestEnvironment env, int frame, string stage)
        {
            bool found = env.BuffSystem.TryGetBuff(env.Player, BuffID, env.Player, out BuffViewData buff);
            Expect(found, $"{stage} Buff Missing Error: Frame={frame}");
            Expect(buff.Stack == 1, $"{stage} Stack Error: Frame={frame}, Expected=1, Actual={buff.Stack}");
            Expect(buff.RemainingFrames == -1, $"{stage} RemainingFrames Error: Frame={frame}, Expected=-1, Actual={buff.RemainingFrames}");
        }

        private static void AssertBuffAbsent(TestEnvironment env, int frame, string stage)
        {
            bool found = env.BuffSystem.TryGetBuff(env.Player, BuffID, env.Player, out _);
            Expect(!found, $"{stage} Buff Unexpectedly Present Error: Frame={frame}");
        }

        private static void Expect(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private sealed class ConditionalRemoveBuffFrameCommand : ISimulationFrameCommand
        {
            private readonly Entity _player;
            private readonly BuffSystemCore _buffSystem;

            public int FrameNumber { get; }
            public int ExecuteCount { get; private set; }
            public int RemoveCount { get; private set; }

            public ConditionalRemoveBuffFrameCommand(int frameNumber, Entity player, BuffSystemCore buffSystem)
            {
                FrameNumber = frameNumber;
                _player = player;
                _buffSystem = buffSystem;
            }

            public void Execute(World world)
            {
                ExecuteCount++;
                if (!world.TryGetComponent(_player, out PlayerInputSnapshotComponent input) || input.moveX <= 0.5f) return;

                _buffSystem.RemoveBuff(new RemoveBuffCommand(_player, BuffID, _player, 1, false, true));
                RemoveCount++;
            }
        }

        private sealed class TestBuffSystemBridge : IFixedStepSystem
        {
            private readonly BuffSystemCore _core;
            private World _world;

            public SystemTickSequence sequence => SystemTickSequence.logic;

            public TestBuffSystemBridge(BuffSystemCore core) => _core = core;

            public void OnCreate(World world) => _world = world;

            public void Tick(in SimulationContext context) => _core.Tick(_world, context);

            public void OnDestroy(World world) { }
        }

        private sealed class TestEnvironment : IDisposable
        {
            public readonly World World;
            public readonly Entity Player;
            public readonly BuffSystemCore BuffSystem;
            public readonly RollbackCoordinator<PlayerInputSnapshot, EcsWorldSnapshot> Coordinator;
            public readonly SimulationFrameCommandBuffer CommandBuffer;
            public readonly SimulationFrameCommandApplier CommandApplier;
            public readonly SnapshotRingBuffer<EcsWorldSnapshot> SnapshotBuffer;

            public TestEnvironment(World world, Entity player, BuffSystemCore buffSystem, RollbackCoordinator<PlayerInputSnapshot, EcsWorldSnapshot> coordinator, SimulationFrameCommandBuffer commandBuffer, SimulationFrameCommandApplier commandApplier, SnapshotRingBuffer<EcsWorldSnapshot> snapshotBuffer)
            {
                World = world;
                Player = player;
                BuffSystem = buffSystem;
                Coordinator = coordinator;
                CommandBuffer = commandBuffer;
                CommandApplier = commandApplier;
                SnapshotBuffer = snapshotBuffer;
            }

            public void Dispose()
            {
                SnapshotBuffer.Clear();
                CommandBuffer.Clear();
                BuffSystem.Dispose();
                World.Dispose();
            }
        }
    }
}