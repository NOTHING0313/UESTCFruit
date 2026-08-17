using ECSFrameWork;
using System;

namespace FrameWork.RollBackSystem
{
    /// <summary>
    /// 连续多次 Rollback 稳定性验证。
    /// </summary>
    public static class RepeatedRollbackValidationTestBootstrap
    {
        private const int PlayerID = 1;
        private const int Seed = 20260817;
        private const int TotalFrames = 10000;
        private const int FirstCorrectionFrame = 120;
        private const int CorrectionInterval = 90;
        private const float TickLength = 1f / 60f;
        private static readonly int[] RollbackDepths = { 1, 3, 6, 12, 30, 60 };

        /// <summary>
        /// 10000 帧内重复制造错误预测，并验证每次 Rollback 后重新收敛。
        /// </summary>
        public static void RunRepeatedRollbackStressTestStatic()
        {
            using var reference = CreateEnvironment(true);
            using var predicted = CreateEnvironment(true);

            int correctionIndex = 0;
            int rollbackCount = 0;
            RollbackEvent pending = default;
            bool hasPending = false;

            for (int frame = 1; frame <= TotalFrames; frame++)
            {
                PlayerInputSnapshot authoritative = CreateInput(frame);
                PlayerInputSnapshot local = authoritative;

                if (!hasPending && IsCorrectionFrame(frame, correctionIndex))
                {
                    int depth = RollbackDepths[correctionIndex % RollbackDepths.Length];
                    int receiveFrame = frame + depth;

                    if (receiveFrame <= TotalFrames)
                    {
                        local = CreateWrongPrediction(authoritative);
                        pending = new RollbackEvent(frame, receiveFrame, depth, authoritative);
                        hasPending = true;
                        correctionIndex++;
                    }
                }

                DriveFrame(reference, frame, authoritative, true);
                DriveFrame(predicted, frame, local, true);

                if (hasPending && frame == pending.ReceiveFrame)
                {
                    ExpectStateDifferent(reference, predicted, frame, pending);
                    predicted.Coordinator.ReceiveAuthoritativeInput(pending.CorrectionFrame, pending.AuthoritativeInput);

                    Expect(predicted.Coordinator.CurrentFrame == frame,
                        $"RepeatedRollback CurrentFrame Error: Rollback={rollbackCount + 1}, CorrectionFrame={pending.CorrectionFrame}, ReceiveFrame={frame}, Depth={pending.Depth}, Expected={frame}, Actual={predicted.Coordinator.CurrentFrame}");

                    AssertStateEqual(reference, predicted, frame,
                        $"AfterRollback #{rollbackCount + 1} Depth={pending.Depth}");

                    rollbackCount++;
                    hasPending = false;
                    continue;
                }

                if (!hasPending)
                    AssertStateEqual(reference, predicted, frame, $"Stable Frame={frame}");
            }

            Expect(!hasPending, $"RepeatedRollback Pending Error: CorrectionFrame={pending.CorrectionFrame}, ReceiveFrame={pending.ReceiveFrame}");
            Expect(rollbackCount >= 100, $"RepeatedRollback Count Error: Expected>=100, Actual={rollbackCount}");

            AssertStateEqual(reference, predicted, TotalFrames, "RepeatedRollback Final");
            Expect(reference.Coordinator.CurrentFrame == TotalFrames, $"RepeatedRollback Reference FinalFrame Error: Expected={TotalFrames}, Actual={reference.Coordinator.CurrentFrame}");
            Expect(predicted.Coordinator.CurrentFrame == TotalFrames, $"RepeatedRollback Predicted FinalFrame Error: Expected={TotalFrames}, Actual={predicted.Coordinator.CurrentFrame}");
        }

        private static bool IsCorrectionFrame(int frame, int correctionIndex)
            => frame == FirstCorrectionFrame + correctionIndex * CorrectionInterval;

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
            Expect(result.Succeeded, $"RepeatedRollback DriveFrame Error: Frame={frame}, Kind={result.FailureKind}, Message={result.Message}");

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

            Expect(moveX >= -1f && moveX <= 1f, $"RepeatedRollback CreateInput MoveX Error: Frame={frame}, Value={moveX}");
            Expect(moveY >= -1f && moveY <= 1f, $"RepeatedRollback CreateInput MoveY Error: Frame={frame}, Value={moveY}");

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

