namespace FrameWork.RollBackSystem
{
    public sealed class RollbackProcessor
    {
        public bool ShouldRollback(
            InputComparisonResult result)
        {
            return result.IsDifferent;
        }
    }
}