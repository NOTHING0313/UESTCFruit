using System.Collections.Generic;
using FrameWork.RollBackSystem.Interfaces;

namespace FrameWork.RollBackSystem
{
    public sealed class SnapshotRingBuffer<TSnapshot>
        where TSnapshot : ISnapshot
    {
        private readonly int _capacity;

        private readonly Dictionary<int, TSnapshot>
            _snapshots;

        public SnapshotRingBuffer(int capacity)
        {
            _capacity = capacity;

            _snapshots =
                new Dictionary<int, TSnapshot>();
        }

        public void Save(TSnapshot snapshot)
        {
            int frame = snapshot.Frame;

            if (_snapshots.TryGetValue(
                frame,
                out var oldSnapshot))
            {
                oldSnapshot.Release();
            }

            _snapshots[frame] = snapshot;

            int removeFrame =
                frame - _capacity;

            if (_snapshots.TryGetValue(
                removeFrame,
                out var removedSnapshot))
            {
                removedSnapshot.Release();

                _snapshots.Remove(removeFrame);
            }
        }

        public bool TryGet(
            int frame,
            out TSnapshot snapshot)
        {
            return _snapshots.TryGetValue(
                frame,
                out snapshot);
        }

        public bool TryGetNearestSnapshot(
            int targetFrame,
            out TSnapshot snapshot)
        {
            snapshot = default;

            int nearestFrame = -1;

            foreach (var pair in _snapshots)
            {
                int frame = pair.Key;

                if (frame > targetFrame)
                {
                    continue;
                }

                if (frame > nearestFrame)
                {
                    nearestFrame = frame;

                    snapshot = pair.Value;
                }
            }

            return nearestFrame >= 0;
        }

        public void Clear()
        {
            foreach (var snapshot in _snapshots.Values)
            {
                snapshot.Release();
            }

            _snapshots.Clear();
        }
    }
}