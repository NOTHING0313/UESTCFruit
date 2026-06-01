using System.Collections.Generic;
using ECSFrameWork;

namespace View
{
    /// <summary>
    /// 在 ViewSpawnSystem 之后运行，将新出现的 ViewComponent 绑定到 EntityViewBinder，
    /// 并清理已不存在 ViewComponent 或实体已死亡的绑定。
    /// </summary>
    public sealed class EntityViewBindingSystem : FixedStepSystemBase
    {
        private readonly EntityViewBinder _binder;
        private readonly List<Entity> _entities = new(128);
        private readonly HashSet<Entity> _boundEntities = new();

        private EntityQueryDescription _viewQuery;

        public override SystemTickSequence sequence => SystemTickSequence.spawn + 1;

        public EntityViewBindingSystem(EntityViewBinder binder)
        {
            _binder = binder;
        }

        protected override void OnSystemCreate()
        {
            _viewQuery = World.Query().With<ViewComponent>().BuildDescription();
        }

        public override void Tick(in SimulationContext context)
        {
            World.FillQuery(_viewQuery, _entities, false);

            for (int i = 0; i < _entities.Count; i++)
            {
                Entity entity = _entities[i];
                if (_boundEntities.Contains(entity)) continue;

                if (World.TryGetComponent(entity, out ViewComponent viewComp) && viewComp.viewID > 0)
                {
                    _binder.Bind(entity, viewComp.viewID);
                    _boundEntities.Add(entity);
                }
            }

            // 清理失效的绑定
            RemoveInvalidBindings();
        }

        private void RemoveInvalidBindings()
        {
            var toRemove = new List<Entity>();
            foreach (var entity in _boundEntities)
            {
                if (!World.IsAlive(entity) || !World.HasComponent<ViewComponent>(entity))
                    toRemove.Add(entity);
            }

            foreach (var entity in toRemove)
            {
                _binder.Unbind(entity);
                _boundEntities.Remove(entity);
            }
        }

        protected override void OnSystemDestroy()
        {
            _binder.Clear();
            _boundEntities.Clear();
        }
    }
}