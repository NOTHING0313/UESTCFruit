using ECSFrameWork;
using System;
using System.Collections.Generic;

namespace FrameWork.RollBackSystem
{
    /// <summary>
    /// ECS 结构变化 Rollback 验证。
    /// </summary>
    public static class StructuralRollbackValidationTestBootstrap
    {
        private const int PlayerID = 1;
        private const int Seed = 20260817;
        private const int CorrectionFrame = 120;
        private const int AddHealthFrame = 121;
        private const int CreateAFrame = 122;
        private const int AddStatFrame = 125;
        private const int RemoveHealthFrame = 128;
        private const int DestroyAFrame = 130;
        private const int UpdateStatFrame = 135;
        private const int CreateBFrame = 140;
        private const int ReAddHealthFrame = 145;
        private const int RemoveStatFrame = 155;
        private const int PostRollbackFrameCount = 60;
        private const float TickLength = 1f / 60f;

        /// <summary>
        /// 验证无 Rollback 情况下结构命令本身的时间线正确。
        /// </summary>
        public static void RunStructuralCommandTimelineTestStatic()
        {
            const int endFrame = 180;
            using var env = CreateEnvironment(false);
            StructuralScenario scenario = ScheduleScenario(env);
            bool destroyScheduled = false;

            for (int frame = 1; frame <= endFrame; frame++)
            {
                DriveFrame(env, frame, CreateInput(frame), false);

                if (frame == CreateAFrame && !destroyScheduled)
                {
                    ScheduleDestroyA(env, scenario);
                    destroyScheduled = true;
                }

                AssertExpectedScenarioState(env, scenario, frame, "StructuralTimeline");
            }

            Expect(destroyScheduled, "StructuralTimeline DestroySchedule Error: Destroy Command Was Not Scheduled");
            AssertEntityReuse(scenario, endFrame, "StructuralTimeline Final");
        }

        /// <summary>
        /// 验证结构变化在不同 Rollback 深度下重放后与 Reference World 严格收敛。
        /// </summary>
        public static void RunStructuralRollbackReplayTestStatic(int rollbackDepth)
        {
            Expect(rollbackDepth > 0, $"Structural Rollback Depth Error: Value={rollbackDepth}");

            int receiveFrame = CorrectionFrame + rollbackDepth;
            int endFrame = receiveFrame + PostRollbackFrameCount;
            bool rollbackCompleted = false;
            bool destroyScheduled = false;

            using var reference = CreateEnvironment(true);
            using var predicted = CreateEnvironment(true);
            StructuralScenario referenceScenario = ScheduleScenario(reference);
            StructuralScenario predictedScenario = ScheduleScenario(predicted);
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

                if (frame == CreateAFrame && !destroyScheduled)
                {
                    ScheduleDestroyA(reference, referenceScenario);
                    ScheduleDestroyA(predicted, predictedScenario);
                    destroyScheduled = true;

                    Expect(referenceScenario.CreateA.LastCreatedEntity == predictedScenario.CreateA.LastCreatedEntity,
                        $"Structural CreateA Initial Entity Error: Frame={frame}, Reference={referenceScenario.CreateA.LastCreatedEntity}, Predicted={predictedScenario.CreateA.LastCreatedEntity}");
                }

                AssertExpectedScenarioState(reference, referenceScenario, frame, $"Reference Depth={rollbackDepth}");
                AssertExpectedScenarioState(predicted, predictedScenario, frame, $"Predicted Depth={rollbackDepth}");

                if (frame == receiveFrame)
                {
                    ExpectMovementStateDifferent(reference, predicted, frame);
                    predicted.Coordinator.ReceiveAuthoritativeInput(CorrectionFrame, authoritativeCorrection);

                    Expect(predicted.Coordinator.CurrentFrame == frame,
                        $"Structural Rollback CurrentFrame Error: Depth={rollbackDepth}, Expected={frame}, Actual={predicted.Coordinator.CurrentFrame}");

                    AssertWorldEquivalent(reference, predicted, referenceScenario, predictedScenario, frame, $"AfterRollback Depth={rollbackDepth}");
                    AssertExpectedScenarioState(predicted, predictedScenario, frame, $"AfterRollback Expected Depth={rollbackDepth}");
                    rollbackCompleted = true;
                    continue;
                }

                if (rollbackCompleted)
                    AssertWorldEquivalent(reference, predicted, referenceScenario, predictedScenario, frame, $"PostRollback Depth={rollbackDepth}");
            }

