/*
 * 文件说明：InputMoveSystem 把当前逻辑帧的输入组件转换为速度组件。
 * 设计约束：该系统使用 World.ForEach<T1,T2,T3> 直接遍历组件 Store，避免每帧构建 Query 结果 List 和重复 GetComponent 查找。
 */

/// <summary>
/// 根据玩家输入修改实体速度。
/// </summary>
public sealed class InputMoveSystem : FixedStepSystemBase
{
    private readonly EntityComponentAction<PlayerInputComponent, MoveSpeedComponent, VelocityComponent> _inputMoveAction;
    private int _frameNumber;

    public override SystemTickSequence sequence => SystemTickSequence.input;

    /// <summary>创建输入移动系统，并缓存 ForEach 回调委托，避免每帧创建闭包。</summary>
    public InputMoveSystem()
    {
        _inputMoveAction = ApplyInputMove;
    }

    /// <summary>把当前帧有效输入转换为 VelocityComponent。</summary>
    public override void Tick(in SimulationContext context)
    {
        _frameNumber = context.frameNumber;
        World.ForEach(_inputMoveAction);
    }

    /// <summary>把单个实体的输入转换为速度；输入帧无效时清零速度。</summary>
    private void ApplyInputMove(EntityInfo entity, ref PlayerInputComponent input, ref MoveSpeedComponent speed, ref VelocityComponent velocity)
    {
        if (!input.IsValidForFrame(_frameNumber))
        {
            velocity.x = 0f;
            velocity.y = 0f;
            velocity.z = 0f;
            return;
        }

        velocity.x = input.moveX * speed.value;
        velocity.y = 0f;
        velocity.z = input.moveY * speed.value;
    }
}
