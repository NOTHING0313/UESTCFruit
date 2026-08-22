using ECSFrameWork;
using System;
using System.Net;

namespace FrameWork.NetworkSync
{
    /// <summary>
    /// 网络同步客户端运行时主链：Input Transport → Authority Pump → Rollback Driver。
    /// </summary>
    public sealed class NetworkRollbackClientRuntime : IDisposable
    {
        private readonly NetworkInputClientPump _pump;
        private readonly NetworkAuthorityRollbackDriver _authorityDriver;
        private bool _isDisposed;

        public NetworkInputTransportMode TransportMode => _pump.Client.TransportMode;
        public uint SessionId => _pump.Client.SessionId;
        public int PlayerID => _pump.Client.PlayerID;
        public bool IsReady => _pump.Client.IsReady;
        public NetworkInputClientConnectionState ConnectionState => _pump.ConnectionState;
        public bool CanReconnect => _pump.CanReconnect;
        public uint LastSentSequence => _pump.Client.LastSentSequence;
        public IPEndPoint LocalEndPoint => _pump.Client.LocalEndPoint;
        public int ReceivedAuthorityCount => _pump.ReceivedAuthorityCount;
        public int AppliedAuthorityCount => _authorityDriver.AppliedAuthorityCount;
        public int OutOfOrderAuthorityCount => _authorityDriver.OutOfOrderAuthorityCount;
        public int LastAuthorityFrame => _authorityDriver.LastAuthorityFrame;
        public string LastTransportError => _pump.Client.LastTransportError;

        /// <summary>底层传输连接状态发生变化。</summary>
        public event Action<NetworkInputClientConnectionState> ConnectionStateChanged
        {
            add=>_pump.ConnectionStateChanged+=value;
            remove=>_pump.ConnectionStateChanged-=value;
        }

        public NetworkRollbackClientRuntime(INetworkInputClient client,NetworkAuthorityRollbackDriver authorityDriver)
        {
            _authorityDriver=authorityDriver??throw new ArgumentNullException(nameof(authorityDriver));
            _pump=new NetworkInputClientPump(client??throw new ArgumentNullException(nameof(client)));
            _pump.AuthorityReceived+=OnAuthorityReceived;
        }

        /// <summary>推进网络收发，并把所有已到达 Authority 应用到 RollbackCoordinator。</summary>
        public int Tick()
        {
            ThrowIfDisposed();
            return _pump.Tick();
        }

        /// <summary>重新建立支持该能力的底层 Transport。</summary>
        public void Reconnect()
        {
            ThrowIfDisposed();
            _pump.Reconnect();
        }

        /// <summary>发送本地玩家输入。</summary>
        public void SendInput(in PlayerInputSnapshot input)
        {
            ThrowIfDisposed();
            _pump.SendInput(in input);
        }

        public void Dispose()
        {
            if(_isDisposed) return;
            _isDisposed=true;
            _pump.AuthorityReceived-=OnAuthorityReceived;
            _pump.Dispose();
        }

        private void OnAuthorityReceived(ServerAuthorityFramePacket packet)=>_authorityDriver.Apply(in packet);

        private void ThrowIfDisposed()
        {
            if(_isDisposed) throw new ObjectDisposedException(nameof(NetworkRollbackClientRuntime));
        }
    }
}
