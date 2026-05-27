/*
 * 文件说明：FrameChecksum 表示某个逻辑帧对应的状态校验值。
 * 设计约束：Frame 与 Checksum 必须严格绑定，用于检测逻辑同步一致性。
 */

namespace FrameWork.RollBackSystem
{
    public readonly struct FrameChecksum
    {
        public readonly int Frame;

        public readonly uint Value;

        public FrameChecksum(
            int frame,
            uint value)
        {
            Frame = frame;

            Value = value;
        }
    }
}