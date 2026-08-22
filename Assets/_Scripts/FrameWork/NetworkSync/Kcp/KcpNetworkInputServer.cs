using FrameWork.RollBackSystem;
using kcp2k;
using System;
using System.Collections.Generic;
using System.Net;

namespace FrameWork.NetworkSync
{
    /// <summary>
    /// 基于 kcp2k Reliable Channel 的权威输入服务器。
    /// </summary>
    public sealed class KcpNetworkInputServer : IDisposable
    {
        private readonly KcpServer _server;
        private readonly ServerInputFrameCollector _collector;
        private readonly Dictionary<int,int> _playerConnectionIds=new();
        private readonly Dictionary<int,int> _connectionPlayerIds=new();
        private readonly Queue<ServerAuthorityFramePacket> _generatedAuthorities=new();
        private readonly int _expectedPlayerCount;
        private uint _nextAuthoritySequence=1;
        private bool _isDisposed;
        private bool _sessionBarrierActive;

        public uint SessionId { get; }
        public IPEndPoint LocalEndPoint => _server.LocalEndPoint as IPEndPoint;
        public int BoundPlayerCount => _playerConnectionIds.Count;
        public int ConnectedConnectionCount => _server.connections.Count;
        public int ProcessedMessageCount { get; private set; }
        public int RejectedMessageCount { get; private set; }
        public int AuthorityFrameCount { get; private set; }
        public int LastAuthorityFrame { get; private set; }
        public int PendingFrameCount => _collector.PendingFrameCount;
        public int DroppedPendingFrameCount { get; private set; }
        public int DroppedIncompleteSessionInputCount { get; private set; }
        public bool IsSessionBarrierActive => _sessionBarrierActive;
        public ErrorCode? LastKcpError { get; private set; }
        public string LastKcpErrorMessage { get; private set; }
        public string LastRejectMessage { get; private set; }

        public event Action<int,IPEndPoint> PlayerBound;
        public event Action<ServerAuthorityFramePacket> AuthorityGenerated;
        public event Action<int,ErrorCode,string> KcpError;

        public KcpNetworkInputServer(int port,int playerCount,uint sessionId,int completedFrameRetention=512,KcpConfig config=null)
        {
            if(port<0||port>ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(port));
            if(playerCount<=0||playerCount>NetworkProtocolConstants.MaxPlayerCount) throw new ArgumentOutOfRangeException(nameof(playerCount));

            SessionId=sessionId;
            _expectedPlayerCount=playerCount;
            _collector=new ServerInputFrameCollector(completedFrameRetention);

            for(int playerID=1;playerID<=playerCount;playerID++)
                _collector.RegisterPlayer(playerID);

            _server=new KcpServer(
                OnConnected,
                OnData,
                OnDisconnected,
                OnError,
                config??KcpNetworkConfigFactory.Create());

            _server.Start((ushort)port);
        }

        /// <summary>推进 KCP 收发、ACK 与重传。</summary>
        public void Tick()
        {
            ThrowIfDisposed();
            _server.Tick();
        }

        /// <summary>获取本次运行中生成的一个 Authority，用于测试与诊断。</summary>
        public bool TryDequeueGeneratedAuthority(out ServerAuthorityFramePacket packet)
        {
            ThrowIfDisposed();

            if(_generatedAuthorities.Count==0)
            {
                packet=default;
                return false;
            }

            packet=_generatedAuthorities.Dequeue();
            return true;
        }

        public void Dispose()
        {
            if(_isDisposed) return;
            _isDisposed=true;
            _server.Stop();
            _generatedAuthorities.Clear();
            _playerConnectionIds.Clear();
            _connectionPlayerIds.Clear();
        }

        private void OnConnected(int connectionId){}

