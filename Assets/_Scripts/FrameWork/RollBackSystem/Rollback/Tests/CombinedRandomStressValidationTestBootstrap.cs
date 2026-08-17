using BuffSystem;
using ECSFrameWork;
using System;
using System.Collections.Generic;

namespace FrameWork.RollBackSystem
{
    /// <summary>
    /// Input、FrameCommand、ECS 结构变化、Buff 与随机重复 Rollback 的组合压力验证。
    /// </summary>
    public static class CombinedRandomStressValidationTestBootstrap
    {
        private const int PlayerID = 1;
        private const int BuffID = 990002;
        private const uint Seed = 20260817u;
        private const int TotalFrames = 10000;
        private const int FirstCorrectionFrame = 120;
        private const int MinCorrectionInterval = 90;
        private const int MaxCorrectionInterval = 180;
        private const int MinRollbackDepth = 1;
        private const int MaxRollbackDepth = 60;
        private const int MinExpectedRollbackCount = 50;
        private const float TickLength = 1f / 60f;

        /// <summary>
        /// 在固定 Seed 下组合执行随机输入、帧命令、组件增删、Entity 创建销毁、Buff 增删与随机 Rollback。
        /// </summary>
        public static void RunCombinedRandomStressTestStatic()
        {
            using var reference = CreateEnvironment(true);
            using var predicted = CreateEnvironment(true);
            var scenario = new CombinedScenario();

            uint rollbackRandomState = Seed ^ 0xB5297A4Du;
            int nextCorrectionFrame = FirstCorrectionFrame;
            int rollbackCount = 0;
            bool hasPending = false;
            RollbackEvent pending = default;

            for (int frame = 1; frame <= TotalFrames; frame++)
            {
                ScheduleGameplayEvents(reference, predicted, scenario, frame);

                PlayerInputSnapshot authoritative = CreateInput(frame);
                PlayerInputSnapshot local = authoritative;

                if (!hasPending && frame == nextCorrectionFrame)
                {
                    int depth = NextRange(ref rollbackRandomState, MinRollbackDepth, MaxRollbackDepth);
                    int receiveFrame = frame + depth;

                    if (receiveFrame <= TotalFrames)
                    {
                        local = CreateWrongPrediction(authoritative);
                        pending = new RollbackEvent(rollbackCount + 1, frame, receiveFrame, depth, authoritative);
                        hasPending = true;
                    }
                    else nextCorrectionFrame = int.MaxValue;
                }

                DriveFrame(reference, frame, authoritative, true);
                DriveFrame(predicted, frame, local, true);
                FinalizeCreatesForFrame(reference, predicted, scenario, frame);

                AssertPlannedPlayerState(reference, scenario, frame, "Reference");
                AssertPlannedPlayerState(predicted, scenario, frame, "Predicted");

                if (hasPending && frame == pending.ReceiveFrame)
                {
                    ExpectMovementStateDifferent(reference, predicted, frame, pending);
                    predicted.Coordinator.ReceiveAuthoritativeInput(pending.CorrectionFrame, pending.AuthoritativeInput);

                    Expect(predicted.Coordinator.CurrentFrame == frame,
                        $"CombinedStress CurrentFrame Error: Seed={Seed}, RollbackIndex={pending.Index}, CorrectionFrame={pending.CorrectionFrame}, ReceiveFrame={pending.ReceiveFrame}, Depth={pending.Depth}, Expected={frame}, Actual={predicted.Coordinator.CurrentFrame}");

                    AssertWorldEquivalent(reference, predicted, scenario, frame,
                        $"AfterRollback Seed={Seed} Index={pending.Index} Correction={pending.CorrectionFrame} Receive={pending.ReceiveFrame} Depth={pending.Depth}");
                    AssertPlannedPlayerState(predicted, scenario, frame, $"AfterRollback Predicted Index={pending.Index}");

                    rollbackCount++;
                    hasPending = false;
                    nextCorrectionFrame = frame + NextRange(ref rollbackRandomState, MinCorrectionInterval, MaxCorrectionInterval);
                    continue;
                }

                if (!hasPending && frame % 120 == 0) AssertWorldEquivalent(reference, predicted, scenario, frame, $"StableCheckpoint Frame={frame}");
            }

            Expect(!hasPending,
                $"CombinedStress Pending Error: Seed={Seed}, RollbackIndex={pending.Index}, CorrectionFrame={pending.CorrectionFrame}, ReceiveFrame={pending.ReceiveFrame}, Depth={pending.Depth}");
            Expect(rollbackCount >= MinExpectedRollbackCount, $"CombinedStress RollbackCount Error: Seed={Seed}, Expected>={MinExpectedRollbackCount}, Actual={rollbackCount}");
            Expect(scenario.PositionCommandCount >= 800, $"CombinedStress PositionCommandCount Error: Expected>=800, Actual={scenario.PositionCommandCount}");
            Expect(scenario.HealthCommandCount >= 300, $"CombinedStress HealthCommandCount Error: Expected>=300, Actual={scenario.HealthCommandCount}");
            Expect(scenario.BuffCommandCount >= 300, $"CombinedStress BuffCommandCount Error: Expected>=300, Actual={scenario.BuffCommandCount}");
            Expect(scenario.CreateCount >= 80, $"CombinedStress CreateCount Error: Expected>=80, Actual={scenario.CreateCount}");
            Expect(scenario.DestroyCount >= 70, $"CombinedStress DestroyCount Error: Expected>=70, Actual={scenario.DestroyCount}");

            AssertWorldEquivalent(reference, predicted, scenario, TotalFrames, "CombinedStress Final");
            AssertPlannedPlayerState(reference, scenario, TotalFrames, "Reference Final");
            AssertPlannedPlayerState(predicted, scenario, TotalFrames, "Predicted Final");
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
                "CombinedStressBuff",
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

        private static void ScheduleGameplayEvents(TestEnvironment reference, TestEnvironment predicted, CombinedScenario scenario, int frame)
        {
            // FrameCommand：随机 BeforeTick / AfterTick 修改 Position.y。
            if (FrameRandom(frame, 0x11A2B3C4u) % 100u < 10u)
            {
                int delta = (int)(FrameRandom(frame, 0x51E2D3C4u) % 7u) - 3;
                if (delta == 0) delta = 1;

                SimulationFrameCommandTiming timing = (FrameRandom(frame, 0x61F2E3D4u) & 1u) == 0u
                    ? SimulationFrameCommandTiming.BeforeTick
                    : SimulationFrameCommandTiming.AfterTick;

                reference.CommandBuffer.AddCommand(new PositionDeltaFrameCommand(frame, reference.Player, delta), timing);
                predicted.CommandBuffer.AddCommand(new PositionDeltaFrameCommand(frame, predicted.Player, delta), timing);
                scenario.PositionCommandCount++;
            }

            // Component Add / Remove。
            if (FrameRandom(frame, 0x22B3C4D5u) % 100u < 4u)
            {
                if (!scenario.HealthPlannedPresent)
                {
                    int current = 50 + (int)(FrameRandom(frame, 0x72A3B4C5u) % 51u);
                    int max = current + 50;
                    var health = new HealthComponent(current, max);

                    reference.CommandBuffer.AddCommand(new SetComponentFrameCommand<HealthComponent>(frame, reference.Player, in health), SimulationFrameCommandTiming.BeforeTick);
                    predicted.CommandBuffer.AddCommand(new SetComponentFrameCommand<HealthComponent>(frame, predicted.Player, in health), SimulationFrameCommandTiming.BeforeTick);
                }
                else
                {
                    reference.CommandBuffer.AddCommand(new RemoveComponentFrameCommand<HealthComponent>(frame, reference.Player), SimulationFrameCommandTiming.BeforeTick);
                    predicted.CommandBuffer.AddCommand(new RemoveComponentFrameCommand<HealthComponent>(frame, predicted.Player), SimulationFrameCommandTiming.BeforeTick);
                }

                scenario.HealthPlannedPresent = !scenario.HealthPlannedPresent;
                scenario.HealthCommandCount++;
            }

            // Buff Add / Remove，通过正式 Buff FrameCommand 请求入口进入 ECS。
            if (FrameRandom(frame, 0x33C4D5E6u) % 100u < 4u)
            {
                if (!scenario.BuffPlannedPresent)
                {
                    var add = new AddBuffCommand(reference.Player, BuffID, reference.Player, 1);
                    var addPredicted = new AddBuffCommand(predicted.Player, BuffID, predicted.Player, 1);
                    reference.CommandBuffer.AddBuffAtFrame(frame, SimulationFrameCommandTiming.BeforeTick, in add);
                    predicted.CommandBuffer.AddBuffAtFrame(frame, SimulationFrameCommandTiming.BeforeTick, in addPredicted);
                }
                else
                {
                    var remove = new RemoveBuffCommand(reference.Player, BuffID, reference.Player, 1, false, true);
                    var removePredicted = new RemoveBuffCommand(predicted.Player, BuffID, predicted.Player, 1, false, true);
                    reference.CommandBuffer.RemoveBuffAtFrame(frame, SimulationFrameCommandTiming.BeforeTick, in remove);
                    predicted.CommandBuffer.RemoveBuffAtFrame(frame, SimulationFrameCommandTiming.BeforeTick, in removePredicted);
                }

                scenario.BuffPlannedPresent = !scenario.BuffPlannedPresent;
                scenario.BuffCommandCount++;
            }

            // Entity Create，之后随机 20~60 帧再 Destroy。
            if (FrameRandom(frame, 0x44D5E6F7u) % 100u < 1u)
            {
                float x = (int)(FrameRandom(frame, 0x83B4C5D6u) % 21u) - 10;
                float y = (int)(FrameRandom(frame, 0x93C4D5E6u) % 11u);
                float z = (int)(FrameRandom(frame, 0xA4D5E6F7u) % 21u) - 10;
                int hp = 20 + (int)(FrameRandom(frame, 0xB5E6F708u) % 81u);
                int attack = 1 + (int)(FrameRandom(frame, 0xC6F70819u) % 20u);
                int defense = 1 + (int)(FrameRandom(frame, 0xD708192Au) % 20u);
                int moveSpeed = 1 + (int)(FrameRandom(frame, 0xE8192A3Bu) % 10u);

                CreateEntityFrameCommand createA = reference.CommandBuffer.CreateEntityAtFrame(frame, SimulationFrameCommandTiming.BeforeTick);
                CreateEntityFrameCommand createB = predicted.CommandBuffer.CreateEntityAtFrame(frame, SimulationFrameCommandTiming.BeforeTick);

                createA.WithComponent(new PositionComponent(x, y, z))
                    .WithComponent(new HealthComponent(hp, hp))
                    .WithComponent(new StatComponent(attack, defense, moveSpeed));

                createB.WithComponent(new PositionComponent(x, y, z))
                    .WithComponent(new HealthComponent(hp, hp))
                    .WithComponent(new StatComponent(attack, defense, moveSpeed));

                int destroyDelay = 20 + (int)(FrameRandom(frame, 0xF92A3B4Cu) % 41u);
                scenario.Creates.Add(new CreateRecord(frame, frame + destroyDelay, createA, createB));
                scenario.CreateCount++;
            }
        }

        private static void FinalizeCreatesForFrame(TestEnvironment reference, TestEnvironment predicted, CombinedScenario scenario, int frame)
        {
            for (int i = 0; i < scenario.Creates.Count; i++)
            {
                CreateRecord record = scenario.Creates[i];
                if (record.CreateFrame != frame || record.Finalized) continue;

                Entity entityA = record.ReferenceCreate.LastCreatedEntity;
                Entity entityB = record.PredictedCreate.LastCreatedEntity;

                Expect(entityA.IsValid && entityB.IsValid, $"CombinedStress Create Invalid Error: Frame={frame}, A={entityA}, B={entityB}");
                Expect(entityA == entityB, $"CombinedStress Create Identity Error: Frame={frame}, A={entityA}, B={entityB}");
                Expect(reference.World.IsAlive(entityA) && predicted.World.IsAlive(entityB), $"CombinedStress Create Alive Error: Frame={frame}, A={entityA}, B={entityB}");

                record.ReferenceEntity = entityA;
                record.PredictedEntity = entityB;
                record.Finalized = true;

                if (record.DestroyFrame > TotalFrames) continue;

                reference.CommandBuffer.AddCommand(new DestroyEntityFrameCommand(record.DestroyFrame, entityA), SimulationFrameCommandTiming.BeforeTick);
                predicted.CommandBuffer.AddCommand(new DestroyEntityFrameCommand(record.DestroyFrame, entityB), SimulationFrameCommandTiming.BeforeTick);
                record.DestroyScheduled = true;
                scenario.DestroyCount++;
            }
        }

        private static void DriveFrame(TestEnvironment env, int frame, PlayerInputSnapshot input, bool saveSnapshot)
        {
            RollbackStepResult result = env.Coordinator.TryStep(frame, input);

            Expect(result.Succeeded,
                $"CombinedStress DriveFrame Error: Seed={Seed}, Frame={frame}, Kind={result.FailureKind}, Message={result.Message}");

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
            float moveX = (int)(state % 3u) - 1;
            state = NextRandom(state);
            float moveY = (int)(state % 3u) - 1;

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

        private static void AssertPlannedPlayerState(TestEnvironment env, CombinedScenario scenario, int frame, string stage)
        {
            bool hasHealth = env.World.HasComponent<HealthComponent>(env.Player);

            Expect(hasHealth == scenario.HealthPlannedPresent,
                $"{stage} PlannedHealth Error: Frame={frame}, Expected={scenario.HealthPlannedPresent}, Actual={hasHealth}");

            bool hasBuff = env.BuffSystem.TryGetBuff(env.Player, BuffID, env.Player, out BuffViewData buff);

            Expect(hasBuff == scenario.BuffPlannedPresent,
                $"{stage} PlannedBuff Error: Frame={frame}, Expected={scenario.BuffPlannedPresent}, Actual={hasBuff}");

            if (!hasBuff) return;

            Expect(buff.Stack == 1, $"{stage} BuffStack Error: Frame={frame}, Expected=1, Actual={buff.Stack}");
            Expect(buff.RemainingFrames == -1, $"{stage} BuffRemaining Error: Frame={frame}, Expected=-1, Actual={buff.RemainingFrames}");
        }

        private static void AssertWorldEquivalent(TestEnvironment a, TestEnvironment b, CombinedScenario scenario, int frame, string stage)
        {
            Expect(a.Coordinator.CurrentFrame == b.Coordinator.CurrentFrame,
                $"{stage} CoordinatorFrame Error: Frame={frame}, A={a.Coordinator.CurrentFrame}, B={b.Coordinator.CurrentFrame}");

            Expect(a.World.AliveEntityCount == b.World.AliveEntityCount,
                $"{stage} AliveEntityCount Error: Frame={frame}, A={a.World.AliveEntityCount}, B={b.World.AliveEntityCount}");

            Expect(a.World.CreatedEntityCount == b.World.CreatedEntityCount,
                $"{stage} CreatedEntityCount Error: Frame={frame}, A={a.World.CreatedEntityCount}, B={b.World.CreatedEntityCount}");

            Expect(a.World.FreeEntityCount == b.World.FreeEntityCount,
                $"{stage} FreeEntityCount Error: Frame={frame}, A={a.World.FreeEntityCount}, B={b.World.FreeEntityCount}");

            Expect(a.World.RegisteredComponentTypeCount == b.World.RegisteredComponentTypeCount,
                $"{stage} ComponentTypeCount Error: Frame={frame}, A={a.World.RegisteredComponentTypeCount}, B={b.World.RegisteredComponentTypeCount}");

            Expect(a.World.SystemCount == b.World.SystemCount,
                $"{stage} SystemCount Error: Frame={frame}, A={a.World.SystemCount}, B={b.World.SystemCount}");

            Expect(a.World.PendingCommandCount == 0 && b.World.PendingCommandCount == 0,
                $"{stage} PendingCommand Error: Frame={frame}, A={a.World.PendingCommandCount}, B={b.World.PendingCommandCount}");

            var entitiesA = new List<Entity>();
            var entitiesB = new List<Entity>();

            a.World.FillAliveEntities(entitiesA);
            b.World.FillAliveEntities(entitiesB);
            entitiesA.Sort(EntityComparer.Instance);
            entitiesB.Sort(EntityComparer.Instance);

            Expect(entitiesA.Count == entitiesB.Count,
                $"{stage} AliveSet Count Error: Frame={frame}, A={entitiesA.Count}, B={entitiesB.Count}");

            for (int i = 0; i < entitiesA.Count; i++)
            {
                Expect(entitiesA[i] == entitiesB[i],
                    $"{stage} AliveSet Entity Error: Frame={frame}, Index={i}, A={entitiesA[i]}, B={entitiesB[i]}");

                AssertEntityEquivalent(a.World, b.World, entitiesA[i], entitiesB[i], frame, stage);
            }

            // 检查所有已创建过的 CreateEntityFrameCommand 在 Rollback replay 后仍生成同样 Entity。
            for (int i = 0; i < scenario.Creates.Count; i++)
            {
                CreateRecord record = scenario.Creates[i];
                if (record.CreateFrame > frame || !record.Finalized) continue;

                Expect(record.ReferenceCreate.LastCreatedEntity == record.PredictedCreate.LastCreatedEntity,
                    $"{stage} CreateReplay Identity Error: Frame={frame}, CreateFrame={record.CreateFrame}, A={record.ReferenceCreate.LastCreatedEntity}, B={record.PredictedCreate.LastCreatedEntity}");
            }

            // Buff Public View 也必须一致，避免 ECS 真状态正确但 Buff cache stale。
            bool hasBuffA = a.BuffSystem.TryGetBuff(a.Player, BuffID, a.Player, out BuffViewData buffA);
            bool hasBuffB = b.BuffSystem.TryGetBuff(b.Player, BuffID, b.Player, out BuffViewData buffB);

            Expect(hasBuffA == hasBuffB,
                $"{stage} Buff Presence Error: Frame={frame}, A={hasBuffA}, B={hasBuffB}");

            if (hasBuffA)
            {
                Expect(buffA.Target == buffB.Target, $"{stage} Buff.Target Error: Frame={frame}, A={buffA.Target}, B={buffB.Target}");
                Expect(buffA.Source == buffB.Source, $"{stage} Buff.Source Error: Frame={frame}, A={buffA.Source}, B={buffB.Source}");
                Expect(buffA.ConfigId == buffB.ConfigId, $"{stage} Buff.ConfigId Error: Frame={frame}, A={buffA.ConfigId}, B={buffB.ConfigId}");
                Expect(buffA.Stack == buffB.Stack, $"{stage} Buff.Stack Error: Frame={frame}, A={buffA.Stack}, B={buffB.Stack}");
                Expect(buffA.RemainingFrames == buffB.RemainingFrames, $"{stage} Buff.RemainingFrames Error: Frame={frame}, A={buffA.RemainingFrames}, B={buffB.RemainingFrames}");
                Expect(buffA.RuntimeHandle == buffB.RuntimeHandle, $"{stage} Buff.RuntimeHandle Error: Frame={frame}, A={buffA.RuntimeHandle}, B={buffB.RuntimeHandle}");
            }

            uint checksumA = WorldChecksumCalculator.Calculate(a.World);
            uint checksumB = WorldChecksumCalculator.Calculate(b.World);

            Expect(checksumA == checksumB,
                $"{stage} Checksum Error: Frame={frame}, A=0x{checksumA:X8}, B=0x{checksumB:X8}");
        }

        private static void AssertEntityEquivalent(World a, World b, Entity entityA, Entity entityB, int frame, string stage)
        {
            var typesA = new List<Type>();
            var typesB = new List<Type>();

            a.FillEntityComponentTypes(entityA, typesA);
            b.FillEntityComponentTypes(entityB, typesB);
            typesA.Sort(TypeComparer.Instance);
            typesB.Sort(TypeComparer.Instance);

            Expect(typesA.Count == typesB.Count,
                $"{stage} ComponentSet Count Error: Frame={frame}, Entity={entityA}, A={typesA.Count}, B={typesB.Count}");

            for (int i = 0; i < typesA.Count; i++)
            {
                Expect(typesA[i] == typesB[i],
                    $"{stage} ComponentSet Type Error: Frame={frame}, Entity={entityA}, Index={i}, A={typesA[i].FullName}, B={typesB[i].FullName}");
            }

            if (a.TryGetComponent(entityA, out PositionComponent positionA) &&
               b.TryGetComponent(entityB, out PositionComponent positionB))
            {
                ExpectFloatBits(positionA.x, positionB.x, frame, stage, $"Entity{entityA.ID}.Position.X");
                ExpectFloatBits(positionA.y, positionB.y, frame, stage, $"Entity{entityA.ID}.Position.Y");
                ExpectFloatBits(positionA.z, positionB.z, frame, stage, $"Entity{entityA.ID}.Position.Z");
            }

            if (a.TryGetComponent(entityA, out VelocityComponent velocityA) &&
               b.TryGetComponent(entityB, out VelocityComponent velocityB))
            {
                ExpectFloatBits(velocityA.x, velocityB.x, frame, stage, $"Entity{entityA.ID}.Velocity.X");
                ExpectFloatBits(velocityA.y, velocityB.y, frame, stage, $"Entity{entityA.ID}.Velocity.Y");
                ExpectFloatBits(velocityA.z, velocityB.z, frame, stage, $"Entity{entityA.ID}.Velocity.Z");
            }

            if (a.TryGetComponent(entityA, out MoveSpeedComponent speedA) &&
               b.TryGetComponent(entityB, out MoveSpeedComponent speedB))
            {
                ExpectFloatBits(speedA.value, speedB.value, frame, stage, $"Entity{entityA.ID}.MoveSpeed");
            }

            if (a.TryGetComponent(entityA, out PlayerInputSnapshotComponent inputA) &&
               b.TryGetComponent(entityB, out PlayerInputSnapshotComponent inputB))
            {
                AssertInputEqual(inputA, inputB, frame, stage, entityA);
            }

            if (a.TryGetComponent(entityA, out HealthComponent healthA) &&
               b.TryGetComponent(entityB, out HealthComponent healthB))
            {
                Expect(healthA.current == healthB.current,
                    $"{stage} Health.Current Error: Frame={frame}, Entity={entityA}, A={healthA.current}, B={healthB.current}");

                Expect(healthA.max == healthB.max,
                    $"{stage} Health.Max Error: Frame={frame}, Entity={entityA}, A={healthA.max}, B={healthB.max}");
            }

            if (a.TryGetComponent(entityA, out StatComponent statA) &&
               b.TryGetComponent(entityB, out StatComponent statB))
            {
                Expect(statA.attack == statB.attack,
                    $"{stage} Stat.Attack Error: Frame={frame}, Entity={entityA}, A={statA.attack}, B={statB.attack}");

                Expect(statA.defense == statB.defense,
                    $"{stage} Stat.Defense Error: Frame={frame}, Entity={entityA}, A={statA.defense}, B={statB.defense}");

                Expect(statA.moveSpeed == statB.moveSpeed,
                    $"{stage} Stat.MoveSpeed Error: Frame={frame}, Entity={entityA}, A={statA.moveSpeed}, B={statB.moveSpeed}");
            }

            if (a.TryGetComponent(entityA, out BuffRuntimeComponent buffA) &&
               b.TryGetComponent(entityB, out BuffRuntimeComponent buffB))
            {
                Expect(buffA.target == buffB.target,
                    $"{stage} BuffRuntime.Target Error: Frame={frame}, Entity={entityA}, A={buffA.target}, B={buffB.target}");

                Expect(buffA.source == buffB.source,
                    $"{stage} BuffRuntime.Source Error: Frame={frame}, Entity={entityA}, A={buffA.source}, B={buffB.source}");

                Expect(buffA.configId == buffB.configId,
                    $"{stage} BuffRuntime.ConfigId Error: Frame={frame}, Entity={entityA}, A={buffA.configId}, B={buffB.configId}");

                Expect(buffA.runtimeHandle == buffB.runtimeHandle,
                    $"{stage} BuffRuntime.Handle Error: Frame={frame}, Entity={entityA}, A={buffA.runtimeHandle}, B={buffB.runtimeHandle}");

                Expect(buffA.stack == buffB.stack,
                    $"{stage} BuffRuntime.Stack Error: Frame={frame}, Entity={entityA}, A={buffA.stack}, B={buffB.stack}");

                Expect(buffA.remainingFrames == buffB.remainingFrames,
                    $"{stage} BuffRuntime.Remaining Error: Frame={frame}, Entity={entityA}, A={buffA.remainingFrames}, B={buffB.remainingFrames}");
            }
        }

        private static void AssertInputEqual(PlayerInputSnapshotComponent a, PlayerInputSnapshotComponent b, int frame, string stage, Entity entity)
        {
            Expect(a.inputFrame == b.inputFrame, $"{stage} InputFrame Error: Frame={frame}, Entity={entity}, A={a.inputFrame}, B={b.inputFrame}");
            Expect(a.playerID == b.playerID, $"{stage} PlayerID Error: Frame={frame}, Entity={entity}, A={a.playerID}, B={b.playerID}");

            ExpectFloatBits(a.moveX, b.moveX, frame, stage, $"Entity{entity.ID}.Input.MoveX");
            ExpectFloatBits(a.moveY, b.moveY, frame, stage, $"Entity{entity.ID}.Input.MoveY");
            ExpectFloatBits(a.mouseX, b.mouseX, frame, stage, $"Entity{entity.ID}.Input.MouseX");
            ExpectFloatBits(a.mouseY, b.mouseY, frame, stage, $"Entity{entity.ID}.Input.MouseY");
            ExpectFloatBits(a.mouseDeltaX, b.mouseDeltaX, frame, stage, $"Entity{entity.ID}.Input.MouseDeltaX");
            ExpectFloatBits(a.mouseDeltaY, b.mouseDeltaY, frame, stage, $"Entity{entity.ID}.Input.MouseDeltaY");
            ExpectFloatBits(a.scrollX, b.scrollX, frame, stage, $"Entity{entity.ID}.Input.ScrollX");
            ExpectFloatBits(a.scrollY, b.scrollY, frame, stage, $"Entity{entity.ID}.Input.ScrollY");

            Expect(a.pressedButtons == b.pressedButtons,
                $"{stage} PressedButtons Error: Frame={frame}, Entity={entity}, A={a.pressedButtons}, B={b.pressedButtons}");

            Expect(a.heldButtons == b.heldButtons,
                $"{stage} HeldButtons Error: Frame={frame}, Entity={entity}, A={a.heldButtons}, B={b.heldButtons}");

            Expect(a.releasedButtons == b.releasedButtons,
                $"{stage} ReleasedButtons Error: Frame={frame}, Entity={entity}, A={a.releasedButtons}, B={b.releasedButtons}");
        }

        private static void ExpectMovementStateDifferent(TestEnvironment a, TestEnvironment b, int frame, RollbackEvent rollback)
        {
            Expect(a.World.TryGetComponent(a.Player, out PositionComponent positionA),
                $"CombinedStress PreCheck PositionA Error: Frame={frame}");

            Expect(b.World.TryGetComponent(b.Player, out PositionComponent positionB),
                $"CombinedStress PreCheck PositionB Error: Frame={frame}");

            bool different =
                FloatBits(positionA.x) != FloatBits(positionB.x) ||
                FloatBits(positionA.z) != FloatBits(positionB.z);

            Expect(different,
                $"CombinedStress PreCheck Error: Seed={Seed}, RollbackIndex={rollback.Index}, CorrectionFrame={rollback.CorrectionFrame}, ReceiveFrame={rollback.ReceiveFrame}, Depth={rollback.Depth}, Predicted World Did Not Diverge");
        }

        private static uint FrameRandom(int frame, uint salt)
            => NextRandom(Seed ^ unchecked((uint)frame * 0x9E3779B9u) ^ salt);

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

        private static void ExpectFloatBits(float a, float b, int frame, string stage, string field)
        {
            int bitsA = BitConverter.SingleToInt32Bits(a);
            int bitsB = BitConverter.SingleToInt32Bits(b);

            Expect(bitsA == bitsB,
                $"{stage} {field} Error: Frame={frame}, A={a}({bitsA:X8}), B={b}({bitsB:X8})");
        }

        private static int FloatBits(float value) => BitConverter.SingleToInt32Bits(value);

        private static void Expect(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private sealed class PositionDeltaFrameCommand : ISimulationFrameCommand
        {
            private readonly Entity _entity;
            private readonly float _delta;

            public int FrameNumber { get; }

            public PositionDeltaFrameCommand(int frameNumber, Entity entity, float delta)
            {
                FrameNumber = frameNumber;
                _entity = entity;
                _delta = delta;
            }

            public void Execute(World world)
            {
                ref PositionComponent position = ref world.GetComponent<PositionComponent>(_entity);
                position.y += _delta;
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

        private sealed class CombinedScenario
        {
            public bool HealthPlannedPresent;
            public bool BuffPlannedPresent;

            public int PositionCommandCount;
            public int HealthCommandCount;
            public int BuffCommandCount;
            public int CreateCount;
            public int DestroyCount;

            public readonly List<CreateRecord> Creates = new List<CreateRecord>();
        }

        private sealed class CreateRecord
        {
            public readonly int CreateFrame;
            public readonly int DestroyFrame;
            public readonly CreateEntityFrameCommand ReferenceCreate;
            public readonly CreateEntityFrameCommand PredictedCreate;

            public Entity ReferenceEntity;
            public Entity PredictedEntity;

            public bool Finalized;
            public bool DestroyScheduled;

            public CreateRecord(int createFrame, int destroyFrame, CreateEntityFrameCommand referenceCreate, CreateEntityFrameCommand predictedCreate)
            {
                CreateFrame = createFrame;
                DestroyFrame = destroyFrame;
                ReferenceCreate = referenceCreate;
                PredictedCreate = predictedCreate;
                ReferenceEntity = Entity.Invalid;
                PredictedEntity = Entity.Invalid;
            }
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

        private sealed class EntityComparer : IComparer<Entity>
        {
            public static readonly EntityComparer Instance = new EntityComparer();

            public int Compare(Entity x, Entity y)
            {
                int id = x.ID.CompareTo(y.ID);
                return id != 0 ? id : x.Version.CompareTo(y.Version);
            }
        }

        private sealed class TypeComparer : IComparer<Type>
        {
            public static readonly TypeComparer Instance = new TypeComparer();

            public int Compare(Type x, Type y) => string.CompareOrdinal(x?.FullName, y?.FullName);
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

            public TestEnvironment(
                World world,
                Entity player,
                BuffSystemCore buffSystem,
                RollbackCoordinator<PlayerInputSnapshot, EcsWorldSnapshot> coordinator,
                SimulationFrameCommandBuffer commandBuffer,
                SimulationFrameCommandApplier commandApplier,
                SnapshotRingBuffer<EcsWorldSnapshot> snapshotBuffer)
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