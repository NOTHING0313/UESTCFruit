
/*
 * 文件说明：World 是 ECS Core 的统一入口，负责整合 Entity、Component、ArcheType、System 与结构变更缓冲。外部业务应优先通过 World 访问 ECS，而不是直接操作各 Manager。
 * 设计约束：ECS Core 逻辑应尽量保持确定性；Unity 表现、输入采样、外部指令通过 Adapter 或 Buffer 接入。
 */

using System;
using System.Collections.Generic;

namespace ECSFrameWork
{

/// <summary>
/// ECS 逻辑世界入口，统一管理 Entity、Component、ArcheType、Query、System 与结构变更缓冲。
/// </summary>
public class World
{
    private EntityManager _entityManager;
    private ComponentManager _componentManager;
    private ArcheTypeManager _archeTypeManager;
    private SystemManager _systemManager;
    private ComponentTypeRegistry _registry;
    private StructuralChangeBuffer _structuralChangeBuffer;
    private SystemChangeBuffer _systemChangeBuffer;
    private WorldEventBuffer _worldEventBuffer;
    private Dictionary<Type, Entity> _singletonEntities;

    private WorldStates _currentState = WorldStates.Initialization;

    internal StructuralChangeBuffer Commands => _structuralChangeBuffer;
    public WorldStates CurrentState => _currentState;

    public int CreatedEntityCount => _entityManager.CreatedEntityCount;
    public int EntityCapacity => _entityManager.EntityCapacity;
    public int AliveEntityCount => _entityManager.AliveEntityCount;
    public int EntityCount => _entityManager.AliveEntityCount;
    public int FreeEntityCount => _entityManager.FreeEntityCount;
    public int ComponentStoreCount => _componentManager.StoreCount;
    public int ArcheTypeCount => _archeTypeManager.ArcheTypeCount;
    public int SystemCount => _systemManager.SystemCount;
    public int PendingCommandCount => Commands.Count;
    public int PendingSystemCommandCount => _systemManager.PendingSystemCommandCount;
    public int QueryCacheCount => _archeTypeManager.QueryCacheCount;
    public int ArcheTypeVersion => _archeTypeManager.ArcheTypeVersion;
    public int WorldEventCount => _worldEventBuffer.Count;
    public int WorldEventTypeCount => _worldEventBuffer.EventTypeCount;
    public int SingletonCount => _singletonEntities.Count;
    public int RegisteredComponentTypeCount => _registry.RegisteredTypeCount;

    /// <summary>是否启用 System Tick 耗时统计；关闭后 System 仍正常执行，但不会更新 Profile。</summary>
    public bool EnableSystemProfile
    {
        get => _systemManager.EnableSystemProfile;
        set => _systemManager.EnableSystemProfile = value;
    }

    /// <summary>当前持有的 System 性能统计对象数量。</summary>
    public int SystemProfileCount => _systemManager.ProfileCount;

    /// <summary>
    /// 创建 World，并初始化 Entity、Component、ArcheType、System 与命令缓冲管理器。
    /// </summary>
    public World()
    {
        LoadManagers();
    }

    /// <summary>
    /// 初始化 World 内部所有核心 Manager 与 Buffer。
    /// </summary>
    private void LoadManagers()
    {
        SetWorldState(WorldStates.Initialization);

        _registry = new ComponentTypeRegistry();
        _entityManager = new EntityManager();
        _archeTypeManager = new ArcheTypeManager(_registry);
        _componentManager = new ComponentManager(_entityManager, _archeTypeManager, _registry);
        _structuralChangeBuffer = new StructuralChangeBuffer();
        _worldEventBuffer = new WorldEventBuffer();
        _singletonEntities = new Dictionary<Type, Entity>();
        _systemManager = new SystemManager(this);
    }

    /// <summary>
    /// 切换 World 当前生命周期阶段。
    /// </summary>
    private void SetWorldState(WorldStates state)
    {
        _currentState = state;
    }

    /// <summary>
    /// 判断 World 是否正在释放。
    /// </summary>
    public bool IsDisposing()
    {
        return _currentState == WorldStates.Disposing;
    }

