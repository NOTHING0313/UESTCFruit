namespace FrameWork.RollBackSystem.Interfaces
{
    public interface ISnapshot
    {
        int Frame { get; }

        void Release();
    }
}