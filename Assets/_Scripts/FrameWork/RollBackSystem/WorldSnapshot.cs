using FrameWork.RollBackSystem.Interfaces;
using PoolSystem;

namespace FrameWork.RollBackSystem
{
    public sealed class WorldSnapshot
        : ISnapshot,
          IReference<WorldSnapshot>
    {
        public int Frame { get; private set; }

        int IReference.IndexInReferencePool
        {
            get;
            set;
        }

        public void Initialize(int frame)
        {
            Frame = frame;
        }

        public static WorldSnapshot Create(
            int frame)
        {
            var snapshot =
                ReferencePoolCenter.Instance
                    .GetReference<WorldSnapshot>();

            snapshot.Initialize(frame);

            return snapshot;
        }

        public void Release()
        {
            ReferencePoolCenter.Instance
                .ReleaseReference(this);
        }

        void IReference.OnRecycle()
        {
            Frame = 0;
        }

        IReference IReference.GetNewInstance()
        {
            return new WorldSnapshot();
        }

        void System.IDisposable.Dispose()
        {
        }
    }
}