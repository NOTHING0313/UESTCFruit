using FrameWork.RollBackSystem.Interfaces;

namespace FrameWork.RollBackSystem
{
    public sealed class SnapshotRingBuffer<TSnapshot>
        where TSnapshot : ISnapshot
    {
        private readonly TSnapshot[] _buffer;

        private readonly int _capacity;

        public SnapshotRingBuffer(int capacity)
        {
            _capacity = capacity;
            _buffer = new TSnapshot[capacity];
        }

        public void Save(TSnapshot snapshot)
        {
            int index = snapshot.Frame % _capacity;

            if (_buffer[index] != null)
            {
                _buffer[index].Release();
            }

            _buffer[index] = snapshot;
        }

        public bool TryGet(int frame, out TSnapshot snapshot)
        {
            int index = frame % _capacity;

            snapshot = _buffer[index];

            if (snapshot == null)
            {
                return false;
            }

            return snapshot.Frame == frame;
        }

        public void Clear()
        {
            for (int i = 0; i < _buffer.Length; i++)
            {
                if (_buffer[i] != null)
                {
                    _buffer[i].Release();
                    _buffer[i] = default;
                }
            }
        }
    }
}