using Contracts;
using ECSFrameWork;

namespace View
{
    public sealed class WorldViewEventConsumer : FixedStepSystemBase
    {
        private readonly IViewBridge _viewBridge;
        private readonly ViewEffectIdConfig _effectIds;

        public override SystemTickSequence sequence => SystemTickSequence.view + 2;

        public WorldViewEventConsumer(IViewBridge viewBridge, ViewEffectIdConfig effectIds = null)
        {
            _viewBridge = viewBridge;
            _effectIds = effectIds;
        }

        public override void Tick(in SimulationContext context)
        {
            if (context.isRollback)
            {
                // Resimulate 会重新产生历史逻辑事件，但历史表现不能再次播放，
                // 同时也不能把事件残留到下一正常帧，否则会发生重复特效/音效。
                World.ClearWorldEvents();
                return;
            }

            var damageEvents = World.GetWorldEvents<DamageWorldEvent>();
            if (damageEvents != null)
            {
                foreach (var e in damageEvents)
                {
                    int effectId = _effectIds != null ? _effectIds.DamageEffectId : 100;
                    var command = new ViewEffectCommand(effectId, e.source, e.target, e.frameNumber);
                    _viewBridge.PlayEffect(in command);
                }
            }

            var deadEvents = World.GetWorldEvents<EntityDeadWorldEvent>();
            if (deadEvents != null)
            {
                foreach (var e in deadEvents)
                {
                    int effectId = _effectIds != null ? _effectIds.DeadEffectId : 200;
                    var command = new ViewEffectCommand(effectId, e.entity, e.entity, e.frameNumber);
                    _viewBridge.PlayEffect(in command);
                }
            }

            World.ClearWorldEvents();
        }
    }
}