            Expect(rollbackCompleted, $"Structural Rollback Execution Error: Depth={rollbackDepth}, Rollback Was Not Triggered");
            Expect(destroyScheduled, $"Structural DestroySchedule Error: Depth={rollbackDepth}, Destroy Command Was Not Scheduled");

            AssertWorldEquivalent(reference, predicted, referenceScenario, predictedScenario, endFrame, $"Final Depth={rollbackDepth}");
            AssertExpectedScenarioState(reference, referenceScenario, endFrame, $"Reference Final Depth={rollbackDepth}");
            AssertExpectedScenarioState(predicted, predictedScenario, endFrame, $"Predicted Final Depth={rollbackDepth}");
            AssertEntityReuse(referenceScenario, endFrame, $"Reference Final Depth={rollbackDepth}");
            AssertEntityReuse(predictedScenario, endFrame, $"Predicted Final Depth={rollbackDepth}");
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

        private static StructuralScenario ScheduleScenario(TestEnvironment env)
        {
            env.CommandBuffer.AddCommand(
                new SetComponentFrameCommand<HealthComponent>(AddHealthFrame, env.Player, new HealthComponent(70, 100)),
                SimulationFrameCommandTiming.BeforeTick);

            var createA = new CreateEntityFrameCommand(CreateAFrame)
                .WithComponent(new PositionComponent(7f, 8f, 9f))
                .WithComponent(new HealthComponent(50, 50));

            env.CommandBuffer.AddCommand(createA, SimulationFrameCommandTiming.BeforeTick);

            env.CommandBuffer.AddCommand(
                new SetComponentFrameCommand<StatComponent>(AddStatFrame, env.Player, new StatComponent(11, 22, 33)),
                SimulationFrameCommandTiming.BeforeTick);

            env.CommandBuffer.AddCommand(
                new RemoveComponentFrameCommand<HealthComponent>(RemoveHealthFrame, env.Player),
                SimulationFrameCommandTiming.BeforeTick);

            env.CommandBuffer.AddCommand(
                new SetComponentFrameCommand<StatComponent>(UpdateStatFrame, env.Player, new StatComponent(44, 55, 66)),
                SimulationFrameCommandTiming.BeforeTick);

            var createB = new CreateEntityFrameCommand(CreateBFrame)
                .WithComponent(new PositionComponent(-3f, 4f, 5f))
                .WithComponent(new HealthComponent(80, 80))
                .WithComponent(new StatComponent(3, 4, 5));

            env.CommandBuffer.AddCommand(createB, SimulationFrameCommandTiming.BeforeTick);

            env.CommandBuffer.AddCommand(
                new SetComponentFrameCommand<HealthComponent>(ReAddHealthFrame, env.Player, new HealthComponent(90, 120)),
                SimulationFrameCommandTiming.BeforeTick);

            env.CommandBuffer.AddCommand(
                new RemoveComponentFrameCommand<StatComponent>(RemoveStatFrame, env.Player),
                SimulationFrameCommandTiming.BeforeTick);

            return new StructuralScenario(createA, createB);
        }

        private static void ScheduleDestroyA(TestEnvironment env, StructuralScenario scenario)
        {
            Entity entity = scenario.CreateA.LastCreatedEntity;
            Expect(entity.IsValid, $"Structural DestroySchedule Error: CreateA Entity Invalid At Frame={CreateAFrame}");
            Expect(env.World.IsAlive(entity), $"Structural DestroySchedule Error: CreateA Entity Not Alive At Frame={CreateAFrame}, Entity={entity}");

            scenario.DestroyTargetA = entity;
            env.CommandBuffer.AddCommand(
                new DestroyEntityFrameCommand(DestroyAFrame, entity),
                SimulationFrameCommandTiming.BeforeTick);
        }

