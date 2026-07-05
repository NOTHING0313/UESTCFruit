/*
 * 文件说明：
 * SnapshotRingBuffer 用于缓存最近若干帧 Snapshot。
 *
 * 设计目标：
 * 1. 保存最近 N 帧世界快照。
 * 2. 支持回滚时快速恢复历史状态。
 * 3. 自动淘汰过旧 Snapshot。
 * 4. 支持查找最近合法快照（二分查找 O(log n)）。
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

        // 内存预算（字节），0 表示不限制
        private readonly long _maxMemoryBytes;

        // 粗略估算每个快照的内存占用（字节），用于内存预算控制
        // 默认 1KB，可通过构造函数调整
        private readonly long _estimatedSnapshotSizeBytes;

        private readonly Dictionary<int, TSnapshot>
            _snapshots;

        // 维护有序帧号列表，支持二分查找
        private readonly List<int> _frameKeys;

        // 当前估算内存占用（字节）
        private long _currentMemoryBytes;

        /// <summary>
        /// 创建 SnapshotRingBuffer。
        /// </summary>
        /// <param name="capacity">最大缓存帧数</param>
        /// <param name="maxMemoryBytes">内存预算（字节），0 表示不限制</param>
        /// <param name="estimatedSnapshotSizeBytes">粗略估算每个快照的内存占用（字节），默认 1024</param>
        public SnapshotRingBuffer(int capacity, long maxMemoryBytes = 0, long estimatedSnapshotSizeBytes = 1024)
        {
            _capacity = capacity;
            _maxMemoryBytes = maxMemoryBytes;
            _estimatedSnapshotSizeBytes = estimatedSnapshotSizeBytes;

            _snapshots =
                new Dictionary<int, TSnapshot>();

            _frameKeys = new List<int>();
        }

        /// <summary>
        /// 当前缓存的快照数量。
        /// </summary>
        public int Count => _snapshots.Count;

        /// <summary>
        /// 缓存中最旧的快照帧号，无快照时返回 -1。
        /// </summary>
        public int MinFrame
        {
            get
            {
                if (_frameKeys.Count == 0)
                    return -1;
                return _frameKeys[0];
            }
        }

        /// <summary>
        /// 缓存中最新的快照帧号，无快照时返回 -1。
        /// </summary>
        public int MaxFrame
        {
            get
            {
                if (_frameKeys.Count == 0)
                    return -1;
                return _frameKeys[_frameKeys.Count - 1];
            }
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
                _currentMemoryBytes -= _estimatedSnapshotSizeBytes;
                oldSnapshot.Release();

                // 帧号已存在，无需更新 _frameKeys
                _snapshots[frame] = snapshot;
                _currentMemoryBytes += _estimatedSnapshotSizeBytes;
                return;
            }

            _snapshots[frame] = snapshot;
            _currentMemoryBytes += _estimatedSnapshotSizeBytes;

            // 插入到 _frameKeys 保持有序
            int insertIndex = _frameKeys.BinarySearch(frame);
            if (insertIndex < 0)
                insertIndex = ~insertIndex;
            _frameKeys.Insert(insertIndex, frame);

            //--------------------------------
            // Remove expired snapshot (by frame count)
            //--------------------------------

            int removeFrame =
                frame - _capacity;

            if (removeFrame > 0)
            {
                TryRemoveFrame(removeFrame);
            }

            //--------------------------------
            // Memory budget control
            //--------------------------------

            if (_maxMemoryBytes > 0)
            {
                while (_currentMemoryBytes > _maxMemoryBytes && _frameKeys.Count > 1)
                {
                    // 淘汰最旧快照
                    int oldestFrame = _frameKeys[0];
                    if (_snapshots.TryGetValue(oldestFrame, out var oldestSnapshot))
                    {
                        _currentMemoryBytes -= _estimatedSnapshotSizeBytes;
                        oldestSnapshot.Release();
                        _snapshots.Remove(oldestFrame);
                    }
                    _frameKeys.RemoveAt(0);

                    UnityEngine.Debug.LogWarning(
                        $"[SnapshotRingBuffer] Memory budget exceeded, evicted snapshot at frame {oldestFrame}. " +
                        $"Current memory: {_currentMemoryBytes} bytes.");
                }
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
        /// 获取不超过目标帧的最近快照（二分查找 O(log n)）。
        /// </summary>
        public bool TryGetNearestSnapshot(
            int targetFrame,
            out TSnapshot snapshot)
        {
            snapshot = default;

            if (_frameKeys.Count == 0)
                return false;

            // BinarySearch 找 <= targetFrame 的最大帧号
            int index = _frameKeys.BinarySearch(targetFrame);

            if (index < 0)
            {
                // 未找到精确匹配，~index 是大于 targetFrame 的第一个位置
                // 所以 nearestIndex = ~index - 1
                int nearestIndex = ~index - 1;

                if (nearestIndex < 0)
                    return false;

                snapshot = _snapshots[_frameKeys[nearestIndex]];
                return true;
            }

            // 精确匹配
            snapshot = _snapshots[_frameKeys[index]];
            return true;
        }

        /// <summary>
        /// 清理指定帧之前的快照（不含该帧），并调用 Release 释放。
        /// </summary>
        public void ClearBefore(int frame)
        {
            if (_frameKeys.Count == 0)
                return;

            // 找到第一个 >= frame 的位置，之前的都要删除
            int splitIndex = _frameKeys.BinarySearch(frame);
            if (splitIndex < 0)
                splitIndex = ~splitIndex;

            if (splitIndex == 0)
                return;

            for (int i = 0; i < splitIndex; i++)
            {
                int key = _frameKeys[i];
                if (_snapshots.TryGetValue(key, out var snapshot))
                {
                    _currentMemoryBytes -= _estimatedSnapshotSizeBytes;
                    snapshot.Release();
                    _snapshots.Remove(key);
                }
            }

            _frameKeys.RemoveRange(0, splitIndex);
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
            _frameKeys.Clear();
            _currentMemoryBytes = 0;
        }

        //--------------------------------
        // Private Helpers
        //--------------------------------

        private void TryRemoveFrame(int frame)
        {
            if (_snapshots.TryGetValue(
                frame,
                out var removedSnapshot))
            {
                removedSnapshot.Release();
                _snapshots.Remove(frame);

                int idx = _frameKeys.BinarySearch(frame);
                if (idx >= 0)
                    _frameKeys.RemoveAt(idx);
            }
        }
    }
}