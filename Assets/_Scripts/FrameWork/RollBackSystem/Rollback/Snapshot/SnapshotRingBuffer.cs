/*
 * 文件说明：
 * SnapshotRingBuffer 用于缓存最近若干帧 Snapshot。
 *
 * 设计目标：
 * 1. 保存最近 N 帧世界快照。
 * 2. 支持回滚时快速恢复历史状态。
 * 3. 自动淘汰过旧 Snapshot。
 * 4. 支持查找最近合法快照。
 *
 * 使用场景：
 * - RollbackCoordinator
 * - WorldSnapshot 缓存
 * - 回滚重模拟
 */

using Simulation.Contracts;
using System.Collections.Generic;

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

        /// <summary>
        /// 保存指定帧快照。
        /// </summary>
        public void Save(TSnapshot snapshot)
        {
            int frame = snapshot.Frame;

            //--------------------------------
            // Replace old snapshot
            //--------------------------------

            if (_snapshots.TryGetValue(
                frame,
                out var oldSnapshot))
            {
                oldSnapshot.Release();
            }

            _snapshots[frame] = snapshot;

            //--------------------------------
            // Remove expired snapshot
            //--------------------------------

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

        /// <summary>
        /// 获取指定帧快照。
        /// </summary>
        public bool TryGet(
            int frame,
            out TSnapshot snapshot)
        {
            return _snapshots.TryGetValue(
                frame,
                out snapshot);
        }

        /// <summary>
        /// 获取不超过目标帧的最近快照。
        /// </summary>
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
                    continue;

                if (frame > nearestFrame)
                {
                    nearestFrame = frame;

                    snapshot = pair.Value;
                }
            }

            return nearestFrame >= 0;
        }

        /// <summary>
        /// 清空所有快照。
        /// </summary>
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