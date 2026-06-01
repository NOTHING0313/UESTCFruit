using System;
using System.Collections.Generic;

namespace BuffSystem
{
    /// <summary>
    /// ECS Buff 运行时纯数据定义；Unity 配置资产必须先转换成该结构再进入模拟。
    /// </summary>
    public readonly struct BuffDefinition
    {
        public readonly int ConfigId;
        public readonly string Name;
        public readonly int Priority;
        public readonly int MaxStack;
        public readonly bool Unlimited;
        public readonly bool IsForever;
        public readonly int DurationFrames;
        public readonly int TickIntervalFrames;
        public readonly int DurationExtendFramesPerStack;
        public readonly BuffTriggerType TriggerType;
        public readonly BuffInstanceType BuffType;
        public readonly NormalBuffStackPolicy NormalStackPolicy;
        public readonly ParallelBuffStackUpPolicy ParallelStackUpPolicy;
        public readonly ParallelBuffStackDownPolicy ParallelStackDownPolicy;
        public readonly ParallelBuffStorageMode ParallelStorageMode;
        public readonly int EffectId;
        public readonly int[] EventIds;

        public BuffDefinition(
            int configId,
            string name,
            int priority,
            int maxStack,
            bool unlimited,
            bool isForever,
            int durationFrames,
            int tickIntervalFrames,
            int durationExtendFramesPerStack,
            BuffTriggerType triggerType,
            BuffInstanceType buffType,
            NormalBuffStackPolicy normalStackPolicy,
            ParallelBuffStackUpPolicy parallelStackUpPolicy,
            ParallelBuffStackDownPolicy parallelStackDownPolicy,
            int effectId,
            int[] eventIds = null,
            ParallelBuffStorageMode parallelStorageMode = ParallelBuffStorageMode.EntityPerStack)
        {
            ConfigId = configId;
            Name = name ?? string.Empty;
            Priority = priority;
            MaxStack = maxStack > 0 ? maxStack : 1;
            Unlimited = unlimited;
            IsForever = isForever;
            DurationFrames = isForever ? 0 : Math.Max(1, durationFrames);
            TickIntervalFrames = Math.Max(0, tickIntervalFrames);
            DurationExtendFramesPerStack = Math.Max(0, durationExtendFramesPerStack);
            TriggerType = triggerType;
            BuffType = buffType;
            NormalStackPolicy = normalStackPolicy;
            ParallelStackUpPolicy = parallelStackUpPolicy;
            ParallelStackDownPolicy = parallelStackDownPolicy;
            ParallelStorageMode = parallelStorageMode;
            EffectId = effectId;
            EventIds = CopyEventIds(eventIds);
        }

        /// <summary>
        /// 判断该 Buff 定义是否允许响应指定事件编号；非事件触发 Buff 永远返回 false。
        /// </summary>
        public bool CanRespondToEvent(int eventId)
        {
            if (TriggerType != BuffTriggerType.EventTrigger || EventIds == null || EventIds.Length == 0)
                return false;

            for (int i = 0; i < EventIds.Length; i++)
            {
                if (EventIds[i] == eventId)
                    return true;
            }

            return false;
        }

        private static int[] CopyEventIds(int[] eventIds)
        {
            if (eventIds == null || eventIds.Length == 0)
                return Array.Empty<int>();

            int[] copy = new int[eventIds.Length];
            Array.Copy(eventIds, copy, eventIds.Length);
            return copy;
        }
    }

    /// <summary>
    /// 向 BuffSystemCore 提供纯运行时 Buff 定义。
    /// </summary>
    public interface IBuffDefinitionProvider
    {
        bool TryGetDefinition(int configId, out BuffDefinition definition);
    }

    /// <summary>
    /// 确定性的内存定义表，适合本地模式和测试场景。
    /// </summary>
    public sealed class BuffDefinitionRegistry : IBuffDefinitionProvider
    {
        private readonly Dictionary<int, BuffDefinition> _definitions = new Dictionary<int, BuffDefinition>();

        public int Count => _definitions.Count;

        public void Register(in BuffDefinition definition)
        {
            if (definition.ConfigId <= 0)
                return;

            _definitions[definition.ConfigId] = definition;
        }

        public bool Remove(int configId)
        {
            return _definitions.Remove(configId);
        }

        public void Clear()
        {
            _definitions.Clear();
        }

        public bool TryGetDefinition(int configId, out BuffDefinition definition)
        {
            return _definitions.TryGetValue(configId, out definition);
        }
    }
}