    /// <summary>
    /// 判断 World 是否正在播放 SystemChangeBuffer。
    /// </summary>
    public bool IsSystemOperating()
    {
        return _currentState == WorldStates.SystemOperating;
    }

    /// <summary>
    /// 判断当前阶段是否允许立即执行 Entity/Component 结构修改。
    /// </summary>
    internal bool CanExcuteImmediately(ExcuteType excuteType)
    {
        if (_currentState == WorldStates.Disposing)
            return false;

        switch (excuteType)
        {
            case ExcuteType.Add:
            case ExcuteType.Remove:
            case ExcuteType.DestroyEntity:
                return _currentState == WorldStates.Initialization || _currentState == WorldStates.AfterTicking;

            case ExcuteType.Default:
            default:
                return true;
        }
    }

    /// <summary>
    /// 判断当前阶段是否允许立即修改 System 列表。
    /// </summary>
    internal bool CanExcuteSystemImmediately(ExcuteType excuteType)
    {
        if (_currentState == WorldStates.Disposing)
            return false;

        switch (excuteType)
        {
            case ExcuteType.Add:
            case ExcuteType.Remove:
            case ExcuteType.DestroyEntity:
                return _currentState == WorldStates.Initialization;

            case ExcuteType.Default:
            default:
                return true;
        }
    }

    /// <summary>
    /// 推进一个逻辑帧：执行 System、播放结构变更、播放 System 变更。
    /// </summary>
    public void Tick(in SimulationContext context)
    {
        if (_currentState == WorldStates.Disposing)
            return;

        try
        {
            SetWorldState(WorldStates.Ticking);
            _systemManager.Tick(in context);

            if (_currentState == WorldStates.Disposing)
                return;

            SetWorldState(WorldStates.AfterTicking);
            PlaybackStructuralChanges();

            if (_currentState == WorldStates.Disposing)
                return;

            SetWorldState(WorldStates.SystemOperating);
            _systemManager.PlaybackSystemChanges();
        }
        finally
        {
            if (_currentState != WorldStates.Disposing)
                SetWorldState(WorldStates.Initialization);
        }
    }

    /// <summary>
    /// 在 AfterTicking 阶段播放 StructuralChangeBuffer。
    /// </summary>
    private void PlaybackStructuralChanges()
    {
        if (_currentState != WorldStates.AfterTicking)
            return;

        _structuralChangeBuffer.Playback(this);
    }

    /// <summary>
    /// 确保 EntityData 底层数组容量至少达到指定长度。
    /// 该方法只预分配容量，不会创建 Entity。
    /// </summary>
    public void EnsureEntityCapacity(int capacity)
    {
        if (_currentState == WorldStates.Disposing)
            return;

        _entityManager.EnsureCapacity(capacity);
    }

    /// <summary>
    /// 确保指定组件 Store 的容量至少达到指定长度。
    /// sparse 和 dense 默认使用相同容量，适合大量 Entity 预热场景。
    /// </summary>
    public void EnsureComponentCapacity<T>(int capacity) where T : struct, IComponentData
    {
        if (_currentState == WorldStates.Disposing)
            return;

        _componentManager.EnsureComponentCapacity<T>(capacity, capacity);
    }

    /// <summary>
    /// 获取指定组件 Store 的 dense 容量；Store 不存在时返回 0。
    /// </summary>
    public int GetComponentStoreCapacity<T>() where T : struct, IComponentData
    {
        return _componentManager.GetStoreCapacity<T>();
    }

    /// <summary>
    /// 按 Entity ID 从小到大枚举当前存活实体。
    /// </summary>
    public IEnumerable<Entity> GetAliveEntities()
    {
        return _entityManager.GetAliveEntities();
    }

    /// <summary>
    /// 创建 EntityQueryBuilder，用于链式构建 ECS 查询。
    /// </summary>
    public EntityQueryBuilder Query()
    {
        return new EntityQueryBuilder(this);
    }

    /// <summary>
    /// 通过组件类型注册表创建单组件 Mask。
    /// </summary>
    internal ComponentMask256 CreateMask<T>() where T : struct, IComponentData
    {
        return _registry.CreateMask<T>();
    }

