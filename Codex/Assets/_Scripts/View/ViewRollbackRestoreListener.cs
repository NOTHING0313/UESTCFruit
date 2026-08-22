using ECSFrameWork;
using FrameWork.RollBackSystem;
using System.Collections.Generic;
using UnityEngine;

namespace View
{
    /// <summary>
    /// Rollback 恢复后对账 ECS 与 Unity View。
    /// GameObject / Transform / viewID 不进入 Snapshot；仅使用稳定 ViewPrefabComponent 决定是否需要重建表现。
    /// </summary>
    public sealed class ViewRollbackRestoreListener : IRollbackRestoreListener
    {
        private readonly EntityViewBinder _binder;
        private readonly ViewManager _viewManager;
        private readonly List<KeyValuePair<Entity,int>> _bindings=new(128);
        private readonly List<Entity> _missingViews=new(128);

        public ViewRollbackRestoreListener(EntityViewBinder binder,ViewManager viewManager)
        {
            _binder=binder;
            _viewManager=viewManager;
        }

        public void OnRollbackWorldRestored(World world,int restoredFrame)
        {
            if(world==null||_binder==null||_viewManager==null) return;

            ReconcileExistingBindings(world,restoredFrame);
            RequestMissingViews(world);
        }

        public void OnRollbackResimulated(World world,int currentFrame)
        {
        }

        private void ReconcileExistingBindings(World world,int restoredFrame)
        {
            _binder.FillBindings(_bindings);

            for(int i=0;i<_bindings.Count;i++)
            {
                Entity entity=_bindings[i].Key;
                int viewID=_bindings[i].Value;

                // Entity 是 Snapshot 之后才创建的：逻辑回滚后已不存在，旧 View 必须立即 Release。
                if(!world.IsAlive(entity))
                {
                    _binder.Unbind(entity);
                    _viewManager.DestroyView(viewID);
                    continue;
                }

                // View 仍然存在：恢复瞬时 ViewComponent，Resimulate 继续复用原 GameObject。
                if(_viewManager.TryGetTransform(viewID,out Transform transform)&&transform!=null)
                {
                    world.SetComponent(entity,new ViewComponent(viewID));
                    continue;
                }

                // Entity 被 Snapshot 复活，但预测路径已经 Release 了 View。
                // 清理 stale binding，后续由稳定 prefabID 重新产生一次性 Spawn Request。
                _binder.Unbind(entity);

                if(world.TryGetComponent(entity,out ViewPrefabComponent prefab)&&prefab.prefabID>0)
                    world.SetComponent(entity,new PrefabViewRequestComponent(prefab.prefabID));
                else
                    Debug.LogWarning(
                        $"ViewRollbackRestoreListener ReconcileExistingBindings Warning: Missing View Prefab Descriptor, Entity={entity}, ViewID={viewID}, RestoredFrame={restoredFrame}");
            }

            _bindings.Clear();
        }

        private void RequestMissingViews(World world)
        {
            EntityQueryDescription query=world.Query()
                .With<PositionComponent>()
                .With<ViewPrefabComponent>()
                .Without<ViewComponent>()
                .Without<PrefabViewRequestComponent>()
                .Without<EntityDestroyRequestComponent>()
                .BuildDescription();

            world.FillQuery(query,_missingViews,false);

            for(int i=0;i<_missingViews.Count;i++)
            {
                Entity entity=_missingViews[i];

                if(!world.TryGetComponent(entity,out ViewPrefabComponent prefab)||prefab.prefabID<=0)
                    continue;

                world.SetComponent(entity,new PrefabViewRequestComponent(prefab.prefabID));
            }

            _missingViews.Clear();
        }
    }
}
