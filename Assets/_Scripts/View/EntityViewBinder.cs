using System.Collections.Generic;
using Contracts;
using ECSFrameWork;
using UnityEngine;

namespace View
{
    /// <summary>
    /// Entity 与 GameObject 的双向绑定器。
    /// </summary>
    public sealed class EntityViewBinder : IEntityViewBinder
    {
        private readonly Dictionary<Entity, GameObject> _viewByEntity = new Dictionary<Entity, GameObject>();
        private readonly Dictionary<int, Entity> _entityByViewId = new Dictionary<int, Entity>();

        /// <summary>绑定 Entity 与 View，并返回 View 的实例 ID。</summary>
        public int Bind(Entity entity, GameObject view)
        {
            if (!entity.IsValid || view == null)
                return -1;

            int viewId = view.GetInstanceID();
            _viewByEntity[entity] = view;
            _entityByViewId[viewId] = entity;
            return viewId;
        }

        /// <summary>解除指定 Entity 与 View 的绑定。</summary>
        public void Unbind(Entity entity)
        {
            if (!_viewByEntity.TryGetValue(entity, out GameObject view))
                return;

            _viewByEntity.Remove(entity);
            if (view != null)
                _entityByViewId.Remove(view.GetInstanceID());
        }

        /// <summary>尝试通过 Entity 查找对应 View。</summary>
        public bool TryGetView(Entity entity, out GameObject view)
        {
            return _viewByEntity.TryGetValue(entity, out view) && view != null;
        }

        /// <summary>尝试通过 View 实例 ID 查找对应 Entity。</summary>
        public bool TryGetEntity(int viewId, out Entity entity)
        {
            return _entityByViewId.TryGetValue(viewId, out entity);
        }
    }
}
