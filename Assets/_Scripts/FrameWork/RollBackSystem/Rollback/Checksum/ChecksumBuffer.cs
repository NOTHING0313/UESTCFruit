/*
 * 文件说明：ChecksumBuffer 保存本地逻辑帧对应的状态校验值。
 * 设计约束：Checksum 必须与逻辑帧严格对应，用于检测回滚同步中的状态漂移。
 */

using System.Collections.Generic;

namespace FrameWork.RollBackSystem
{
    public sealed class ChecksumBuffer
    {
        private readonly Dictionary<int, FrameChecksum>
            _checksums =
                new Dictionary<int, FrameChecksum>();

        // 复用缓冲区，避免 ClearBefore 中产生 GC
        private readonly List<int> _recycleKeys = new();

        /// <summary>
        /// 保存指定逻辑帧的 Checksum。
        /// </summary>
        public void Save(FrameChecksum checksum)
        {
            _checksums[checksum.Frame] = checksum;
        }

        /// <summary>
        /// 尝试读取指定逻辑帧的 Checksum。
        /// </summary>
        public bool TryGet(
            int frame,
            out FrameChecksum checksum)
        {
            return _checksums.TryGetValue(
                frame,
                out checksum);
        }

        /// <summary>
        /// 清除指定逻辑帧之前的历史校验值（不含该帧）。
        /// 使用成员缓冲区，零 GC。
        /// </summary>
        public void ClearBefore(int frame)
        {
            _recycleKeys.Clear();

            foreach (var pair in _checksums)
            {
                if (pair.Key < frame)
                {
                    _recycleKeys.Add(pair.Key);
                }
            }

            for (int i = 0; i < _recycleKeys.Count; i++)
            {
                _checksums.Remove(_recycleKeys[i]);
            }
        }
    }
}