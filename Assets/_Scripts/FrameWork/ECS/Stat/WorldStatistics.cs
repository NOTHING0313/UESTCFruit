namespace ECSFrameWork
{
/// <summary>
/// World 当前运行状态的只读统计快照。
/// 该结构只用于 Debug、测试、性能观测和 Editor 面板展示，不参与 ECS 逻辑。
/// </summary>
public readonly struct WorldStatistics
{
    /// <summary>已经创建过的 Entity 槽位总数，包含已销毁但可复用的 Entity。</summary>
    public readonly int createdEntityCount;

    /// <summary>当前仍然存活的 Entity 数量。</summary>
    public readonly int aliveEntityCount;

    /// <summary>当前进入回收池、可被复用的 Entity ID 数量。</summary>
    public readonly int freeEntityCount;

    /// <summary>当前已经创建的 ComponentStore 数量。</summary>
    public readonly int componentStoreCount;

    /// <summary>当前 ArcheType 分组数量。</summary>
    public readonly int archeTypeCount;

    /// <summary>当前 Query 缓存数量。</summary>
    public readonly int queryCacheCount;

    /// <summary>ArcheTypeManager 当前版本号，用于判断 Query 缓存是否需要刷新。</summary>
    public readonly int archeTypeVersion;

    /// <summary>当前注册的 System 数量。</summary>
    public readonly int systemCount;

    /// <summary>当前 Singleton Component 映射数量。</summary>
    public readonly int singletonCount;

    /// <summary>当前等待播放的结构变化命令数量。</summary>
    public readonly int pendingStructuralChangeCount;

    /// <summary>当前等待播放的 System 变化命令数量。</summary>
    public readonly int pendingSystemChangeCount;

    /// <summary>World 当前生命周期状态。</summary>
    public readonly WorldStates currentState;

    /// <summary>创建 World 统计快照。</summary>
    public WorldStatistics(int createdEntityCount, int aliveEntityCount, int freeEntityCount, int componentStoreCount, int archeTypeCount, int queryCacheCount, int archeTypeVersion, int systemCount, int singletonCount, int pendingStructuralChangeCount, int pendingSystemChangeCount, WorldStates currentState)
    {
        this.createdEntityCount = createdEntityCount;
        this.aliveEntityCount = aliveEntityCount;
        this.freeEntityCount = freeEntityCount;
        this.componentStoreCount = componentStoreCount;
        this.archeTypeCount = archeTypeCount;
        this.queryCacheCount = queryCacheCount;
        this.archeTypeVersion = archeTypeVersion;
        this.systemCount = systemCount;
        this.singletonCount = singletonCount;
        this.pendingStructuralChangeCount = pendingStructuralChangeCount;
        this.pendingSystemChangeCount = pendingSystemChangeCount;
        this.currentState = currentState;
    }

    /// <summary>返回便于 Debug.Log 查看的一行统计文本。</summary>
    public override string ToString()
    {
        return $"WorldStatistics(CreatedEntities={createdEntityCount}, AliveEntities={aliveEntityCount}, FreeEntities={freeEntityCount}, Stores={componentStoreCount}, ArcheTypes={archeTypeCount}, Queries={queryCacheCount}, ArcheTypeVersion={archeTypeVersion}, Systems={systemCount}, Singletons={singletonCount}, StructuralChanges={pendingStructuralChangeCount}, SystemChanges={pendingSystemChangeCount}, State={currentState})";
    }
}

}
