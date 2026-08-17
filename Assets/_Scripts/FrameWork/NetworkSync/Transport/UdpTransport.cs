using System;
using System.Net;
using System.Net.Sockets;

namespace FrameWork.NetworkSync
{
    /// <summary>
    /// IPv4 非阻塞 UDP Datagram Transport。
    /// </summary>
    public sealed class UdpTransport : IUdpTransport
    {
        private readonly Socket _socket;
        private readonly int _maxDatagramSize;
        private bool _isDisposed;

        public bool IsDisposed => _isDisposed;

        public IPEndPoint LocalEndPoint
        {
            get
            {
                ThrowIfDisposed();
                return (IPEndPoint)_socket.LocalEndPoint;
            }
        }

        public UdpTransport(UdpTransportConfig config)
        {
            if (!IPAddress.TryParse(config.BindAddress, out IPAddress address))
                throw new ArgumentException($"Invalid Bind Address: {config.BindAddress}", nameof(config));

            if (address.AddressFamily != AddressFamily.InterNetwork)
                throw new NotSupportedException($"UdpTransport V1 Supports IPv4 Only: {address}");

            _maxDatagramSize = config.MaxDatagramSize;

            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
            {
                Blocking = false
            };

            try
            {
                _socket.Bind(new IPEndPoint(address, config.BindPort));
            }
            catch
            {
                _socket.Dispose();
                throw;
            }
        }

        /// <summary>发送一个完整 UDP Datagram。</summary>
        public void Send(byte[] data, IPEndPoint remoteEndPoint)
        {
            ThrowIfDisposed();

            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.Length == 0) throw new ArgumentException("UDP Datagram Is Empty", nameof(data));
            if (data.Length > _maxDatagramSize)
                throw new ArgumentOutOfRangeException(nameof(data), data.Length, $"UDP Datagram Exceeds Max Size: Max={_maxDatagramSize}");

            if (remoteEndPoint == null) throw new ArgumentNullException(nameof(remoteEndPoint));
            if (remoteEndPoint.AddressFamily != AddressFamily.InterNetwork)
                throw new NotSupportedException("UdpTransport V1 Supports IPv4 Only");

            int sent = _socket.SendTo(data, 0, data.Length, SocketFlags.None, remoteEndPoint);

            if (sent != data.Length)
                throw new InvalidOperationException($"UdpTransport Send Error: Expected={data.Length}, Actual={sent}");
        }

        /// <summary>非阻塞尝试接收一个完整 UDP Datagram。</summary>
        public bool TryReceive(out UdpReceivedDatagram datagram)
        {
            ThrowIfDisposed();
            datagram = default;

            if (!_socket.Poll(0, SelectMode.SelectRead)) return false;

            var buffer = new byte[_maxDatagramSize];
            EndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

            try
            {
                int received = _socket.ReceiveFrom(
                    buffer,
                    0,
                    buffer.Length,
                    SocketFlags.None,
                    ref remoteEndPoint);

                var data = new byte[received];
                if (received > 0) Buffer.BlockCopy(buffer, 0, data, 0, received);

                datagram = new UdpReceivedDatagram(
                    data,
                    (IPEndPoint)remoteEndPoint);

                return true;
            }
            catch (SocketException exception) when (
                exception.SocketErrorCode == SocketError.WouldBlock ||
                exception.SocketErrorCode == SocketError.IOPending ||
                exception.SocketErrorCode == SocketError.NoBufferSpaceAvailable)
            {
                return false;
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;

            _isDisposed = true;
            _socket.Dispose();
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(UdpTransport));
        }
    }
}