using System;

namespace ECSFrameWork
{
/// <summary>
/// 帧命令执行状态；用于 DebugCommandHistory 展示，不直接影响命令执行结果。
/// </summary>
public enum CommandExecuteStatus
{
    Pending = 0,
    Executed = 1,
    Skipped = 2,
    Failed = 3
}

/// <summary>
/// 单条命令的执行摘要；只保存 Editor 调试需要的信息，不保存可修改的运行时状态。
/// </summary>
public readonly struct CommandDebugRecord
{
    public readonly int frameNumber;
    public readonly SimulationFrameCommandTiming timing;
    public readonly string commandTypeName;
    public readonly Entity targetEntity;
    public readonly CommandExecuteStatus status;
    public readonly string summary;
    public readonly string message;
    public readonly bool isReplay;

    public CommandDebugRecord(int frameNumber, SimulationFrameCommandTiming timing, string commandTypeName, Entity targetEntity, CommandExecuteStatus status, string summary, string message, bool isReplay)
    {
        this.frameNumber = frameNumber;
        this.timing = timing;
        this.commandTypeName = string.IsNullOrEmpty(commandTypeName) ? "UnknownCommand" : commandTypeName;
        this.targetEntity = targetEntity;
        this.status = status;
        this.summary = summary ?? string.Empty;
        this.message = message ?? string.Empty;
        this.isReplay = isReplay;
    }

    public override string ToString()
    {
        return $"[{status}] Frame={frameNumber}, Timing={timing}, Command={commandTypeName}, Target={targetEntity}, Replay={isReplay}, Summary={summary}, Message={message}";
    }
}

/// <summary>
/// 某一逻辑帧的命令执行摘要集合。
/// </summary>
public readonly struct CommandDebugFrame
{
    public readonly int frameNumber;
    public readonly CommandDebugRecord[] records;
    public readonly int recordCount;
    public readonly int executedCount;
    public readonly int skippedCount;
    public readonly int failedCount;
    public readonly int replayCount;

    public CommandDebugFrame(int frameNumber, CommandDebugRecord[] records)
    {
        this.frameNumber = frameNumber;
        this.records = records ?? Array.Empty<CommandDebugRecord>();

        int executed = 0;
        int skipped = 0;
        int failed = 0;
        int replay = 0;

        for (int i = 0; i < this.records.Length; i++)
        {
            CommandDebugRecord record = this.records[i];
            if (record.status == CommandExecuteStatus.Executed)
                executed++;
            else if (record.status == CommandExecuteStatus.Skipped)
                skipped++;
            else if (record.status == CommandExecuteStatus.Failed)
                failed++;

            if (record.isReplay)
                replay++;
        }

        recordCount = this.records.Length;
        executedCount = executed;
        skippedCount = skipped;
        failedCount = failed;
        replayCount = replay;
    }
}

/// <summary>
/// 帧命令历史中的单条原始命令摘要；用于 EditorWindow 展示最近一段时间内加入 Buffer 的命令。
/// </summary>
public readonly struct FrameCommandHistoryRecord
{
    public readonly int frameNumber;
    public readonly SimulationFrameCommandTiming timing;
    public readonly string commandTypeName;
    public readonly Entity targetEntity;
    public readonly string summary;

    public FrameCommandHistoryRecord(int frameNumber, SimulationFrameCommandTiming timing, string commandTypeName, Entity targetEntity, string summary)
    {
        this.frameNumber = frameNumber;
        this.timing = timing;
        this.commandTypeName = string.IsNullOrEmpty(commandTypeName) ? "UnknownCommand" : commandTypeName;
        this.targetEntity = targetEntity;
        this.summary = summary ?? string.Empty;
    }
}

/// <summary>
/// 某一逻辑帧的帧命令历史摘要。
/// </summary>
public readonly struct FrameCommandHistoryFrameDebugInfo
{
    public readonly int frameNumber;
    public readonly int beforeTickCount;
    public readonly int afterTickCount;
    public readonly int totalCount;
    public readonly FrameCommandHistoryRecord[] commands;

    public FrameCommandHistoryFrameDebugInfo(int frameNumber, int beforeTickCount, int afterTickCount, FrameCommandHistoryRecord[] commands)
    {
        this.frameNumber = frameNumber;
        this.beforeTickCount = beforeTickCount;
        this.afterTickCount = afterTickCount;
        this.totalCount = beforeTickCount + afterTickCount;
        this.commands = commands ?? Array.Empty<FrameCommandHistoryRecord>();
    }
}
}
