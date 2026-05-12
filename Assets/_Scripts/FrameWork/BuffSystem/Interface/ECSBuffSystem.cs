using ECSFrameWork;
using System;
using System.Collections.Generic;

namespace BuffSystem
{
    /// <summary>
    /// BuffSystemCore 的 ECS System 适配器；注册到 World 后随固定帧推进。
    /// </summary>
    public sealed class ECSBuffSystem : FixedStepSystemBase, IBuffSystem
    {
        private readonly IBuffSystem _core;

        public IBuffSystem Core => _core;

        public override SystemTickSequence sequence => SystemTickSequence.logic;

        public ECSBuffSystem() : this(new BuffSystemCore())
        {
        }

        public ECSBuffSystem(IBuffDefinitionProvider definitionProvider, BuffEffectRegistry effectRegistry = null)
            : this(new BuffSystemCore(definitionProvider, effectRegistry))
        {
        }

        public ECSBuffSystem(IBuffSystem core)
        {
            _core = core ?? new BuffSystemCore();
        }

        public override void Tick(in SimulationContext context)
        {
            _core.Tick(World, context);
        }

        public void Tick(World world, SimulationContext context)
        {
            _core.Tick(world, context);
        }

        public void AddBuff(AddBuffCommand command)
        {
            _core.AddBuff(command);
        }

        public void RemoveBuff(RemoveBuffCommand command)
        {
            _core.RemoveBuff(command);
        }

        public void Raise<TEvent>(World world, SimulationContext context, in TEvent gameEvent) where TEvent : struct, IGameEvent
        {
            _core.Raise(world, context, in gameEvent);
        }

        public bool TryGetBuff(Entity target, int configId, Entity source, out BuffViewData data)
        {
            return _core.TryGetBuff(target, configId, source, out data);
        }

        public IReadOnlyList<BuffViewData> GetBuffs(Entity target)
        {
            return _core.GetBuffs(target);
        }

        protected override void OnSystemDestroy()
        {
            if (_core is IDisposable disposable)
                disposable.Dispose();
        }
    }
}
