/*
 * 文件说明：EntityDestroySystem 处理 EntityDestroyRequestComponent，并在销毁 Entity 前释放对应 View。
 * 设计约束：View 释放走 ViewManager；Entity 销毁走 World.DestroyEntity，由 World 生命周期决定立即执行或延迟执行。
 */

using System.Collections.Generic;

namespace ECSFrameWork
{

/// <summary>
/// 处理 EntityDestroyRequestComponent 的实体销毁系统。
/// </summary>
public sealed class EntityDestroySystem : FixedStepSystemBase
{
    private readonly ViewManager _viewManager;
    private readonly List<Entity> _entities = new List<Entity>(128);
    private EntityQueryDescription _destroyQuery;

    public override SystemTickSequence sequence => SystemTickSequence.entityCleanup;

    public EntityDestroySystem(ViewManager viewManager)
    {
        _viewManager = viewManager;
    }

    /// <summary>System 创建时构建并缓存销毁请求查询条件。</summary>
    protected override void OnSystemCreate()
    {
        _destroyQuery = World.Query().With<EntityDestroyRequestComponent>().BuildDescription();
    }

    /// <summary>处理实体销毁请求，并释放实体对应的表现对象。</summary>
    public override void Tick(in SimulationContext context)
    {
        World.FillQuery(_destroyQuery, _entities, false);

        for (int i = 0; i < _entities.Count; i++)
        {
            Entity entity = _entities[i];

            if (World.TryGetComponent(entity, out ViewComponent view))
                _viewManager?.DestroyView(view.viewID);

            World.DestroyEntity(entity);
        }
    }

    /// <summary>释放系统持有的临时结果容器。</summary>
    protected override void OnSystemDestroy()
    {
        _entities.Clear();
    }
}

}
