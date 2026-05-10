/*
 * 文件说明：按逻辑帧记录和消费外部模拟指令。
 * 设计约束：ECS Core 逻辑应尽量保持确定性；Unity 表现、输入采样、外部指令通过 Adapter 或 Buffer 接入。
 */

/// <summary>
/// 外部帧指令在指定逻辑帧中的执行时机。
/// </summary>
public enum SimulationFrameCommandTiming
{
    /// <summary>在 World.Tick 之前执行，本帧 System 可以看到修改结果。</summary>
    BeforeTick,

    /// <summary>在 World.Tick 之后执行，本帧 System 看不到，下一帧开始可见。</summary>
    AfterTick,
}
