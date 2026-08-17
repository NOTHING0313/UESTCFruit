using ECSFrameWork;
using System;
using System.Collections.Generic;

namespace FrameWork.RollBackSystem
{
    /// <summary>
    /// 收集单个逻辑帧已经到达的真实玩家输入。
    /// </summary>
    public sealed class FrameInputAccumulator
    {
        private readonly Dictionary<int, PlayerInputSnapshot> _inputs = new();
        private readonly PlayerInputSnapshotComparer _comparer = new();

        public int FrameNumber { get; }
        public int Count => _inputs.Count;

        public FrameInputAccumulator(int frameNumber)
        {
            if (frameNumber <= 0) throw new ArgumentOutOfRangeException(nameof(frameNumber), frameNumber, "Frame Number Must Be Greater Than Zero");
            FrameNumber = frameNumber;
        }

        /// <summary>
        /// 添加真实输入。完全相同的重复包视为幂等；同玩家同帧内容冲突直接拒绝。
        /// </summary>
        public bool TryAddInput(in PlayerInputSnapshot input)
        {
            if (input.frameNumber != FrameNumber)
                throw new ArgumentException($"Frame Input Accumulator Frame Mismatch: Expected={FrameNumber}, PlayerID={input.playerID}, Actual={input.frameNumber}", nameof(input));

            if (_inputs.TryGetValue(input.playerID, out PlayerInputSnapshot existing))
            {
                if (_comparer.IsEqual(existing, input)) return false;

                throw new InvalidOperationException(
                    $"Conflicting Player Input: Frame={FrameNumber}, PlayerID={input.playerID}");
            }

            _inputs.Add(input.playerID, input);
            return true;
        }

        /// <summary>获取指定玩家已经到达的真实输入。</summary>
        public bool TryGetInput(int playerID, out PlayerInputSnapshot input) => _inputs.TryGetValue(playerID, out input);

        internal void FillPlayerIDs(List<int> output)
        {
            output.Clear();
            foreach (int playerID in _inputs.Keys) output.Add(playerID);
        }
    }
}