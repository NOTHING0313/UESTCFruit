/*
 * 文件说明：ComponentManager 负责按组件类型创建和管理 ComponentStore，并在组件新增 / 移除时同步 Entity 的 Mask 与 ArcheType 分组。
 * 设计约束：ECS Core 逻辑应尽量保持确定性；Unity 表现、输入采样、外部指令通过 Adapter 或 Buffer 接入。
 */

using System;
using System.Collections.Generic;

namespace ECSFrameWork
{

/// <summary>
/// 组件管理器，负责创建 ComponentStore、转发组件读写，并同步 Entity Mask 与 ArcheType 分组。
/// </summary>
internal class ComponentManager
{
    private readonly Dictionary<Type, IComponentStore> _stores = new Dictionary<Type, IComponentStore>();
    private readonly ComponentTypeRegistry _registry;

    public EntityManager _entityManager;
    public ArcheTypeManager _archeTypeManager;
    public ComponentTypeRegistry Registry => _registry;

    public int StoreCount => _stores.Count;
    /// <summary>
    /// 创建 ComponentManager，并注入 EntityManager、ArcheTypeManager 和组件注册表。
    /// </summary>
    public ComponentManager(EntityManager entityManager, ArcheTypeManager archeTypeManager, ComponentTypeRegistry registry)
    {
        _entityManager = entityManager;
        _archeTypeManager = archeTypeManager;
        _registry = registry;
    }

    /// <summary>
    /// 判断指定组件类型的 Store 是否已经创建。
    /// </summary>
    public bool HasStore<T>() where T : struct, IComponentData
    {
        return _stores.ContainsKey(typeof(T));
    }

    /// <summary>
    /// 确保指定组件 Store 的 sparse 和 dense 容量足够；Store 不存在时会自动创建。
    /// </summary>
    public void EnsureComponentCapacity<T>(int entityCapacity, int componentCapacity) where T : struct, IComponentData
    {
        if (entityCapacity <= 0 && componentCapacity <= 0)
            return;

        ComponentStore<T> store = GetStore<T>();

        if (store == null)
            return;

        store.EnsureCapacity(entityCapacity, componentCapacity);
    }

    /// <summary>
    /// 获取指定组件 Store 的 dense 数组容量；Store 不存在时返回 0。
    /// </summary>
    public int GetStoreCapacity<T>() where T : struct, IComponentData
    {
        return TryGetStore<T>(out ComponentStore<T> store) ? store.Capacity : 0;
    }

    /// <summary>
    /// 获取指定组件类型的 Store；不存在时自动创建。
    /// </summary>
    public ComponentStore<T> GetStore<T>() where T : struct, IComponentData
    {
        Type type = typeof(T);

        if (_stores.TryGetValue(type, out IComponentStore store))
            return (ComponentStore<T>)store;

        return CreateStore<T>();
    }

    /// <summary>
    /// 通过运行时 Type 获取或创建 ComponentStore，供快照恢复按注册表顺序重建 Store。
    /// </summary>
    internal IComponentStore GetOrCreateStore(Type componentType)
    {
        if (_entityManager == null || _registry == null || !IsValidComponentType(componentType))
            return null;

        if (_stores.TryGetValue(componentType, out IComponentStore existingStore))
            return existingStore;

        int registerID = _registry.GetOrRegister(componentType);
        IComponentStore store = CreateStoreByType(componentType, registerID);
        _stores.Add(componentType, store);
        return store;
    }

    /// <summary>
    /// 捕获当前所有 ComponentStore 的 dense 顺序组件快照。
    /// </summary>
    internal List<EcsComponentStoreSnapshot> CaptureStoreSnapshots()
    {
        List<IComponentStore> stores = new List<IComponentStore>(_stores.Values);
        stores.Sort((left, right) => left.RegisterID.CompareTo(right.RegisterID));

        List<EcsComponentStoreSnapshot> snapshots = new List<EcsComponentStoreSnapshot>(stores.Count);

        for (int i = 0; i < stores.Count; i++)
        {
            IComponentStore store = stores[i];
            List<EcsComponentSnapshot> components = new List<EcsComponentSnapshot>(store.Count);

            for (int denseIndex = 0; denseIndex < store.Count; denseIndex++)
            {
                if (store.TryGetBoxedByDenseIndex(denseIndex, out Entity entity, out object component))
                    components.Add(new EcsComponentSnapshot(entity, component));
            }

            snapshots.Add(new EcsComponentStoreSnapshot(store.ComponentType, store.RegisterID, components));
        }

        return snapshots;
    }