    /// <summary>
    /// 创建新实体；World 正在释放时返回 Invalid。
    /// </summary>
    public Entity CreateEntity()
    {
        if (_currentState == WorldStates.Disposing)
            return Entity.Invalid;

        return _entityManager.GetEntity();
    }

    /// <summary>
    /// 创建 EntityBuilder，用于链式创建 Entity 并设置初始组件。
    /// </summary>
    public EntityBuilder CreateEntityBuilder()
    {
        return new EntityBuilder(this);
    }

    /// <summary>
    /// 使用委托集中配置并创建 Entity。configure 为空时仅创建 Entity。
    /// </summary>
    public Entity BuildEntity(Action<EntityBuilder> configure)
    {
        EntityBuilder builder = CreateEntityBuilder();
        configure?.Invoke(builder);
        return builder.Build();
    }

    /// <summary>
    /// 判断实体句柄是否仍然有效且存活。
    /// </summary>
    public bool IsAlive(Entity entity)
    {
        return _entityManager.IsAlive(entity);
    }

    /// <summary>
    /// 销毁实体；Tick 中调用时进入 StructuralChangeBuffer。
    /// </summary>
    public void DestroyEntity(Entity entity)
    {
        if (_currentState == WorldStates.Disposing)
            return;

        if (!_entityManager.IsAlive(entity))
            return;

        if (CanExcuteImmediately(ExcuteType.DestroyEntity))
        {
            DestroyEntityImmediately(entity);
            return;
        }

        Commands.DestroyEntity(entity);
    }

    /// <summary>
    /// 立即销毁实体并移除其所有组件。
    /// </summary>
    internal void DestroyEntityImmediately(Entity entity)
    {
        if (!_entityManager.IsAlive(entity))
            return;

        RemoveSingletonMappingsByEntity(entity);
        _componentManager.RemoveAllComponents(entity);
        _entityManager.DestroyEntity(entity);
    }

    /// <summary>
    /// 设置组件；新增组件在禁止立即结构修改的阶段进入 StructuralChangeBuffer。
    /// </summary>
    public void SetComponent<T>(Entity entity, in T component) where T : struct, IComponentData
    {
        if (_currentState == WorldStates.Disposing)
            return;

        if (!_entityManager.IsAlive(entity))
            return;

        bool hasComponent = _componentManager.HasComponent<T>(entity);

        if (hasComponent)
        {
            SetComponentImmediately(entity, in component);
            return;
        }

        if (CanExcuteImmediately(ExcuteType.Add))
        {
            SetComponentImmediately(entity, in component);
            return;
        }

        Commands.SetComponent(entity, in component);
    }

    /// <summary>
    /// 立即设置组件并同步 ArcheType。
    /// </summary>
    internal void SetComponentImmediately<T>(Entity entity, in T component) where T : struct, IComponentData
    {
        if (!_entityManager.IsAlive(entity))
            return;

        _componentManager.SetComponent(entity, in component);
    }

    /// <summary>
    /// 移除组件；禁止立即结构修改时进入 StructuralChangeBuffer。
    /// </summary>
    public bool RemoveComponent<T>(Entity entity) where T : struct, IComponentData
    {
        if (_currentState == WorldStates.Disposing)
            return false;

        if (!_entityManager.IsAlive(entity))
            return false;

        if (!_componentManager.HasComponent<T>(entity))
            return false;

        if (CanExcuteImmediately(ExcuteType.Remove))
            return RemoveComponentImmediately<T>(entity);

        Commands.RemoveComponent<T>(entity);
        return true;
    }

    /// <summary>
    /// 立即移除组件并同步 ArcheType。
    /// </summary>
    internal bool RemoveComponentImmediately<T>(Entity entity) where T : struct, IComponentData
    {
        if (!_entityManager.IsAlive(entity))
            return false;

        bool removed = _componentManager.RemoveComponent<T>(entity);

        if (removed && _singletonEntities.TryGetValue(typeof(T), out Entity singletonEntity) && singletonEntity == entity)
            _singletonEntities.Remove(typeof(T));

        return removed;
    }


