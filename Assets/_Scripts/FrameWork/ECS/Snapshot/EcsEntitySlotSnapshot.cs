namespace ECSFrameWork
{

/// <summary>
/// 单个 EntityData 槽位的快照。
/// </summary>
public sealed class EcsEntitySlotSnapshot
{
    public int Id { get; }
    public int Version { get; }
    public bool IsAlive { get; }

    public EcsEntitySlotSnapshot(int id, int version, bool isAlive)
    {
        Id = id;
        Version = version;
        IsAlive = isAlive;
    }
}

}
