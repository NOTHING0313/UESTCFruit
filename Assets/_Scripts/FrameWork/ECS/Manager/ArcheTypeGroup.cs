/*
 * 文件说明：ArcheTypeGroup 封装单个 ArcheType 分组中的 Entity 列表，并提供 O(1) 级别的 Swap Remove。
 * 设计约束：该类只维护分组内 Entity 的顺序无关集合，不负责组件数据和 Entity 生命周期。
 */

using System.Collections.Generic;

/// <summary>
/// 单个 ArcheType 分组，内部使用 List + Dictionary 维护 Entity 与下标映射。
/// </summary>
public sealed class ArcheTypeGroup
{
    private readonly List<EntityInfo> _entities = new List<EntityInfo>();
    private readonly Dictionary<EntityInfo, int> _indices = new Dictionary<EntityInfo, int>();

    /// <summary>当前分组中的 Entity 列表。</summary>
    public List<EntityInfo> Entities => _entities;

    /// <summary>当前分组中的 Entity 数量。</summary>
    public int Count => _entities.Count;

    /// <summary>判断分组中是否已经存在指定 Entity。</summary>
    public bool Contains(EntityInfo entity)
    {
        return entity.IsValid && _indices.ContainsKey(entity);
    }

    /// <summary>
    /// 向分组中添加 Entity。
    /// 如果 Entity 已存在，则不会重复添加。
    /// </summary>
    public bool Add(EntityInfo entity)
    {
        if (!entity.IsValid || _indices.ContainsKey(entity))
            return false;

        _indices.Add(entity, _entities.Count);
        _entities.Add(entity);
        return true;
    }

    /// <summary>
    /// 使用尾元素回填的方式移除 Entity，避免 List.Remove 的线性查找和中间搬移。
    /// </summary>
    public bool Remove(EntityInfo entity)
    {
        if (!entity.IsValid || !_indices.TryGetValue(entity, out int index))
            return false;

        int lastIndex = _entities.Count - 1;
        EntityInfo lastEntity = _entities[lastIndex];

        _entities[index] = lastEntity;
        _indices[lastEntity] = index;

        _entities.RemoveAt(lastIndex);
        _indices.Remove(entity);
        return true;
    }

    /// <summary>清空分组中的 Entity 与下标映射。</summary>
    public void Clear()
    {
        _entities.Clear();
        _indices.Clear();
    }
}
