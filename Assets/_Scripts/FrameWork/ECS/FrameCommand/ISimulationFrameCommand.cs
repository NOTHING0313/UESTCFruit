namespace ECSFrameWork
{
/*
 * 文件说明：按逻辑帧记录和消费外部模拟指令。
 * 设计约束：ECS Core 逻辑应尽量保持确定性；Unity 表现、输入采样、外部指令通过 Adapter 或 Buffer 接入。
 */

/// <summary>
/// 可按逻辑帧缓存和重放的外部模拟指令；用于把 Tick 外部的 Entity、Component、System 修改绑定到指定帧、指定时机执行。
/// </summary>
public interface ISimulationFrameCommand
{
    int FrameNumber { get; }

    /// <summary>执行该外部模拟指令。</summary>
    void Execute(World world);
}

}
