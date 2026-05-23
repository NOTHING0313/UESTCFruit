using System.Collections.Generic;

namespace FrameWork.RollBackSystem
{
    public sealed class AuthoritativeChecksumBuffer
    {
        private readonly Dictionary<int, FrameChecksum>
            _checksums =
                new Dictionary<int, FrameChecksum>();

        public void Save(FrameChecksum checksum)
        {
            _checksums[checksum.Frame] = checksum;
        }

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