        private static void DriveFrame(TestEnvironment env, int frame, PlayerInputSnapshot input, bool saveSnapshot)
        {
            RollbackStepResult result = env.Coordinator.TryStep(frame, input);
            Expect(result.Succeeded, $"Structural DriveFrame TryStep Error: Frame={frame}, Kind={result.FailureKind}, Message={result.Message}");

            var context = new SimulationContext(frame, TickLength, false);
            env.CommandApplier.ApplyCommandsToWorld(frame, SimulationFrameCommandTiming.BeforeTick);
            env.World.Tick(in context);
            env.CommandApplier.ApplyCommandsToWorld(frame, SimulationFrameCommandTiming.AfterTick);

            if (saveSnapshot) env.Coordinator.SaveSnapshot();
        }

        private static void AssertExpectedScenarioState(TestEnvironment env, StructuralScenario scenario, int frame, string stage)
        {
            AssertExpectedPlayerHealth(env, frame, stage);
            AssertExpectedPlayerStat(env, frame, stage);

            if (frame < CreateAFrame)
            {
                Expect(!scenario.CreateA.LastCreatedEntity.IsValid, $"{stage} CreateA Error: Frame={frame}, Entity Should Still Be Invalid, Actual={scenario.CreateA.LastCreatedEntity}");
            }
            else
            {
                Entity a = scenario.CreateA.LastCreatedEntity;
                Expect(a.IsValid, $"{stage} CreateA Error: Frame={frame}, Entity Invalid");

                if (frame < DestroyAFrame)
                {
                    Expect(env.World.IsAlive(a), $"{stage} CreateA Alive Error: Frame={frame}, Entity={a}");
                    AssertPosition(env, a, 7f, 8f, 9f, frame, stage, "CreateA");
                    AssertHealth(env, a, 50, 50, frame, stage, "CreateA");
                }
                else
                    Expect(!env.World.IsAlive(a), $"{stage} DestroyA Error: Frame={frame}, Entity Still Alive={a}");
            }

            if (frame < CreateBFrame)
            {
                Expect(!scenario.CreateB.LastCreatedEntity.IsValid, $"{stage} CreateB Error: Frame={frame}, Entity Should Still Be Invalid, Actual={scenario.CreateB.LastCreatedEntity}");
                return;
            }

            Entity b = scenario.CreateB.LastCreatedEntity;
            Expect(b.IsValid, $"{stage} CreateB Error: Frame={frame}, Entity Invalid");
            Expect(env.World.IsAlive(b), $"{stage} CreateB Alive Error: Frame={frame}, Entity={b}");
            AssertPosition(env, b, -3f, 4f, 5f, frame, stage, "CreateB");
            AssertHealth(env, b, 80, 80, frame, stage, "CreateB");
            AssertStat(env, b, 3, 4, 5, frame, stage, "CreateB");
        }

        private static void AssertExpectedPlayerHealth(TestEnvironment env, int frame, string stage)
        {
            if (frame < AddHealthFrame)
            {
                Expect(!env.World.HasComponent<HealthComponent>(env.Player), $"{stage} PlayerHealth Error: Frame={frame}, Health Should Be Absent");
                return;
            }

            if (frame < RemoveHealthFrame)
            {
                AssertHealth(env, env.Player, 70, 100, frame, stage, "Player");
                return;
            }

            if (frame < ReAddHealthFrame)
            {
                Expect(!env.World.HasComponent<HealthComponent>(env.Player), $"{stage} PlayerHealth Error: Frame={frame}, Health Should Be Removed");
                return;
            }

            AssertHealth(env, env.Player, 90, 120, frame, stage, "Player");
        }

