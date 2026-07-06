/*
 * 文件说明：Rollback 重模拟阶段的帧命令重放内部边界。
 * 设计约束：A1-A6 不创建孤立 FrameCommandApplier；没有真实来源时只报告 skipped。
 */

using ECSFrameWork;

namespace FrameWork.RollBackSystem
{
    internal interface IRollbackFrameCommandReplay
    {
        bool HasFrameCommandSource { get; }

        bool TryReplayFrameCommands(
            SimulationContext context,
            SimulationFrameCommandTiming timing,
            out string message);
    }

    internal interface IRollbackFrameCommandHistoryCleaner
    {
        bool TryRemoveFrameCommandsBefore(int frameNumber, out string message);
    }
}