    /// <summary>
    /// 按快照恢复 ComponentStore。所有校验和临时 Store 构建完成后，才会替换当前 Store。
    /// </summary>
    internal bool RestoreStoreSnapshots(IReadOnlyList<EcsComponentStoreSnapshot> snapshots, out string errorMessage)
    {
        if (!ValidateStoreSnapshots(snapshots, out List<EcsComponentStoreSnapshot> orderedSnapshots, out errorMessage))
            return false;

        if (!BuildRestoredStores(orderedSnapshots, out Dictionary<Type, IComponentStore> restoredStores, out errorMessage))
            return false;

        ClearAllStores();
        ClearAllEntityMasks();

        for (int i = 0; i < orderedSnapshots.Count; i++)
        {
            EcsComponentStoreSnapshot snapshot = orderedSnapshots[i];
            _stores.Add(snapshot.ComponentType, restoredStores[snapshot.ComponentType]);
        }

        RebuildEntityMasksFromStores(orderedSnapshots);
        errorMessage = string.Empty;
        return true;
    }

    /// <summary>
    /// 清空并移除当前已创建的全部 ComponentStore。
    /// </summary>
    internal void ClearAllStores()
    {
        foreach (IComponentStore store in _stores.Values)
        {
            store.Clear();
        }

        _stores.Clear();
    }

    /// <summary>
    /// 为实体设置组件数据，并在新增组件时更新实体 Mask 与 ArcheType 分组。
    /// </summary>
    public void SetComponent<T>(Entity entity, in T component) where T : struct, IComponentData
    {
        if (_entityManager == null || !_entityManager.IsAlive(entity))
            return;

        ComponentStore<T> store = GetStore<T>();

        if (store == null)
            return;

        ComponentMask256 oldMask = _entityManager.GetMask(entity);
        bool added = store.Set(entity, in component);

        if (!added)
            return;

        _entityManager.SetMask(entity, store.RegisterID);
        ComponentMask256 newMask = _entityManager.GetMask(entity);

        _archeTypeManager?.ChangeGroup(entity, oldMask, newMask);
    }

    /// <summary>
    /// 获取实体组件数据的 ref 引用。
    /// </summary>
    public ref T GetComponent<T>(Entity entity) where T : struct, IComponentData
    {
        if (!TryGetStore<T>(out ComponentStore<T> store))
            throw new InvalidOperationException($"Component store does not exist: {typeof(T).Name}");

        return ref store.Get(entity);
    }

    /// <summary>
    /// 安全尝试获取实体组件数据。
    /// </summary>
    public bool TryGetComponent<T>(Entity entity, out T component) where T : struct, IComponentData
    {
        if (_entityManager == null || !_entityManager.IsAlive(entity))
        {
            component = default;
            return false;
        }

        if (!TryGetStore<T>(out ComponentStore<T> store))
        {
            component = default;
            return false;
        }

        return store.TryGet(entity, out component);
    }

    /// <summary>
    /// 安全判断实体是否持有指定组件。
    /// </summary>
    public bool HasComponent<T>(Entity entity) where T : struct, IComponentData
    {
        if (_entityManager == null || !_entityManager.IsAlive(entity))
            return false;

        if (!TryGetStore<T>(out ComponentStore<T> store))
            return false;

        return store.Has(entity);
    }

    /// <summary>
    /// 高频遍历拥有 T 的实体，并以 ref 形式暴露组件。
    /// 该方法直接遍历 ComponentStore 的 dense 数组，不创建 Query 结果 List。
    /// </summary>
    public int ForEach<T>(EntityComponentAction<T> action) where T : struct, IComponentData
    {
        if (action == null)
            return 0;

        if (!TryGetStore<T>(out ComponentStore<T> store))
            return 0;

        int executedCount = 0;
        int count = store.Count;

        for (int i = 0; i < count; i++)
        {
            Entity entity = store.GetEntityByDenseIndex(i);
            ref T component = ref store.GetComponentByDenseIndex(i);

            action(entity, ref component);
            executedCount++;
        }

        return executedCount;
    }

