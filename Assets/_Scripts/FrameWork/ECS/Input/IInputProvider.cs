/*
 * 文件说明：输入快照、输入组件和输入应用器。
 * 设计约束：ECS Core 逻辑应尽量保持确定性；Unity 表现、输入采样、外部指令通过 Adapter 或 Buffer 接入。
 */

/// <summary>
/// 按逻辑帧提供输入快照的接口；本地输入、网络输入和回滚重放都可以实现该接口。
/// </summary>
public interface IInputProvider
{
    /// <summary>尝试获取指定逻辑帧、指定玩家的输入快照。</summary>
    bool TryGetInput(int frameNumber, int playerID, out PlayerInputSnapshot input);
}
