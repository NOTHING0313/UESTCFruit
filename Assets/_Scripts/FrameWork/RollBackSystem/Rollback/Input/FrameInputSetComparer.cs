using ECSFrameWork;
using FrameWork.RollBackSystem.Interfaces;

namespace FrameWork.RollBackSystem
{
    /// <summary>
    /// 比较预测帧输入集合与权威帧输入集合是否一致。
    /// </summary>
    public sealed class FrameInputSetComparer : IInputComparer<FrameInputSet>
    {
        private readonly PlayerInputSnapshotComparer _inputComparer = new();

        public bool IsEqual(FrameInputSet a, FrameInputSet b)
        {
            if (a.IsCreated != b.IsCreated || a.frameNumber != b.frameNumber || a.Count != b.Count) return false;
            if (!a.IsCreated) return true;

            for (int i = 0; i < a.Count; i++)
            {
                PlayerInputSnapshot inputA = a.GetInputAt(i);
                PlayerInputSnapshot inputB = b.GetInputAt(i);

                if (!_inputComparer.IsEqual(inputA, inputB)) return false;
            }

            return true;
        }
    }
}