    /// <summary>
    /// 获取组件 ref 引用。
    /// </summary>
    public ref T GetComponent<T>(Entity entity) where T : struct, IComponentData
    {
        return ref _componentManager.GetComponent<T>(entity);
    }

    /// <summary>
    /// 安全尝试读取组件数据。
    /// </summary>
    public bool TryGetComponent<T>(Entity entity, out T component) where T : struct, IComponentData
    {
        if (!_entityManager.IsAlive(entity))
        {
            component = default;
            return false;
        }

        return _componentManager.TryGetComponent(entity, out component);
    }

    /// <summary>
    /// 安全判断实体是否拥有指定组件。
    /// </summary>
    public bool HasComponent<T>(Entity entity) where T : struct, IComponentData
    {
        if (!_entityManager.IsAlive(entity))
            return false;

        return _componentManager.HasComponent<T>(entity);
    }

    /// <summary>
    /// 设置指定类型的 Singleton Component。
    /// 如果该 Singleton 已存在，则覆盖组件数据；如果不存在，则创建一个内部 Entity 承载该组件。
    /// </summary>
    public Entity SetSingleton<T>(in T component) where T : struct, IComponentData
    {
        if (_currentState == WorldStates.Disposing)
            return Entity.Invalid;

        Type type = typeof(T);

        if (_singletonEntities.TryGetValue(type, out Entity entity) && _entityManager.IsAlive(entity))
        {
            SetComponent(entity, in component);
            return entity;
        }

        entity = CreateEntity();

        if (!entity.IsValid)
            return Entity.Invalid;

        _singletonEntities[type] = entity;
        SetComponent(entity, in component);
        return entity;
    }

    /// <summary>
    /// 判断指定类型的 Singleton Component 是否存在且对应 Entity 仍然存活。
    /// </summary>
    public bool HasSingleton<T>() where T : struct, IComponentData
    {
        return TryGetSingletonEntity<T>(out Entity entity) && HasComponent<T>(entity);
    }

    /// <summary>
    /// 获取指定类型 Singleton Component 的 ref 引用。
    /// Singleton 不存在时抛出异常，调用前可先使用 HasSingleton 或 TryGetSingleton。
    /// </summary>
    public ref T GetSingleton<T>() where T : struct, IComponentData
    {
        if (!TryGetSingletonEntity<T>(out Entity entity) || !HasComponent<T>(entity))
            throw new InvalidOperationException($"Singleton component does not exist: {typeof(T).Name}");

        return ref GetComponent<T>(entity);
    }

    /// <summary>
    /// 安全尝试获取指定类型 Singleton Component 的数据副本。
    /// </summary>
    public bool TryGetSingleton<T>(out T component) where T : struct, IComponentData
    {
        if (!TryGetSingletonEntity<T>(out Entity entity))
        {
            component = default;
            return false;
        }

        return TryGetComponent(entity, out component);
    }

    /// <summary>
    /// 安全尝试获取承载指定 Singleton Component 的 Entity。
    /// </summary>
    public bool TryGetSingletonEntity<T>(out Entity entity) where T : struct, IComponentData
    {
        Type type = typeof(T);

        if (_singletonEntities.TryGetValue(type, out entity) && _entityManager.IsAlive(entity))
            return true;

        _singletonEntities.Remove(type);
        entity = Entity.Invalid;
        return false;
    }

    /// <summary>
    /// 移除指定类型的 Singleton Component，并销毁其内部承载 Entity。
    /// Tick 中调用时销毁会进入 StructuralChangeBuffer。
    /// </summary>
    public bool RemoveSingleton<T>() where T : struct, IComponentData
    {
        Type type = typeof(T);

        if (!_singletonEntities.TryGetValue(type, out Entity entity) || !_entityManager.IsAlive(entity))
        {
            _singletonEntities.Remove(type);
            return false;
        }

        _singletonEntities.Remove(type);
        DestroyEntity(entity);
        return true;
    }

