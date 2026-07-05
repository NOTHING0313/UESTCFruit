/*
 * 文件说明：AuthoritativeChecksumBuffer 保存来自服务器或权威端的历史校验值。
 * 设计约束：该缓存只保存远端最终确认的 Checksum，用于与本地模拟结果进行一致性校验。
 */

using System.Collections.Generic;

namespace FrameWork.RollBackSystem
{
    public sealed class AuthoritativeChecksumBuffer
    {
        private readonly Dictionary<int, FrameChecksum>
            _checksums =
                new Dictionary<int, FrameChecksum>();

        // 复用缓冲区，避免 ClearBefore 中产生 GC
        private readonly List<int> _recycleKeys = new List<int>();

        /// <summary>
        /// 保存指定逻辑帧的权威 Checksum。
        /// </summary>
        public void Save(FrameChecksum checksum)
        {
            _checksums[checksum.Frame] = checksum;
        }

        /// <summary>
        /// 尝试获取指定逻辑帧的权威 Checksum。
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
        /// 清除指定逻辑帧之前的权威校验值（不含该帧）。
        /// 使用成员缓冲区，零 GC。
        /// </summary>
        public void ClearBefore(int frame)
        {
            _recycleKeys.Clear();

            foreach (var pair in _checksums)
            {
                if (pair.Key < frame)
                    _recycleKeys.Add(pair.Key);
            }

            for (int i = 0; i < _recycleKeys.Count; i++)
            {
                _checksums.Remove(_recycleKeys[i]);
            }
        }
    }
}