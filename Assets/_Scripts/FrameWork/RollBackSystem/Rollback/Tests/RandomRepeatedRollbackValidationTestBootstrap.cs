using ECSFrameWork;
using System;

namespace FrameWork.RollBackSystem
{
    /// <summary>
    /// 固定随机种子的连续随机 Rollback 稳定性验证。
    /// </summary>
    public static class RandomRepeatedRollbackValidationTestBootstrap
    {
        private const int PlayerID = 1;
        private const uint Seed = 20260817u;
        private const int TotalFrames = 100000;
        private const int FirstCorrectionFrame = 120;
        private const int MinCorrectionInterval = 60;
        private const int MaxCorrectionInterval = 180;
        private const int MinRollbackDepth = 1;
        private const int MaxRollbackDepth = 60;
        private const int MinExpectedRollbackCount = 500;
        private const float TickLength = 1f / 60f;

        /// <summary>
        /// 在 100000 帧内随机制造 500+ 次预测错误，并验证每次 Rollback 后严格重新收敛。
        /// </summary>
        public static void RunRandomRepeatedRollbackStressTestStatic()
        {
            using var reference = CreateEnvironment(true);
            using var predicted = CreateEnvironment(true);

            uint eventRandomState = Seed ^ 0xA341316Cu;
            int nextCorrectionFrame = FirstCorrectionFrame;
            int rollbackCount = 0;
            bool hasPending = false;
            RollbackEvent pending = default;

            for (int frame = 1; frame <= TotalFrames; frame++)
            {
                PlayerInputSnapshot authoritative = CreateInput(frame);
                PlayerInputSnapshot local = authoritative;

                if (!hasPending && frame == nextCorrectionFrame)
                {
                    int depth = NextRange(ref eventRandomState, MinRollbackDepth, MaxRollbackDepth);
                    int receiveFrame = frame + depth;

                    if (receiveFrame <= TotalFrames)
                    {
                        local = CreateWrongPrediction(authoritative, ref eventRandomState);
                        pending = new RollbackEvent(rollbackCount + 1, frame, receiveFrame, depth, authoritative);
                        hasPending = true;
                    }
                    else nextCorrectionFrame = int.MaxValue;
                }

                DriveFrame(reference, frame, authoritative, true);
                DriveFrame(predicted, frame, local, true);

                if (hasPending && frame == pending.ReceiveFrame)
                {
                    ExpectStateDifferent(reference, predicted, frame, pending);

                    predicted.Coordinator.ReceiveAuthoritativeInput(pending.CorrectionFrame, pending.AuthoritativeInput);

                    Expect(predicted.Coordinator.CurrentFrame == frame,
                        $"RandomRepeatedRollback CurrentFrame Error: Seed={Seed}, RollbackIndex={pending.Index}, CorrectionFrame={pending.CorrectionFrame}, ReceiveFrame={pending.ReceiveFrame}, Depth={pending.Depth}, Expected={frame}, Actual={predicted.Coordinator.CurrentFrame}");

                    AssertStateEqual(reference, predicted, frame,
                        $"AfterRollback Seed={Seed} Index={pending.Index} Correction={pending.CorrectionFrame} Receive={pending.ReceiveFrame} Depth={pending.Depth}");

                    rollbackCount++;
                    hasPending = false;

                    int interval = NextRange(ref eventRandomState, MinCorrectionInterval, MaxCorrectionInterval);
                    nextCorrectionFrame = frame + interval;
                    continue;
                }

                if (!hasPending)
                    AssertStateEqual(reference, predicted, frame, $"Stable Seed={Seed} Frame={frame}");
            }

            Expect(!hasPending,
                $"RandomRepeatedRollback Pending Error: Seed={Seed}, RollbackIndex={pending.Index}, CorrectionFrame={pending.CorrectionFrame}, ReceiveFrame={pending.ReceiveFrame}, Depth={pending.Depth}");

            Expect(rollbackCount >= MinExpectedRollbackCount,
                $"RandomRepeatedRollback Count Error: Seed={Seed}, Expected>={MinExpectedRollbackCount}, Actual={rollbackCount}");

            AssertStateEqual(reference, predicted, TotalFrames, $"RandomRepeatedRollback Final Seed={Seed}");

            Expect(reference.Coordinator.CurrentFrame == TotalFrames,
                $"RandomRepeatedRollback Reference FinalFrame Error: Seed={Seed}, Expected={TotalFrames}, Actual={reference.Coordinator.CurrentFrame}");

            Expect(predicted.Coordinator.CurrentFrame == TotalFrames,
                $"RandomRepeatedRollback Predicted FinalFrame Error: Seed={Seed}, Expected={TotalFrames}, Actual={predicted.Coordinator.CurrentFrame}");
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

            Expect(result.Succeeded,
                $"RandomRepeatedRollback DriveFrame Error: Seed={Seed}, Frame={frame}, Kind={result.FailureKind}, Message={result.Message}");

            var context = new SimulationContext(frame, TickLength, false);
            env.CommandApplier.ApplyCommandsToWorld(frame, SimulationFrameCommandTiming.BeforeTick);
            env.World.Tick(in context);
            env.CommandApplier.ApplyCommandsToWorld(frame, SimulationFrameCommandTiming.AfterTick);

            if (saveSnapshot) env.Coordinator.SaveSnapshot();
        }

