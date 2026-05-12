using System;

namespace ECSFrameWork
{
/// <summary>
/// 帧命令调试摘要工具；集中处理命令名称、目标 Entity 和摘要生成。
/// </summary>
internal static class CommandDebugUtility
{
    public static CommandDebugRecord CreateExecutionRecord(ISimulationFrameCommand command, int frameNumber, SimulationFrameCommandTiming timing, CommandExecuteStatus status, string message, bool isReplay)
    {
        GetCommandDebugInfo(command, out string name, out Entity target, out string summary);
        return new CommandDebugRecord(frameNumber, timing, name, target, status, summary, message, isReplay);
    }

    public static FrameCommandHistoryRecord CreateHistoryRecord(ISimulationFrameCommand command, SimulationFrameCommandTiming timing)
    {
        GetCommandDebugInfo(command, out string name, out Entity target, out string summary);
        int frameNumber = command != null ? command.FrameNumber : -1;
        return new FrameCommandHistoryRecord(frameNumber, timing, name, target, summary);
    }

    private static void GetCommandDebugInfo(ISimulationFrameCommand command, out string name, out Entity target, out string summary)
    {
        target = Entity.Invalid;
        summary = string.Empty;

        if (command == null)
        {
            name = "NullCommand";
            summary = "Command is null.";
            return;
        }

        ICommandDebugView debugView = command as ICommandDebugView;
        if (debugView != null)
        {
            name = string.IsNullOrEmpty(debugView.DebugName) ? command.GetType().Name : debugView.DebugName;
            target = debugView.DebugTargetEntity;
            summary = SafeGetSummary(debugView);
            return;
        }

        Type type = command.GetType();
        name = type.Name;
        summary = $"Frame={command.FrameNumber}, Type={type.FullName}";
    }

    private static string SafeGetSummary(ICommandDebugView debugView)
    {
        try
        {
            return debugView.GetDebugSummary() ?? string.Empty;
        }
        catch (Exception exception)
        {
            return $"<summary failed: {exception.GetType().Name}>";
        }
    }
}
}
