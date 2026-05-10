/*
 * 文件说明：ViewSyncSystem 把 ECS PositionComponent 同步到 Unity Transform。Transform 只是表现结果，不是逻辑真值。
 * 设计约束：表现同步不改变 ECS 逻辑状态，因此使用 World.ForEach<T1,T2> 直接遍历组件 Store。
 */

using UnityEngine;

/// <summary>
/// 将 ECS 中的 PositionComponent 同步到 Unity Transform。
/// </summary>
public sealed class ViewSyncSystem : FixedStepSystemBase
{
    private readonly ViewManager _viewManager;
    private readonly EntityComponentAction<PositionComponent, ViewComponent> _viewSyncAction;

    public override SystemTickSequence sequence => SystemTickSequence.view;

    /// <summary>创建 View 同步系统，并缓存 ForEach 回调委托。</summary>
    public ViewSyncSystem(ViewManager viewManager)
    {
        _viewManager = viewManager;
        _viewSyncAction = SyncView;
    }

    /// <summary>把 PositionComponent 写入对应 View Transform。</summary>
    public override void Tick(in SimulationContext context)
    {
        World.ForEach(_viewSyncAction);
    }

    /// <summary>同步单个实体对应的 Unity Transform。</summary>
    private void SyncView(EntityInfo entity, ref PositionComponent position, ref ViewComponent view)
    {
        if (_viewManager != null && _viewManager.TryGetTransform(view.viewID, out Transform target))
            target.position = new Vector3(position.x, position.y, position.z);
    }
}
