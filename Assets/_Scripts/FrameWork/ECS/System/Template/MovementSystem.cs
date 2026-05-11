namespace ECSFrameWork
{
/*
 * 文件说明：MovementSystem 根据 VelocityComponent 推进 PositionComponent。
 * 设计约束：该系统使用 World.ForEach<T1,T2> 直接遍历组件 Store，避免每帧构建 Query 结果 List 和重复 GetComponent 查找。
 */

/// <summary>
/// 根据 VelocityComponent 推进 PositionComponent 的示例移动系统。
/// </summary>
public class MovementSystem : FixedStepSystemBase
{
    private readonly EntityComponentAction<PositionComponent, VelocityComponent> _moveAction;
    private float _tickLength;

    public override SystemTickSequence sequence => SystemTickSequence.movement;

    /// <summary>创建移动系统，并缓存 ForEach 回调委托，避免每帧创建闭包。</summary>
    public MovementSystem()
    {
        _moveAction = MoveEntity;
    }

    /// <summary>按固定逻辑帧推进所有拥有 Position + Velocity 的实体。</summary>
    public override void Tick(in SimulationContext context)
    {
        _tickLength = context.tickLength;
        World.ForEach(_moveAction);
    }

    /// <summary>移动单个实体，只修改 PositionComponent，不改变 Entity/Component 结构。</summary>
    private void MoveEntity(Entity entity, ref PositionComponent position, ref VelocityComponent velocity)
    {
        position.x += velocity.x * _tickLength;
        position.y += velocity.y * _tickLength;
        position.z += velocity.z * _tickLength;
    }
}

}