    /// <summary>
    /// 高频遍历同时拥有 T1 和 T2 的实体，并以 ref 形式暴露两个组件。
    /// 该方法不创建 Query 结果 List，会优先遍历组件数量更少的 Store，再用 sparse 映射定位另一个组件。
    /// </summary>
    public int ForEach<T1, T2>(EntityComponentAction<T1, T2> action) where T1 : struct, IComponentData where T2 : struct, IComponentData
    {
        if (action == null)
            return 0;

        if (!TryGetStore<T1>(out ComponentStore<T1> store1))
            return 0;

        if (!TryGetStore<T2>(out ComponentStore<T2> store2))
            return 0;

        if (store1.Count <= store2.Count)
            return ForEachByFirstStore(store1, store2, action);

        return ForEachBySecondStore(store1, store2, action);
    }

    /// <summary>
    /// 以第一个组件 Store 作为主遍历源执行双组件遍历。
    /// </summary>
    private int ForEachByFirstStore<T1, T2>(ComponentStore<T1> store1, ComponentStore<T2> store2, EntityComponentAction<T1, T2> action) where T1 : struct, IComponentData where T2 : struct, IComponentData
    {
        int executedCount = 0;
        int count = store1.Count;

        for (int i = 0; i < count; i++)
        {
            Entity entity = store1.GetEntityByDenseIndex(i);

            if (!store2.TryGetDenseIndex(entity, out int index2))
                continue;

            ref T1 component1 = ref store1.GetComponentByDenseIndex(i);
            ref T2 component2 = ref store2.GetComponentByDenseIndex(index2);

            action?.Invoke(entity, ref component1, ref component2);
            executedCount++;
        }

        return executedCount;
    }

    /// <summary>
    /// 以第二个组件 Store 作为主遍历源执行双组件遍历，同时保持回调参数顺序仍为 T1、T2。
    /// </summary>
    private int ForEachBySecondStore<T1, T2>(ComponentStore<T1> store1, ComponentStore<T2> store2, EntityComponentAction<T1, T2> action) where T1 : struct, IComponentData where T2 : struct, IComponentData
    {
        int executedCount = 0;
        int count = store2.Count;

        for (int i = 0; i < count; i++)
        {
            Entity entity = store2.GetEntityByDenseIndex(i);

            if (!store1.TryGetDenseIndex(entity, out int index1))
                continue;

            ref T1 component1 = ref store1.GetComponentByDenseIndex(index1);
            ref T2 component2 = ref store2.GetComponentByDenseIndex(i);

            action?.Invoke(entity, ref component1, ref component2);
            executedCount++;
        }

        return executedCount;
    }

    /// <summary>
    /// 高频遍历同时拥有 T1、T2 和 T3 的实体，并以 ref 形式暴露三个组件。
    /// 该方法会选择组件数量最少的 Store 作为主遍历源，减少无效 sparse 查找。
    /// </summary>
    public int ForEach<T1, T2, T3>(EntityComponentAction<T1, T2, T3> action) where T1 : struct, IComponentData where T2 : struct, IComponentData where T3 : struct, IComponentData
    {
        if (action == null)
            return 0;

        if (!TryGetStore<T1>(out ComponentStore<T1> store1))
            return 0;

        if (!TryGetStore<T2>(out ComponentStore<T2> store2))
            return 0;

        if (!TryGetStore<T3>(out ComponentStore<T3> store3))
            return 0;

        if (store1.Count <= store2.Count && store1.Count <= store3.Count)
            return ForEachByFirstStore(store1, store2, store3, action);

        if (store2.Count <= store1.Count && store2.Count <= store3.Count)
            return ForEachBySecondStore(store1, store2, store3, action);

        return ForEachByThirdStore(store1, store2, store3, action);
    }

    /// <summary>
    /// 以第一个组件 Store 作为主遍历源执行三组件遍历。
    /// </summary>
    private int ForEachByFirstStore<T1, T2, T3>(ComponentStore<T1> store1, ComponentStore<T2> store2, ComponentStore<T3> store3, EntityComponentAction<T1, T2, T3> action) where T1 : struct, IComponentData where T2 : struct, IComponentData where T3 : struct, IComponentData
    {
        int executedCount = 0;
        int count = store1.Count;

        for (int i = 0; i < count; i++)
        {
            Entity entity = store1.GetEntityByDenseIndex(i);

            if (!store2.TryGetDenseIndex(entity, out int index2))
                continue;

            if (!store3.TryGetDenseIndex(entity, out int index3))
                continue;

            ref T1 component1 = ref store1.GetComponentByDenseIndex(i);
            ref T2 component2 = ref store2.GetComponentByDenseIndex(index2);
            ref T3 component3 = ref store3.GetComponentByDenseIndex(index3);

            action(entity, ref component1, ref component2, ref component3);
            executedCount++;
        }

        return executedCount;
    }

