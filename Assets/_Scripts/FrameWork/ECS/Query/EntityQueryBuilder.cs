/*
 * 文件说明：EntityQueryBuilder 提供链式 Query API，负责把 With / Without 条件转换为 Mask 查询。
 * 设计约束：Query 缓存的是查询条件匹配到的 ArcheType 分组，不缓存某次查询得到的 Entity 结果。
 */

using System.Collections.Generic;

/// <summary>
/// 链式 Entity 查询构建器。
/// 它只负责构建 Query 条件，并在执行时通过 World 访问当前最新的 ArcheType 分组。
/// </summary>
public sealed class EntityQueryBuilder
{
    private readonly World _world;

    private ComponentMask256 _includeMask;
    private ComponentMask256 _excludeMask;

    private bool _hasDescription;
    private EntityQueryDescription _description;

    /// <summary>创建 Query 构建器，并绑定执行查询的 World。</summary>
    internal EntityQueryBuilder(World world)
    {
        _world = world;
        _includeMask = default;
        _excludeMask = default;
    }

    /// <summary>要求查询结果必须包含指定组件。</summary>
    public EntityQueryBuilder With<T>() where T : struct, IComponentData
    {
        ComponentMask256 mask = _world.CreateMask<T>();
        _includeMask.Merge(mask);
        _hasDescription = false;
        return this;
    }

    /// <summary>要求查询结果必须不包含指定组件。</summary>
    public EntityQueryBuilder Without<T>() where T : struct, IComponentData
    {
        ComponentMask256 mask = _world.CreateMask<T>();
        _excludeMask.Merge(mask);
        _hasDescription = false;
        return this;
    }

    /// <summary>构建 QueryDescription。System 可以缓存该对象，避免每帧重复构造查询条件。</summary>
    public EntityQueryDescription BuildDescription()
    {
        if (_hasDescription)
            return _description;

        _description = new EntityQueryDescription(_includeMask, _excludeMask);
        _hasDescription = true;
        return _description;
    }

    /// <summary>执行查询，返回未排序的当前结果快照。</summary>
    public List<EntityInfo> Execute()
    {
        return Execute(false);
    }

    /// <summary>执行查询，返回按 Entity ID / Version 排序后的稳定结果快照。</summary>
    public List<EntityInfo> ExecuteSorted()
    {
        return Execute(true);
    }

    /// <summary>执行查询，并通过 sorted 参数决定是否对结果进行稳定排序。</summary>
    public List<EntityInfo> Execute(bool sorted)
    {
        List<EntityInfo> results = new List<EntityInfo>();
        Fill(results, sorted);
        return results;
    }

    /// <summary>
    /// 兼容旧测试和旧调用习惯的快照查询入口。
    /// 新代码优先使用 Execute / ExecuteSorted / Fill。
    /// </summary>
    public List<EntityInfo> ToList(bool sorted = false)
    {
        return Execute(sorted);
    }

    /// <summary>无额外结果 List 分配地执行查询，把结果写入外部传入的 results。</summary>
    public int Fill(List<EntityInfo> results, bool sorted = false)
    {
        if (results == null)
            return 0;

        EntityQueryDescription query = BuildDescription();
        return _world.FillQuery(query, results, sorted);
    }
}
