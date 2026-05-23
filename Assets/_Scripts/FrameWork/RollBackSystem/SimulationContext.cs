namespace FrameWork.RollBackSystem
{
    public sealed class SimulationContext
    {
        public int Frame { get; private set; }

        public bool IsRollback { get; private set; }

        public void SetFrame(int frame)
        {
            Frame = frame;
        }

        public void SetRollback(bool isRollback)
        {
            IsRollback = isRollback;
        }
    }
}