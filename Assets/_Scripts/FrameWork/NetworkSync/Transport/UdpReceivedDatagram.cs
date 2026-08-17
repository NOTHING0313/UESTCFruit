using System.Net;

namespace FrameWork.NetworkSync
{
    /// <summary>单个 UDP Datagram 接收结果。</summary>
    public readonly struct UdpReceivedDatagram
    {
        public readonly byte[] Data;
        public readonly IPEndPoint RemoteEndPoint;

        public int Length => Data?.Length ?? 0;

        public UdpReceivedDatagram(byte[] data, IPEndPoint remoteEndPoint)
        {
            Data = data;
            RemoteEndPoint = remoteEndPoint;
        }
    }
}