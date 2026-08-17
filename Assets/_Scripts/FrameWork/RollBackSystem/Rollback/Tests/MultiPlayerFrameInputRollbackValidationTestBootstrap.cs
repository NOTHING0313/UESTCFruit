using ECSFrameWork;
using System;

namespace FrameWork.RollBackSystem
{
    /// <summary>
    /// 双玩家 FrameInputSet 与 RollbackCoordinator 组合验证。
    /// </summary>
    public static class MultiPlayerFrameInputRollbackValidationTestBootstrap
    {
        private const int Player1ID = 1;
        private const int Player2ID = 2;
        private const uint Seed = 20260817u;
        private const int CorrectionFrame = 120;
        private const int PostRollbackFrameCount = 60;
        private const float TickLength = 1f / 60f;

        /// <summary>
        /// 验证只有 Player2 预测错误时，整个 FrameInputSet Rollback 后两个玩家均重新收敛。
        /// </summary>
        public static void RunTwoPlayerRollbackTestStatic(int rollbackDepth)
        {
            Expect(rollbackDepth > 0, $"MultiPlayer Rollback Depth Error: Value={rollbackDepth}");

            int receiveFrame = CorrectionFrame + rollbackDepth;
            int endFrame = receiveFrame + PostRollbackFrameCount;
            bool rollbackCompleted = false;

            using var reference = CreateEnvironment(true);
            using var predicted = CreateEnvironment(true);
            FrameInputSet authoritativeCorrection = default;

            for (int frame = 1; frame <= endFrame; frame++)
            {
                FrameInputSet referenceInput = CreateAuthoritativeFrameInputSet(frame);
                FrameInputSet predictedInput = CreateAuthoritativeFrameInputSet(frame);

                if (frame == CorrectionFrame)
                {
                    authoritativeCorrection = referenceInput;
                    predictedInput = CreateWrongPlayer2FrameInputSet(frame);
                }

                DriveFrame(reference, frame, referenceInput, true);
                DriveFrame(predicted, frame, predictedInput, true);

                if (frame == receiveFrame)
                {
                    AssertPlayerEqual(reference, predicted, reference.Player1, predicted.Player1, frame, "PreRollback Player1");
                    ExpectPlayerStateDifferent(reference, predicted, reference.Player2, predicted.Player2, frame, rollbackDepth);

                    predicted.Coordinator.ReceiveAuthoritativeInput(CorrectionFrame, authoritativeCorrection);

                    Expect(predicted.Coordinator.CurrentFrame == frame,
                        $"MultiPlayer Rollback CurrentFrame Error: Depth={rollbackDepth}, Expected={frame}, Actual={predicted.Coordinator.CurrentFrame}");

                    AssertWorldEqual(reference, predicted, frame, $"AfterRollback Depth={rollbackDepth}");
                    rollbackCompleted = true;
                    continue;
                }

                if (rollbackCompleted) AssertWorldEqual(reference, predicted, frame, $"PostRollback Depth={rollbackDepth}");
            }

            Expect(rollbackCompleted, $"MultiPlayer Rollback Execution Error: Depth={rollbackDepth}, Rollback Was Not Triggered");
            AssertWorldEqual(reference, predicted, endFrame, $"Final Depth={rollbackDepth}");

            Expect(reference.Coordinator.CurrentFrame == endFrame,
                $"MultiPlayer Reference FinalFrame Error: Depth={rollbackDepth}, Expected={endFrame}, Actual={reference.Coordinator.CurrentFrame}");

            Expect(predicted.Coordinator.CurrentFrame == endFrame,
                $"MultiPlayer Predicted FinalFrame Error: Depth={rollbackDepth}, Expected={endFrame}, Actual={predicted.Coordinator.CurrentFrame}");
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
                $"MultiPlayer DriveFrame Error: Frame={frame}, Kind={result.FailureKind}, Message={result.Message}");

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

        private static FrameInputSet CreateWrongPlayer2FrameInputSet(int frame)
        {
            PlayerInputSnapshot player1 = CreatePlayerInput(frame, Player1ID);
            PlayerInputSnapshot player2 = CreatePlayerInput(frame, Player2ID);

            player2.moveX = player2.moveX == 1f ? -1f : 1f;
            player2.moveY = player2.moveY == -1f ? 1f : -1f;

            return new FrameInputSet(frame, new[]
            {
                player1,
                player2
            });
        }

