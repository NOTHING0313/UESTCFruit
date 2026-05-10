/*
 * 文件说明：EntityQueryCache 缓存 Query 与 ArcheType 分组的匹配结果，降低重复查询的 Mask 匹配成本。
 * 设计约束：缓存的是 ArcheTypeGroup 引用，不缓存某次查询得到的 Entity 结果。
 */

using System.Collections.Generic;

/// <summary>
/// Query 到匹配 ArcheType 分组的缓存。
/// </summary>
public sealed class EntityQueryCache
{
    private sealed class CacheItem
    {
        public int version;
        public readonly List<ArcheTypeGroup> groups = new List<ArcheTypeGroup>();
    }

    private readonly Dictionary<EntityQueryDescription, CacheItem> _cache = new Dictionary<EntityQueryDescription, CacheItem>();

    public int Count => _cache.Count;

    /// <summary>
    /// 根据 QueryDescription 获取匹配的 ArcheType 分组；缓存版本过期时重新扫描分组。
    /// </summary>
    public List<ArcheTypeGroup> GetGroups(EntityQueryDescription query, Dictionary<ComponentMask256, ArcheTypeGroup> archeGroups, int version)
    {
        if (_cache.TryGetValue(query, out CacheItem item) && item.version == version)
            return item.groups;

        if (item == null)
        {
            item = new CacheItem();
            _cache.Add(query, item);
        }

        item.version = version;
        item.groups.Clear();

        foreach (KeyValuePair<ComponentMask256, ArcheTypeGroup> pair in archeGroups)
        {
            ComponentMask256 archeMask = pair.Key;

            if (!archeMask.ContainsAll(query.includeMask))
                continue;

            if (query.excludeMask != default && archeMask.ContainsAny(query.excludeMask))
                continue;

            item.groups.Add(pair.Value);
        }

        return item.groups;
    }

    /// <summary>清空所有 Query 缓存项。</summary>
    public void Clear()
    {
        _cache.Clear();
    }
}
