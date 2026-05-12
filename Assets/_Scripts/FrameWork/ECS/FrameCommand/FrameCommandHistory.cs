using System.Collections.Generic;

namespace ECSFrameWork
{
/// <summary>
/// 按逻辑帧保存最近一段时间的原始命令，供 EditorWindow 和联调工具观察最近一段时间内被加入 Buffer 的帧命令。
/// </summary>
public sealed class FrameCommandHistory
{
    private sealed class FrameCommandLists
    {
        public readonly List<ISimulationFrameCommand> beforeTickCommands = new List<ISimulationFrameCommand>(4);
        public readonly List<ISimulationFrameCommand> afterTickCommands = new List<ISimulationFrameCommand>(4);
    }

    private readonly Dictionary<int, FrameCommandLists> _commandsByFrame = new Dictionary<int, FrameCommandLists>();
    private readonly List<int> _frameNumbers = new List<int>(128);
    private readonly List<int> _removeFrames = new List<int>(16);
    private int _historyFrameCapacity;
    private int _commandCount;

    public int HistoryFrameCapacity
    {
        get => _historyFrameCapacity;
        set
        {
            _historyFrameCapacity = value > 0 ? value : 1;
            TrimToCapacity();
        }
    }

    public int FrameCount => _commandsByFrame.Count;
    public int CommandCount => _commandCount;

    public FrameCommandHistory(int historyFrameCapacity = 256)
    {
        _historyFrameCapacity = historyFrameCapacity > 0 ? historyFrameCapacity : 1;
    }

    /// <summary>记录一条已记录帧命令。</summary>
    public void Record(ISimulationFrameCommand command, SimulationFrameCommandTiming timing)
    {
        if (command == null || command.FrameNumber <= 0)
            return;

        FrameCommandLists lists = GetOrCreateFrame(command.FrameNumber);
        GetList(lists, timing).Add(command);
        _commandCount++;
        TrimToCapacity();
    }

    /// <summary>尝试读取指定逻辑帧、指定时机的已记录帧命令。</summary>
    public bool TryGetCommands(int frameNumber, SimulationFrameCommandTiming timing, out IReadOnlyList<ISimulationFrameCommand> commands)
    {
        commands = null;

        if (!_commandsByFrame.TryGetValue(frameNumber, out FrameCommandLists lists))
            return false;

        List<ISimulationFrameCommand> target = GetList(lists, timing);
        if (target.Count == 0)
            return false;

        commands = target;
        return true;
    }

    /// <summary>统计不早于指定帧的历史命令数量，通常用于 Debugger 显示待执行或未来帧命令。</summary>
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

    /// <summary>把最近的帧命令历史帧按新到旧写入 results。</summary>
    public int FillDebugFrames(List<FrameCommandHistoryFrameDebugInfo> results)
    {
        if (results == null)
            return 0;

        results.Clear();

        for (int i = _frameNumbers.Count - 1; i >= 0; i--)
        {
            int frameNumber = _frameNumbers[i];
            if (!_commandsByFrame.TryGetValue(frameNumber, out FrameCommandLists lists))
                continue;

            int beforeCount = lists.beforeTickCommands.Count;
            int afterCount = lists.afterTickCommands.Count;
            FrameCommandHistoryRecord[] records = new FrameCommandHistoryRecord[beforeCount + afterCount];
            int index = 0;

            for (int j = 0; j < lists.beforeTickCommands.Count; j++)
                records[index++] = CommandDebugUtility.CreateHistoryRecord(lists.beforeTickCommands[j], SimulationFrameCommandTiming.BeforeTick);

            for (int j = 0; j < lists.afterTickCommands.Count; j++)
                records[index++] = CommandDebugUtility.CreateHistoryRecord(lists.afterTickCommands[j], SimulationFrameCommandTiming.AfterTick);

            results.Add(new FrameCommandHistoryFrameDebugInfo(frameNumber, beforeCount, afterCount, records));
        }

        return results.Count;
    }

    /// <summary>移除指定逻辑帧之前的帧命令历史。</summary>
    public void RemoveBefore(int frameNumber)
    {
        _removeFrames.Clear();

        for (int i = 0; i < _frameNumbers.Count; i++)
        {
            int cachedFrame = _frameNumbers[i];
            if (cachedFrame < frameNumber)
                _removeFrames.Add(cachedFrame);
        }

        RemoveFrames(_removeFrames);
        _removeFrames.Clear();
    }

    /// <summary>清空全部帧命令历史。</summary>
    public void Clear()
    {
        _commandsByFrame.Clear();
        _frameNumbers.Clear();
        _removeFrames.Clear();
        _commandCount = 0;
    }

    private FrameCommandLists GetOrCreateFrame(int frameNumber)
    {
        if (!_commandsByFrame.TryGetValue(frameNumber, out FrameCommandLists lists))
        {
            lists = new FrameCommandLists();
            _commandsByFrame.Add(frameNumber, lists);
            _frameNumbers.Add(frameNumber);
            _frameNumbers.Sort();
        }

        return lists;
    }

    private void TrimToCapacity()
    {
        if (_frameNumbers.Count <= _historyFrameCapacity)
            return;

        _removeFrames.Clear();
        int removeCount = _frameNumbers.Count - _historyFrameCapacity;
        for (int i = 0; i < removeCount; i++)
            _removeFrames.Add(_frameNumbers[i]);

        RemoveFrames(_removeFrames);
        _removeFrames.Clear();
    }

    private void RemoveFrames(List<int> removeFrames)
    {
        if (removeFrames == null || removeFrames.Count == 0)
            return;

        for (int i = 0; i < removeFrames.Count; i++)
        {
            int frameNumber = removeFrames[i];
            if (_commandsByFrame.TryGetValue(frameNumber, out FrameCommandLists lists))
                _commandCount -= lists.beforeTickCommands.Count + lists.afterTickCommands.Count;

            _commandsByFrame.Remove(frameNumber);
            _frameNumbers.Remove(frameNumber);
        }

        if (_commandCount < 0)
            _commandCount = 0;
    }

    private static List<ISimulationFrameCommand> GetList(FrameCommandLists lists, SimulationFrameCommandTiming timing)
    {
        return timing == SimulationFrameCommandTiming.BeforeTick ? lists.beforeTickCommands : lists.afterTickCommands;
    }
}
}