    /// <summary>
    /// 以第二个组件 Store 作为主遍历源执行三组件遍历，同时保持回调参数顺序仍为 T1、T2、T3。
    /// </summary>
    private int ForEachBySecondStore<T1, T2, T3>(ComponentStore<T1> store1, ComponentStore<T2> store2, ComponentStore<T3> store3, EntityComponentAction<T1, T2, T3> action) where T1 : struct, IComponentData where T2 : struct, IComponentData where T3 : struct, IComponentData
    {
        int executedCount = 0;
        int count = store2.Count;

        for (int i = 0; i < count; i++)
        {
            Entity entity = store2.GetEntityByDenseIndex(i);

            if (!store1.TryGetDenseIndex(entity, out int index1))
                continue;

            if (!store3.TryGetDenseIndex(entity, out int index3))
                continue;

            ref T1 component1 = ref store1.GetComponentByDenseIndex(index1);
            ref T2 component2 = ref store2.GetComponentByDenseIndex(i);
            ref T3 component3 = ref store3.GetComponentByDenseIndex(index3);

            action(entity, ref component1, ref component2, ref component3);
            executedCount++;
        }

        return executedCount;
    }

    /// <summary>
    /// 以第三个组件 Store 作为主遍历源执行三组件遍历，同时保持回调参数顺序仍为 T1、T2、T3。
    /// </summary>
    private int ForEachByThirdStore<T1, T2, T3>(ComponentStore<T1> store1, ComponentStore<T2> store2, ComponentStore<T3> store3, EntityComponentAction<T1, T2, T3> action) where T1 : struct, IComponentData where T2 : struct, IComponentData where T3 : struct, IComponentData
    {
        int executedCount = 0;
        int count = store3.Count;

        for (int i = 0; i < count; i++)
        {
            Entity entity = store3.GetEntityByDenseIndex(i);

            if (!store1.TryGetDenseIndex(entity, out int index1))
                continue;

            if (!store2.TryGetDenseIndex(entity, out int index2))
                continue;

            ref T1 component1 = ref store1.GetComponentByDenseIndex(index1);
            ref T2 component2 = ref store2.GetComponentByDenseIndex(index2);
            ref T3 component3 = ref store3.GetComponentByDenseIndex(i);

            action(entity, ref component1, ref component2, ref component3);
            executedCount++;
        }

        return executedCount;
    }

    /// <summary>
    /// 移除实体上的指定组件，并同步更新实体 Mask 与 ArcheType 分组。
    /// </summary>
    public bool RemoveComponent<T>(Entity entity) where T : struct, IComponentData
    {
        if (_entityManager == null || !_entityManager.IsAlive(entity))
            return false;

        if (!TryGetStore<T>(out ComponentStore<T> store))
            return false;

        ComponentMask256 oldMask = _entityManager.GetMask(entity);
        bool removed = store.Remove(entity);

        if (!removed)
            return false;

        _entityManager.RemoveMask(entity, store.RegisterID);
        ComponentMask256 newMask = _entityManager.GetMask(entity);

        _archeTypeManager?.ChangeGroup(entity, oldMask, newMask);
        return true;
    }

    /// <summary>
    /// 移除实体持有的所有组件，并把实体从 ArcheType 分组中移除。
    /// </summary>
    public void RemoveAllComponents(Entity entity)
    {
        if (_entityManager == null || !_entityManager.IsAlive(entity))
            return;

        ComponentMask256 oldMask = _entityManager.GetMask(entity);

        foreach (IComponentStore store in _stores.Values)
        {
            store.Remove(entity);
        }

        _entityManager.ClearMask(entity);
        _archeTypeManager?.ChangeGroup(entity, oldMask, default);
    }


    /// <summary>
    /// 把当前已经创建的 ComponentStore 调试信息写入外部 List。
    /// </summary>
    public int FillComponentStoreDebugInfos(List<ComponentStoreDebugInfo> results)
    {
        if (results == null)
            return 0;

        results.Clear();

        foreach (IComponentStore store in _stores.Values)
        {
            results.Add(new ComponentStoreDebugInfo(store.ComponentType, store.RegisterID, store.Count, store.Capacity, store.SparseCapacity));
        }

        return results.Count;
    }

