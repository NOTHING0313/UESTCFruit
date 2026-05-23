using PoolSystem;

namespace FrameWork.RollBackSystem.Interfaces
{
    public interface ISnapshot : IReference
    {
        int Frame { get; }

        void Release();
    }
}