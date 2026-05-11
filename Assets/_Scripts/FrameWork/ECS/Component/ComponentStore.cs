/*
 * 文件说明：ComponentStore<T> 使用 Sparse Set 存储单一组件类型的数据，提供 O(1) 查找和连续数组遍历能力。
 * 设计约束：ECS Core 逻辑应尽量保持确定性；Unity 表现、输入采样、外部指令通过 Adapter 或 Buffer 接入。
 */

using System;

namespace ECSFrameWork
{

/// <summary>
/// 单组件类型的 Sparse Set 存储。
/// </summary>
internal class ComponentStore<T> : IComponentStore where T : struct, IComponentData
{
    private Entity[] _denseEntity = Array.Empty<Entity>();
    private T[] _denseComponent = Array.Empty<T>();
    private int[] _sparse = Array.Empty<int>();

    private int _count;
    private readonly int _registerID;
    private readonly EntityManager _entityManager;
    public Type ComponentType => typeof(T);
    public int RegisterID => _registerID;
    public int Count => _count;
    public int Capacity => _denseComponent.Length;
    public int SparseCapacity => _sparse.Length;

    /// <summary>
    /// 创建指定组件类型的稀疏集合 Store，并记录组件注册 ID 与 EntityManager。
    /// </summary>
    public ComponentStore(int registerID, EntityManager manager)
    {
        _registerID = registerID;
        _entityManager = manager;
    }

    /// <summary>
    /// 确保 sparse 和 dense 数组容量足够。
    /// sparseCapacity 对应可索引的 Entity ID 范围，denseCapacity 对应当前组件数量容量。
    /// </summary>
    public void EnsureCapacity(int sparseCapacity, int denseCapacity)
    {
        if (sparseCapacity > 0)
        {
            int oldSparseLength = _sparse.Length;
            ToolFunction.EnsureArrayLength(ref _sparse, sparseCapacity);

            for (int i = oldSparseLength; i < _sparse.Length; i++)
                _sparse[i] = -1;
        }

        if (denseCapacity > 0)
            EnsureDenseCapacity(denseCapacity);
    }

    /// <summary>
    /// 写入组件数据；如果是新增组件则返回 true，如果只是覆盖已有数据则返回 false。
    /// </summary>
    public bool Set(Entity entity, in T component)
    {
        if (!entity.IsValid)
            return false;

        if (_entityManager != null && !_entityManager.IsAlive(entity))
            return false;

        EnsureSparseCapacity(entity.ID);

        int denseIndex = GetDenseIndex(entity);

        if (denseIndex >= 0)
        {
            _denseComponent[denseIndex] = component;
            return false;
        }

        EnsureDenseCapacity(_count + 1);

        _denseEntity[_count] = entity;
        _denseComponent[_count] = component;
        _sparse[entity.ID] = _count;

        _count++;
        return true;
    }
    /// <summary>
    /// 返回实体对应组件数据的 ref 引用；实体没有该组件时抛出异常。
    /// </summary>
    public ref T Get(Entity entity)
    {
        int denseIndex = GetDenseIndex(entity);

        if (denseIndex < 0)
            throw new InvalidOperationException($"Entity does not have component: {typeof(T).Name}");

        return ref _denseComponent[denseIndex];
    }

    /// <summary>
    /// 尝试读取实体对应的组件数据，失败时返回 false 并输出 default。
    /// </summary>
    public bool TryGet(Entity entity, out T component)
    {
        int denseIndex = GetDenseIndex(entity);

        if (denseIndex < 0)
        {
            component = default;
            return false;
        }

        component = _denseComponent[denseIndex];
        return true;
    }

    /// <summary>
    /// 尝试获取实体在 dense 数组中的下标。
    /// 该方法会校验 Entity ID 与 Version，用于高频 ForEach 遍历时避免重复组件查找。
    /// </summary>
    public bool TryGetDenseIndex(Entity entity, out int denseIndex)
    {
        denseIndex = GetDenseIndex(entity);
        return denseIndex >= 0;
    }

