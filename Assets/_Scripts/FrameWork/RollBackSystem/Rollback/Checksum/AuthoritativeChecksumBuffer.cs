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
    }
}