    /// <summary>
    /// 移除指向指定 Entity 的 Singleton 映射。
    /// </summary>
    private void RemoveSingletonMappingsByEntity(Entity entity)
    {
        if (_singletonEntities == null || _singletonEntities.Count == 0)
            return;

        List<Type> removeTypes = null;

        foreach (KeyValuePair<Type, Entity> pair in _singletonEntities)
        {
            if (pair.Value != entity)
                continue;

            if (removeTypes == null)
                removeTypes = new List<Type>();

            removeTypes.Add(pair.Key);
        }

        if (removeTypes == null)
            return;

        for (int i = 0; i < removeTypes.Count; i++)
            _singletonEntities.Remove(removeTypes[i]);
    }

    /// <summary>
    /// 高频遍历拥有 T 的实体，并以 ref 形式暴露组件。
    /// 该方法不会创建 Query 结果 List，适合冷却、生命周期、恢复等单组件系统。
    /// </summary>
    public int ForEach<T>(EntityComponentAction<T> action) where T : struct, IComponentData
    {
        if (_currentState == WorldStates.Disposing)
            return 0;

        return _componentManager.ForEach(action);
    }

    /// <summary>
    /// 高频遍历同时拥有 T1 和 T2 的实体，并以 ref 形式暴露组件。
    /// 该方法不会创建 Query 结果 List，适合 MovementSystem 等高频无排序需求的系统。
    /// </summary>
    public int ForEach<T1, T2>(EntityComponentAction<T1, T2> action) where T1 : struct, IComponentData where T2 : struct, IComponentData
    {
        if (_currentState == WorldStates.Disposing)
            return 0;

        return _componentManager.ForEach(action);
    }

    /// <summary>
    /// 高频遍历同时拥有 T1、T2 和 T3 的实体，并以 ref 形式暴露组件。
    /// 该方法不会创建 Query 结果 List，适合 InputMoveSystem 等高频无排序需求的系统。
    /// </summary>
    public int ForEach<T1, T2, T3>(EntityComponentAction<T1, T2, T3> action) where T1 : struct, IComponentData where T2 : struct, IComponentData where T3 : struct, IComponentData
    {
        if (_currentState == WorldStates.Disposing)
            return 0;

        return _componentManager.ForEach(action);
    }

    /// <summary>
    /// 写入一个 World 逻辑事件。
    /// 事件用于描述当前逻辑帧产生的一次性结果，通常由 System 写入，由表现层、UI 或音效层读取。
    /// </summary>
    public void AddWorldEvent<T>(T worldEvent) where T : struct, IWorldEvent
    {
        if (_currentState == WorldStates.Disposing)
            return;

        _worldEventBuffer.Add(worldEvent);
    }

    /// <summary>
    /// 获取指定类型的 World 事件只读列表。
    /// 返回列表由 WorldEventBuffer 持有，外部应只读取，不应缓存跨生命周期使用。
    /// </summary>
    public IReadOnlyList<T> GetWorldEvents<T>() where T : struct, IWorldEvent
    {
        return _worldEventBuffer.GetEvents<T>();
    }

    /// <summary>
    /// 清理所有 World 事件。
    /// 通常在表现层消费完当前事件后调用。
    /// </summary>
    public void ClearWorldEvents()
    {
        _worldEventBuffer.Clear();
    }

    /// <summary>
    /// 清理指定逻辑帧之前产生的 World 事件。
    /// </summary>
    public void ClearWorldEventsBeforeFrame(int frameNumber)
    {
        _worldEventBuffer.ClearBeforeFrame(frameNumber);
    }

    /// <summary>
    /// 向 World 添加 System。
    /// </summary>
    public void AddSystem(IFixedStepSystem system)
    {
        _systemManager.AddSystem(system);
    }

    /// <summary>
    /// 从 World 移除 System。
    /// </summary>
    public bool RemoveSystem(IFixedStepSystem system)
    {
        return _systemManager.RemoveSystem(system);
    }

    /// <summary>
    /// 清空 World 中的全部 System。
    /// </summary>
    public void ClearSystem()
    {
        _systemManager.ClearSystem();
    }

    /// <summary>
    /// 尝试获取指定 System 的性能统计信息。
    /// </summary>
    public bool TryGetSystemProfile(IFixedStepSystem system, out SystemProfileInfo profile)
    {
        return _systemManager.TryGetSystemProfile(system, out profile);
    }

