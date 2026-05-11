using System.Collections.Generic;
using ECSFrameWork;

namespace Contracts
{
    /// <summary>
    /// 表现层读取 World 状态的只读接口。
    /// </summary>
    public interface IWorldViewReader
    {
        bool TryGetViewId(Entity entity, out int viewId);
        bool TryGetPosition(Entity entity, out PositionComponent position);
        bool TryGetHealth(Entity entity, out HealthComponent health);
        IEnumerable<Entity> GetAliveEntities();
    }
}