        private static PlayerInputSnapshot CreateInput(int frame)
        {
            uint state = Seed ^ unchecked((uint)frame * 0x9E3779B9u);
            state = NextRandom(state);
            float moveX = (int)(state % 3) - 1;
            state = NextRandom(state);
            float moveY = (int)(state % 3) - 1;

            Expect(moveX >= -1f && moveX <= 1f, $"RandomRepeatedRollback CreateInput MoveX Error: Seed={Seed}, Frame={frame}, Value={moveX}");
            Expect(moveY >= -1f && moveY <= 1f, $"RandomRepeatedRollback CreateInput MoveY Error: Seed={Seed}, Frame={frame}, Value={moveY}");

            return new PlayerInputSnapshot(frame, PlayerID)
            {
                moveX = moveX,
                moveY = moveY
            };
        }

        private static PlayerInputSnapshot CreateWrongPrediction(PlayerInputSnapshot authoritative, ref uint randomState)
        {
            PlayerInputSnapshot predicted = authoritative;
            randomState = NextRandom(randomState);

            if ((randomState & 1u) == 0)
                predicted.moveX = CreateDifferentAxisValue(authoritative.moveX, ref randomState);
            else
                predicted.moveY = CreateDifferentAxisValue(authoritative.moveY, ref randomState);

            return predicted;
        }