    /// <summary>
    /// 获取当前所有 System 的性能统计信息。
    /// 返回新的 List，避免外部修改 SystemManager 内部集合。
    /// </summary>
    public List<SystemProfileInfo> GetSystemProfiles()
    {
        return _systemManager.GetSystemProfiles();
    }

    /// <summary>
    /// 重置全部 System 性能统计数据，但不移除已注册的 Profile。
    /// </summary>
    public void ResetSystemProfiles()
    {
        _systemManager.ResetSystemProfiles();
    }

    /// <summary>
    /// 释放 World，清理结构命令、System 命令与全部 System。
    /// </summary>
    public void Dispose()
    {
        if (_currentState == WorldStates.Disposing)
            return;

        SetWorldState(WorldStates.Disposing);

        _structuralChangeBuffer.Clear();
        _worldEventBuffer.Clear();
        _singletonEntities.Clear();
        _systemManager.ClearPendingSystemChanges();
        _systemManager.ClearSystemImmediately();
        _systemManager.ClearPendingSystemChanges();
    }

    /// <summary>
    /// 执行 QueryDescription 查询，并把结果填充到外部 List 中。
    /// sorted 为 true 时，会按 Entity ID / Version 排序。
    /// </summary>
    public int FillQuery(EntityQueryDescription query, List<Entity> results, bool sorted = false)
    {
        if (results == null)
            return 0;

        results.Clear();

        _archeTypeManager.FillEntityByQuery(query, results);

        if (sorted)
            results.Sort(EntityComparer.Instance);

        return results.Count;
    }


    /// <summary>
    /// 获取当前 World 的调试总览快照。
    /// 该方法不会修改 ECS 状态，适合 EditorWindow 或 Inspector 面板按需刷新。
    /// </summary>
    public WorldDebugSnapshot GetDebugSnapshot()
    {
        WorldStatistics statistics = GetStatistics();

        return new WorldDebugSnapshot(
            statistics,
            EntityCapacity,
            RegisteredComponentTypeCount,
            ComponentStoreCount,
            ArcheTypeCount,
            QueryCacheCount,
            SystemCount,
            SingletonCount,
            WorldEventTypeCount,
            WorldEventCount,
            PendingCommandCount,
            PendingSystemCommandCount,
            _currentState
        );
    }

    /// <summary>
    /// 把当前存活 Entity 写入外部 List。
    /// Debug 工具应优先使用该方法复用 List，避免每次刷新产生额外 GC。
    /// </summary>
    public int FillAliveEntities(List<Entity> results)
    {
        return _entityManager.FillAliveEntities(results);
    }

    /// <summary>
    /// 尝试获取指定 Entity 的调试信息。
    /// </summary>
    public bool TryGetEntityDebugInfo(Entity entity, out EntityDebugInfo info)
    {
        return _entityManager.TryGetDebugInfo(entity, out info);
    }

    /// <summary>
    /// 把指定 Entity 当前拥有的组件类型写入外部 List。
    /// </summary>
    public int FillEntityComponentTypes(Entity entity, List<Type> results)
    {
        return _componentManager.FillEntityComponentTypes(entity, results);
    }

    /// <summary>
    /// 把已经注册过的组件类型写入外部 List。
    /// </summary>
    public int FillRegisteredComponentTypes(List<Type> results)
    {
        return _registry.FillRegisteredTypes(results);
    }

    /// <summary>
    /// 把当前 ComponentStore 调试信息写入外部 List。
    /// </summary>
    public int FillComponentStoreDebugInfos(List<ComponentStoreDebugInfo> results)
    {
        return _componentManager.FillComponentStoreDebugInfos(results);
    }

    /// <summary>
    /// 把当前 ArcheType 分组调试信息写入外部 List。
    /// </summary>
    public int FillArcheTypeDebugInfos(List<ArcheTypeDebugInfo> results)
    {
        return _archeTypeManager.FillArcheTypeDebugInfos(results);
    }

    /// <summary>
    /// 把指定 ArcheType Mask 下的 Entity 写入外部 List。
    /// </summary>
    public int FillEntitiesByArcheType(ComponentMask256 mask, List<Entity> results)
    {
        return _archeTypeManager.FillEntitiesByArcheType(mask, results);
    }

