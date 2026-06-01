using Contracts;
using ECSFrameWork;

namespace View
{
    /// <summary>
    /// 消费 WorldEvent 并转换为 ViewEffectCommand 发送给 IViewBridge。
    /// 在回滚重模拟期间跳过一次性表现。
    /// </summary>
    public sealed class WorldViewEventConsumer : FixedStepSystemBase
    {
        private readonly IViewBridge _viewBridge;

        public override SystemTickSequence sequence => SystemTickSequence.view + 2;

        public WorldViewEventConsumer(IViewBridge viewBridge)
        {
            _viewBridge = viewBridge;
        }

        public override void Tick(in SimulationContext context)
        {
            if (context.isRollback) return;

            // 消费伤害事件
            var damageEvents = World.GetWorldEvents<DamageWorldEvent>();
            if (damageEvents != null)
            {
                foreach (var e in damageEvents)
                {
                    // 假设受击特效 EffectId = 100 （需提前注册预制体）
                    var cmd = new ViewEffectCommand(100, e.source, e.target, e.frameNumber);
                    _viewBridge.PlayEffect(in cmd);
                }
            }

            // 消费死亡事件
            var deadEvents = World.GetWorldEvents<EntityDeadWorldEvent>();
            if (deadEvents != null)
            {
                foreach (var e in deadEvents)
                {
                    // 假设死亡特效 EffectId = 200
                    var cmd = new ViewEffectCommand(200, e.entity, e.entity, e.frameNumber);
                    _viewBridge.PlayEffect(in cmd);
                }
            }

            // 消费后清理所有事件（避免重复处理）
            World.ClearWorldEvents();
        }
    }
}