    /// <summary>
    /// 获取 dense 数组指定下标对应的 Entity。
    /// 调用方必须保证 denseIndex 在 [0, Count) 范围内。
    /// </summary>
    public Entity GetEntityByDenseIndex(int denseIndex)
    {
        if (denseIndex < 0 || denseIndex >= _count)
            throw new ArgumentOutOfRangeException(nameof(denseIndex));

        return _denseEntity[denseIndex];
    }

    /// <summary>
    /// 获取 dense 数组指定下标对应组件的 ref 引用。
    /// 调用方必须保证 denseIndex 在 [0, Count) 范围内。
    /// </summary>
    public ref T GetComponentByDenseIndex(int denseIndex)
    {
        if (denseIndex < 0 || denseIndex >= _count)
            throw new ArgumentOutOfRangeException(nameof(denseIndex));

        return ref _denseComponent[denseIndex];
    }

    /// <summary>
    /// 判断实体是否持有当前 Store 管理的组件类型。
    /// </summary>
    public bool Has(Entity entity)
    {
        return GetDenseIndex(entity) >= 0;
    }

    /// <summary>
    /// 从 Store 中移除实体组件，并用尾元素回填保证 dense 数组连续。
    /// </summary>
    public bool Remove(Entity entity)
    {
        int denseIndex = GetDenseIndex(entity);

        if (denseIndex < 0)
            return false;

        int lastIndex = _count - 1;

        if (denseIndex != lastIndex)
        {
            _denseEntity[denseIndex] = _denseEntity[lastIndex];
            _denseComponent[denseIndex] = _denseComponent[lastIndex];

            Entity movedEntity = _denseEntity[denseIndex];
            _sparse[movedEntity.ID] = denseIndex;
        }

        _denseEntity[lastIndex] = default;
        _denseComponent[lastIndex] = default;

        if (entity.ID >= 0 && entity.ID < _sparse.Length)
            _sparse[entity.ID] = -1;

        _count--;
        return true;
    }

    /// <summary>
    /// 通过 sparse 数组定位实体在 dense 数组中的位置，并校验 Entity 版本。
    /// </summary>
    private int GetDenseIndex(Entity entity)
    {
        if (!entity.IsValid)
            return -1;

        if (entity.ID < 0 || entity.ID >= _sparse.Length)
            return -1;

        int denseIndex = _sparse[entity.ID];

        if (denseIndex < 0 || denseIndex >= _count)
            return -1;

        Entity storedEntity = _denseEntity[denseIndex];

        if (!storedEntity.IsValid)
            return -1;

        if (storedEntity.ID != entity.ID || storedEntity.Version != entity.Version)
            return -1;

        return denseIndex;
    }

    /// <summary>
    /// 确保 sparse 数组可以用 entityID 作为下标访问。
    /// </summary>
    private void EnsureSparseCapacity(int entityID)
    {
        if (entityID < _sparse.Length)
            return;

        int oldLength = _sparse.Length;

        ToolFunction.EnsureArrayLength(ref _sparse, entityID + 1);

        for (int i = oldLength; i < _sparse.Length; i++)
        {
            _sparse[i] = -1;
        }
    }

    /// <summary>
    /// 确保 denseEntity 和 denseComponent 数组容量足够。
    /// </summary>
    private void EnsureDenseCapacity(int required)
    {
        ToolFunction.EnsureArrayLength(ref _denseEntity, required);
        ToolFunction.EnsureArrayLength(ref _denseComponent, required);
    }
}

/// <summary>
/// ComponentStore 的非泛型访问接口，用于 ComponentManager 批量管理不同组件类型的 Store。
/// </summary>
internal interface IComponentStore
{
    Type ComponentType { get; }
    int RegisterID { get; }

    /// <summary>
    /// 判断实体是否持有当前 Store 管理的组件类型。
    /// </summary>
    bool Has(Entity entity);
    /// <summary>
    /// 从 Store 中移除实体组件，并用尾元素回填保证 dense 数组连续。
    /// </summary>
    bool Remove(Entity entity);
}

}
