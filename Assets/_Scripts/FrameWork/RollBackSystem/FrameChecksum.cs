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