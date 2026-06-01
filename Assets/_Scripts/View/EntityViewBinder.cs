using Contracts;
using ECSFrameWork;
using System.Collections.Generic;
using UnityEngine;

namespace View
{
    /// <summary>
    /// 实体-视图绑定器，维护 Entity ↔ viewID/GameObject 双向查询。
    /// 不负责生命周期，仅管理映射。
    /// </summary>
    public class EntityViewBinder : IEntityViewBinder
    {
        private readonly Dictionary<Entity, int> _entityToViewId = new();
        private readonly Dictionary<int, Entity> _viewIdToEntity = new();
        private readonly ViewManager _viewManager;

        public EntityViewBinder(ViewManager viewManager)
        {
            _viewManager = viewManager;
        }

        public int Bind(Entity entity, int viewId)
        {
            if (viewId <= 0) return 0;
            Unbind(entity);
            _entityToViewId[entity] = viewId;
            _viewIdToEntity[viewId] = entity;
            return viewId;
        }

        public void Unbind(Entity entity)
        {
            if (_entityToViewId.TryGetValue(entity, out int viewId))
            {
                _entityToViewId.Remove(entity);
                _viewIdToEntity.Remove(viewId);
            }
        }

        public bool TryGetView(Entity entity, out GameObject view)
        {
            view = null;
            if (_entityToViewId.TryGetValue(entity, out int viewId))
            {
                if (_viewManager.TryGetTransform(viewId, out Transform t))
                {
                    view = t.gameObject;
                    return true;
                }
            }
            return false;
        }

        public bool TryGetEntity(int viewId, out Entity entity)
        {
            return _viewIdToEntity.TryGetValue(viewId, out entity);
        }

        public void Clear()
        {
            _entityToViewId.Clear();
            _viewIdToEntity.Clear();
        }
    }
}