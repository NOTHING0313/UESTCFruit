using ECSFrameWork;
using kcp2k;
using System;
using System.Collections.Generic;
using System.Net;

namespace FrameWork.NetworkSync
{
    /// <summary>
    /// 基于 kcp2k Reliable Channel 的网络输入客户端。
    /// </summary>
    public sealed class KcpNetworkInputClient : INetworkInputClient,IReconnectableNetworkInputClient
    {
        private readonly KcpClient _client;
        private readonly string _serverAddress;
        private readonly int _serverPort;
        private readonly Queue<ServerAuthorityFramePacket> _authorityQueue=new();
        private uint _nextSequence=1;
        private bool _isDisposed;

        public NetworkInputTransportMode TransportMode => NetworkInputTransportMode.Kcp;
        public uint SessionId { get; }
        public int PlayerID { get; }
        public bool IsConnected => _client.connected;
        public bool IsReady => ConnectionState==NetworkInputClientConnectionState.Connected&&IsConnected&&!_isDisposed;
        public NetworkInputClientConnectionState ConnectionState { get; private set; }=NetworkInputClientConnectionState.Disconnected;
        public IPEndPoint LocalEndPoint => _client.LocalEndPoint as IPEndPoint;
        public uint LastSentSequence { get; private set; }
        public NetworkInputExchangeRejectReason LastRejectReason { get; private set; }
        public NetworkPacketDecodeError LastDecodeError { get; private set; }
        public ErrorCode? LastKcpError { get; private set; }
        public string LastKcpErrorMessage { get; private set; }
        public bool HasTransportError => LastKcpError.HasValue;
        public string LastTransportError => LastKcpError.HasValue?$"{LastKcpError}: {LastKcpErrorMessage}":null;
        public bool CanReconnect => !_isDisposed&&(ConnectionState==NetworkInputClientConnectionState.Disconnected||ConnectionState==NetworkInputClientConnectionState.Faulted);

        public event Action<NetworkInputClientConnectionState> ConnectionStateChanged;

        public KcpNetworkInputClient(string serverAddress,int serverPort,uint sessionId,int playerID,KcpConfig config=null)
        {
            if(string.IsNullOrWhiteSpace(serverAddress)) throw new ArgumentException("Server Address Is Empty",nameof(serverAddress));
            if(serverPort<=0||serverPort>ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(serverPort));
            if(playerID<=0) throw new ArgumentOutOfRangeException(nameof(playerID));

            SessionId=sessionId;
            PlayerID=playerID;
            _serverAddress=serverAddress;
            _serverPort=serverPort;

            _client=new KcpClient(OnConnected,OnData,OnDisconnected,OnError,config??KcpNetworkConfigFactory.Create());
            ConnectTransport();
        }

        /// <summary>
        /// 在同一个 Client / Session / PlayerID 上重新建立 KCP Transport。
        /// 保留应用层发送 Sequence，清理旧连接残留的 Authority 与错误状态。
        /// </summary>
        public void Reconnect()
        {
            ThrowIfDisposed();

            if(!CanReconnect)
                throw new InvalidOperationException(
                    $"KCP Client Cannot Reconnect From State={ConnectionState}: PlayerID={PlayerID}");

            _authorityQueue.Clear();
            LastRejectReason=NetworkInputExchangeRejectReason.None;
            LastDecodeError=NetworkPacketDecodeError.None;
            LastKcpError=null;
            LastKcpErrorMessage=null;

            ConnectTransport();
        }

        /// <summary>推进 KCP 收发、ACK 与重传。</summary>
        public void Tick()
        {
            ThrowIfDisposed();
            _client.Tick();
        }

        /// <summary>发送当前客户端玩家的一帧输入。</summary>
        public void SendInput(in PlayerInputSnapshot input)
        {
            ThrowIfDisposed();

            if(!IsConnected) throw new InvalidOperationException($"KCP Client Is Not Connected: PlayerID={PlayerID}");
            if(input.playerID!=PlayerID)
                throw new InvalidOperationException($"KCP Client Input Player Mismatch: Expected={PlayerID}, Actual={input.playerID}");

            uint sequence=_nextSequence++;
            var packet=new ClientInputPacket(SessionId,sequence,input);
            byte[] data=NetworkPacketSerializer.SerializeClientInput(in packet);

            _client.Send(new ArraySegment<byte>(data),KcpChannel.Reliable);
            _client.TickOutgoing();
            LastSentSequence=sequence;
        }

        /// <summary>非阻塞获取一个已由 KCP 可靠交付的 Authority Frame。</summary>
        public bool TryReceiveAuthority(out ServerAuthorityFramePacket packet)
        {
            ThrowIfDisposed();
            _client.Tick();

            if(_authorityQueue.Count==0)
            {
                packet=default;
                return false;
            }

            packet=_authorityQueue.Dequeue();
            return true;
        }

        public void Dispose()
        {
            if(_isDisposed) return;
            _isDisposed=true;
            _client.Disconnect();
            _authorityQueue.Clear();
            SetConnectionState(NetworkInputClientConnectionState.Disconnected);
        }

        private void ConnectTransport()
        {
            SetConnectionState(NetworkInputClientConnectionState.Connecting);
            _client.Connect(_serverAddress,(ushort)_serverPort);
        }

        private void OnConnected()
        {
            LastKcpError=null;
            LastKcpErrorMessage=null;
            SetConnectionState(NetworkInputClientConnectionState.Connected);
        }

        private void OnData(ArraySegment<byte> message,KcpChannel channel)
        {
            if(channel!=KcpChannel.Reliable) return;
            if(message.Array==null)
            {
                LastRejectReason=NetworkInputExchangeRejectReason.DecodeFailed;
                return;
            }

            byte[] data=new byte[message.Count];
            Buffer.BlockCopy(message.Array,message.Offset,data,0,message.Count);

            if(!NetworkPacketSerializer.TryDeserializeServerAuthorityFrame(data,out ServerAuthorityFramePacket packet,out NetworkPacketDecodeError decodeError))
            {
                LastDecodeError=decodeError;
                LastRejectReason=NetworkInputExchangeRejectReason.DecodeFailed;
                return;
            }

            if(packet.SessionId!=SessionId)
            {
                LastRejectReason=NetworkInputExchangeRejectReason.SessionMismatch;
                return;
            }

            LastDecodeError=NetworkPacketDecodeError.None;
            LastRejectReason=NetworkInputExchangeRejectReason.None;
            _authorityQueue.Enqueue(packet);
        }

        private void OnDisconnected()
        {
            if(_isDisposed) return;
            if(ConnectionState!=NetworkInputClientConnectionState.Faulted)
                SetConnectionState(NetworkInputClientConnectionState.Disconnected);
        }

        private void OnError(ErrorCode error,string message)
        {
            LastKcpError=error;
            LastKcpErrorMessage=message;
            SetConnectionState(NetworkInputClientConnectionState.Faulted);
        }

        private void SetConnectionState(NetworkInputClientConnectionState state)
        {
            if(ConnectionState==state) return;
            ConnectionState=state;
            ConnectionStateChanged?.Invoke(state);
        }

        private void ThrowIfDisposed()
        {
            if(_isDisposed) throw new ObjectDisposedException(nameof(KcpNetworkInputClient));
        }
    }
}
