/*
 * 文件说明：ChecksumComparisonResult 表示一次本地与远端状态校验结果。
 * 设计约束：结果对象应保持只读，用于调试、同步检测与回滚决策。
 */

namespace FrameWork.RollBackSystem
{
    public readonly struct ChecksumComparisonResult
    {
        public readonly bool IsMatch;

        public readonly int Frame;

        public readonly uint LocalChecksum;

        public readonly uint RemoteChecksum;

        public ChecksumComparisonResult(
            bool isMatch,
            int frame,
            uint localChecksum,
            uint remoteChecksum)
        {
            IsMatch = isMatch;

            Frame = frame;

            LocalChecksum = localChecksum;

            RemoteChecksum = remoteChecksum;
        }
    }
}