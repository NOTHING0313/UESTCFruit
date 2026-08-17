using ECSFrameWork;
using System;

namespace FrameWork.RollBackSystem
{
    /// <summary>
    /// 单个逻辑帧内全部玩家输入的确定性集合，按 PlayerID 升序存储。
    /// </summary>
    public readonly struct FrameInputSet
    {
        private readonly PlayerInputSnapshot[] _inputs;

        public readonly int frameNumber;
        public int Count => _inputs?.Length ?? 0;
        public bool IsCreated => _inputs != null;

        /// <summary>
        /// 创建帧输入集合。inputs 所有权转交给 FrameInputSet，构造后调用方不得继续修改该数组。
        /// </summary>
        public FrameInputSet(int frameNumber, PlayerInputSnapshot[] inputs)
        {
            if (frameNumber <= 0) throw new ArgumentOutOfRangeException(nameof(frameNumber), frameNumber, "Frame Number Must Be Greater Than Zero");
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));
            if (inputs.Length == 0) throw new ArgumentException("Frame Input Set Must Contain At Least One Player Input", nameof(inputs));

            SortByPlayerID(inputs);

            for (int i = 0; i < inputs.Length; i++)
            {
                if (inputs[i].frameNumber != frameNumber)
                    throw new ArgumentException($"Player Input Frame Mismatch: Expected={frameNumber}, PlayerID={inputs[i].playerID}, Actual={inputs[i].frameNumber}", nameof(inputs));

                if (i > 0 && inputs[i - 1].playerID == inputs[i].playerID)
                    throw new ArgumentException($"Duplicate Player Input: PlayerID={inputs[i].playerID}", nameof(inputs));
            }

            this.frameNumber = frameNumber;
            _inputs = inputs;
        }

        /// <summary>按确定性顺序获取指定索引的玩家输入。</summary>
        public PlayerInputSnapshot GetInputAt(int index)
        {
            if (_inputs == null) throw new InvalidOperationException("Frame Input Set Is Not Created");
            if ((uint)index >= (uint)_inputs.Length) throw new ArgumentOutOfRangeException(nameof(index));
            return _inputs[index];
        }

        /// <summary>按 PlayerID 查找玩家输入。</summary>
        public bool TryGetInput(int playerID, out PlayerInputSnapshot input)
        {
            if (_inputs == null)
            {
                input = default;
                return false;
            }

            int left = 0, right = _inputs.Length - 1;
            while (left <= right)
            {
                int mid = left + ((right - left) >> 1);
                int current = _inputs[mid].playerID;

                if (current == playerID)
                {
                    input = _inputs[mid];
                    return true;
                }

                if (current < playerID) left = mid + 1;
                else right = mid - 1;
            }

            input = default;
            return false;
        }

        private static void SortByPlayerID(PlayerInputSnapshot[] inputs)
        {
            for (int i = 1; i < inputs.Length; i++)
            {
                PlayerInputSnapshot value = inputs[i];
                int j = i - 1;

                while (j >= 0 && inputs[j].playerID > value.playerID)
                {
                    inputs[j + 1] = inputs[j];
                    j--;
                }

                inputs[j + 1] = value;
            }
        }
    }
}