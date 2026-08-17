using System;
using System.Net;

namespace FrameWork.NetworkSync
{
    /// <summary>最小 UDP Datagram Transport。</summary>
    public interface IUdpTransport : IDisposable
    {
        IPEndPoint LocalEndPoint { get; }
        bool IsDisposed { get; }

        /// <summary>发送一个完整 UDP Datagram。</summary>
        void Send(byte[] data, IPEndPoint remoteEndPoint);

        /// <summary>非阻塞尝试接收一个完整 UDP Datagram。</summary>
        bool TryReceive(out UdpReceivedDatagram datagram);
    }
}
