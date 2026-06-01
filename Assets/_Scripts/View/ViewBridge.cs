using BuffSystem;
using Contracts;
using ECSFrameWork;

using UnityEngine;

namespace View
{
    public class ViewBridge : IViewBridge
    {
        private readonly IEntityViewBinder _binder;
        private readonly ViewManager _viewManager;
        private readonly IBuffSystem _buffSystem;

        public ViewBridge(IEntityViewBinder binder, ViewManager viewManager, IBuffSystem buffSystem)
        {
            _binder = binder;
            _viewManager = viewManager;
            _buffSystem = buffSystem;
        }

        public void PlayEffect(in ViewEffectCommand command)
        {
            if (_binder.TryGetView(command.Target, out GameObject targetView))
            {
                _viewManager.SpawnView(command.EffectId, targetView.transform.position, Quaternion.identity);
            }
        }

        public void SyncBuffUI(Entity target, IBuffSystem buffSystem)
        {
            var buffs = buffSystem.GetBuffs(target);
            // TODO: 更新目标实体头顶的 Buff 图标
        }
    }
}