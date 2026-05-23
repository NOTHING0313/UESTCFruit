using FrameWork.RollBackSystem.Interfaces;
using PoolSystem;

namespace FrameWork.RollBackSystem
{
    public sealed class SimulationSnapshot
        : ISnapshot,
          IReference<SimulationSnapshot>
    {
        public int Frame { get; private set; }

        public ISnapshot WorldSnapshot;

        public ISnapshot BuffSnapshot;

        int IReference.IndexInReferencePool
        {
            get;
            set;
        }

        public void Initialize(
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

            ReferencePoolCenter.Instance
                .ReleaseReference(this);
        }

        void IReference.OnRecycle()
        {
            Frame = 0;

            WorldSnapshot = null;

            BuffSnapshot = null;
        }

        IReference IReference.GetNewInstance()
        {
            return new SimulationSnapshot();
        }

        void System.IDisposable.Dispose()
        {
        }
        public static SimulationSnapshot Create(
    int frame,
    ISnapshot worldSnapshot,
    ISnapshot buffSnapshot)
        {
            var snapshot =
                ReferencePoolCenter.Instance
                    .GetReference<SimulationSnapshot>();

            snapshot.Initialize(
                frame,
                worldSnapshot,
                buffSnapshot);

            return snapshot;
        }
    }
}