        private static float CreateDifferentAxisValue(float current, ref uint randomState)
        {
            randomState = NextRandom(randomState);
            int offset = (int)(randomState % 2) + 1;
            int currentIndex = (int)current + 1;
            return ((currentIndex + offset) % 3) - 1;
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

        private static void ExpectStateDifferent(TestEnvironment a, TestEnvironment b, int frame, RollbackEvent rollback)
        {
            Expect(a.World.TryGetComponent(a.Player, out PositionComponent positionA),
                $"RandomRepeatedRollback PreCheck PositionA Error: Seed={Seed}, Frame={frame}");

            Expect(b.World.TryGetComponent(b.Player, out PositionComponent positionB),
                $"RandomRepeatedRollback PreCheck PositionB Error: Seed={Seed}, Frame={frame}");

            bool different =
                FloatBits(positionA.x) != FloatBits(positionB.x) ||
                FloatBits(positionA.y) != FloatBits(positionB.y) ||
                FloatBits(positionA.z) != FloatBits(positionB.z);

            Expect(different,
                $"RandomRepeatedRollback PreCheck Error: Seed={Seed}, RollbackIndex={rollback.Index}, CorrectionFrame={rollback.CorrectionFrame}, ReceiveFrame={rollback.ReceiveFrame}, Depth={rollback.Depth}, Predicted World Did Not Diverge");
        }

        private static void AssertStateEqual(TestEnvironment a, TestEnvironment b, int frame, string stage)
        {
            Expect(a.Coordinator.CurrentFrame == b.Coordinator.CurrentFrame,
                $"{stage} CoordinatorFrame Error: Frame={frame}, A={a.Coordinator.CurrentFrame}, B={b.Coordinator.CurrentFrame}");

            Expect(a.Player == b.Player,
                $"{stage} Entity Error: Frame={frame}, A={a.Player}, B={b.Player}");

            Expect(a.World.AliveEntityCount == b.World.AliveEntityCount,
                $"{stage} AliveEntityCount Error: Frame={frame}, A={a.World.AliveEntityCount}, B={b.World.AliveEntityCount}");

            Expect(a.World.CreatedEntityCount == b.World.CreatedEntityCount,
                $"{stage} CreatedEntityCount Error: Frame={frame}, A={a.World.CreatedEntityCount}, B={b.World.CreatedEntityCount}");

            Expect(a.World.RegisteredComponentTypeCount == b.World.RegisteredComponentTypeCount,
                $"{stage} ComponentTypeCount Error: Frame={frame}, A={a.World.RegisteredComponentTypeCount}, B={b.World.RegisteredComponentTypeCount}");

            Expect(a.World.SystemCount == b.World.SystemCount,
                $"{stage} SystemCount Error: Frame={frame}, A={a.World.SystemCount}, B={b.World.SystemCount}");

            Expect(a.World.TryGetComponent(a.Player, out PositionComponent positionA),
                $"{stage} PositionA Error: Frame={frame}");

            Expect(b.World.TryGetComponent(b.Player, out PositionComponent positionB),
                $"{stage} PositionB Error: Frame={frame}");

            ExpectFloatBits(positionA.x, positionB.x, frame, stage, "Position.X");
            ExpectFloatBits(positionA.y, positionB.y, frame, stage, "Position.Y");
            ExpectFloatBits(positionA.z, positionB.z, frame, stage, "Position.Z");

            Expect(a.World.TryGetComponent(a.Player, out VelocityComponent velocityA),
                $"{stage} VelocityA Error: Frame={frame}");

            Expect(b.World.TryGetComponent(b.Player, out VelocityComponent velocityB),
                $"{stage} VelocityB Error: Frame={frame}");

            ExpectFloatBits(velocityA.x, velocityB.x, frame, stage, "Velocity.X");
            ExpectFloatBits(velocityA.y, velocityB.y, frame, stage, "Velocity.Y");
            ExpectFloatBits(velocityA.z, velocityB.z, frame, stage, "Velocity.Z");

            Expect(a.World.TryGetComponent(a.Player, out MoveSpeedComponent speedA),
                $"{stage} MoveSpeedA Error: Frame={frame}");

            Expect(b.World.TryGetComponent(b.Player, out MoveSpeedComponent speedB),
                $"{stage} MoveSpeedB Error: Frame={frame}");

            ExpectFloatBits(speedA.value, speedB.value, frame, stage, "MoveSpeed");

            Expect(a.World.TryGetComponent(a.Player, out PlayerInputSnapshotComponent inputA),
                $"{stage} InputA Error: Frame={frame}");

            Expect(b.World.TryGetComponent(b.Player, out PlayerInputSnapshotComponent inputB),
                $"{stage} InputB Error: Frame={frame}");

            AssertInputEqual(inputA, inputB, frame, stage);

            Expect(
                a.World.HasComponent<PlayerTagComponent>(a.Player) == b.World.HasComponent<PlayerTagComponent>(b.Player),
                $"{stage} PlayerTag Error: Frame={frame}");
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

            Expect(a.pressedButtons == b.pressedButtons,
                $"{stage} PressedButtons Error: Frame={frame}, A={a.pressedButtons}, B={b.pressedButtons}");

            Expect(a.heldButtons == b.heldButtons,
                $"{stage} HeldButtons Error: Frame={frame}, A={a.heldButtons}, B={b.heldButtons}");

            Expect(a.releasedButtons == b.releasedButtons,
                $"{stage} ReleasedButtons Error: Frame={frame}, A={a.releasedButtons}, B={b.releasedButtons}");
        }

        private static void ExpectFloatBits(float a, float b, int frame, string stage, string field)
        {
            int bitsA = FloatBits(a), bitsB = FloatBits(b);

            Expect(bitsA == bitsB,
                $"{stage} {field} Error: Frame={frame}, A={a}({bitsA:X8}), B={b}({bitsB:X8})");
        }

        private static int FloatBits(float value) => BitConverter.SingleToInt32Bits(value);

        private static void Expect(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private readonly struct RollbackEvent
        {
            public readonly int Index;
            public readonly int CorrectionFrame;
            public readonly int ReceiveFrame;
            public readonly int Depth;
            public readonly PlayerInputSnapshot AuthoritativeInput;

            public RollbackEvent(int index, int correctionFrame, int receiveFrame, int depth, PlayerInputSnapshot authoritativeInput)
            {
                Index = index;
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