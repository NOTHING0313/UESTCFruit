using FrameWork.RollBackSystem.Interfaces;
using PoolSystem;

namespace FrameWork.RollBackSystem
{
    public sealed class BuffSnapshot
        : ISnapshot,
          IReference<BuffSnapshot>
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

        public static BuffSnapshot Create(
            int frame)
        {
            var snapshot =
                ReferencePoolCenter.Instance
                    .GetReference<BuffSnapshot>();

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
            return new BuffSnapshot();
        }

        void System.IDisposable.Dispose()
        {
        }
    }
}