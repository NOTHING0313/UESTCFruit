namespace FrameWork.RollBackSystem
{
    public readonly struct InputComparisonResult
    {
        public readonly bool IsDifferent;

        public readonly int Frame;

        public InputComparisonResult(
            bool isDifferent,
            int frame)
        {
            IsDifferent = isDifferent;
            Frame = frame;
        }
    }
}