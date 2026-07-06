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
