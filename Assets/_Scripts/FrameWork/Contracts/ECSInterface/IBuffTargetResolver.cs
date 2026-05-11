using ECSFrameWork;

namespace Contracts
{
    /// <summary>
    /// Buff 系统访问目标 Entity 逻辑数据的受限接口。
    /// </summary>
    public interface IBuffTargetResolver
    {
        bool IsAlive(Entity entity);
        bool HasHealth(Entity entity);
        ref HealthComponent GetHealth(Entity entity);
        bool HasPosition(Entity entity);
        ref PositionComponent GetPosition(Entity entity);
        bool HasStat(Entity entity);
        ref StatComponent GetStat(Entity entity);
    }
}
