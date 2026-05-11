/*
 * 文件说明：IEntityPrefabElement 是 EntityPrefab 内部组件写入单元，用于隐藏具体组件泛型类型。
 */

using System;

namespace ECSFrameWork
{

/// <summary>
/// EntityPrefab 内部组件写入单元。
/// 外部不应直接依赖该接口，应通过 EntityPrefab.With 配置组件模板。
/// </summary>
internal interface IEntityPrefabElement
{
    /// <summary>该模板元素对应的组件类型。</summary>
    Type ComponentType { get; }

    /// <summary>把该组件模板写入指定 Entity。</summary>
    void Apply(World world, Entity entity);
}

/// <summary>
/// EntityPrefab 内部泛型组件写入器。
/// </summary>
internal readonly struct EntityPrefabComponent<T> : IEntityPrefabElement where T : struct, IComponentData
{
    private readonly T _component;

    /// <summary>组件类型。</summary>
    public Type ComponentType => typeof(T);

    /// <summary>缓存一份组件默认值。</summary>
    public EntityPrefabComponent(in T component)
    {
        _component = component;
    }

    /// <summary>把缓存的组件默认值写入目标 Entity。</summary>
    public void Apply(World world, Entity entity)
    {
        if (world == null || !entity.IsValid || !world.IsAlive(entity))
            return;

        world.SetComponent(entity, in _component);
    }
}

}
