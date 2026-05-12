using FrameWork.RollBackSystem.Interfaces;

namespace FrameWork.RollBackSystem
{
    public sealed class RollbackCoordinator<TInput, TSnapshot>
        where TSnapshot : ISnapshot
    {
        public int CurrentFrame { get; private set; }

        private readonly IInputBuffer<TInput> _inputBuffer;

        private readonly SnapshotRingBuffer<TSnapshot> _snapshotBuffer;

        private readonly IRollbackableWorld<TInput> _world;

        public RollbackCoordinator(
            IInputBuffer<TInput> inputBuffer,
            SnapshotRingBuffer<TSnapshot> snapshotBuffer,
            IRollbackableWorld<TInput> world)
        {
            _inputBuffer = inputBuffer;
            _snapshotBuffer = snapshotBuffer;
            _world = world;
        }

        public void Step(in TInput input)
        {
            _inputBuffer.Save(CurrentFrame, input);

            _world.Simulate(input);

            var snapshot =
                (TSnapshot)_world.Capture(CurrentFrame);

            _snapshotBuffer.Save(snapshot);

            CurrentFrame++;
        }

        public bool RollbackTo(int frame)
        {
            bool found =
                _snapshotBuffer.TryGet(frame, out var snapshot);

            if (!found)
            {
                return false;
            }

            _world.Restore(snapshot);

            CurrentFrame = frame;

            return true;
        }

        public void ResimulateTo(int targetFrame)
        {
            while (CurrentFrame < targetFrame)
            {
                bool found =
                    _inputBuffer.TryGet(CurrentFrame, out var input);

                if (!found)
                {
                    break;
                }

                Step(input);
            }
        }

        public uint CalculateChecksum()
        {
            return _world.CalculateChecksum();
        }
    }
}