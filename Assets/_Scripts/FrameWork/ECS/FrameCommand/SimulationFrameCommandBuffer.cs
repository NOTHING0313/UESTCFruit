/*
 * 文件说明：SimulationFrameCommandBuffer 按 frameNumber 和执行时机保存外部模拟指令。它是外部修改 ECS 的帧同步友好入口。
 * 设计约束：ECS Core 逻辑应尽量保持确定性；Unity 表现、输入采样、外部指令通过 Adapter 或 Buffer 接入。
 */

using System.Collections.Generic;

namespace ECSFrameWork
{

/// <summary>
/// 按逻辑帧和执行时机缓存外部模拟指令，供帧同步和调试回放按帧消费。
/// </summary>
public sealed class SimulationFrameCommandBuffer
{
    private sealed class FrameCommandLists
    {
        public readonly List<ISimulationFrameCommand> beforeTickCommands = new List<ISimulationFrameCommand>();
        public readonly List<ISimulationFrameCommand> afterTickCommands = new List<ISimulationFrameCommand>();
    }

    private readonly Dictionary<int, FrameCommandLists> _commandsByFrame = new Dictionary<int, FrameCommandLists>();
    private readonly List<int> _removeFrames = new List<int>();
    private readonly FrameCommandHistory _commandHistory;
    private int _commandCount;

    public int FrameCount => _commandsByFrame.Count;
    public int CommandCount => _commandCount;
    public FrameCommandHistory CommandHistory => _commandHistory;
    public int CommandHistoryFrameCount => _commandHistory.FrameCount;
    public int CommandHistoryCommandCount => _commandHistory.CommandCount;

    /// <summary>创建帧命令缓冲，并设置帧命令历史最多保留的逻辑帧数量。</summary>
    public SimulationFrameCommandBuffer(int commandHistoryFrameCapacity = 256)
    {
        _commandHistory = new FrameCommandHistory(commandHistoryFrameCapacity);
    }

    /// <summary>加入一条指定逻辑帧开始前执行的外部模拟指令。</summary>
    public void AddCommand(ISimulationFrameCommand command)
    {
        AddCommand(command, SimulationFrameCommandTiming.BeforeTick);
    }

    /// <summary>加入一条指定逻辑帧、指定时机执行的外部模拟指令。</summary>
    public void AddCommand(ISimulationFrameCommand command, SimulationFrameCommandTiming timing)
    {
        if (command == null || command.FrameNumber <= 0)
            return;

        FrameCommandLists lists = GetOrCreateFrameCommandLists(command.FrameNumber);

        if (timing == SimulationFrameCommandTiming.BeforeTick)
            lists.beforeTickCommands.Add(command);
        else
            lists.afterTickCommands.Add(command);

        _commandCount++;
        _commandHistory.Record(command, timing);
    }

    /// <summary>尝试读取指定逻辑帧开始前的外部模拟指令；读取不会移除历史，方便调试回放。</summary>
    public bool TryGetCommands(int frameNumber, out IReadOnlyList<ISimulationFrameCommand> commands)
    {
        return TryGetCommands(frameNumber, SimulationFrameCommandTiming.BeforeTick, out commands);
    }

    /// <summary>尝试读取指定逻辑帧、指定时机的外部模拟指令；读取不会移除历史，方便调试回放。</summary>
    public bool TryGetCommands(int frameNumber, SimulationFrameCommandTiming timing, out IReadOnlyList<ISimulationFrameCommand> commands)
    {
        commands = null;

        if (!_commandsByFrame.TryGetValue(frameNumber, out FrameCommandLists lists))
            return false;

        List<ISimulationFrameCommand> target = timing == SimulationFrameCommandTiming.BeforeTick
            ? lists.beforeTickCommands
            : lists.afterTickCommands;

        if (target.Count == 0)
            return false;

        commands = target;
        return true;
    }

    /// <summary>移除指定逻辑帧之前的指令历史，用于限制缓存长度。</summary>
    public void RemoveBefore(int frameNumber)
    {
        _removeFrames.Clear();

        foreach (int cachedFrame in _commandsByFrame.Keys)
        {
            if (cachedFrame < frameNumber)
                _removeFrames.Add(cachedFrame);
        }

        for (int i = 0; i < _removeFrames.Count; i++)
        {
            int cachedFrame = _removeFrames[i];
            if (_commandsByFrame.TryGetValue(cachedFrame, out FrameCommandLists lists))
                _commandCount -= lists.beforeTickCommands.Count + lists.afterTickCommands.Count;

            _commandsByFrame.Remove(cachedFrame);
        }

        if (_commandCount < 0)
            _commandCount = 0;

        _commandHistory.RemoveBefore(frameNumber);
        _removeFrames.Clear();
    }

    /// <summary>清空全部外部模拟指令历史。</summary>
    public void Clear()
    {
        _commandsByFrame.Clear();
        _commandCount = 0;
        _commandHistory.Clear();
    }

    /// <summary>把最近的帧命令历史按新到旧写入外部 List，供 EditorWindow 展示。</summary>
    public int FillFrameCommandHistoryDebugFrames(List<FrameCommandHistoryFrameDebugInfo> results)
    {
        return _commandHistory.FillDebugFrames(results);
    }

    /// <summary>统计不早于指定帧的缓存命令数量。</summary>
    public int CountCommandsFromFrame(int minFrameInclusive)
    {
        int count = 0;
        foreach (KeyValuePair<int, FrameCommandLists> pair in _commandsByFrame)
        {
            if (pair.Key < minFrameInclusive)
                continue;

            count += pair.Value.beforeTickCommands.Count;
            count += pair.Value.afterTickCommands.Count;
        }

        return count;
    }

    private FrameCommandLists GetOrCreateFrameCommandLists(int frameNumber)
    {
        if (!_commandsByFrame.TryGetValue(frameNumber, out FrameCommandLists lists))
        {
            lists = new FrameCommandLists();
            _commandsByFrame.Add(frameNumber, lists);
        }

        return lists;
    }
}

}
