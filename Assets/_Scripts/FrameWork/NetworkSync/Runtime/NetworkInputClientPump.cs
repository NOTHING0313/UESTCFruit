using ECSFrameWork;
using System;

namespace FrameWork.NetworkSync
{
    /// <summary>
    /// 统一推进网络输入客户端并派发服务器 Authority。
    /// </summary>
    public sealed class NetworkInputClientPump : IDisposable
    {
        private readonly INetworkInputClient _client;
        private bool _isDisposed;

        public INetworkInputClient Client => _client;
        public NetworkInputClientConnectionState ConnectionState => _client.ConnectionState;
        public bool CanReconnect => _client is IReconnectableNetworkInputClient reconnectable&&reconnectable.CanReconnect;
        public int ReceivedAuthorityCount { get; private set; }

        /// <summary>收到一个已完成传输层与协议校验的 Authority。</summary>
        public event Action<ServerAuthorityFramePacket> AuthorityReceived;

        /// <summary>底层传输连接状态发生变化。</summary>
        public event Action<NetworkInputClientConnectionState> ConnectionStateChanged;

        public NetworkInputClientPump(INetworkInputClient client)
        {
            _client=client??throw new ArgumentNullException(nameof(client));
            _client.ConnectionStateChanged+=OnConnectionStateChanged;
        }

        /// <summary>推进底层传输并派发当前已到达的全部 Authority。</summary>
        public int Tick()
        {
            ThrowIfDisposed();
            _client.Tick();
            ThrowIfClientError();

            int received=0;

            while(_client.TryReceiveAuthority(out ServerAuthorityFramePacket packet))
            {
                ReceivedAuthorityCount++;
                received++;
                AuthorityReceived?.Invoke(packet);
                ThrowIfClientError();
            }

            return received;
        }

        /// <summary>重新建立支持该能力的底层 Transport。</summary>
        public void Reconnect()
        {
            ThrowIfDisposed();

            if(_client is not IReconnectableNetworkInputClient reconnectable)
                throw new NotSupportedException(
                    $"Network Input Client Does Not Support Reconnect: Transport={_client.TransportMode}");

            reconnectable.Reconnect();
        }

        /// <summary>发送当前客户端玩家的一帧输入。</summary>
        public void SendInput(in PlayerInputSnapshot input)
        {
            ThrowIfDisposed();
            ThrowIfClientError();
            _client.SendInput(in input);
        }

        public void Dispose()
        {
            if(_isDisposed) return;
            _isDisposed=true;
            _client.ConnectionStateChanged-=OnConnectionStateChanged;
            _client.Dispose();
        }

        private void OnConnectionStateChanged(NetworkInputClientConnectionState state)
            =>ConnectionStateChanged?.Invoke(state);

        private void ThrowIfClientError()
        {
            if(_client.LastRejectReason!=NetworkInputExchangeRejectReason.None)
                throw new InvalidOperationException(
                    $"NetworkInputClientPump Tick Error: PlayerID={_client.PlayerID}, Reject={_client.LastRejectReason}, Decode={_client.LastDecodeError}");

            if(_client.HasTransportError)
                throw new InvalidOperationException(
                    $"NetworkInputClientPump Tick Error: PlayerID={_client.PlayerID}, Transport={_client.TransportMode}, Error={_client.LastTransportError}");
        }

        private void ThrowIfDisposed()
        {
            if(_isDisposed) throw new ObjectDisposedException(nameof(NetworkInputClientPump));
        }
    }
}
