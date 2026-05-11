/*
 * 文件说明：ViewSpawnSystem 根据 PrefabViewRequestComponent 创建 Unity View，并把 viewID 写回 Entity。
 * 设计约束：查询条件在 System 创建时缓存，运行时只复用结果 List。
 */

using System.Collections.Generic;
using UnityEngine;

namespace ECSFrameWork
{

/// <summary>
/// 根据 PrefabViewRequestComponent 创建 Unity View 的 System。
/// </summary>
public sealed class ViewSpawnSystem : FixedStepSystemBase
{
    private readonly ViewManager _viewManager;
    private readonly List<Entity> _entities = new List<Entity>(128);

    private EntityQueryDescription _spawnQuery;
    private EntityQueryDescription _redundantRequestQuery;

    public override SystemTickSequence sequence => SystemTickSequence.spawn;

    public ViewSpawnSystem(ViewManager viewManager)
    {
        _viewManager = viewManager;
    }

    /// <summary>System 创建时构建并缓存 View 创建相关查询条件。</summary>
    protected override void OnSystemCreate()
    {
        _spawnQuery = World.Query().With<PositionComponent>().With<PrefabViewRequestComponent>().Without<ViewComponent>().Without<EntityDestroyRequestComponent>().BuildDescription();
        _redundantRequestQuery = World.Query().With<PrefabViewRequestComponent>().With<ViewComponent>().BuildDescription();
    }

    /// <summary>处理 View 创建请求，并清理已经拥有 View 的重复请求。</summary>
    public override void Tick(in SimulationContext context)
    {
        SpawnRequestedViews();
        ClearRedundantRequests();
    }

    /// <summary>为满足条件的实体创建 View，并把 viewID 写回 ViewComponent。</summary>
    private void SpawnRequestedViews()
    {
        World.FillQuery(_spawnQuery, _entities, false);

        for (int i = 0; i < _entities.Count; i++)
        {
            Entity entity = _entities[i];

            ref PositionComponent position = ref World.GetComponent<PositionComponent>(entity);
            ref PrefabViewRequestComponent request = ref World.GetComponent<PrefabViewRequestComponent>(entity);

            Vector3 spawnPosition = new Vector3(position.x, position.y, position.z);
            int viewID = _viewManager != null ? _viewManager.SpawnView(request.prefabID, spawnPosition, Quaternion.identity) : 0;

            if (viewID <= 0)
            {
                Debug.LogWarning($"[ViewSpawnSystem] Failed to spawn view. PrefabID = {request.prefabID}");
                World.RemoveComponent<PrefabViewRequestComponent>(entity);
                continue;
            }

            World.SetComponent(entity, new ViewComponent(viewID));
            World.RemoveComponent<PrefabViewRequestComponent>(entity);
        }
    }

    /// <summary>清理已经拥有 ViewComponent 但仍残留 PrefabViewRequestComponent 的实体。</summary>
    private void ClearRedundantRequests()
    {
        World.FillQuery(_redundantRequestQuery, _entities, false);

        for (int i = 0; i < _entities.Count; i++)
            World.RemoveComponent<PrefabViewRequestComponent>(_entities[i]);
    }

    /// <summary>释放系统持有的临时结果容器。</summary>
    protected override void OnSystemDestroy()
    {
        _entities.Clear();
    }
}

}
