using ECSFrameWork;       // 依赖 1 号提供的 World, Entity
using BuffSystem;      // 依赖 3 号提供的 IBuffSystem

namespace Contracts
{
    /// <summary>
    /// 表现层同步接口（4号提供，自身使用）。
    /// 逻辑帧结束后由 Bootstrap 调用，从逻辑世界同步数据到 Unity 表现，单向只读。
    /// </summary>
    public interface IViewBridge
    {
        void Sync(World world, IBuffSystem buffSystem, int frame);
        void SpawnView(Entity entity, int prefabId);
        void DespawnView(Entity entity);
        void PlayEffect(in ViewEffectCommand command);
    }
}