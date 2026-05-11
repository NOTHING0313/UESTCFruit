using BuffSystem;
using Contracts;
using ECSFrameWork;

namespace View
{
    /// <summary>
    /// 表现层桥接器空壳。
    /// 后续可在这里读取 World 和 BuffSystem，把逻辑状态同步到 Unity GameObject、动画和特效。
    /// </summary>
    public sealed class ViewBridge : IViewBridge
    {
        public void Sync(World world, IBuffSystem buffSystem, int frame) { }
        public void SpawnView(Entity entity, int prefabId) { }
        public void DespawnView(Entity entity) { }
        public void PlayEffect(in ViewEffectCommand command) { }
    }
}
