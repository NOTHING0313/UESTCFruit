namespace FrameWork.RollBackSystem.Interfaces
{
    public interface ISnapshotable<TSnapshot>
        where TSnapshot : ISnapshot
    {
        TSnapshot Capture(int frame);

        void Restore(TSnapshot snapshot);
    }
}