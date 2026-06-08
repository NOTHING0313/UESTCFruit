using BuffSystem;
using Contracts;
using ECSFrameWork;
using UnityEngine;

namespace View
{
    /// <summary>
    /// 表现层桥接器，负责播放特效、同步 Buff UI。
    /// 所有表现操作前先校验 Entity 存活及 View 有效性，避免在解绑窗口内播放错误表现。
    /// </summary>
    public class ViewBridge : IViewBridge
    {
        private readonly IEntityViewBinder _binder;
        private readonly ViewManager _viewManager;
        private readonly IBuffSystem _buffSystem;
        private readonly World _world;                     // 用于校验 Entity 存活

        public ViewBridge(IEntityViewBinder binder,
                          ViewManager viewManager,
                          IBuffSystem buffSystem,
                          World world)
        {
            _binder = binder;
            _viewManager = viewManager;
            _buffSystem = buffSystem;
            _world = world;
        }

        public void PlayEffect(in ViewEffectCommand command)
        {
            // 安全校验：目标实体必须存活且仍拥有 ViewComponent
            if (!_world.IsAlive(command.Target) ||
                !_world.HasComponent<ViewComponent>(command.Target))
                return;

            if (_binder.TryGetView(command.Target, out GameObject targetView))
            {
                int viewId = _viewManager.SpawnView(command.EffectId,
                    targetView.transform.position, Quaternion.identity);
                if (viewId <= 0)
                    Debug.LogWarning($"[ViewBridge] Failed to spawn effect, prefabID={command.EffectId}");
            }
        }

        public void SyncBuffUI(Entity target, IBuffSystem buffSystem)
        {
            if (!_world.IsAlive(target))
                return;

            var buffs = buffSystem.GetBuffs(target);
            // TODO: 将 buffs 渲染到目标头顶的 Buff 图标列表 (Milestone V1)
            // 当前仅打印调试信息
            Debug.Log($"[ViewBridge] SyncBuffUI for Entity {target.ID}: {buffs.Count} buff(s)");
        }
    }
}