        private static void ExpectStateDifferent(TestEnvironment a, TestEnvironment b, int frame, RollbackEvent rollback)
        {
            Expect(a.World.TryGetComponent(a.Player, out PositionComponent positionA), $"RepeatedRollback PreCheck PositionA Error: Frame={frame}");
            Expect(b.World.TryGetComponent(b.Player, out PositionComponent positionB), $"RepeatedRollback PreCheck PositionB Error: Frame={frame}");

            bool different = FloatBits(positionA.x) != FloatBits(positionB.x) || FloatBits(positionA.y) != FloatBits(positionB.y) || FloatBits(positionA.z) != FloatBits(positionB.z);

            Expect(different,
                $"RepeatedRollback PreCheck Error: CorrectionFrame={rollback.CorrectionFrame}, ReceiveFrame={rollback.ReceiveFrame}, Depth={rollback.Depth}, Predicted World Did Not Diverge");
        }

        private static void AssertStateEqual(TestEnvironment a, TestEnvironment b, int frame, string stage)
        {
            Expect(a.Coordinator.CurrentFrame == b.Coordinator.CurrentFrame, $"{stage} CoordinatorFrame Error: Frame={frame}, A={a.Coordinator.CurrentFrame}, B={b.Coordinator.CurrentFrame}");
            Expect(a.Player == b.Player, $"{stage} Entity Error: Frame={frame}, A={a.Player}, B={b.Player}");
            Expect(a.World.AliveEntityCount == b.World.AliveEntityCount, $"{stage} AliveEntityCount Error: Frame={frame}, A={a.World.AliveEntityCount}, B={b.World.AliveEntityCount}");
            Expect(a.World.CreatedEntityCount == b.World.CreatedEntityCount, $"{stage} CreatedEntityCount Error: Frame={frame}, A={a.World.CreatedEntityCount}, B={b.World.CreatedEntityCount}");
            Expect(a.World.RegisteredComponentTypeCount == b.World.RegisteredComponentTypeCount, $"{stage} ComponentTypeCount Error: Frame={frame}, A={a.World.RegisteredComponentTypeCount}, B={b.World.RegisteredComponentTypeCount}");
            Expect(a.World.SystemCount == b.World.SystemCount, $"{stage} SystemCount Error: Frame={frame}, A={a.World.SystemCount}, B={b.World.SystemCount}");

            Expect(a.World.TryGetComponent(a.Player, out PositionComponent positionA), $"{stage} PositionA Error: Frame={frame}");
            Expect(b.World.TryGetComponent(b.Player, out PositionComponent positionB), $"{stage} PositionB Error: Frame={frame}");
            ExpectFloatBits(positionA.x, positionB.x, frame, stage, "Position.X");
            ExpectFloatBits(positionA.y, positionB.y, frame, stage, "Position.Y");
            ExpectFloatBits(positionA.z, positionB.z, frame, stage, "Position.Z");

            Expect(a.World.TryGetComponent(a.Player, out VelocityComponent velocityA), $"{stage} VelocityA Error: Frame={frame}");
            Expect(b.World.TryGetComponent(b.Player, out VelocityComponent velocityB), $"{stage} VelocityB Error: Frame={frame}");
            ExpectFloatBits(velocityA.x, velocityB.x, frame, stage, "Velocity.X");
            ExpectFloatBits(velocityA.y, velocityB.y, frame, stage, "Velocity.Y");
            ExpectFloatBits(velocityA.z, velocityB.z, frame, stage, "Velocity.Z");

            Expect(a.World.TryGetComponent(a.Player, out MoveSpeedComponent speedA), $"{stage} MoveSpeedA Error: Frame={frame}");
            Expect(b.World.TryGetComponent(b.Player, out MoveSpeedComponent speedB), $"{stage} MoveSpeedB Error: Frame={frame}");
            ExpectFloatBits(speedA.value, speedB.value, frame, stage, "MoveSpeed");

            Expect(a.World.TryGetComponent(a.Player, out PlayerInputSnapshotComponent inputA), $"{stage} InputA Error: Frame={frame}");
            Expect(b.World.TryGetComponent(b.Player, out PlayerInputSnapshotComponent inputB), $"{stage} InputB Error: Frame={frame}");
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

        private readonly struct RollbackEvent
        {
            public readonly int CorrectionFrame;
            public readonly int ReceiveFrame;
            public readonly int Depth;
            public readonly PlayerInputSnapshot AuthoritativeInput;

            public RollbackEvent(int correctionFrame, int receiveFrame, int depth, PlayerInputSnapshot authoritativeInput)
            {
                CorrectionFrame = correctionFrame;
                ReceiveFrame = receiveFrame;
                Depth = depth;
                AuthoritativeInput = authoritativeInput;
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