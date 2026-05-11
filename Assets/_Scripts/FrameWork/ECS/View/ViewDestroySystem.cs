/*
 * 文件说明：ViewDestroySystem 根据 ViewDestroyRequestComponent 销毁或注销 Unity View。
 * 设计约束：ViewManager.DestroyView 在池化场景下表示 Release，而不是直接 Destroy。
 */

using System.Collections.Generic;

namespace ECSFrameWork
{

/// <summary>
/// 根据 ViewDestroyRequestComponent 清理 Unity View 的 System。
/// </summary>
public sealed class ViewDestroySystem : FixedStepSystemBase
{
    private readonly ViewManager _viewManager;
    private readonly List<Entity> _entities = new List<Entity>(128);

    private EntityQueryDescription _viewDestroyQuery;
    private EntityQueryDescription _requestOnlyQuery;

    public override SystemTickSequence sequence => SystemTickSequence.viewCleanup;

    public ViewDestroySystem(ViewManager viewManager)
    {
        _viewManager = viewManager;
    }

    /// <summary>System 创建时构建并缓存 View 销毁相关查询条件。</summary>
    protected override void OnSystemCreate()
    {
        _viewDestroyQuery = World.Query().With<ViewComponent>().With<ViewDestroyRequestComponent>().BuildDescription();
        _requestOnlyQuery = World.Query().With<ViewDestroyRequestComponent>().Without<ViewComponent>().BuildDescription();
    }

    /// <summary>释放带 ViewComponent 的 View，并清理仅残留的销毁请求组件。</summary>
    public override void Tick(in SimulationContext context)
    {
        DestroyRequestedViews();
        ClearRequestOnlyEntities();
    }

    /// <summary>释放实体对应的 View，并移除 View 相关组件。</summary>
    private void DestroyRequestedViews()
    {
        World.FillQuery(_viewDestroyQuery, _entities, false);

        for (int i = 0; i < _entities.Count; i++)
        {
            Entity entity = _entities[i];
            ref ViewComponent view = ref World.GetComponent<ViewComponent>(entity);

            _viewManager?.DestroyView(view.viewID);

            World.RemoveComponent<ViewComponent>(entity);
            World.RemoveComponent<ViewDestroyRequestComponent>(entity);
        }
    }

    /// <summary>移除没有 ViewComponent 的 ViewDestroyRequestComponent。</summary>
    private void ClearRequestOnlyEntities()
    {
        World.FillQuery(_requestOnlyQuery, _entities, false);

        for (int i = 0; i < _entities.Count; i++)
            World.RemoveComponent<ViewDestroyRequestComponent>(_entities[i]);
    }

    /// <summary>释放系统持有的临时结果容器。</summary>
    protected override void OnSystemDestroy()
    {
        _entities.Clear();
    }
}

}
