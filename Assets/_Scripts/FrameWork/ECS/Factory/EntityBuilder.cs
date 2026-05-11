/*
 * 文件说明：EntityBuilder 提供链式 Entity 创建入口，用于集中创建 Entity 并设置初始组件。
 * 设计约束：Builder 不直接访问 Manager / Store，只通过 World 对外 API 写入组件，以保持生命周期、Buffer 与 ArcheType 规则一致。
 */

using System;

namespace ECSFrameWork
{

/// <summary>
/// Entity 链式创建工具。
/// Builder 会在创建时向 World 申请一个 Entity，或者包裹一个已有 Entity，随后通过 With 写入组件，最后通过 Build 返回该 Entity。
/// </summary>
public sealed class EntityBuilder
{
    private readonly World _world;
    private readonly Entity _entity;
    private bool _isBuilt;

    /// <summary>Builder 所属的 World。</summary>
    public World World => _world;

    /// <summary>Builder 当前正在配置的 Entity。</summary>
    public Entity Entity => _entity;

    /// <summary>是否已经调用过 Build。Build 多次调用会返回同一个 Entity，不会重复创建。</summary>
    public bool IsBuilt => _isBuilt;

    /// <summary>
    /// 创建 EntityBuilder。该构造函数只允许 World 使用，外部应通过 World.CreateEntityBuilder 创建。
    /// </summary>
    internal EntityBuilder(World world)
    {
        _world = world;
        _entity = world != null ? world.CreateEntity() : Entity.Invalid;
        _isBuilt = false;
    }

    /// <summary>
    /// 使用已有 Entity 创建 EntityBuilder。该构造函数用于 EntityPrefab / EntityFactory 覆盖已有实体组件。
    /// </summary>
    internal EntityBuilder(World world, Entity entity)
    {
        _world = world;
        _entity = entity;
        _isBuilt = false;
    }

    /// <summary>
    /// 为当前 Entity 设置组件。若 Entity 无效、World 为空、World 已释放或 Entity 已失效，则该调用会被忽略。
    /// </summary>
    public EntityBuilder With<T>(in T component) where T : struct, IComponentData
    {
        if (_world == null || !_entity.IsValid || !_world.IsAlive(_entity))
            return this;

        _world.SetComponent(_entity, in component);
        return this;
    }

    /// <summary>
    /// 返回当前构建的 Entity。重复调用不会创建新 Entity。
    /// </summary>
    public Entity Build()
    {
        _isBuilt = true;
        return _entity;
    }
}

}
