/*
 * 文件说明：WorldViewReader 是表现层读取 ECS 状态的只读适配器，避免 View 层反向修改逻辑世界。
 * 设计约束：ECS Core 逻辑应尽量保持确定性；Unity 表现、输入采样、外部指令通过 Adapter 或 Buffer 接入。
 */

using System.Collections.Generic;

/// <summary>
/// 基于 World 的表现层只读访问器。
/// </summary>
public sealed class WorldViewReader : IWorldViewReader
{
    private readonly World _world;

    public WorldViewReader(World world)
    {
        _world = world;
    }

    /// <summary>尝试读取实体绑定的 ViewID。</summary>
    public bool TryGetViewId(EntityInfo entity, out int viewId)
    {
        viewId = 0;

        if (_world == null || !_world.IsAlive(entity))
            return false;

        if (!_world.TryGetComponent(entity, out ViewComponent view))
            return false;

        viewId = view.viewID;
        return true;
    }

    /// <summary>尝试读取实体位置。</summary>
    public bool TryGetPosition(EntityInfo entity, out PositionComponent position)
    {
        position = default;

        if (_world == null || !_world.IsAlive(entity))
            return false;

        return _world.TryGetComponent(entity, out position);
    }

    /// <summary>尝试读取实体生命值。</summary>
    public bool TryGetHealth(EntityInfo entity, out HealthComponent health)
    {
        health = default;

        if (_world == null || !_world.IsAlive(entity))
            return false;

        return _world.TryGetComponent(entity, out health);
    }

    /// <summary>枚举当前存活实体。</summary>
    public IEnumerable<EntityInfo> GetAliveEntities()
    {
        if (_world == null)
            yield break;

        foreach (EntityInfo entity in _world.GetAliveEntities())
            yield return entity;
    }
}
