/*
 * 文件说明：WorldBuffTargetResolver 是 Buff 系统访问 World 数据的受限适配器，避免 Buff 直接持有完整 World 权限。
 * 设计约束：ECS Core 逻辑应尽量保持确定性；Unity 表现、输入采样、外部指令通过 Adapter 或 Buffer 接入。
 */

/// <summary>
/// 基于 World 的 Buff 目标数据访问器。
/// </summary>
public sealed class WorldBuffTargetResolver : IBuffTargetResolver
{
    private readonly World _world;

    public WorldBuffTargetResolver(World world)
    {
        _world = world;
    }

    /// <summary>判断目标 Entity 是否仍然存活。</summary>
    public bool IsAlive(EntityInfo entity)
    {
        return _world != null && _world.IsAlive(entity);
    }

    /// <summary>判断目标 Entity 是否拥有生命值组件。</summary>
    public bool HasHealth(EntityInfo entity)
    {
        return _world != null && _world.HasComponent<HealthComponent>(entity);
    }

    /// <summary>获取目标 Entity 的生命值组件引用。</summary>
    public ref HealthComponent GetHealth(EntityInfo entity)
    {
        return ref _world.GetComponent<HealthComponent>(entity);
    }

    /// <summary>判断目标 Entity 是否拥有位置组件。</summary>
    public bool HasPosition(EntityInfo entity)
    {
        return _world != null && _world.HasComponent<PositionComponent>(entity);
    }

    /// <summary>获取目标 Entity 的位置组件引用。</summary>
    public ref PositionComponent GetPosition(EntityInfo entity)
    {
        return ref _world.GetComponent<PositionComponent>(entity);
    }

    /// <summary>判断目标 Entity 是否拥有属性组件。</summary>
    public bool HasStat(EntityInfo entity)
    {
        return _world != null && _world.HasComponent<StatComponent>(entity);
    }

    /// <summary>获取目标 Entity 的属性组件引用。</summary>
    public ref StatComponent GetStat(EntityInfo entity)
    {
        return ref _world.GetComponent<StatComponent>(entity);
    }
}
