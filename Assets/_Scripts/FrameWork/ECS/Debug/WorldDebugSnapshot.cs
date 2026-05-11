using System;

namespace ECSFrameWork
{
/// <summary>
/// World 调试总览快照。
/// 该结构只用于 Debug、测试和 Editor 面板展示，不参与 ECS 逻辑状态。
/// </summary>
public readonly struct WorldDebugSnapshot
{
    /// <summary>基础统计信息。</summary>
    public readonly WorldStatistics statistics;

    /// <summary>EntityData 底层数组容量。</summary>
    public readonly int entityCapacity;

    /// <summary>已经注册过的组件类型数量。</summary>
    public readonly int componentTypeCount;

    /// <summary>当前已经创建的 ComponentStore 数量。</summary>
    public readonly int componentStoreCount;

    /// <summary>当前 ArcheType 分组数量。</summary>
    public readonly int archeTypeCount;

    /// <summary>当前 Query 缓存数量。</summary>
    public readonly int queryCacheCount;

    /// <summary>当前 System 数量。</summary>
    public readonly int systemCount;

    /// <summary>当前 SingletonComponent 数量。</summary>
    public readonly int singletonCount;

    /// <summary>当前 WorldEvent 类型数量。</summary>
    public readonly int worldEventTypeCount;

    /// <summary>当前 WorldEvent 总数量。</summary>
    public readonly int worldEventCount;

    /// <summary>当前待播放的结构变化命令数量。</summary>
    public readonly int pendingStructuralChangeCount;

    /// <summary>当前待播放的 System 变化命令数量。</summary>
    public readonly int pendingSystemChangeCount;

    /// <summary>World 当前生命周期状态。</summary>
    public readonly WorldStates currentState;

    /// <summary>创建 World 调试总览快照。</summary>
    public WorldDebugSnapshot(WorldStatistics statistics, int entityCapacity, int componentTypeCount, int componentStoreCount, int archeTypeCount, int queryCacheCount, int systemCount, int singletonCount, int worldEventTypeCount, int worldEventCount, int pendingStructuralChangeCount, int pendingSystemChangeCount, WorldStates currentState)
    {
        this.statistics = statistics;
        this.entityCapacity = entityCapacity;
        this.componentTypeCount = componentTypeCount;
        this.componentStoreCount = componentStoreCount;
        this.archeTypeCount = archeTypeCount;
        this.queryCacheCount = queryCacheCount;
        this.systemCount = systemCount;
        this.singletonCount = singletonCount;
        this.worldEventTypeCount = worldEventTypeCount;
        this.worldEventCount = worldEventCount;
        this.pendingStructuralChangeCount = pendingStructuralChangeCount;
        this.pendingSystemChangeCount = pendingSystemChangeCount;
        this.currentState = currentState;
    }

    /// <summary>返回便于 Debug.Log 查看的一行统计文本。</summary>
    public override string ToString()
    {
        return $"WorldDebugSnapshot(State={currentState}, EntityCapacity={entityCapacity}, ComponentTypes={componentTypeCount}, Stores={componentStoreCount}, ArcheTypes={archeTypeCount}, Queries={queryCacheCount}, Systems={systemCount}, Singletons={singletonCount}, EventTypes={worldEventTypeCount}, Events={worldEventCount}, StructuralChanges={pendingStructuralChangeCount}, SystemChanges={pendingSystemChangeCount})";
    }
}

/// <summary>
/// 单个 Entity 的调试信息。
/// </summary>
public readonly struct EntityDebugInfo
{
    /// <summary>被观测的 Entity。</summary>
    public readonly Entity entity;

    /// <summary>Entity 是否仍然存活且版本有效。</summary>
    public readonly bool isAlive;

    /// <summary>Entity 当前组件 Mask。</summary>
    public readonly ComponentMask256 componentMask;

    /// <summary>Entity 当前拥有的组件数量。</summary>
    public readonly int componentCount;

    /// <summary>创建 Entity 调试信息。</summary>
    public EntityDebugInfo(Entity entity, bool isAlive, ComponentMask256 componentMask, int componentCount)
    {
        this.entity = entity;
        this.isAlive = isAlive;
        this.componentMask = componentMask;
        this.componentCount = componentCount;
    }

    /// <summary>返回便于 Debug.Log 查看的一行统计文本。</summary>
    public override string ToString()
    {
        return $"EntityDebugInfo(Entity={entity}, Alive={isAlive}, Components={componentCount}, Mask={componentMask})";
    }
}

/// <summary>
/// 单个 ArcheType 分组的调试信息。
/// </summary>
public readonly struct ArcheTypeDebugInfo
{
    /// <summary>该 ArcheType 对应的组件组合 Mask。</summary>
    public readonly ComponentMask256 mask;

    /// <summary>该 ArcheType 下的 Entity 数量。</summary>
    public readonly int entityCount;

    /// <summary>该 ArcheType 的组件数量。</summary>
    public readonly int componentCount;

    /// <summary>创建 ArcheType 调试信息。</summary>
    public ArcheTypeDebugInfo(ComponentMask256 mask, int entityCount, int componentCount)
    {
        this.mask = mask;
        this.entityCount = entityCount;
        this.componentCount = componentCount;
    }

    /// <summary>返回便于 Debug.Log 查看的一行统计文本。</summary>
    public override string ToString()
    {
        return $"ArcheTypeDebugInfo(Entities={entityCount}, Components={componentCount}, Mask={mask})";
    }
}

/// <summary>
/// 单个 ComponentStore 的调试信息。
/// </summary>
public readonly struct ComponentStoreDebugInfo
{
    /// <summary>组件类型。</summary>
    public readonly Type componentType;

    /// <summary>组件类型注册 ID，对应 ComponentMask256 中的 bit 下标。</summary>
    public readonly int registerID;

    /// <summary>当前 Store 中的组件实例数量。</summary>
    public readonly int count;

    /// <summary>dense 组件数组容量。</summary>
    public readonly int capacity;

    /// <summary>sparse Entity 索引数组容量。</summary>
    public readonly int sparseCapacity;

    /// <summary>创建 ComponentStore 调试信息。</summary>
    public ComponentStoreDebugInfo(Type componentType, int registerID, int count, int capacity, int sparseCapacity)
    {
        this.componentType = componentType;
        this.registerID = registerID;
        this.count = count;
        this.capacity = capacity;
        this.sparseCapacity = sparseCapacity;
    }

    /// <summary>返回便于 Debug.Log 查看的一行统计文本。</summary>
    public override string ToString()
    {
        string name = componentType != null ? componentType.Name : "UnknownComponent";
        return $"ComponentStoreDebugInfo(Type={name}, ID={registerID}, Count={count}, Capacity={capacity}, SparseCapacity={sparseCapacity})";
    }
}

/// <summary>
/// 单个 System 的调试信息。
/// </summary>
public readonly struct SystemDebugInfo
{
    /// <summary>System 实例类型。</summary>
    public readonly Type systemType;

    /// <summary>System 类型名称。</summary>
    public readonly string name;

    /// <summary>System 执行顺序。</summary>
    public readonly SystemTickSequence sequence;

    /// <summary>System 是否处于启用状态。当前框架尚未实现禁用机制，因此注册后的 System 默认为 true。</summary>
    public readonly bool enabled;

    /// <summary>最近一次 Tick 耗时，单位毫秒。</summary>
    public readonly double lastTickMilliseconds;

    /// <summary>平均 Tick 耗时，单位毫秒。</summary>
    public readonly double averageTickMilliseconds;

    /// <summary>最大 Tick 耗时，单位毫秒。</summary>
    public readonly double maxTickMilliseconds;

    /// <summary>已记录 Tick 次数。</summary>
    public readonly int tickCount;

    /// <summary>创建 System 调试信息。</summary>
    public SystemDebugInfo(Type systemType, string name, SystemTickSequence sequence, bool enabled, double lastTickMilliseconds, double averageTickMilliseconds, double maxTickMilliseconds, int tickCount)
    {
        this.systemType = systemType;
        this.name = string.IsNullOrEmpty(name) ? "UnknownSystem" : name;
        this.sequence = sequence;
        this.enabled = enabled;
        this.lastTickMilliseconds = lastTickMilliseconds;
        this.averageTickMilliseconds = averageTickMilliseconds;
        this.maxTickMilliseconds = maxTickMilliseconds;
        this.tickCount = tickCount;
    }

    /// <summary>返回便于 Debug.Log 查看的一行统计文本。</summary>
    public override string ToString()
    {
        return $"SystemDebugInfo(Name={name}, Sequence={sequence}, Enabled={enabled}, Last={lastTickMilliseconds:F4}ms, Avg={averageTickMilliseconds:F4}ms, Max={maxTickMilliseconds:F4}ms, TickCount={tickCount})";
    }
}

/// <summary>
/// SingletonComponent 映射调试信息。
/// </summary>
public readonly struct SingletonDebugInfo
{
    /// <summary>Singleton 组件类型。</summary>
    public readonly Type componentType;

    /// <summary>承载该 SingletonComponent 的内部 Entity。</summary>
    public readonly Entity entity;

    /// <summary>该 Singleton 映射是否仍然有效。</summary>
    public readonly bool isAlive;

    /// <summary>创建 Singleton 调试信息。</summary>
    public SingletonDebugInfo(Type componentType, Entity entity, bool isAlive)
    {
        this.componentType = componentType;
        this.entity = entity;
        this.isAlive = isAlive;
    }

    /// <summary>返回便于 Debug.Log 查看的一行统计文本。</summary>
    public override string ToString()
    {
        string name = componentType != null ? componentType.Name : "UnknownSingleton";
        return $"SingletonDebugInfo(Type={name}, Entity={entity}, Alive={isAlive})";
    }
}

/// <summary>
/// WorldEvent 缓冲区调试信息。
/// </summary>
public readonly struct WorldEventDebugInfo
{
    /// <summary>WorldEvent 类型。</summary>
    public readonly Type eventType;

    /// <summary>当前缓存的事件数量。</summary>
    public readonly int eventCount;

    /// <summary>当前类型事件中的最早逻辑帧；没有事件时为 -1。</summary>
    public readonly int oldestFrame;

    /// <summary>当前类型事件中的最新逻辑帧；没有事件时为 -1。</summary>
    public readonly int newestFrame;

    /// <summary>创建 WorldEvent 调试信息。</summary>
    public WorldEventDebugInfo(Type eventType, int eventCount, int oldestFrame, int newestFrame)
    {
        this.eventType = eventType;
        this.eventCount = eventCount;
        this.oldestFrame = oldestFrame;
        this.newestFrame = newestFrame;
    }

    /// <summary>返回便于 Debug.Log 查看的一行统计文本。</summary>
    public override string ToString()
    {
        string name = eventType != null ? eventType.Name : "UnknownWorldEvent";
        return $"WorldEventDebugInfo(Type={name}, Count={eventCount}, OldestFrame={oldestFrame}, NewestFrame={newestFrame})";
    }
}
}
