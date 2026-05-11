/*
 * 文件说明：ArcheTypeManager 按 ComponentMask256 对 Entity 进行分组，并为 Query 提供匹配的 ArcheType 集合。
 * 设计约束：QueryCache 缓存 Query 条件匹配到的 ArcheType 分组，不缓存具体 Entity 查询结果。
 */

using System.Collections.Generic;

namespace ECSFrameWork
{

/// <summary>
/// ArcheType 分组管理器，按 ComponentMask256 维护 Entity 分组并支持 Query 匹配。
/// </summary>
internal class ArcheTypeManager
{
    private readonly Dictionary<ComponentMask256, ArcheTypeGroup> _archeGroups = new Dictionary<ComponentMask256, ArcheTypeGroup>();
    private readonly EntityQueryCache _queryCache = new EntityQueryCache();

    private int _version;

    public ComponentTypeRegistry register;

    public int ArcheTypeCount => _archeGroups.Count;
    public int ArcheTypeVersion => _version;
    public int QueryCacheCount => _queryCache.Count;

    /// <summary>创建 ArcheType 管理器，并注入组件类型注册表。</summary>
    public ArcheTypeManager(ComponentTypeRegistry register)
    {
        this.register = register;
    }

    /// <summary>枚举当前已经存在的所有 ArcheType Mask。</summary>
    public IEnumerable<ComponentMask256> GetAllArcheTypes()
    {
        foreach (ComponentMask256 mask in _archeGroups.Keys)
            yield return mask;
    }

    /// <summary>
    /// 根据组件 Mask 变化，把实体从旧 ArcheType 分组移动到新分组，并更新缓存版本。
    /// </summary>
    public void ChangeGroup(Entity entity, ComponentMask256 oldMask = default, ComponentMask256 newMask = default)
    {
        if (!entity.IsValid || oldMask == newMask)
            return;

        bool changed = false;

        if (!oldMask.IsEmpty && _archeGroups.TryGetValue(oldMask, out ArcheTypeGroup oldGroup))
        {
            if (oldGroup.Remove(entity))
                changed = true;

            if (oldGroup.Count == 0)
            {
                _archeGroups.Remove(oldMask);
                changed = true;
            }
        }

        if (!newMask.IsEmpty)
        {
            if (!_archeGroups.TryGetValue(newMask, out ArcheTypeGroup newGroup))
            {
                newGroup = new ArcheTypeGroup();
                _archeGroups.Add(newMask, newGroup);
                changed = true;
            }

            if (newGroup.Add(entity))
                changed = true;
        }

        if (changed)
            _version++;
    }

    /// <summary>执行 QueryDescription 查询，并通过 QueryCache 获取匹配的 ArcheType 分组。</summary>
    public IEnumerable<Entity> GetEntityByQuery(EntityQueryDescription query)
    {
        List<ArcheTypeGroup> groups = _queryCache.GetGroups(query, _archeGroups, _version);

        for (int i = 0; i < groups.Count; i++)
        {
            List<Entity> entities = groups[i].Entities;

            for (int j = 0; j < entities.Count; j++)
                yield return entities[j];
        }
    }

    /// <summary>用 includeMask 和 excludeMask 构造 QueryDescription 并执行查询。</summary>
    public IEnumerable<Entity> GetEntityByMask(ComponentMask256 includeMask, ComponentMask256 excludeMask = default)
    {
        return GetEntityByQuery(new EntityQueryDescription(includeMask, excludeMask));
    }

    /// <summary>
    /// 执行 QueryDescription 查询，并把当前匹配到的 Entity 写入 results。
    /// QueryCache 缓存的是匹配的 ArcheType 分组，不缓存具体 Entity 结果。
    /// </summary>
    public int FillEntityByQuery(EntityQueryDescription query, List<Entity> results)
    {
        if (results == null)
            return 0;

        List<ArcheTypeGroup> groups = _queryCache.GetGroups(query, _archeGroups, _version);

        for (int i = 0; i < groups.Count; i++)
        {
            List<Entity> entities = groups[i].Entities;

            for (int j = 0; j < entities.Count; j++)
                results.Add(entities[j]);
        }

        return results.Count;
    }

    /// <summary>清空 Query 缓存。</summary>
    public void ClearQueryCache()
    {
        _queryCache.Clear();
    }
}

}
