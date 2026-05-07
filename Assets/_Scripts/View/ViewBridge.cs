using Contracts;
using BuffSystem;
using ECS;

namespace View
{
    /// <summary>
    /// 表现层桥接器空壳（4号实现，后续填充）。
    /// 根据逻辑世界状态同步 GameObject，当前方法均为空。
    /// </summary>
    public sealed class ViewBridge : IViewBridge
    {
        public void Sync(World world, IBuffSystem buffSystem, int frame) { }
        public void SpawnView(EntityHandle entity, int prefabId) { }
        public void DespawnView(EntityHandle entity) { }
        public void PlayEffect(in ViewEffectCommand command) { }
    }
}