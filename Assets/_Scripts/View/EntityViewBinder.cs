using Contracts;
using ECSFrameWork;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace View
{
    public sealed class EntityViewBinder : IEntityViewBinder
    {
        private readonly Dictionary<Entity, int> _entityToViewId = new Dictionary<Entity, int>();
        private readonly Dictionary<int, Entity> _viewIdToEntity = new Dictionary<int, Entity>();
        private readonly ViewManager _viewManager;
        private readonly Func<Entity, bool> _isEntityAlive;

        public EntityViewBinder(ViewManager viewManager, Func<Entity, bool> isEntityAlive = null)
        {
            _viewManager = viewManager;
            _isEntityAlive = isEntityAlive;
        }

        public int Bind(Entity entity, int viewId)
        {
            if (!entity.IsValid || viewId <= 0)
                return 0;

            Unbind(entity);
            UnbindViewId(viewId);

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

        public void UnbindViewId(int viewId)
        {
            if (_viewIdToEntity.TryGetValue(viewId, out Entity entity))
            {
                _viewIdToEntity.Remove(viewId);
                _entityToViewId.Remove(entity);
            }
        }

        public bool TryGetView(Entity entity, out GameObject view)
        {
            view = null;

            if (!entity.IsValid)
                return false;

            if (_isEntityAlive != null && !_isEntityAlive(entity))
            {
                Unbind(entity);
                return false;
            }

            if (!_entityToViewId.TryGetValue(entity, out int viewId))
                return false;

            if (_viewManager == null || !_viewManager.TryGetTransform(viewId, out Transform transform) || transform == null)
            {
                Unbind(entity);
                return false;
            }

            view = transform.gameObject;
            return true;
        }

        /// <summary>把当前 Entity -> ViewID 绑定复制到外部列表，供 Rollback 后表现层对账。</summary>
        public int FillBindings(List<KeyValuePair<Entity,int>> results)
        {
            if (results == null)
                return 0;

            results.Clear();

            foreach (KeyValuePair<Entity,int> pair in _entityToViewId)
                results.Add(pair);

            return results.Count;
        }

        public bool TryGetEntity(int viewId, out Entity entity)
        {
            if (!_viewIdToEntity.TryGetValue(viewId, out entity))
                return false;

            if (_isEntityAlive != null && !_isEntityAlive(entity))
            {
                UnbindViewId(viewId);
                entity = Entity.Invalid;
                return false;
            }

            return true;
        }

        public void Clear()
        {
            _entityToViewId.Clear();
            _viewIdToEntity.Clear();
        }
    }
}
