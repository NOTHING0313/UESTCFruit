using ECSFrameWork;       // 依赖 1 号提供的 World, Entity
using BuffSystem;      // 依赖 3 号提供的 IBuffSystem

namespace Contracts
{
    /// <summary>
    /// 表现层桥接接口（4号定义，自身实现）。
    /// 用于触发一次性表现效果、同步 Buff 图标等，不参与逻辑循环。
    /// </summary>
    public interface IViewBridge
    {
        void PlayEffect(in ViewEffectCommand command);
        void SyncBuffUI(Entity target, IBuffSystem buffSystem);
    }
}