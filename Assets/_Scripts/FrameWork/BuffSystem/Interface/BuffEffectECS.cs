using ECSFrameWork;
using System;
using System.Collections.Generic;

namespace BuffSystem
{
    /// <summary>
    /// 传递给 ECS Buff Effect 的只读上下文；Effect 可以通过 World 修改 ECS Component，但不能持有运行时私有状态。
    /// </summary>
    public readonly struct BuffEffectContext
    {
        public readonly World World;
        public readonly SimulationContext SimulationContext;
        public readonly Entity BuffEntity;
        public readonly BuffRuntimeComponent Runtime;
        public readonly BuffDefinition Definition;

        public BuffEffectContext(
            World world,
            in SimulationContext simulationContext,
            Entity buffEntity,
            in BuffRuntimeComponent runtime,
            in BuffDefinition definition)
        {
            World = world;
            SimulationContext = simulationContext;
            BuffEntity = buffEntity;
            Runtime = runtime;
            Definition = definition;
        }
    }

    /// <summary>
    /// 纯 ECS Buff Effect 执行器；需要回滚的状态必须写入 ECS Component。
    /// </summary>
    public interface IBuffEffectExecutor
    {
        void OnApply(in BuffEffectContext context);
        void OnRefresh(in BuffEffectContext context);
        void OnStackChanged(in BuffEffectContext context, int delta);
        void OnTick(in BuffEffectContext context);
        void OnRemove(in BuffEffectContext context);
    }

    /// <summary>
    /// Buff 事件 Effect 的非泛型标记接口，仅用于注册期能力缓存。
    /// </summary>
    public interface IBuffEventEffectExecutor
    {
    }

    /// <summary>
    /// 泛型事件 Effect 接口；事件参数保持 struct 传递，避免 Raise 热路径装箱。
    /// </summary>
    public interface IBuffEventEffectExecutor<TEvent> : IBuffEventEffectExecutor where TEvent : struct, IGameEvent
    {
        /// <summary>
        /// 判断当前 Buff 是否应该响应该事件；该方法必须无副作用。
        /// </summary>
        bool ShouldTrigger(in BuffEffectContext context, in TEvent gameEvent);

        /// <summary>
        /// 旧版 BuffEffect.OnEvent 的 ECS 等价入口；需要修改状态时只写 ECS Component。
        /// </summary>
        void OnEvent(in BuffEffectContext context, in TEvent gameEvent);
    }

    /// <summary>
    /// 可选基类，适合只实现部分 Buff 生命周期的 Effect。
    /// </summary>
    public abstract class BuffEffectExecutorBase : IBuffEffectExecutor
    {
        public virtual void OnApply(in BuffEffectContext context) { }
        public virtual void OnRefresh(in BuffEffectContext context) { }
        public virtual void OnStackChanged(in BuffEffectContext context, int delta) { }
        public virtual void OnTick(in BuffEffectContext context) { }
        public virtual void OnRemove(in BuffEffectContext context) { }
    }

    /// <summary>
    /// Buff Effect 注册表；事件能力在注册阶段扫描并缓存，避免 Raise 热路径反射。
    /// </summary>
    public sealed class BuffEffectRegistry
    {
        private static readonly Type EventEffectInterfaceDefinition = typeof(IBuffEventEffectExecutor<>);

        private readonly Dictionary<int, IBuffEffectExecutor> _effects = new Dictionary<int, IBuffEffectExecutor>();
        private readonly Dictionary<Type, Dictionary<int, IBuffEventEffectExecutor>> _eventEffectsByEventType =
            new Dictionary<Type, Dictionary<int, IBuffEventEffectExecutor>>();

        public int Count => _effects.Count;

        public void Register(int effectId, IBuffEffectExecutor effect)
        {
            if (effectId == 0 || effect == null)
                return;

            _effects[effectId] = effect;
            RemoveEventCapabilities(effectId);
            CacheEventCapabilities(effectId, effect);
        }

        public bool Remove(int effectId)
        {
            RemoveEventCapabilities(effectId);
            return _effects.Remove(effectId);
        }

        public void Clear()
        {
            _effects.Clear();
            _eventEffectsByEventType.Clear();
        }

        public bool TryGet(int effectId, out IBuffEffectExecutor effect)
        {
            if (effectId == 0)
            {
                effect = null;
                return false;
            }

            return _effects.TryGetValue(effectId, out effect);
        }

        public bool TryGetEventEffect<TEvent>(int effectId, out IBuffEventEffectExecutor<TEvent> effect)
            where TEvent : struct, IGameEvent
        {
            effect = null;

            if (effectId == 0)
                return false;

            if (!_eventEffectsByEventType.TryGetValue(typeof(TEvent), out Dictionary<int, IBuffEventEffectExecutor> effects))
                return false;

            if (!effects.TryGetValue(effectId, out IBuffEventEffectExecutor cachedEffect))
                return false;

            effect = cachedEffect as IBuffEventEffectExecutor<TEvent>;
            return effect != null;
        }

        private void CacheEventCapabilities(int effectId, IBuffEffectExecutor effect)
        {
            Type[] interfaces = effect.GetType().GetInterfaces();

            for (int i = 0; i < interfaces.Length; i++)
            {
                Type interfaceType = interfaces[i];

                if (!interfaceType.IsGenericType || interfaceType.GetGenericTypeDefinition() != EventEffectInterfaceDefinition)
                    continue;

                Type eventType = interfaceType.GetGenericArguments()[0];

                if (!_eventEffectsByEventType.TryGetValue(eventType, out Dictionary<int, IBuffEventEffectExecutor> effects))
                {
                    effects = new Dictionary<int, IBuffEventEffectExecutor>();
                    _eventEffectsByEventType.Add(eventType, effects);
                }

                effects[effectId] = (IBuffEventEffectExecutor)effect;
            }
        }

        private void RemoveEventCapabilities(int effectId)
        {
            foreach (Dictionary<int, IBuffEventEffectExecutor> effects in _eventEffectsByEventType.Values)
                effects.Remove(effectId);
        }
    }
}
