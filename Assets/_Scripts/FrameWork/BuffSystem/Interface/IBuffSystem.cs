using ECSFrameWork;
using System.Collections.Generic;

namespace BuffSystem
{
    /// <summary>
    /// ECS Buff 系统运行时接口；Tick 外的 Buff 增删请求会排队到下一次 Buff Tick 消费。
    /// </summary>
    public interface IBuffSystem
    {
        /// <summary>
        /// 推进一帧 Buff 模拟，通常由 ECSBuffSystem 在 World.Tick 中调用。
        /// </summary>
        void Tick(World world, SimulationContext context);

        /// <summary>
        /// 添加或刷新 Buff；Tick 外调用会排队到下一次 Buff Tick。
        /// </summary>
        void AddBuff(AddBuffCommand command);

        /// <summary>
        /// 移除 Buff 层数；Tick 外调用会排队到下一次 Buff Tick。
        /// </summary>
        void RemoveBuff(RemoveBuffCommand command);

        /// <summary>
        /// 抛出 ECS 逻辑事件，只触发 EventTrigger Buff，不负责表现播放。
        /// </summary>
        void Raise<TEvent>(World world, SimulationContext context, in TEvent gameEvent) where TEvent : struct, IGameEvent;

        /// <summary>
        /// 按目标、配置编号和来源读取单个 Buff 视图。
        /// </summary>
        bool TryGetBuff(Entity target, int configId, Entity source, out BuffViewData data);

        /// <summary>
        /// 读取指定目标当前缓存的全部 Buff 视图。
        /// </summary>
        IReadOnlyList<BuffViewData> GetBuffs(Entity target);
    }

    /// <summary>
    /// 添加或刷新 Buff 的请求；运行时数据由 ConfigId 对应的 BuffDefinition 决定。
    /// </summary>
    public readonly struct AddBuffCommand
    {
        public readonly Entity Target;
        public readonly Entity Source;
        public readonly int ConfigId;
        public readonly int Stack;

        public AddBuffCommand(Entity target, int configId, Entity source = default, int stack = 1)
        {
            Target = target;
            Source = source.IsValid ? source : Entity.Invalid;
            ConfigId = configId;
            Stack = stack > 0 ? stack : 1;
        }

        public bool IsValid => Target.IsValid && ConfigId > 0 && Stack > 0;
    }

    /// <summary>
    /// 移除 Buff 层数的请求。
    /// </summary>
    public readonly struct RemoveBuffCommand
    {
        public readonly Entity Target;
        public readonly Entity Source;
        public readonly int ConfigId;
        public readonly int StackCount;
        public readonly bool MatchAnySource;
        public readonly bool ClearAllStacks;

        public RemoveBuffCommand(
            Entity target,
            int configId,
            Entity source = default,
            int stackCount = 1,
            bool matchAnySource = false,
            bool clearAllStacks = false)
        {
            Target = target;
            Source = source.IsValid ? source : Entity.Invalid;
            ConfigId = configId;
            StackCount = stackCount > 0 ? stackCount : 1;
            MatchAnySource = matchAnySource;
            ClearAllStacks = clearAllStacks;
        }

        public bool IsValid => Target.IsValid && ConfigId > 0;
    }
}