        private void OnData(int connectionId,ArraySegment<byte> message,KcpChannel channel)
        {
            ProcessedMessageCount++;

            if(channel!=KcpChannel.Reliable)
            {
                Reject($"UnsupportedChannel ConnectionID={connectionId} Channel={channel}");
                return;
            }

            byte[] data=new byte[message.Count];
            Buffer.BlockCopy(message.Array,message.Offset,data,0,message.Count);

            if(!NetworkPacketSerializer.TryDeserializeClientInput(data,out ClientInputPacket packet,out NetworkPacketDecodeError decodeError))
            {
                Reject($"DecodeFailed ConnectionID={connectionId} Error={decodeError}");
                return;
            }

            if(packet.SessionId!=SessionId)
            {
                Reject($"SessionMismatch ConnectionID={connectionId} Expected=0x{SessionId:X8} Actual=0x{packet.SessionId:X8}");
                return;
            }

            int playerID=packet.Input.playerID;

            if(playerID<=0||playerID>_expectedPlayerCount)
            {
                Reject($"InvalidPlayer ConnectionID={connectionId} PlayerID={playerID}");
                return;
            }

            if(!TryBindPlayer(connectionId,playerID)) return;

            // 初次建房阶段允许先到玩家的输入进入 Collector：
            // P1 的首包既负责 BIND，也可能先于 P2 首包到达。
            // 只有发生过真实成员断开后才进入 Session Barrier。
            if(_sessionBarrierActive)
            {
                if(_playerConnectionIds.Count!=_expectedPlayerCount)
                {
                    DroppedIncompleteSessionInputCount++;
                    return;
                }

                // 当前输入完成了最后一个缺失 Player 的 Rebind。
                // 从这一包开始重新接受 fresh input。
                _sessionBarrierActive=false;
            }

            FrameInputSet completedFrame;

            try
            {
                if(!_collector.TryAddInput(in packet.Input,out completedFrame)) return;
            }
            catch(InvalidOperationException exception)
            {
                Reject($"InputConflict ConnectionID={connectionId} PlayerID={playerID} Frame={packet.Input.frameNumber} Message={exception.Message}");
                return;
            }

            if(_playerConnectionIds.Count!=_expectedPlayerCount)
                throw new InvalidOperationException($"KCP Authority Completed Before All Players Bound: Bound={_playerConnectionIds.Count}, Expected={_expectedPlayerCount}");

            var authorityPacket=new ServerAuthorityFramePacket(SessionId,_nextAuthoritySequence++,completedFrame);
            byte[] authorityBytes=NetworkPacketSerializer.SerializeServerAuthorityFrame(in authorityPacket);
            var segment=new ArraySegment<byte>(authorityBytes);

            for(int id=1;id<=_expectedPlayerCount;id++)
                _server.Send(_playerConnectionIds[id],segment,KcpChannel.Reliable);

            AuthorityFrameCount++;
            LastAuthorityFrame=completedFrame.frameNumber;
            _generatedAuthorities.Enqueue(authorityPacket);
            AuthorityGenerated?.Invoke(authorityPacket);
        }

        private void OnDisconnected(int connectionId)
        {
            if(!_connectionPlayerIds.TryGetValue(connectionId,out int playerID)) return;

            _connectionPlayerIds.Remove(connectionId);
            _playerConnectionIds.Remove(playerID);

            // 任何已绑定成员断开都进入 Session Barrier。
            // completed Authority 保留；只丢弃尚未提交的 pending 输入。
            _sessionBarrierActive=true;
            DroppedPendingFrameCount+=_collector.ClearPendingFrames();
        }

        private void OnError(int connectionId,ErrorCode error,string message)
        {
            LastKcpError=error;
            LastKcpErrorMessage=message;
            KcpError?.Invoke(connectionId,error,message);
        }

        private bool TryBindPlayer(int connectionId,int playerID)
        {
            if(_connectionPlayerIds.TryGetValue(connectionId,out int boundPlayerID))
            {
                if(boundPlayerID==playerID) return true;
                Reject($"ConnectionPlayerMismatch ConnectionID={connectionId} ExpectedPlayer={boundPlayerID} ActualPlayer={playerID}");
                return false;
            }

            if(_playerConnectionIds.TryGetValue(playerID,out int existingConnectionID))
            {
                Reject($"PlayerConnectionMismatch PlayerID={playerID} ExistingConnectionID={existingConnectionID} ActualConnectionID={connectionId}");
                return false;
            }

            _connectionPlayerIds.Add(connectionId,playerID);
            _playerConnectionIds.Add(playerID,connectionId);

            IPEndPoint endPoint=_server.GetClientEndPoint(connectionId);
            PlayerBound?.Invoke(playerID,endPoint);
            return true;
        }

        private void Reject(string message)
        {
            RejectedMessageCount++;
            LastRejectMessage=message;
        }

        private void ThrowIfDisposed()
        {
            if(_isDisposed) throw new ObjectDisposedException(nameof(KcpNetworkInputServer));
        }
    }
}