        private static PlayerInputSnapshot CreatePlayerInput(int frame, int playerID)
        {
            uint state = Seed ^ unchecked((uint)frame * 0x9E3779B9u) ^ unchecked((uint)playerID * 0x85EBCA6Bu);
            state = NextRandom(state);
            float moveX = (int)(state % 3u) - 1;
            state = NextRandom(state);
            float moveY = (int)(state % 3u) - 1;

            Expect(moveX >= -1f && moveX <= 1f,
                $"MultiPlayer CreateInput MoveX Error: Frame={frame}, PlayerID={playerID}, Value={moveX}");

            Expect(moveY >= -1f && moveY <= 1f,
                $"MultiPlayer CreateInput MoveY Error: Frame={frame}, PlayerID={playerID}, Value={moveY}");

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

            Expect(a.Player1 == b.Player1,
                $"{stage} Player1 Entity Error: Frame={frame}, A={a.Player1}, B={b.Player1}");

            Expect(a.Player2 == b.Player2,
                $"{stage} Player2 Entity Error: Frame={frame}, A={a.Player2}, B={b.Player2}");

            AssertPlayerEqual(a, b, a.Player1, b.Player1, frame, $"{stage} Player1");
            AssertPlayerEqual(a, b, a.Player2, b.Player2, frame, $"{stage} Player2");

            uint checksumA = WorldChecksumCalculator.Calculate(a.World);
            uint checksumB = WorldChecksumCalculator.Calculate(b.World);

            Expect(checksumA == checksumB,
                $"{stage} Checksum Error: Frame={frame}, A=0x{checksumA:X8}, B=0x{checksumB:X8}");
        }

        private static void AssertPlayerEqual(TestEnvironment a, TestEnvironment b, Entity playerA, Entity playerB, int frame, string stage)
        {
            Expect(a.World.IsAlive(playerA) == b.World.IsAlive(playerB),
                $"{stage} Alive Error: Frame={frame}, A={a.World.IsAlive(playerA)}, B={b.World.IsAlive(playerB)}");

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

            Expect(a.World.TryGetComponent(playerA, out MoveSpeedComponent speedA),
                $"{stage} MoveSpeedA Missing Error: Frame={frame}");

            Expect(b.World.TryGetComponent(playerB, out MoveSpeedComponent speedB),
                $"{stage} MoveSpeedB Missing Error: Frame={frame}");

            ExpectFloatBits(speedA.value, speedB.value, frame, stage, "MoveSpeed");

            Expect(a.World.TryGetComponent(playerA, out PlayerInputSnapshotComponent inputA),
                $"{stage} InputA Missing Error: Frame={frame}");

            Expect(b.World.TryGetComponent(playerB, out PlayerInputSnapshotComponent inputB),
                $"{stage} InputB Missing Error: Frame={frame}");

            AssertInputEqual(inputA, inputB, frame, stage);

            Expect(a.World.HasComponent<PlayerTagComponent>(playerA) == b.World.HasComponent<PlayerTagComponent>(playerB),
                $"{stage} PlayerTag Error: Frame={frame}");
        }

        private static void ExpectPlayerStateDifferent(TestEnvironment reference, TestEnvironment predicted, Entity referencePlayer, Entity predictedPlayer, int frame, int rollbackDepth)
        {
            Expect(reference.World.TryGetComponent(referencePlayer, out PositionComponent positionA),
                $"MultiPlayer PreCheck Player2 Reference Position Missing Error: Frame={frame}");

            Expect(predicted.World.TryGetComponent(predictedPlayer, out PositionComponent positionB),
                $"MultiPlayer PreCheck Player2 Predicted Position Missing Error: Frame={frame}");

            bool different =
                FloatBits(positionA.x) != FloatBits(positionB.x) ||
                FloatBits(positionA.z) != FloatBits(positionB.z);

            Expect(different,
                $"MultiPlayer PreCheck Error: Frame={frame}, Depth={rollbackDepth}, Player2 Predicted State Did Not Diverge");
        }

        private static void AssertInputEqual(PlayerInputSnapshotComponent a, PlayerInputSnapshotComponent b, int frame, string stage)
        {
            Expect(a.inputFrame == b.inputFrame,
                $"{stage} InputFrame Error: Frame={frame}, A={a.inputFrame}, B={b.inputFrame}");

            Expect(a.playerID == b.playerID,
                $"{stage} PlayerID Error: Frame={frame}, A={a.playerID}, B={b.playerID}");

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