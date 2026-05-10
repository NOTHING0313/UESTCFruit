using System.Collections.Generic;

/// <summary>
/// 表现层读取 World 状态的只读接口。
/// </summary>
public interface IWorldViewReader
{
    bool TryGetViewId(EntityInfo entity, out int viewId);
    bool TryGetPosition(EntityInfo entity, out PositionComponent position);
    bool TryGetHealth(EntityInfo entity, out HealthComponent health);
    IEnumerable<EntityInfo> GetAliveEntities();
}
