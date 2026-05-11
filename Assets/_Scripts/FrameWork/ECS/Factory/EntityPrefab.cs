/*
 * 文件说明：EntityPrefab 是 ECS 层实体模板，用于保存一组默认组件并应用到新建或已有 Entity。
 * 设计约束：EntityPrefab 不直接访问 Manager / Store，不直接操作 Unity GameObject，只通过 World.SetComponent 写入组件。
 */

using System;
using System.Collections.Generic;

namespace ECSFrameWork
{

/// <summary>
/// ECS 层实体模板。
/// 用于描述某类实体默认拥有的组件组合，例如单位、子弹、建筑、掉落物等。
/// </summary>
public sealed class EntityPrefab : IEntityPrefab, IEntityPrefabComponentInfo
{
    private readonly string _name;
    private readonly List<IEntityPrefabElement> _elements = new List<IEntityPrefabElement>();
    private readonly Dictionary<Type, int> _elementIndexByType = new Dictionary<Type, int>();

    /// <summary>Prefab 名称，主要用于调试、日志和工厂注册。</summary>
    public string Name => _name;

    /// <summary>Prefab 当前包含的组件模板数量。</summary>
    public int ComponentCount => _elements.Count;

    /// <summary>创建空 EntityPrefab。</summary>
    public EntityPrefab(string name)
    {
        _name = string.IsNullOrEmpty(name) ? "EntityPrefab" : name;
    }

    /// <summary>
    /// 添加或覆盖一个组件模板。
    /// 同类型组件重复写入时，后写入的组件会覆盖前写入的组件。
    /// </summary>
    public EntityPrefab With<T>(in T component) where T : struct, IComponentData
    {
        Type type = typeof(T);
        EntityPrefabComponent<T> element = new EntityPrefabComponent<T>(in component);

        if (_elementIndexByType.TryGetValue(type, out int index))
        {
            _elements[index] = element;
            return this;
        }

        _elementIndexByType.Add(type, _elements.Count);
        _elements.Add(element);
        return this;
    }

    /// <summary>判断模板中是否包含指定组件类型。</summary>
    public bool Has<T>() where T : struct, IComponentData
    {
        return _elementIndexByType.ContainsKey(typeof(T));
    }

    /// <summary>判断模板中是否包含指定组件类型。</summary>
    public bool HasComponent(Type componentType)
    {
        return componentType != null && _elementIndexByType.ContainsKey(componentType);
    }

    /// <summary>移除指定组件模板。</summary>
    public bool Remove<T>() where T : struct, IComponentData
    {
        Type type = typeof(T);

        if (!_elementIndexByType.TryGetValue(type, out int index))
            return false;

        _elements.RemoveAt(index);
        _elementIndexByType.Remove(type);
        RebuildIndexCache(index);
        return true;
    }

    /// <summary>清空当前模板的全部组件模板。</summary>
    public void Clear()
    {
        _elements.Clear();
        _elementIndexByType.Clear();
    }

    /// <summary>把当前模板包含的组件类型填充到外部 List 中。</summary>
    public int FillComponentTypes(List<Type> results)
    {
        if (results == null)
            return 0;

        results.Clear();

        for (int i = 0; i < _elements.Count; i++)
            results.Add(_elements[i].ComponentType);

        return results.Count;
    }

    /// <summary>基于当前模板创建新 Entity，并写入全部模板组件。</summary>
    public Entity Create(World world)
    {
        if (world == null || world.IsDisposing())
            return Entity.Invalid;

        Entity entity = world.CreateEntity();

        if (!entity.IsValid)
            return Entity.Invalid;

        ApplyTo(world, entity);
        return entity;
    }

    /// <summary>把当前模板中的全部组件写入已有 Entity。</summary>
    public void ApplyTo(World world, Entity entity)
    {
        if (world == null || !entity.IsValid || !world.IsAlive(entity))
            return;

        for (int i = 0; i < _elements.Count; i++)
            _elements[i].Apply(world, entity);
    }

    /// <summary>重建被移除位置之后的组件类型索引。</summary>
    private void RebuildIndexCache(int startIndex)
    {
        for (int i = startIndex; i < _elements.Count; i++)
            _elementIndexByType[_elements[i].ComponentType] = i;
    }
}

}
