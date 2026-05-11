namespace ECSFrameWork
{
/// <summary>
/// 伤害事件，用于通知表现层播放受击、飘字、音效等一次性反馈。
/// </summary>
public readonly struct DamageWorldEvent : IWorldEvent
{
    /// <summary>事件发生的逻辑帧编号。</summary>
    public int frameNumber { get; }

    /// <summary>伤害来源 Entity；允许为 Entity.Invalid。</summary>
    public readonly Entity source;

    /// <summary>伤害目标 Entity。</summary>
    public readonly Entity target;

    /// <summary>实际应用的伤害数值。</summary>
    public readonly int amount;

    /// <summary>伤害应用后目标剩余生命值。</summary>
    public readonly int remainingHealth;

    /// <summary>创建伤害事件。</summary>
    public DamageWorldEvent(int frameNumber, Entity source, Entity target, int amount, int remainingHealth)
    {
        this.frameNumber = frameNumber;
        this.source = source;
        this.target = target;
        this.amount = amount;
        this.remainingHealth = remainingHealth;
    }
}

}
