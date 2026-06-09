using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using ECSFrameWork;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace BuffSystem
{
    /// <summary>
    /// EntityPerStack 与 CompressedParallel 的存储性能对比入口。
    /// 该 Runner 只做 Editor 手动测量，不参与生产逻辑，也不要求 Compressed 必须更快。
    /// </summary>
    public sealed class BuffSystemStoragePerformanceRunner : MonoBehaviour
    {
        private const float FixedTickLength = 0.02f;
        private const int TickFrameCount = 300;
        private const int QueryRepeatCount = 10;

        private const int AddBuffId = 9301;
        private const int TickBuffId = 9302;
        private const int RemoveEarliestBuffId = 9307;
        private const int RemoveLatestBuffId = 9308;
        private const int RemoveAllBuffId = 9309;
        private const int QueryBuffId = 9312;
        private const int EventBuffId = 9313;

        private const int AddEffectId = 9801;
        private const int TickEffectId = 9802;
        private const int RemoveEarliestEffectId = 9807;
        private const int RemoveLatestEffectId = 9808;
        private const int RemoveAllEffectId = 9809;
        private const int QueryEffectId = 9812;
        private const int EventEffectId = 9813;
        private const int PerformanceEventId = 9813001;

        private static readonly PerformanceScale[] Scales =
        {
            new PerformanceScale(100, 5),
            new PerformanceScale(100, 50),
            new PerformanceScale(1000, 3)
        };

        [ContextMenu("运行 EntityPerStack vs Compressed 性能验证")]
        public void RunStoragePerformanceValidation()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("========== EntityPerStack vs Compressed 性能验证 ==========");
            builder.AppendLine("说明：PASS 只表示性能测量流程完成，不表示 Compressed 必须全部更快。");
            builder.AppendLine("说明：当 layer 数超过 Compressed 容量时，Runner 会用相同 source 分组分布 layer，保证每个 key 仍满足 eligibility。");

            Warmup();

            for (int i = 0; i < Scales.Length; i++)
            {
                PerformanceScale scale = Scales[i];
                builder.AppendLine();
                builder.Append("---------- 规模：").Append(scale.TargetCount).Append(" targets × 每个 ")
                    .Append(scale.LayersPerTarget).AppendLine(" layers ----------");

                AppendAddCase(builder, scale);
                AppendTickCase(builder, scale);
                AppendRemoveCase(builder, scale, ParallelBuffStackDownPolicy.RemoveEarliest);
                AppendRemoveCase(builder, scale, ParallelBuffStackDownPolicy.RemoveLatest);
                AppendRemoveCase(builder, scale, ParallelBuffStackDownPolicy.ClearAll);
                AppendQueryCase(builder, scale);
            }

            AppendEventCase(builder, raiseCount: 100);
            AppendEventCase(builder, raiseCount: 1000);

            builder.AppendLine("========== EntityPerStack vs Compressed Performance Result: PASS ==========");
            Debug.Log(builder.ToString());
        }

        private static void Warmup()
        {
            PerformanceScale warmupScale = new PerformanceScale(4, 2);
            RunAddMeasurement(StorageKind.EntityPerStack, warmupScale);
            RunAddMeasurement(StorageKind.CompressedParallel, warmupScale);
        }

        private static void AppendAddCase(StringBuilder builder, PerformanceScale scale)
        {
            MetricResult entity = RunAddMeasurement(StorageKind.EntityPerStack, scale);
            MetricResult compressed = RunAddMeasurement(StorageKind.CompressedParallel, scale);
            AppendOperationPair(builder, "AddBuff + Tick 消费", scale, entity, compressed);
        }

        private static void AppendTickCase(StringBuilder builder, PerformanceScale scale)
        {
            MetricResult entity = RunTickMeasurement(StorageKind.EntityPerStack, scale);
            MetricResult compressed = RunTickMeasurement(StorageKind.CompressedParallel, scale);

            builder.AppendLine("[Tick]");
            AppendScaleInfo(builder, scale);
            AppendMetric(builder, "EntityPerStack", entity, useFrameAverage: true);
            AppendMetric(builder, "CompressedParallel", compressed, useFrameAverage: true);
            AppendRatio(builder, entity, compressed);
        }

        private static void AppendRemoveCase(StringBuilder builder, PerformanceScale scale, ParallelBuffStackDownPolicy removePolicy)
        {
            MetricResult entity = RunRemoveMeasurement(StorageKind.EntityPerStack, scale, removePolicy);
            MetricResult compressed = RunRemoveMeasurement(StorageKind.CompressedParallel, scale, removePolicy);
            AppendOperationPair(builder, "Remove " + removePolicy, scale, entity, compressed);
        }

        private static void AppendQueryCase(StringBuilder builder, PerformanceScale scale)
        {
            QueryMetricResult entity = RunQueryMeasurement(StorageKind.EntityPerStack, scale);
            QueryMetricResult compressed = RunQueryMeasurement(StorageKind.CompressedParallel, scale);

            builder.AppendLine("[Query]");
            AppendScaleInfo(builder, scale);
            AppendMetric(builder, "EntityPerStack TryGetBuff", entity.TryGet);
            AppendMetric(builder, "CompressedParallel TryGetBuff", compressed.TryGet);
            AppendRatio(builder, entity.TryGet, compressed.TryGet);
            AppendMetric(builder, "EntityPerStack GetBuffs(target)", entity.GetBuffs);
            AppendMetric(builder, "CompressedParallel GetBuffs(target)", compressed.GetBuffs);
            AppendRatio(builder, entity.GetBuffs, compressed.GetBuffs);
            AppendMetric(builder, "EntityPerStack 大量 target 查询", entity.ManyTargets);
            AppendMetric(builder, "CompressedParallel 大量 target 查询", compressed.ManyTargets);
            AppendRatio(builder, entity.ManyTargets, compressed.ManyTargets);
        }

        private static void AppendEventCase(StringBuilder builder, int raiseCount)
        {
            EventMetricResult entity = RunEventMeasurement(StorageKind.EntityPerStack, raiseCount);
            EventMetricResult compressed = RunEventMeasurement(StorageKind.CompressedParallel, raiseCount);

            builder.AppendLine("[EventTrigger Raise]");
            builder.Append("RaiseCount=").Append(raiseCount)
                .Append(", EventId=").Append(PerformanceEventId)
                .AppendLine(", CompressedParallel 在 EventTrigger 配置下按设计 fallback EntityPerStack");
            AppendMetric(builder, "EntityPerStack Raise", entity.Metric);
            builder.Append("EntityPerStack 触发次数=").Append(entity.TriggerCount).AppendLine();
            AppendMetric(builder, "CompressedParallel Raise", compressed.Metric);
            builder.Append("CompressedParallel 触发次数=").Append(compressed.TriggerCount).AppendLine();
            AppendRatio(builder, entity.Metric, compressed.Metric);
        }

        private static MetricResult RunAddMeasurement(StorageKind kind, PerformanceScale scale)
        {
            PerformanceEnvironment env = CreateEnvironment(kind, AddBuffId, AddEffectId, scale, ParallelBuffStackDownPolicy.RemoveEarliest, BuffTriggerType.Tick);
            int operationCount = scale.TotalLayerCount;

            return Measure(operationCount, () =>
            {
                QueueOneAddCommandPerLayer(env, AddBuffId, scale);
                Tick(env, 1);
            });
        }

        private static MetricResult RunTickMeasurement(StorageKind kind, PerformanceScale scale)
        {
            PerformanceEnvironment env = CreateEnvironment(kind, TickBuffId, TickEffectId, scale, ParallelBuffStackDownPolicy.RemoveEarliest, BuffTriggerType.Tick);
            Prefill(env, TickBuffId, scale);

            return Measure(TickFrameCount, () =>
            {
                for (int i = 0; i < TickFrameCount; i++)
                    Tick(env, 10 + i);
            });
        }

        private static MetricResult RunRemoveMeasurement(StorageKind kind, PerformanceScale scale, ParallelBuffStackDownPolicy removePolicy)
        {
            int configId = GetRemoveConfigId(removePolicy);
            int effectId = GetRemoveEffectId(removePolicy);
            PerformanceEnvironment env = CreateEnvironment(kind, configId, effectId, scale, removePolicy, BuffTriggerType.Tick);
            Prefill(env, configId, scale);
            int operationCount = removePolicy == ParallelBuffStackDownPolicy.ClearAll
                ? scale.TargetCount * env.SourceGroupCount
                : scale.TotalLayerCount;

            return Measure(operationCount, () =>
            {
                QueueRemoveCommands(env, configId, scale, removePolicy);
                Tick(env, 10);
            });
        }

        private static QueryMetricResult RunQueryMeasurement(StorageKind kind, PerformanceScale scale)
        {
            PerformanceEnvironment env = CreateEnvironment(kind, QueryBuffId, QueryEffectId, scale, ParallelBuffStackDownPolicy.RemoveEarliest, BuffTriggerType.Tick);
            Prefill(env, QueryBuffId, scale);

            int tryGetCount = scale.TargetCount * env.SourceGroupCount * QueryRepeatCount;
            MetricResult tryGet = Measure(tryGetCount, () =>
            {
                for (int repeat = 0; repeat < QueryRepeatCount; repeat++)
                {
                    for (int targetIndex = 0; targetIndex < env.Targets.Length; targetIndex++)
                    {
                        for (int sourceIndex = 0; sourceIndex < env.SourceGroupCount; sourceIndex++)
                        {
                            env.BuffSystem.TryGetBuff(env.Targets[targetIndex], QueryBuffId, env.Sources[targetIndex, sourceIndex], out BuffViewData _);
                        }
                    }
                }
            });

            int getBuffsCount = scale.TargetCount * QueryRepeatCount;
            MetricResult getBuffs = Measure(getBuffsCount, () =>
            {
                for (int repeat = 0; repeat < QueryRepeatCount; repeat++)
                {
                    for (int targetIndex = 0; targetIndex < env.Targets.Length; targetIndex++)
                        env.BuffSystem.GetBuffs(env.Targets[targetIndex]);
                }
            });

            MetricResult manyTargets = Measure(getBuffsCount, () =>
            {
                for (int repeat = 0; repeat < QueryRepeatCount; repeat++)
                {
                    for (int targetIndex = 0; targetIndex < env.Targets.Length; targetIndex++)
                    {
                        env.BuffSystem.TryGetBuff(env.Targets[targetIndex], QueryBuffId, env.Sources[targetIndex, 0], out BuffViewData _);
                        env.BuffSystem.GetBuffs(env.Targets[targetIndex]);
                    }
                }
            });

            return new QueryMetricResult(tryGet, getBuffs, manyTargets);
        }

        private static EventMetricResult RunEventMeasurement(StorageKind kind, int raiseCount)
        {
            PerformanceScale scale = new PerformanceScale(100, 1);
            PerformanceEnvironment env = CreateEnvironment(kind, EventBuffId, EventEffectId, scale, ParallelBuffStackDownPolicy.RemoveEarliest, BuffTriggerType.EventTrigger);
            EventCountingEffect eventEffect = (EventCountingEffect)env.Effect;
            Prefill(env, EventBuffId, scale);

            MetricResult metric = Measure(raiseCount, () =>
            {
                for (int i = 0; i < raiseCount; i++)
                {
                    int frameNumber = 20 + i;
                    SimulationContext context = new SimulationContext(frameNumber, FixedTickLength, false);
                    PerformanceProbeEvent probeEvent = new PerformanceProbeEvent(frameNumber, PerformanceEventId);
                    env.BuffSystem.Raise(env.World, context, in probeEvent);
                }
            });

            return new EventMetricResult(metric, eventEffect.TriggerCount);
        }

        private static PerformanceEnvironment CreateEnvironment(
            StorageKind kind,
            int configId,
            int effectId,
            PerformanceScale scale,
            ParallelBuffStackDownPolicy removePolicy,
            BuffTriggerType triggerType)
        {
            World world = new World();
            int sourceGroupCount = CalculateSourceGroupCount(scale.LayersPerTarget);
            Entity[] targets = new Entity[scale.TargetCount];
            Entity[,] sources = new Entity[scale.TargetCount, sourceGroupCount];

            for (int i = 0; i < targets.Length; i++)
            {
                targets[i] = world.CreateEntity();

                for (int sourceIndex = 0; sourceIndex < sourceGroupCount; sourceIndex++)
                    sources[i, sourceIndex] = world.CreateEntity();
            }

            BuffDefinitionRegistry definitions = new BuffDefinitionRegistry();
            BuffEffectRegistry effects = new BuffEffectRegistry();
            BuffEffectExecutorBase effect = triggerType == BuffTriggerType.EventTrigger
                ? (BuffEffectExecutorBase)new EventCountingEffect()
                : new NoOpCountingEffect();

            effects.Register(effectId, effect);
            definitions.Register(CreateDefinition(configId, effectId, scale, removePolicy, triggerType, GetStorageMode(kind)));
            BuffSystemCore buffSystem = kind == StorageKind.CompressedParallel
                ? BuffSystemCore.CreateForCompressedParallelValidation(definitions, effects)
                : new BuffSystemCore(definitions, effects);

            return new PerformanceEnvironment(world, buffSystem, targets, sources, sourceGroupCount, effect);
        }

        private static BuffDefinition CreateDefinition(
            int configId,
            int effectId,
            PerformanceScale scale,
            ParallelBuffStackDownPolicy removePolicy,
            BuffTriggerType triggerType,
            ParallelBuffStorageMode storageMode)
        {
            int maxStack = Math.Min(scale.LayersPerTarget, CompressedParallelBuffLayerBuffer.Capacity);
            int[] eventIds = triggerType == BuffTriggerType.EventTrigger
                ? new[] { PerformanceEventId }
                : null;

            return new BuffDefinition(
                configId,
                "Performance_" + configId,
                0,
                maxStack,
                false,
                false,
                600,
                triggerType == BuffTriggerType.Tick ? 1 : 0,
                0,
                triggerType,
                BuffInstanceType.parallel,
                NormalBuffStackPolicy.AddStackOnly,
                ParallelBuffStackUpPolicy.Append,
                removePolicy,
                effectId,
                eventIds,
                storageMode);
        }

        private static void Prefill(PerformanceEnvironment env, int configId, PerformanceScale scale)
        {
            for (int targetIndex = 0; targetIndex < env.Targets.Length; targetIndex++)
            {
                int remaining = scale.LayersPerTarget;

                for (int sourceIndex = 0; sourceIndex < env.SourceGroupCount; sourceIndex++)
                {
                    int stack = Math.Min(remaining, CompressedParallelBuffLayerBuffer.Capacity);
                    if (stack <= 0)
                        break;

                    env.BuffSystem.AddBuff(new AddBuffCommand(env.Targets[targetIndex], configId, env.Sources[targetIndex, sourceIndex], stack));
                    remaining -= stack;
                }
            }

            Tick(env, 1);
            Tick(env, 2);
        }

        private static void QueueOneAddCommandPerLayer(PerformanceEnvironment env, int configId, PerformanceScale scale)
        {
            for (int targetIndex = 0; targetIndex < env.Targets.Length; targetIndex++)
            {
                for (int layerIndex = 0; layerIndex < scale.LayersPerTarget; layerIndex++)
                {
                    int sourceIndex = layerIndex / CompressedParallelBuffLayerBuffer.Capacity;
                    env.BuffSystem.AddBuff(new AddBuffCommand(env.Targets[targetIndex], configId, env.Sources[targetIndex, sourceIndex], 1));
                }
            }
        }

        private static void QueueRemoveCommands(PerformanceEnvironment env, int configId, PerformanceScale scale, ParallelBuffStackDownPolicy removePolicy)
        {
            for (int targetIndex = 0; targetIndex < env.Targets.Length; targetIndex++)
            {
                int remaining = scale.LayersPerTarget;

                for (int sourceIndex = 0; sourceIndex < env.SourceGroupCount; sourceIndex++)
                {
                    int stack = Math.Min(remaining, CompressedParallelBuffLayerBuffer.Capacity);
                    if (stack <= 0)
                        break;

                    if (removePolicy == ParallelBuffStackDownPolicy.ClearAll)
                    {
                        env.BuffSystem.RemoveBuff(new RemoveBuffCommand(env.Targets[targetIndex], configId, env.Sources[targetIndex, sourceIndex], 1, false, true));
                    }
                    else
                    {
                        for (int i = 0; i < stack; i++)
                            env.BuffSystem.RemoveBuff(new RemoveBuffCommand(env.Targets[targetIndex], configId, env.Sources[targetIndex, sourceIndex], 1));
                    }

                    remaining -= stack;
                }
            }
        }

        private static void Tick(PerformanceEnvironment env, int frameNumber)
        {
            SimulationContext context = new SimulationContext(frameNumber, FixedTickLength, false);
            env.BuffSystem.Tick(env.World, context);
        }

        private static MetricResult Measure(int operationCount, Action action)
        {
            CollectGarbageForMeasurement();
            long beforeBytes = GC.GetAllocatedBytesForCurrentThread();
            Stopwatch stopwatch = Stopwatch.StartNew();
            action();
            stopwatch.Stop();
            long afterBytes = GC.GetAllocatedBytesForCurrentThread();
            return new MetricResult(stopwatch.Elapsed.TotalMilliseconds, operationCount, afterBytes - beforeBytes);
        }

        private static void CollectGarbageForMeasurement()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        private static int CalculateSourceGroupCount(int layersPerTarget)
        {
            int capacity = CompressedParallelBuffLayerBuffer.Capacity;
            return Math.Max(1, (layersPerTarget + capacity - 1) / capacity);
        }

        private static int GetRemoveConfigId(ParallelBuffStackDownPolicy removePolicy)
        {
            if (removePolicy == ParallelBuffStackDownPolicy.RemoveLatest)
                return RemoveLatestBuffId;

            if (removePolicy == ParallelBuffStackDownPolicy.ClearAll)
                return RemoveAllBuffId;

            return RemoveEarliestBuffId;
        }

        private static int GetRemoveEffectId(ParallelBuffStackDownPolicy removePolicy)
        {
            if (removePolicy == ParallelBuffStackDownPolicy.RemoveLatest)
                return RemoveLatestEffectId;

            if (removePolicy == ParallelBuffStackDownPolicy.ClearAll)
                return RemoveAllEffectId;

            return RemoveEarliestEffectId;
        }

        private static ParallelBuffStorageMode GetStorageMode(StorageKind kind)
        {
            return kind == StorageKind.CompressedParallel
                ? ParallelBuffStorageMode.CompressedExpiryFrameList
                : ParallelBuffStorageMode.EntityPerStack;
        }

        private static void AppendOperationPair(StringBuilder builder, string title, PerformanceScale scale, MetricResult entity, MetricResult compressed)
        {
            builder.Append('[').Append(title).AppendLine("]");
            AppendScaleInfo(builder, scale);
            AppendMetric(builder, "EntityPerStack", entity);
            AppendMetric(builder, "CompressedParallel", compressed);
            AppendRatio(builder, entity, compressed);
        }

        private static void AppendScaleInfo(StringBuilder builder, PerformanceScale scale)
        {
            builder.Append("Targets=").Append(scale.TargetCount)
                .Append(", LayersPerTarget=").Append(scale.LayersPerTarget)
                .Append(", TotalLayers=").Append(scale.TotalLayerCount)
                .Append(", SourceGroupsPerTarget=").Append(CalculateSourceGroupCount(scale.LayersPerTarget))
                .AppendLine();
        }

        private static void AppendMetric(StringBuilder builder, string label, MetricResult metric, bool useFrameAverage = false)
        {
            builder.Append(label)
                .Append(": TotalMs=").Append(metric.TotalMs.ToString("F3"))
                .Append(", OperationCount=").Append(metric.OperationCount)
                .Append(", ");

            if (useFrameAverage)
                builder.Append("AvgMsPerFrame=").Append(metric.AvgMsPerOperation.ToString("F6"));
            else
                builder.Append("AvgNsPerOperation=").Append(metric.AvgNsPerOperation.ToString("F1"));

            builder.Append(", GCBytes=").Append(metric.GCBytes).AppendLine();
        }

        private static void AppendRatio(StringBuilder builder, MetricResult entity, MetricResult compressed)
        {
            double ratio = entity.TotalMs > 0.000001d ? compressed.TotalMs / entity.TotalMs : 0d;
            builder.Append("倍率 Compressed / EntityPerStack: ")
                .Append(ratio.ToString("F3"))
                .AppendLine();
        }

        private enum StorageKind
        {
            EntityPerStack,
            CompressedParallel
        }

        private readonly struct PerformanceScale
        {
            public readonly int TargetCount;
            public readonly int LayersPerTarget;

            public int TotalLayerCount => TargetCount * LayersPerTarget;

            public PerformanceScale(int targetCount, int layersPerTarget)
            {
                TargetCount = targetCount;
                LayersPerTarget = layersPerTarget;
            }
        }

        private readonly struct PerformanceEnvironment
        {
            public readonly World World;
            public readonly BuffSystemCore BuffSystem;
            public readonly Entity[] Targets;
            public readonly Entity[,] Sources;
            public readonly int SourceGroupCount;
            public readonly BuffEffectExecutorBase Effect;

            public PerformanceEnvironment(
                World world,
                BuffSystemCore buffSystem,
                Entity[] targets,
                Entity[,] sources,
                int sourceGroupCount,
                BuffEffectExecutorBase effect)
            {
                World = world;
                BuffSystem = buffSystem;
                Targets = targets;
                Sources = sources;
                SourceGroupCount = sourceGroupCount;
                Effect = effect;
            }
        }

        private readonly struct MetricResult
        {
            public readonly double TotalMs;
            public readonly int OperationCount;
            public readonly long GCBytes;

            public double AvgNsPerOperation => OperationCount > 0 ? TotalMs * 1000000d / OperationCount : 0d;
            public double AvgMsPerOperation => OperationCount > 0 ? TotalMs / OperationCount : 0d;

            public MetricResult(double totalMs, int operationCount, long gcBytes)
            {
                TotalMs = totalMs;
                OperationCount = operationCount;
                GCBytes = gcBytes;
            }
        }

        private readonly struct QueryMetricResult
        {
            public readonly MetricResult TryGet;
            public readonly MetricResult GetBuffs;
            public readonly MetricResult ManyTargets;

            public QueryMetricResult(MetricResult tryGet, MetricResult getBuffs, MetricResult manyTargets)
            {
                TryGet = tryGet;
                GetBuffs = getBuffs;
                ManyTargets = manyTargets;
            }
        }

        private readonly struct EventMetricResult
        {
            public readonly MetricResult Metric;
            public readonly int TriggerCount;

            public EventMetricResult(MetricResult metric, int triggerCount)
            {
                Metric = metric;
                TriggerCount = triggerCount;
            }
        }

        private sealed class NoOpCountingEffect : BuffEffectExecutorBase
        {
            public int TickCount { get; private set; }

            public override void OnTick(in BuffEffectContext context)
            {
                TickCount++;
            }
        }

        private sealed class EventCountingEffect : BuffEffectExecutorBase, IBuffEventEffectExecutor<PerformanceProbeEvent>
        {
            public int TriggerCount { get; private set; }

            public bool ShouldTrigger(in BuffEffectContext context, in PerformanceProbeEvent gameEvent)
            {
                return gameEvent.EventId == PerformanceEventId;
            }

            public void OnEvent(in BuffEffectContext context, in PerformanceProbeEvent gameEvent)
            {
                TriggerCount++;
            }
        }

        private readonly struct PerformanceProbeEvent : IGameEvent
        {
            public int FrameNumber { get; }
            public int EventId { get; }

            public PerformanceProbeEvent(int frameNumber, int eventId)
            {
                FrameNumber = frameNumber;
                EventId = eventId;
            }
        }
    }
}
