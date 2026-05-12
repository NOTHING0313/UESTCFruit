using System.Collections.Generic;

namespace ECSFrameWork
{
/// <summary>
/// 保存最近若干逻辑帧的命令执行摘要，供 EditorWindow 观察 DebugCommand。
/// </summary>
public sealed class CommandDebugHistory
{
    private readonly Dictionary<int, List<CommandDebugRecord>> _recordsByFrame = new Dictionary<int, List<CommandDebugRecord>>();
    private readonly List<int> _frameNumbers = new List<int>(128);
    private readonly List<int> _removeFrames = new List<int>(16);
    private int _historyFrameCapacity;
    private int _recordCount;

    public int HistoryFrameCapacity
    {
        get => _historyFrameCapacity;
        set
        {
            _historyFrameCapacity = value > 0 ? value : 1;
            TrimToCapacity();
        }
    }

    public int FrameCount => _recordsByFrame.Count;
    public int RecordCount => _recordCount;

    public CommandDebugHistory(int historyFrameCapacity = 256)
    {
        _historyFrameCapacity = historyFrameCapacity > 0 ? historyFrameCapacity : 1;
    }

    /// <summary>记录一条命令执行摘要。</summary>
    public void Record(in CommandDebugRecord record)
    {
        if (record.frameNumber <= 0)
            return;

        if (!_recordsByFrame.TryGetValue(record.frameNumber, out List<CommandDebugRecord> records))
        {
            records = new List<CommandDebugRecord>(4);
            _recordsByFrame.Add(record.frameNumber, records);
            _frameNumbers.Add(record.frameNumber);
            _frameNumbers.Sort();
        }

        records.Add(record);
        _recordCount++;
        TrimToCapacity();
    }

    /// <summary>把最近的命令执行帧按新到旧写入 results。</summary>
    public int FillRecentFrames(List<CommandDebugFrame> results)
    {
        if (results == null)
            return 0;

        results.Clear();

        for (int i = _frameNumbers.Count - 1; i >= 0; i--)
        {
            int frameNumber = _frameNumbers[i];
            if (!_recordsByFrame.TryGetValue(frameNumber, out List<CommandDebugRecord> records))
                continue;

            results.Add(new CommandDebugFrame(frameNumber, records.ToArray()));
        }

        return results.Count;
    }

    /// <summary>清空全部命令调试摘要。</summary>
    public void Clear()
    {
        _recordsByFrame.Clear();
        _frameNumbers.Clear();
        _removeFrames.Clear();
        _recordCount = 0;
    }

    /// <summary>移除指定逻辑帧之前的命令调试摘要。</summary>
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
            if (_recordsByFrame.TryGetValue(frameNumber, out List<CommandDebugRecord> records))
                _recordCount -= records.Count;

            _recordsByFrame.Remove(frameNumber);
            _frameNumbers.Remove(frameNumber);
        }

        if (_recordCount < 0)
            _recordCount = 0;
    }
}
}