        private static void AssertExpectedPlayerStat(TestEnvironment env, int frame, string stage)
        {
            if (frame < AddStatFrame)
            {
                Expect(!env.World.HasComponent<StatComponent>(env.Player), $"{stage} PlayerStat Error: Frame={frame}, Stat Should Be Absent");
                return;
            }

            if (frame < UpdateStatFrame)
            {
                AssertStat(env, env.Player, 11, 22, 33, frame, stage, "Player");
                return;
            }

            if (frame < RemoveStatFrame)
            {
                AssertStat(env, env.Player, 44, 55, 66, frame, stage, "Player");
                return;
            }

            Expect(!env.World.HasComponent<StatComponent>(env.Player), $"{stage} PlayerStat Error: Frame={frame}, Stat Should Be Removed");
        }

        private static void AssertWorldEquivalent(TestEnvironment a, TestEnvironment b, StructuralScenario scenarioA, StructuralScenario scenarioB, int frame, string stage)
        {
            Expect(a.Coordinator.CurrentFrame == b.Coordinator.CurrentFrame, $"{stage} CoordinatorFrame Error: Frame={frame}, A={a.Coordinator.CurrentFrame}, B={b.Coordinator.CurrentFrame}");
            Expect(a.World.AliveEntityCount == b.World.AliveEntityCount, $"{stage} AliveEntityCount Error: Frame={frame}, A={a.World.AliveEntityCount}, B={b.World.AliveEntityCount}");
            Expect(a.World.CreatedEntityCount == b.World.CreatedEntityCount, $"{stage} CreatedEntityCount Error: Frame={frame}, A={a.World.CreatedEntityCount}, B={b.World.CreatedEntityCount}");
            Expect(a.World.FreeEntityCount == b.World.FreeEntityCount, $"{stage} FreeEntityCount Error: Frame={frame}, A={a.World.FreeEntityCount}, B={b.World.FreeEntityCount}");
            Expect(a.World.RegisteredComponentTypeCount == b.World.RegisteredComponentTypeCount, $"{stage} RegisteredComponentTypeCount Error: Frame={frame}, A={a.World.RegisteredComponentTypeCount}, B={b.World.RegisteredComponentTypeCount}");
            Expect(a.World.PendingCommandCount == 0, $"{stage} PendingCommandA Error: Frame={frame}, Count={a.World.PendingCommandCount}");
            Expect(b.World.PendingCommandCount == 0, $"{stage} PendingCommandB Error: Frame={frame}, Count={b.World.PendingCommandCount}");

            AssertAliveEntitySetEqual(a.World, b.World, frame, stage);
            AssertKnownEntityStateEqual(a.World, b.World, a.Player, b.Player, frame, stage, "Player");

            Expect(scenarioA.CreateA.LastCreatedEntity == scenarioB.CreateA.LastCreatedEntity,
                $"{stage} CreateA Entity Error: Frame={frame}, A={scenarioA.CreateA.LastCreatedEntity}, B={scenarioB.CreateA.LastCreatedEntity}");

            Expect(scenarioA.CreateB.LastCreatedEntity == scenarioB.CreateB.LastCreatedEntity,
                $"{stage} CreateB Entity Error: Frame={frame}, A={scenarioA.CreateB.LastCreatedEntity}, B={scenarioB.CreateB.LastCreatedEntity}");

            if (scenarioA.CreateA.LastCreatedEntity.IsValid)
                AssertKnownEntityStateEqual(a.World, b.World, scenarioA.CreateA.LastCreatedEntity, scenarioB.CreateA.LastCreatedEntity, frame, stage, "CreateA");

            if (scenarioA.CreateB.LastCreatedEntity.IsValid)
                AssertKnownEntityStateEqual(a.World, b.World, scenarioA.CreateB.LastCreatedEntity, scenarioB.CreateB.LastCreatedEntity, frame, stage, "CreateB");

            if (scenarioA.DestroyTargetA.IsValid || scenarioB.DestroyTargetA.IsValid)
                Expect(scenarioA.DestroyTargetA == scenarioB.DestroyTargetA, $"{stage} DestroyTargetA Error: Frame={frame}, A={scenarioA.DestroyTargetA}, B={scenarioB.DestroyTargetA}");

            uint checksumA = WorldChecksumCalculator.Calculate(a.World);
            uint checksumB = WorldChecksumCalculator.Calculate(b.World);
            Expect(checksumA == checksumB, $"{stage} Checksum Error: Frame={frame}, A={checksumA}, B={checksumB}");
        }

