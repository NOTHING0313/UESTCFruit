using FrameWork.RollBackSystem.Interfaces;

namespace FrameWork.RollBackSystem
{
    public sealed class SimulationSnapshot
        : ISnapshot
    {
        public int Frame { get; private set; }

        public ISnapshot WorldSnapshot;

        public ISnapshot BuffSnapshot;

        public SimulationSnapshot(
            int frame,
            ISnapshot worldSnapshot,
            ISnapshot buffSnapshot)
        {
            Frame = frame;

            WorldSnapshot = worldSnapshot;

            BuffSnapshot = buffSnapshot;
        }

        public void Release()
        {
            WorldSnapshot?.Release();

            BuffSnapshot?.Release();
        }
    }
}