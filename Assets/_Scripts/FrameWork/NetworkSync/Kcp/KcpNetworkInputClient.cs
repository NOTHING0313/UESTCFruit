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
    public sealed class KcpNetworkInputClient : INetworkInputClient
    {
        private readonly KcpClient _client;
        private readonly Queue<ServerAuthorityFramePacket> _authorityQueue=new();
        private uint _nextSequence=1;
        private bool _isDisposed;

        public NetworkInputTransportMode TransportMode => NetworkInputTransportMode.Kcp;
        public uint SessionId { get; }
        public int PlayerID { get; }
        public bool IsConnected => _client.connected;
        public bool IsReady => IsConnected&&!_isDisposed;
        public IPEndPoint LocalEndPoint => _client.LocalEndPoint as IPEndPoint;
        public uint LastSentSequence { get; private set; }
        public NetworkInputExchangeRejectReason LastRejectReason { get; private set; }
        public NetworkPacketDecodeError LastDecodeError { get; private set; }
        public ErrorCode? LastKcpError { get; private set; }
        public string LastKcpErrorMessage { get; private set; }
        public bool HasTransportError => LastKcpError.HasValue;
        public string LastTransportError => LastKcpError.HasValue?$"{LastKcpError}: {LastKcpErrorMessage}":null;

        public KcpNetworkInputClient(string serverAddress,int serverPort,uint sessionId,int playerID,KcpConfig config=null)
        {
            if(string.IsNullOrWhiteSpace(serverAddress)) throw new ArgumentException("Server Address Is Empty",nameof(serverAddress));
            if(serverPort<=0||serverPort>ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(serverPort));
            if(playerID<=0) throw new ArgumentOutOfRangeException(nameof(playerID));

            SessionId=sessionId;
            PlayerID=playerID;

            _client=new KcpClient(OnConnected,OnData,OnDisconnected,OnError,config??KcpNetworkConfigFactory.Create());
            _client.Connect(serverAddress,(ushort)serverPort);
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
        }

        private void OnConnected()
        {
            LastKcpError=null;
            LastKcpErrorMessage=null;
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

        private void OnDisconnected(){}

        private void OnError(ErrorCode error,string message)
        {
            LastKcpError=error;
            LastKcpErrorMessage=message;
        }

        private void ThrowIfDisposed()
        {
            if(_isDisposed) throw new ObjectDisposedException(nameof(KcpNetworkInputClient));
        }
    }
}
