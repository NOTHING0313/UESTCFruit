using ECSFrameWork;
using System;

namespace FrameWork.RollBackSystem
{
    /// <summary>
    /// 本地帧同步验证：确定性与 Rollback 等价性。
    /// </summary>
    public static class LocalSyncValidationTestBootstrap
    {
        private const int PlayerID = 1;
        private const int Seed = 20260817;
        private const int CorrectionFrame = 120;
        private const int PostRollbackFrameCount = 60;
        private const float TickLength = 1f / 60f;

        /// <summary>
        /// 验证相同初始状态和相同输入序列下，两个独立 World 每帧严格一致。
        /// </summary>
        public static void RunTwinWorldDeterminismTestStatic(int frameCount)
        {
            Expect(frameCount > 0, $"TwinWorld FrameCount Error: Value={frameCount}");

            using var a = CreateEnvironment(false);
            using var b = CreateEnvironment(false);

            for (int frame = 1; frame <= frameCount; frame++)
            {
                PlayerInputSnapshot input = CreateInput(frame);
                DriveFrame(a, frame, input, false);
                DriveFrame(b, frame, input, false);
                AssertStateEqual(a, b, frame, "TwinWorld");
            }

            Expect(a.Coordinator.CurrentFrame == frameCount, $"TwinWorld FinalFrame Error: Expected={frameCount}, Actual={a.Coordinator.CurrentFrame}");
            Expect(b.Coordinator.CurrentFrame == frameCount, $"TwinWorld FinalFrame Error: Expected={frameCount}, Actual={b.Coordinator.CurrentFrame}");
        }

        /// <summary>
        /// 验证不同 Rollback 深度下，权威修正后立即收敛，并在后续模拟中持续保持一致。
        /// </summary>
        public static void RunRollbackReferenceEquivalenceTestStatic(int rollbackDepth)
        {
            Expect(rollbackDepth > 0, $"Rollback Depth Error: Value={rollbackDepth}");

            int receiveFrame = CorrectionFrame + rollbackDepth;
            int endFrame = receiveFrame + PostRollbackFrameCount;
            bool rollbackCompleted = false;

            using var reference = CreateEnvironment(true);
            using var predicted = CreateEnvironment(true);
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

                    Expect(predicted.Coordinator.CurrentFrame == frame, $"Rollback CurrentFrame Error: Depth={rollbackDepth}, Expected={frame}, Actual={predicted.Coordinator.CurrentFrame}");
                    AssertStateEqual(reference, predicted, frame, $"AfterRollback Depth={rollbackDepth}");
                    rollbackCompleted = true;
                    continue;
                }

                if (rollbackCompleted) AssertStateEqual(reference, predicted, frame, $"PostRollback Depth={rollbackDepth}");
            }

            Expect(rollbackCompleted, $"Rollback Execution Error: Depth={rollbackDepth}, Rollback Was Not Triggered");
            AssertStateEqual(reference, predicted, endFrame, $"Final Depth={rollbackDepth}");
            Expect(reference.Coordinator.CurrentFrame == endFrame, $"Reference FinalFrame Error: Depth={rollbackDepth}, Expected={endFrame}, Actual={reference.Coordinator.CurrentFrame}");
            Expect(predicted.Coordinator.CurrentFrame == endFrame, $"Predicted FinalFrame Error: Depth={rollbackDepth}, Expected={endFrame}, Actual={predicted.Coordinator.CurrentFrame}");
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

        private static void DriveFrame(TestEnvironment env, int frame, PlayerInputSnapshot input, bool saveSnapshot)
        {
            RollbackStepResult result = env.Coordinator.TryStep(frame, input);
            Expect(result.Succeeded, $"DriveFrame TryStep Error: Frame={frame}, Kind={result.FailureKind}, Message={result.Message}");

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

            Expect(moveX >= -1f && moveX <= 1f, $"CreateInput MoveX Error: Frame={frame}, Value={moveX}");
            Expect(moveY >= -1f && moveY <= 1f, $"CreateInput MoveY Error: Frame={frame}, Value={moveY}");

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

        private static void ExpectStateDifferent(TestEnvironment a, TestEnvironment b, int frame)
        {
            Expect(a.World.TryGetComponent(a.Player, out PositionComponent positionA), $"Rollback PreCheck PositionA Error: Frame={frame}");
            Expect(b.World.TryGetComponent(b.Player, out PositionComponent positionB), $"Rollback PreCheck PositionB Error: Frame={frame}");

            bool different = FloatBits(positionA.x) != FloatBits(positionB.x) || FloatBits(positionA.y) != FloatBits(positionB.y) || FloatBits(positionA.z) != FloatBits(positionB.z);
            Expect(different, $"Rollback PreCheck Error: Frame={frame}, Predicted World Did Not Diverge Before Authoritative Correction");
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