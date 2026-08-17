using FrameWork.RollBackSystem;
using System;
using System.Collections.Generic;
using System.Net;

namespace FrameWork.NetworkSync
{
    /// <summary>
    /// 最小本地权威输入服务器：接收 ClientInput，按帧聚合并广播 ServerAuthorityFrame。
    /// </summary>
    public sealed class LocalNetworkInputServer : IDisposable
    {
        private readonly IUdpTransport _transport;
        private readonly ServerInputFrameCollector _collector;
        private readonly Dictionary<int, IPEndPoint> _playerEndPoints = new();
        private uint _nextAuthoritySequence = 1;
        private bool _isDisposed;

        public uint SessionId { get; }
        public IPEndPoint LocalEndPoint => _transport.LocalEndPoint;
        public int PlayerCount => _playerEndPoints.Count;

        public int ProcessedDatagramCount { get; private set; }
        public int RejectedDatagramCount { get; private set; }
        public int AuthorityFrameCount { get; private set; }

        public NetworkInputExchangeRejectReason LastRejectReason { get; private set; }
        public NetworkPacketDecodeError LastDecodeError { get; private set; }

        public LocalNetworkInputServer(UdpTransportConfig config, uint sessionId, int completedFrameRetention = 512)
        {
            _transport = new UdpTransport(config);
            _collector = new ServerInputFrameCollector(completedFrameRetention);
            SessionId = sessionId;
        }

        /// <summary>注册 PlayerID 与该玩家客户端 UDP Endpoint 的绑定关系。</summary>
        public void RegisterPlayer(int playerID, IPEndPoint clientEndPoint)
        {
            ThrowIfDisposed();

            if (playerID <= 0) throw new ArgumentOutOfRangeException(nameof(playerID));
            if (clientEndPoint == null) throw new ArgumentNullException(nameof(clientEndPoint));
            if (_playerEndPoints.ContainsKey(playerID))
                throw new InvalidOperationException($"Server Player Already Registered: PlayerID={playerID}");

            foreach (IPEndPoint existing in _playerEndPoints.Values)
            {
                if (EndPointEquals(existing, clientEndPoint))
                    throw new InvalidOperationException($"Server Endpoint Already Registered: Endpoint={clientEndPoint}");
            }

            _collector.RegisterPlayer(playerID);
            _playerEndPoints.Add(playerID, CloneEndPoint(clientEndPoint));
        }

        /// <summary>
        /// 非阻塞处理一个 Client Datagram。返回 true 表示本次处理产生并广播了一个完整 Authority Frame。
        /// </summary>
        public bool TryProcessOneDatagram(out ServerAuthorityFramePacket authorityPacket)
        {
            ThrowIfDisposed();

            authorityPacket = default;
            LastRejectReason = NetworkInputExchangeRejectReason.None;
            LastDecodeError = NetworkPacketDecodeError.None;

            if (!_transport.TryReceive(out UdpReceivedDatagram datagram)) return false;

            ProcessedDatagramCount++;

            if (!NetworkPacketSerializer.TryDeserializeClientInput(datagram.Data, out ClientInputPacket clientPacket, out NetworkPacketDecodeError decodeError))
                return Reject(NetworkInputExchangeRejectReason.DecodeFailed, decodeError);

            if (clientPacket.SessionId != SessionId)
                return Reject(NetworkInputExchangeRejectReason.SessionMismatch);

            int playerID = clientPacket.Input.playerID;

            if (!_playerEndPoints.TryGetValue(playerID, out IPEndPoint expectedEndPoint))
                return Reject(NetworkInputExchangeRejectReason.UnregisteredPlayer);

            if (!EndPointEquals(datagram.RemoteEndPoint, expectedEndPoint))
                return Reject(NetworkInputExchangeRejectReason.EndpointMismatch);

            FrameInputSet completedFrame;

            try
            {
                if (!_collector.TryAddInput(in clientPacket.Input, out completedFrame)) return false;
            }
            catch (InvalidOperationException)
            {
                return Reject(NetworkInputExchangeRejectReason.InputConflict);
            }

            authorityPacket = new ServerAuthorityFramePacket(
                SessionId,
                _nextAuthoritySequence++,
                completedFrame);

            byte[] authorityBytes = NetworkPacketSerializer.SerializeServerAuthorityFrame(in authorityPacket);

            foreach (IPEndPoint endPoint in _playerEndPoints.Values)
                _transport.Send(authorityBytes, endPoint);

            AuthorityFrameCount++;
            return true;
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            _transport.Dispose();
        }

        private bool Reject(NetworkInputExchangeRejectReason reason, NetworkPacketDecodeError decodeError = NetworkPacketDecodeError.None)
        {
            LastRejectReason = reason;
            LastDecodeError = decodeError;
            RejectedDatagramCount++;
            return false;
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(LocalNetworkInputServer));
        }

        private static bool EndPointEquals(IPEndPoint a, IPEndPoint b)
            => a != null && b != null && a.Port == b.Port && Equals(a.Address, b.Address);

        private static IPEndPoint CloneEndPoint(IPEndPoint endPoint)
            => new IPEndPoint(endPoint.Address, endPoint.Port);
    }
}