        private static void AssertAliveEntitySetEqual(World a, World b, int frame, string stage)
        {
            var entitiesA = new List<Entity>();
            var entitiesB = new List<Entity>();
            a.FillAliveEntities(entitiesA);
            b.FillAliveEntities(entitiesB);

            Expect(entitiesA.Count == entitiesB.Count, $"{stage} AliveSet Count Error: Frame={frame}, A={entitiesA.Count}, B={entitiesB.Count}");

            for (int i = 0; i < entitiesA.Count; i++)
                Expect(entitiesA[i] == entitiesB[i], $"{stage} AliveSet Entity Error: Frame={frame}, Index={i}, A={entitiesA[i]}, B={entitiesB[i]}");
        }

        private static void AssertKnownEntityStateEqual(World a, World b, Entity entityA, Entity entityB, int frame, string stage, string name)
        {
            bool aliveA = a.IsAlive(entityA);
            bool aliveB = b.IsAlive(entityB);
            Expect(aliveA == aliveB, $"{stage} {name} Alive Error: Frame={frame}, A={aliveA}, B={aliveB}");

            if (!aliveA) return;

            AssertComponentPresenceEqual<PositionComponent>(a, b, entityA, entityB, frame, stage, name);
            AssertComponentPresenceEqual<VelocityComponent>(a, b, entityA, entityB, frame, stage, name);
            AssertComponentPresenceEqual<MoveSpeedComponent>(a, b, entityA, entityB, frame, stage, name);
            AssertComponentPresenceEqual<PlayerInputSnapshotComponent>(a, b, entityA, entityB, frame, stage, name);
            AssertComponentPresenceEqual<PlayerTagComponent>(a, b, entityA, entityB, frame, stage, name);
            AssertComponentPresenceEqual<HealthComponent>(a, b, entityA, entityB, frame, stage, name);
            AssertComponentPresenceEqual<StatComponent>(a, b, entityA, entityB, frame, stage, name);

            if (a.TryGetComponent(entityA, out PositionComponent positionA) && b.TryGetComponent(entityB, out PositionComponent positionB))
            {
                ExpectFloatBits(positionA.x, positionB.x, frame, stage, $"{name}.Position.X");
                ExpectFloatBits(positionA.y, positionB.y, frame, stage, $"{name}.Position.Y");
                ExpectFloatBits(positionA.z, positionB.z, frame, stage, $"{name}.Position.Z");
            }

            if (a.TryGetComponent(entityA, out VelocityComponent velocityA) && b.TryGetComponent(entityB, out VelocityComponent velocityB))
            {
                ExpectFloatBits(velocityA.x, velocityB.x, frame, stage, $"{name}.Velocity.X");
                ExpectFloatBits(velocityA.y, velocityB.y, frame, stage, $"{name}.Velocity.Y");
                ExpectFloatBits(velocityA.z, velocityB.z, frame, stage, $"{name}.Velocity.Z");
            }

            if (a.TryGetComponent(entityA, out MoveSpeedComponent speedA) && b.TryGetComponent(entityB, out MoveSpeedComponent speedB))
                ExpectFloatBits(speedA.value, speedB.value, frame, stage, $"{name}.MoveSpeed");

            if (a.TryGetComponent(entityA, out PlayerInputSnapshotComponent inputA) && b.TryGetComponent(entityB, out PlayerInputSnapshotComponent inputB))
                AssertInputEqual(inputA, inputB, frame, stage, name);

            if (a.TryGetComponent(entityA, out HealthComponent healthA) && b.TryGetComponent(entityB, out HealthComponent healthB))
            {
                Expect(healthA.current == healthB.current, $"{stage} {name}.Health.Current Error: Frame={frame}, A={healthA.current}, B={healthB.current}");
                Expect(healthA.max == healthB.max, $"{stage} {name}.Health.Max Error: Frame={frame}, A={healthA.max}, B={healthB.max}");
            }

            if (a.TryGetComponent(entityA, out StatComponent statA) && b.TryGetComponent(entityB, out StatComponent statB))
            {
                Expect(statA.attack == statB.attack, $"{stage} {name}.Stat.Attack Error: Frame={frame}, A={statA.attack}, B={statB.attack}");
                Expect(statA.defense == statB.defense, $"{stage} {name}.Stat.Defense Error: Frame={frame}, A={statA.defense}, B={statB.defense}");
                Expect(statA.moveSpeed == statB.moveSpeed, $"{stage} {name}.Stat.MoveSpeed Error: Frame={frame}, A={statA.moveSpeed}, B={statB.moveSpeed}");
            }
        }

