/*
 * 文件说明：SimulationFrameCommandApplier 在指定逻辑帧和时机把 FrameCommandBuffer 中的指令应用到 World。
 * 设计约束：ECS Core 逻辑应尽量保持确定性；Unity 表现、输入采样、外部指令通过 Adapter 或 Buffer 接入。
 */

using System;
using System.Collections.Generic;

namespace ECSFrameWork
{

/// <summary>
/// 从 SimulationFrameCommandBuffer 中读取并执行对应逻辑帧、对应时机的外部模拟指令。
/// </summary>
public sealed class SimulationFrameCommandApplier
{
    private readonly struct AppliedFrameKey : IEquatable<AppliedFrameKey>
    {
        public readonly int frameNumber;
        public readonly SimulationFrameCommandTiming timing;

        public AppliedFrameKey(int frameNumber, SimulationFrameCommandTiming timing)
        {
            this.frameNumber = frameNumber;
            this.timing = timing;
        }

        public bool Equals(AppliedFrameKey other)
        {
            return frameNumber == other.frameNumber && timing == other.timing;
        }

        public override bool Equals(object obj)
        {
            return obj is AppliedFrameKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (frameNumber * 397) ^ (int)timing;
            }
        }
    }

    private readonly World _world;
    private readonly SimulationFrameCommandBuffer _commandBuffer;
    private readonly HashSet<AppliedFrameKey> _appliedFrames = new HashSet<AppliedFrameKey>();
    private readonly CommandDebugHistory _debugHistory;

    public SimulationFrameCommandBuffer CommandBuffer => _commandBuffer;
    public CommandDebugHistory DebugHistory => _debugHistory;
    public int AppliedFrameCount => _appliedFrames.Count;
    public int DebugHistoryFrameCount => _debugHistory.FrameCount;
    public int DebugRecordCount => _debugHistory.RecordCount;

    /// <summary>创建帧指令应用器。</summary>
    public SimulationFrameCommandApplier(World world, SimulationFrameCommandBuffer commandBuffer, int debugHistoryFrameCapacity = 256)
    {
        _world = world;
        _commandBuffer = commandBuffer;
        _debugHistory = new CommandDebugHistory(debugHistoryFrameCapacity);
    }

    /// <summary>执行指定逻辑帧开始前的全部外部模拟指令；普通模拟同一帧同一时机只执行一次。</summary>
    public void ApplyCommandsToWorld(int frameNumber)
    {
        ApplyCommandsToWorld(frameNumber, SimulationFrameCommandTiming.BeforeTick);
    }

    /// <summary>执行指定逻辑帧、指定时机的全部外部模拟指令；普通模拟同一帧同一时机只执行一次。</summary>
    public void ApplyCommandsToWorld(int frameNumber, SimulationFrameCommandTiming timing)
    {
        ApplyCommandsToWorld(frameNumber, timing, false);
    }

    /// <summary>显式重复执行指定逻辑帧、指定时机的全部外部模拟指令；不会记录为普通模拟已消费。</summary>
    public void ReplayCommandsToWorld(int frameNumber, SimulationFrameCommandTiming timing)
    {
        ApplyCommandsToWorld(frameNumber, timing, true);
    }

    /// <summary>清空普通模拟的已应用帧记录；外部重置 World 状态后可调用它重新对齐已应用状态。</summary>
    public void ClearAppliedHistory()
    {
        _appliedFrames.Clear();
    }

    /// <summary>移除指定逻辑帧之前的已应用记录，用于限制普通模拟历史长度。</summary>
    public void RemoveAppliedBefore(int frameNumber)
    {
        _appliedFrames.RemoveWhere(key => key.frameNumber < frameNumber);
        _debugHistory.RemoveBefore(frameNumber);
    }

    /// <summary>把最近的命令执行历史按新到旧写入外部 List，供 EditorWindow 展示。</summary>
    public int FillCommandDebugFrames(List<CommandDebugFrame> results)
    {
        return _debugHistory.FillRecentFrames(results);
    }

    /// <summary>清空命令调试历史；不会清空原始命令缓冲或回滚历史。</summary>
    public void ClearCommandDebugHistory()
    {
        _debugHistory.Clear();
    }

    private void ApplyCommandsToWorld(int frameNumber, SimulationFrameCommandTiming timing, bool forceReplay)
    {
        if (_world == null || _commandBuffer == null || frameNumber <= 0)
            return;

        if (!_commandBuffer.TryGetCommands(frameNumber, timing, out IReadOnlyList<ISimulationFrameCommand> commands))
            return;

        AppliedFrameKey key = new AppliedFrameKey(frameNumber, timing);

        if (!forceReplay && _appliedFrames.Contains(key))
        {
            RecordSkippedCommands(commands, frameNumber, timing, "Frame timing has already been applied.", false);
            return;
        }

        for (int i = 0; i < commands.Count; i++)
            ExecuteCommand(commands[i], frameNumber, timing, forceReplay);

        if (!forceReplay)
            _appliedFrames.Add(key);
    }

    private void ExecuteCommand(ISimulationFrameCommand command, int frameNumber, SimulationFrameCommandTiming timing, bool forceReplay)
    {
        if (command == null)
        {
            CommandDebugRecord skipped = CommandDebugUtility.CreateExecutionRecord(command, frameNumber, timing, CommandExecuteStatus.Skipped, "Command is null.", forceReplay);
            _debugHistory.Record(in skipped);
            return;
        }

        try
        {
            command.Execute(_world);
            CommandDebugRecord executed = CommandDebugUtility.CreateExecutionRecord(command, frameNumber, timing, CommandExecuteStatus.Executed, string.Empty, forceReplay);
            _debugHistory.Record(in executed);
        }
        catch (Exception exception)
        {
            CommandDebugRecord failed = CommandDebugUtility.CreateExecutionRecord(command, frameNumber, timing, CommandExecuteStatus.Failed, exception.Message, forceReplay);
            _debugHistory.Record(in failed);
            throw;
        }
    }

    private void RecordSkippedCommands(IReadOnlyList<ISimulationFrameCommand> commands, int frameNumber, SimulationFrameCommandTiming timing, string message, bool forceReplay)
    {
        if (commands == null)
            return;

        for (int i = 0; i < commands.Count; i++)
        {
            CommandDebugRecord skipped = CommandDebugUtility.CreateExecutionRecord(commands[i], frameNumber, timing, CommandExecuteStatus.Skipped, message, forceReplay);
            _debugHistory.Record(in skipped);
        }
    }
}

}
