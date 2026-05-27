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
        /// 清除指定逻辑帧之前的历史校验值。
        /// </summary>
        public void ClearBefore(int frame)
        {
            var removeList = new List<int>();

            foreach (var pair in _checksums)
            {
                if (pair.Key < frame)
                {
                    removeList.Add(pair.Key);
                }
            }

            foreach (var key in removeList)
            {
                _checksums.Remove(key);
            }
        }
    }
}