        private static void AssertComponentPresenceEqual<T>(World a, World b, Entity entityA, Entity entityB, int frame, string stage, string name) where T : struct, IComponentData
        {
            bool hasA = a.HasComponent<T>(entityA);
            bool hasB = b.HasComponent<T>(entityB);
            Expect(hasA == hasB, $"{stage} {name}.{typeof(T).Name} Presence Error: Frame={frame}, A={hasA}, B={hasB}");
        }

        private static void AssertHealth(TestEnvironment env, Entity entity, int current, int max, int frame, string stage, string name)
        {
            Expect(env.World.TryGetComponent(entity, out HealthComponent health), $"{stage} {name}.Health Missing Error: Frame={frame}, Entity={entity}");
            Expect(health.current == current, $"{stage} {name}.Health.Current Error: Frame={frame}, Expected={current}, Actual={health.current}");
            Expect(health.max == max, $"{stage} {name}.Health.Max Error: Frame={frame}, Expected={max}, Actual={health.max}");
        }

        private static void AssertStat(TestEnvironment env, Entity entity, int attack, int defense, int moveSpeed, int frame, string stage, string name)
        {
            Expect(env.World.TryGetComponent(entity, out StatComponent stat), $"{stage} {name}.Stat Missing Error: Frame={frame}, Entity={entity}");
            Expect(stat.attack == attack, $"{stage} {name}.Stat.Attack Error: Frame={frame}, Expected={attack}, Actual={stat.attack}");
            Expect(stat.defense == defense, $"{stage} {name}.Stat.Defense Error: Frame={frame}, Expected={defense}, Actual={stat.defense}");
            Expect(stat.moveSpeed == moveSpeed, $"{stage} {name}.Stat.MoveSpeed Error: Frame={frame}, Expected={moveSpeed}, Actual={stat.moveSpeed}");
        }

        private static void AssertPosition(TestEnvironment env, Entity entity, float x, float y, float z, int frame, string stage, string name)
        {
            Expect(env.World.TryGetComponent(entity, out PositionComponent position), $"{stage} {name}.Position Missing Error: Frame={frame}, Entity={entity}");
            ExpectFloatBits(position.x, x, frame, stage, $"{name}.Position.X");
            ExpectFloatBits(position.y, y, frame, stage, $"{name}.Position.Y");
            ExpectFloatBits(position.z, z, frame, stage, $"{name}.Position.Z");
        }

        private static void AssertEntityReuse(StructuralScenario scenario, int frame, string stage)
        {
            Entity a = scenario.CreateA.LastCreatedEntity;
            Entity b = scenario.CreateB.LastCreatedEntity;

            Expect(a.IsValid, $"{stage} EntityReuse CreateA Error: Frame={frame}, Entity Invalid");
            Expect(b.IsValid, $"{stage} EntityReuse CreateB Error: Frame={frame}, Entity Invalid");
            Expect(a.ID == b.ID, $"{stage} EntityReuse ID Error: Frame={frame}, A={a}, B={b}");
            Expect(b.Version == a.Version + 1, $"{stage} EntityReuse Version Error: Frame={frame}, A={a}, B={b}");
        }

