/// <summary>
/// Buff 系统访问目标 Entity 逻辑数据的受限接口。
/// </summary>
public interface IBuffTargetResolver
{
    bool IsAlive(EntityInfo entity);
    bool HasHealth(EntityInfo entity);
    ref HealthComponent GetHealth(EntityInfo entity);
    bool HasPosition(EntityInfo entity);
    ref PositionComponent GetPosition(EntityInfo entity);
    bool HasStat(EntityInfo entity);
    ref StatComponent GetStat(EntityInfo entity);
}
