/*
 * 文件说明：InputSnapshotBuffer 按 frameNumber / playerID 保存输入快照，为帧同步、回放和回滚预留输入历史。
 * 设计约束：ECS Core 逻辑应尽量保持确定性；Unity 表现、输入采样、外部指令通过 Adapter 或 Buffer 接入。
 */

using System.Collections.Generic;

/// <summary>
/// 按逻辑帧和玩家编号缓存输入快照，供帧同步、预测和回滚重放使用。
/// </summary>
public sealed class InputSnapshotBuffer : IInputProvider
{
    private readonly Dictionary<int, Dictionary<int, PlayerInputSnapshot>> _inputsByFrame = new Dictionary<int, Dictionary<int, PlayerInputSnapshot>>();
    private readonly List<int> _removeFrames = new List<int>();

    public int FrameCount => _inputsByFrame.Count;

    /// <summary>写入某一逻辑帧、某个玩家的输入快照；同帧同玩家重复写入时以后写入的数据为准。</summary>
    public void SetInput(in PlayerInputSnapshot snapshot)
    {
        if (snapshot.frameNumber <= 0)
            return;

        if (!_inputsByFrame.TryGetValue(snapshot.frameNumber, out Dictionary<int, PlayerInputSnapshot> frameInputs))
        {
            frameInputs = new Dictionary<int, PlayerInputSnapshot>();
            _inputsByFrame.Add(snapshot.frameNumber, frameInputs);
        }

        frameInputs[snapshot.playerID] = snapshot;
    }

    /// <summary>尝试读取指定逻辑帧、指定玩家的输入快照。</summary>
    public bool TryGetInput(int frameNumber, int playerID, out PlayerInputSnapshot input)
    {
        input = default;

        if (!_inputsByFrame.TryGetValue(frameNumber, out Dictionary<int, PlayerInputSnapshot> frameInputs))
            return false;

        return frameInputs.TryGetValue(playerID, out input);
    }

    /// <summary>判断指定逻辑帧、指定玩家是否已有输入快照。</summary>
    public bool HasInput(int frameNumber, int playerID)
    {
        return TryGetInput(frameNumber, playerID, out _);
    }

    /// <summary>移除指定逻辑帧之前的输入历史，用于限制缓存长度。</summary>
    public void RemoveBefore(int frameNumber)
    {
        _removeFrames.Clear();

        foreach (int cachedFrame in _inputsByFrame.Keys)
        {
            if (cachedFrame < frameNumber)
                _removeFrames.Add(cachedFrame);
        }

        for (int i = 0; i < _removeFrames.Count; i++)
        {
            _inputsByFrame.Remove(_removeFrames[i]);
        }

        _removeFrames.Clear();
    }

    /// <summary>清空全部输入历史。</summary>
    public void Clear()
    {
        _inputsByFrame.Clear();
    }
}