        private static void ExpectMovementStateDifferent(TestEnvironment a, TestEnvironment b, int frame)
        {
            Expect(a.World.TryGetComponent(a.Player, out PositionComponent positionA), $"Structural Rollback PreCheck PositionA Error: Frame={frame}");
            Expect(b.World.TryGetComponent(b.Player, out PositionComponent positionB), $"Structural Rollback PreCheck PositionB Error: Frame={frame}");

            bool different = FloatBits(positionA.x) != FloatBits(positionB.x) || FloatBits(positionA.z) != FloatBits(positionB.z);
            Expect(different, $"Structural Rollback PreCheck Error: Frame={frame}, Predicted World Did Not Diverge Before Authoritative Correction");
        }

        private static void AssertInputEqual(PlayerInputSnapshotComponent a, PlayerInputSnapshotComponent b, int frame, string stage, string name)
        {
            Expect(a.inputFrame == b.inputFrame, $"{stage} {name}.InputFrame Error: Frame={frame}, A={a.inputFrame}, B={b.inputFrame}");
            Expect(a.playerID == b.playerID, $"{stage} {name}.PlayerID Error: Frame={frame}, A={a.playerID}, B={b.playerID}");
            ExpectFloatBits(a.moveX, b.moveX, frame, stage, $"{name}.Input.MoveX");
            ExpectFloatBits(a.moveY, b.moveY, frame, stage, $"{name}.Input.MoveY");
            ExpectFloatBits(a.mouseX, b.mouseX, frame, stage, $"{name}.Input.MouseX");
            ExpectFloatBits(a.mouseY, b.mouseY, frame, stage, $"{name}.Input.MouseY");
            ExpectFloatBits(a.mouseDeltaX, b.mouseDeltaX, frame, stage, $"{name}.Input.MouseDeltaX");
            ExpectFloatBits(a.mouseDeltaY, b.mouseDeltaY, frame, stage, $"{name}.Input.MouseDeltaY");
            ExpectFloatBits(a.scrollX, b.scrollX, frame, stage, $"{name}.Input.ScrollX");
            ExpectFloatBits(a.scrollY, b.scrollY, frame, stage, $"{name}.Input.ScrollY");
            Expect(a.pressedButtons == b.pressedButtons, $"{stage} {name}.PressedButtons Error: Frame={frame}, A={a.pressedButtons}, B={b.pressedButtons}");
            Expect(a.heldButtons == b.heldButtons, $"{stage} {name}.HeldButtons Error: Frame={frame}, A={a.heldButtons}, B={b.heldButtons}");
            Expect(a.releasedButtons == b.releasedButtons, $"{stage} {name}.ReleasedButtons Error: Frame={frame}, A={a.releasedButtons}, B={b.releasedButtons}");
        }

        private static PlayerInputSnapshot CreateInput(int frame)
        {
            uint state = unchecked((uint)Seed) ^ unchecked((uint)frame * 0x9E3779B9u);
            state = NextRandom(state);
            float moveX = (int)(state % 3) - 1;
            state = NextRandom(state);
            float moveY = (int)(state % 3) - 1;

            Expect(moveX >= -1f && moveX <= 1f, $"Structural CreateInput MoveX Error: Frame={frame}, Value={moveX}");
            Expect(moveY >= -1f && moveY <= 1f, $"Structural CreateInput MoveY Error: Frame={frame}, Value={moveY}");

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

        private sealed class StructuralScenario
        {
            public readonly CreateEntityFrameCommand CreateA;
            public readonly CreateEntityFrameCommand CreateB;
            public Entity DestroyTargetA;

            public StructuralScenario(CreateEntityFrameCommand createA, CreateEntityFrameCommand createB)
            {
                CreateA = createA;
                CreateB = createB;
                DestroyTargetA = Entity.Invalid;
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