    /// <summary>
    /// 把 Entity 当前持有的组件类型写入外部 List。
    /// </summary>
    public int FillEntityComponentTypes(Entity entity, List<Type> results)
    {
        if (results == null)
            return 0;

        results.Clear();

        if (_entityManager == null || !_entityManager.IsAlive(entity))
            return 0;

        foreach (IComponentStore store in _stores.Values)
        {
            if (store.Has(entity))
                results.Add(store.ComponentType);
        }

        return results.Count;
    }

    /// <summary>
    /// 尝试以 boxed object 形式读取指定 Entity 上的组件数据，供 Editor Debugger 非泛型展示。
    /// </summary>
    public bool TryGetComponentDebugValue(Entity entity, Type componentType, out object component)
    {
        component = null;

        if (componentType == null || _entityManager == null || !_entityManager.IsAlive(entity))
            return false;

        if (!_stores.TryGetValue(componentType, out IComponentStore store))
            return false;

        return store.TryGetBoxed(entity, out component);
    }

    /// <summary>
    /// 校验 Store 快照结构、组件类型、注册 ID 和实体引用，不修改当前 Store。
    /// </summary>
    private bool ValidateStoreSnapshots(IReadOnlyList<EcsComponentStoreSnapshot> snapshots, out List<EcsComponentStoreSnapshot> orderedSnapshots, out string errorMessage)
    {
        orderedSnapshots = null;

        if (snapshots == null)
        {
            errorMessage = "Component store snapshot list is null.";
            return false;
        }

        if (_entityManager == null)
        {
            errorMessage = "ComponentManager cannot restore stores without EntityManager.";
            return false;
        }

        if (_registry == null)
        {
            errorMessage = "ComponentManager cannot restore stores without ComponentTypeRegistry.";
            return false;
        }

        HashSet<Type> componentTypes = new HashSet<Type>();
        HashSet<int> registerIDs = new HashSet<int>();
        orderedSnapshots = new List<EcsComponentStoreSnapshot>(snapshots.Count);

        for (int i = 0; i < snapshots.Count; i++)
        {
            EcsComponentStoreSnapshot snapshot = snapshots[i];

            if (snapshot == null)
            {
                errorMessage = $"Component store snapshot at index {i} is null.";
                return false;
            }

            if (!ValidateStoreSnapshotHeader(snapshot, componentTypes, registerIDs, out errorMessage))
                return false;

            if (!ValidateStoreSnapshotComponents(snapshot, out errorMessage))
                return false;

            orderedSnapshots.Add(snapshot);
        }

        orderedSnapshots.Sort((left, right) => left.RegisterID.CompareTo(right.RegisterID));
        errorMessage = string.Empty;
        return true;
    }

