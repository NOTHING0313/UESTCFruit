/*
 * 文件说明：DeadCleanupSystem 清理带有 DeadTagComponent 的实体。
 * 设计约束：销毁实体属于结构变化，Tick 中调用会进入 StructuralChangeBuffer。
 */

using System.Collections.Generic;

/// <summary>
/// 清理带有 DeadTagComponent 的实体。
/// </summary>
public sealed class DeadCleanupSystem : FixedStepSystemBase
{
    private readonly List<EntityInfo> _deadEntities = new List<EntityInfo>(128);
    private EntityQueryDescription _deadQuery;

    public override SystemTickSequence sequence => SystemTickSequence.cleanup;

    /// <summary>System 创建时构建并缓存死亡实体查询条件。</summary>
    protected override void OnSystemCreate()
    {
        _deadQuery = World.Query().With<DeadTagComponent>().BuildDescription();
    }

    /// <summary>按稳定顺序请求销毁所有死亡实体。</summary>
    public override void Tick(in SimulationContext context)
    {
        World.FillQuery(_deadQuery, _deadEntities, true);

        for (int i = 0; i < _deadEntities.Count; i++)
            World.DestroyEntity(_deadEntities[i]);
    }

    /// <summary>释放系统持有的临时结果容器。</summary>
    protected override void OnSystemDestroy()
    {
        _deadEntities.Clear();
    }
}
