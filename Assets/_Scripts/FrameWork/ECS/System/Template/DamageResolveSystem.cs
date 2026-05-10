/*
 * 文件说明：DamageResolveSystem 处理 DamageRequestComponent，把伤害应用到目标 HealthComponent。
 * 设计约束：伤害结算影响逻辑结果，因此使用稳定排序查询。
 */

using System.Collections.Generic;

/// <summary>
/// 处理 DamageRequestComponent，把伤害应用到目标 HealthComponent。
/// </summary>
public sealed class DamageResolveSystem : FixedStepSystemBase
{
    private readonly List<EntityInfo> _requests = new List<EntityInfo>(128);
    private EntityQueryDescription _damageRequestQuery;

    public override SystemTickSequence sequence => SystemTickSequence.damage;

    /// <summary>System 创建时构建并缓存伤害请求查询条件。</summary>
    protected override void OnSystemCreate()
    {
        _damageRequestQuery = World.Query().With<DamageRequestComponent>().BuildDescription();
    }

    /// <summary>按稳定顺序处理当前帧所有伤害请求实体。</summary>
    public override void Tick(in SimulationContext context)
    {
        World.FillQuery(_damageRequestQuery, _requests, true);

        for (int i = 0; i < _requests.Count; i++)
        {
            EntityInfo requestEntity = _requests[i];
            ref DamageRequestComponent request = ref World.GetComponent<DamageRequestComponent>(requestEntity);

            ApplyDamage(in request, context.frameNumber);
            World.DestroyEntity(requestEntity);
        }
    }

    /// <summary>把单个伤害请求应用到目标实体。</summary>
    private void ApplyDamage(in DamageRequestComponent request, int frameNumber)
    {
        if (!World.IsAlive(request.target))
            return;

        if (!World.HasComponent<HealthComponent>(request.target))
            return;

        int damage = request.amount < 0 ? 0 : request.amount;

        ref HealthComponent health = ref World.GetComponent<HealthComponent>(request.target);
        health.current -= damage;

        if (health.current < 0)
            health.current = 0;

        World.AddWorldEvent(new DamageWorldEvent(frameNumber, request.source, request.target, damage, health.current));

        if (health.current <= 0 && !World.HasComponent<DeadTagComponent>(request.target))
        {
            World.SetComponent(request.target, new DeadTagComponent());
            World.AddWorldEvent(new EntityDeadWorldEvent(frameNumber, request.target));
        }
    }

    /// <summary>释放系统持有的临时结果容器。</summary>
    protected override void OnSystemDestroy()
    {
        _requests.Clear();
    }
}