    /// <summary>
    /// 把指定 ArcheType Mask 对应的组件类型写入外部 List。
    /// </summary>
    public int FillComponentTypesByMask(ComponentMask256 mask, List<Type> results)
    {
        return _registry.FillTypesByMask(mask, results);
    }

    /// <summary>
    /// 把当前 System 调试信息按执行顺序写入外部 List。
    /// </summary>
    public int FillSystemDebugInfos(List<SystemDebugInfo> results)
    {
        return _systemManager.FillSystemDebugInfos(results);
    }

    /// <summary>
    /// 把当前 SingletonComponent 映射调试信息写入外部 List。
    /// </summary>
    public int FillSingletonDebugInfos(List<SingletonDebugInfo> results)
    {
        if (results == null)
            return 0;

        results.Clear();

        if (_singletonEntities == null)
            return 0;

        foreach (KeyValuePair<Type, Entity> pair in _singletonEntities)
        {
            bool isAlive = _entityManager.IsAlive(pair.Value);
            results.Add(new SingletonDebugInfo(pair.Key, pair.Value, isAlive));
        }

        return results.Count;
    }

    /// <summary>
    /// 把当前 SingletonComponent 类型写入外部 List。
    /// </summary>
    public int FillSingletonTypes(List<Type> results)
    {
        if (results == null)
            return 0;

        results.Clear();

        if (_singletonEntities == null)
            return 0;

        foreach (KeyValuePair<Type, Entity> pair in _singletonEntities)
        {
            if (_entityManager.IsAlive(pair.Value))
                results.Add(pair.Key);
        }

        return results.Count;
    }

    /// <summary>
    /// 把当前 WorldEvent 缓冲区调试信息写入外部 List。
    /// </summary>
    public int FillWorldEventDebugInfos(List<WorldEventDebugInfo> results)
    {
        return _worldEventBuffer.FillDebugInfos(results);
    }

    /// <summary>
    /// 获取当前 World 的只读统计快照。
    /// 该方法不会修改 World 状态，主要用于 Debug、测试、性能观测和 Editor 面板展示。
    /// </summary>
    public WorldStatistics GetStatistics()
    {
        return new WorldStatistics(
            CreatedEntityCount,
            AliveEntityCount,
            FreeEntityCount,
            ComponentStoreCount,
            ArcheTypeCount,
            QueryCacheCount,
            ArcheTypeVersion,
            SystemCount,
            SingletonCount,
            PendingCommandCount,
            PendingSystemCommandCount,
            _currentState
        );
    }
}

/// <summary>
/// World 生命周期阶段。
/// </summary>
public enum WorldStates
{
    /// <summary>
    /// 初始化或空闲阶段；允许立即创建 Entity、添加组件和调整 System。
    /// </summary>
    Initialization = 0,

    /// <summary>
    /// System 正在 Tick；新增/移除组件、销毁 Entity 等结构变化会进入 StructuralChangeBuffer。
    /// </summary>
    Ticking = 1,

    /// <summary>
    /// 当前逻辑帧的 System Tick 已结束，正在播放 StructuralChangeBuffer。
    /// </summary>
    AfterTicking = 2,

    /// <summary>
    /// 正在播放 SystemChangeBuffer；用于统一处理 System 增删。
    /// </summary>
    SystemOperating = 3,

    /// <summary>
    /// World 正在释放或已经释放；对外修改请求会被忽略。
    /// </summary>
    Disposing = 4,
}

/// <summary>
/// World 内部用于判断操作是否允许立即执行的修改类型。
/// </summary>
/// <remarks>
/// 名称 ExcuteType 保留当前代码命名，后续若统一重命名为 ExecuteType，需要同步修改所有调用点。
/// </remarks>
internal enum ExcuteType
{
    /// <summary>不改变结构的普通操作。</summary>
    Default = 0,

    /// <summary>新增组件或新增 System。</summary>
    Add = 1,

    /// <summary>移除组件或移除 System。</summary>
    Remove = 2,

    /// <summary>销毁 Entity。</summary>
    DestroyEntity = 3,
}


}
