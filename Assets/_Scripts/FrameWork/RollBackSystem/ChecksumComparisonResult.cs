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