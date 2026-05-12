namespace FrameWork.RollBackSystem
{
    public sealed class RollbackRuntime
    {
        public int LastRollbackFrame;

        public int LastConfirmedFrame;

        public bool IsResimulating;
    }
}