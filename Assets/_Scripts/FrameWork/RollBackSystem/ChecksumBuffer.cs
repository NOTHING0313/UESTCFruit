using System.Collections.Generic;

namespace FrameWork.RollBackSystem
{
    public sealed class ChecksumBuffer
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