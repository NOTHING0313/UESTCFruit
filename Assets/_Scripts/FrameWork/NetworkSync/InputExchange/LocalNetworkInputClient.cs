using ECSFrameWork;
using FrameWork.RollBackSystem;
using System;
using System.Net;

namespace FrameWork.NetworkSync
{
    /// <summary>
    /// 最小本地网络输入客户端：发送自己的 PlayerInputSnapshot，并接收服务器 Authority Frame。
    /// </summary>
    public sealed class LocalNetworkInputClient : IDisposable
    {
        private readonly IUdpTransport _transport;
        private readonly IPEndPoint _serverEndPoint;
        private uint _nextSequence = 1;
        private bool _isDisposed;

        public uint SessionId { get; }
        public int PlayerID { get; }
        public IPEndPoint LocalEndPoint => _transport.LocalEndPoint;
        public uint LastSentSequence { get; private set; }
        public NetworkInputExchangeRejectReason LastRejectReason { get; private set; }
        public NetworkPacketDecodeError LastDecodeError { get; private set; }

        public LocalNetworkInputClient(UdpTransportConfig config, IPEndPoint serverEndPoint, uint sessionId, int playerID)
        {
            if (serverEndPoint == null) throw new ArgumentNullException(nameof(serverEndPoint));
            if (playerID <= 0) throw new ArgumentOutOfRangeException(nameof(playerID));

            _transport = new UdpTransport(config);
            _serverEndPoint = CloneEndPoint(serverEndPoint);
            SessionId = sessionId;
            PlayerID = playerID;
        }

        /// <summary>发送当前客户端玩家的一帧输入。</summary>
        public void SendInput(in PlayerInputSnapshot input)
        {
            ThrowIfDisposed();

            if (input.playerID != PlayerID)
                throw new InvalidOperationException($"Client Input Player Mismatch: Expected={PlayerID}, Actual={input.playerID}");

            uint sequence = _nextSequence++;
            var packet = new ClientInputPacket(SessionId, sequence, input);
            byte[] data = NetworkPacketSerializer.SerializeClientInput(in packet);

            _transport.Send(data, _serverEndPoint);
            LastSentSequence = sequence;
        }

        /// <summary>非阻塞接收一个服务器权威帧。</summary>
        public bool TryReceiveAuthority(out ServerAuthorityFramePacket packet)
        {
            ThrowIfDisposed();

            packet = default;
            LastRejectReason = NetworkInputExchangeRejectReason.None;
            LastDecodeError = NetworkPacketDecodeError.None;

            if (!_transport.TryReceive(out UdpReceivedDatagram datagram)) return false;

            if (!EndPointEquals(datagram.RemoteEndPoint, _serverEndPoint))
            {
                LastRejectReason = NetworkInputExchangeRejectReason.EndpointMismatch;
                return false;
            }

            if (!NetworkPacketSerializer.TryDeserializeServerAuthorityFrame(datagram.Data, out packet, out NetworkPacketDecodeError decodeError))
            {
                LastDecodeError = decodeError;
                LastRejectReason = NetworkInputExchangeRejectReason.DecodeFailed;
                packet = default;
                return false;
            }

            if (packet.SessionId != SessionId)
            {
                LastRejectReason = NetworkInputExchangeRejectReason.SessionMismatch;
                packet = default;
                return false;
            }

            return true;
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            _transport.Dispose();
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(LocalNetworkInputClient));
        }

        private static bool EndPointEquals(IPEndPoint a, IPEndPoint b)
            => a != null && b != null && a.Port == b.Port && Equals(a.Address, b.Address);

        private static IPEndPoint CloneEndPoint(IPEndPoint endPoint)
            => new IPEndPoint(endPoint.Address, endPoint.Port);
    }
}