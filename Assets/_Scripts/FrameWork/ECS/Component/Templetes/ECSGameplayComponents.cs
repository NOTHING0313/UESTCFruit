namespace ECSFrameWork
{
/*
 * 文件说明：ECSGameplayComponents 提供 World Core 交付阶段的最小业务组件，用于验证 Buff、伤害、死亡清理等逻辑闭环。
 * 设计约束：这些组件是示例级基础组件，后续项目可以扩展或替换，但仍建议保持纯数据结构。
 */

/// <summary>
/// 生命值组件。
/// </summary>
public struct HealthComponent : IComponentData
{
    /// <summary>当前生命值。</summary>
    public int current;

    /// <summary>最大生命值。</summary>
    public int max;

    /// <summary>创建生命值组件。</summary>
    public HealthComponent(int current, int max)
    {
        this.current = current;
        this.max = max;
    }
}

/// <summary>
/// 基础属性组件，作为 Buff / 战斗系统的最小接入点。
/// </summary>
public struct StatComponent : IComponentData
{
    /// <summary>攻击力。</summary>
    public int attack;

    /// <summary>防御力。</summary>
    public int defense;

    /// <summary>逻辑移动速度。</summary>
    public int moveSpeed;

    /// <summary>创建基础属性组件。</summary>
    public StatComponent(int attack, int defense, int moveSpeed)
    {
        this.attack = attack;
        this.defense = defense;
        this.moveSpeed = moveSpeed;
    }
}

/// <summary>
/// 死亡标记组件。
/// </summary>
/// <remarks>
/// 当前系统中 DamageResolveSystem 负责添加该标记，DeadCleanupSystem 负责在后续 Tick 中清理持有该标记的 Entity。
/// </remarks>
public struct DeadTagComponent : IComponentData
{
}

/// <summary>
/// 伤害请求组件，通常挂在一次性请求 Entity 上。
/// </summary>
public struct DamageRequestComponent : IComponentData
{
    /// <summary>伤害来源 Entity；允许为 Entity.Invalid。</summary>
    public Entity source;

    /// <summary>伤害目标 Entity。</summary>
    public Entity target;

    /// <summary>伤害数值；小于 0 时会被 DamageResolveSystem 按 0 处理。</summary>
    public int amount;

    /// <summary>创建伤害请求组件。</summary>
    public DamageRequestComponent(Entity source, Entity target, int amount)
    {
        this.source = source;
        this.target = target;
        this.amount = amount;
    }
}

}