    private bool ValidateStoreSnapshotHeader(EcsComponentStoreSnapshot snapshot, HashSet<Type> componentTypes, HashSet<int> registerIDs, out string errorMessage)
    {
        if (!IsValidComponentType(snapshot.ComponentType))
        {
            errorMessage = $"Invalid component type in store snapshot: {snapshot.ComponentType?.FullName ?? "<null>"}";
            return false;
        }

        if (!componentTypes.Add(snapshot.ComponentType))
        {
            errorMessage = $"Duplicate component store snapshot type: {snapshot.ComponentType.FullName}";
            return false;
        }

        if (!registerIDs.Add(snapshot.RegisterID))
        {
            errorMessage = $"Duplicate component store snapshot register id: {snapshot.RegisterID}";
            return false;
        }

        if (!_registry.TryGetType(snapshot.RegisterID, out Type registeredType))
        {
            errorMessage = $"Component store snapshot register id is not registered: {snapshot.RegisterID}";
            return false;
        }

        if (registeredType != snapshot.ComponentType)
        {
            errorMessage = $"Component store snapshot type does not match registry id {snapshot.RegisterID}: {snapshot.ComponentType.FullName}";
            return false;
        }

        if (snapshot.DenseComponents == null)
        {
            errorMessage = $"Component store snapshot dense component list is null: {snapshot.ComponentType.FullName}";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private bool ValidateStoreSnapshotComponents(EcsComponentStoreSnapshot snapshot, out string errorMessage)
    {
        HashSet<Entity> entities = new HashSet<Entity>();

        for (int i = 0; i < snapshot.DenseComponents.Count; i++)
        {
            EcsComponentSnapshot component = snapshot.DenseComponents[i];

            if (component == null)
            {
                errorMessage = $"Component snapshot at dense index {i} is null: {snapshot.ComponentType.FullName}";
                return false;
            }

            if (!_entityManager.IsAlive(component.Entity))
            {
                errorMessage = $"Component snapshot entity is not alive: {component.Entity}";
                return false;
            }

            if (!entities.Add(component.Entity))
            {
                errorMessage = $"Duplicate entity in component store snapshot: {component.Entity}";
                return false;
            }

            if (component.ComponentValue == null || !snapshot.ComponentType.IsInstanceOfType(component.ComponentValue))
            {
                errorMessage = $"Component snapshot value type mismatch: {snapshot.ComponentType.FullName}";
                return false;
            }
        }

        errorMessage = string.Empty;
        return true;
    }

    private bool BuildRestoredStores(List<EcsComponentStoreSnapshot> orderedSnapshots, out Dictionary<Type, IComponentStore> restoredStores, out string errorMessage)
    {
        restoredStores = new Dictionary<Type, IComponentStore>(orderedSnapshots.Count);

        for (int i = 0; i < orderedSnapshots.Count; i++)
        {
            EcsComponentStoreSnapshot snapshot = orderedSnapshots[i];
            IComponentStore store = CreateStoreByType(snapshot.ComponentType, snapshot.RegisterID);

            for (int denseIndex = 0; denseIndex < snapshot.DenseComponents.Count; denseIndex++)
            {
                EcsComponentSnapshot component = snapshot.DenseComponents[denseIndex];

                if (!store.SetBoxed(component.Entity, component.ComponentValue))
                {
                    errorMessage = $"Failed to restore component store value: {snapshot.ComponentType.FullName}";
                    return false;
                }
            }

            restoredStores.Add(snapshot.ComponentType, store);
        }

        errorMessage = string.Empty;
        return true;
    }

    private void ClearAllEntityMasks()
    {
        if (_entityManager == null)
            return;

        List<Entity> entities = new List<Entity>();
        _entityManager.FillAliveEntities(entities);

        for (int i = 0; i < entities.Count; i++)
        {
            Entity entity = entities[i];
            ComponentMask256 oldMask = _entityManager.GetMask(entity);

            if (oldMask.IsEmpty)
                continue;

            _entityManager.ClearMask(entity);
            _archeTypeManager?.ChangeGroup(entity, oldMask, default);
        }
    }

    private void RebuildEntityMasksFromStores(List<EcsComponentStoreSnapshot> orderedSnapshots)
    {
        for (int i = 0; i < orderedSnapshots.Count; i++)
        {
            EcsComponentStoreSnapshot snapshot = orderedSnapshots[i];

            for (int denseIndex = 0; denseIndex < snapshot.DenseComponents.Count; denseIndex++)
            {
                Entity entity = snapshot.DenseComponents[denseIndex].Entity;
                ComponentMask256 oldMask = _entityManager.GetMask(entity);
                _entityManager.SetMask(entity, snapshot.RegisterID);
                ComponentMask256 newMask = _entityManager.GetMask(entity);
                _archeTypeManager?.ChangeGroup(entity, oldMask, newMask);
            }
        }
    }

    private static bool IsValidComponentType(Type type)
    {
        return type != null && typeof(IComponentData).IsAssignableFrom(type) && type.IsValueType;
    }

    private IComponentStore CreateStoreByType(Type componentType, int registerID)
    {
        Type storeType = typeof(ComponentStore<>).MakeGenericType(componentType);
        return (IComponentStore)Activator.CreateInstance(storeType, registerID, _entityManager);
    }

    /// <summary>
    /// 创建指定组件类型对应的 ComponentStore。
    /// </summary>
    private ComponentStore<T> CreateStore<T>() where T : struct, IComponentData
    {
        if (_entityManager == null || _registry == null)
            return null;

        Type type = typeof(T);

        if (_stores.TryGetValue(type, out IComponentStore existingStore))
            return (ComponentStore<T>)existingStore;

        int id = _registry.GetOrRegister<T>();
        ComponentStore<T> store = new ComponentStore<T>(id, _entityManager);

        _stores.Add(type, store);
        return store;
    }

    /// <summary>
    /// 尝试获取指定组件类型对应的 ComponentStore。
    /// </summary>
    private bool TryGetStore<T>(out ComponentStore<T> store) where T : struct, IComponentData
    {
        if (_stores.TryGetValue(typeof(T), out IComponentStore rawStore))
        {
            store = (ComponentStore<T>)rawStore;
            return true;
        }

        store = null;
        return false;
    }
}

}
