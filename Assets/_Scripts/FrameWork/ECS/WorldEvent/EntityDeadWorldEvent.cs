namespace ECSFrameWork
{
/// <summary>
/// Entity 死亡事件，用于通知表现层播放死亡动画、爆炸、掉落等一次性反馈。
/// </summary>
public readonly struct EntityDeadWorldEvent : IWorldEvent
{
    /// <summary>事件发生的逻辑帧编号。</summary>
    public int frameNumber { get; }

    /// <summary>死亡的 Entity。</summary>
    public readonly Entity entity;

    /// <summary>创建 Entity 死亡事件。</summary>
    public EntityDeadWorldEvent(int frameNumber, Entity entity)
    {
        this.frameNumber = frameNumber;
        this.entity = entity;
    }
}

}
