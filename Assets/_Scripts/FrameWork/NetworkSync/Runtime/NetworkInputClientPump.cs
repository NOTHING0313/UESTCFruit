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
        public int ReceivedAuthorityCount { get; private set; }

        /// <summary>收到一个已完成传输层与协议校验的 Authority。</summary>
        public event Action<ServerAuthorityFramePacket> AuthorityReceived;

        public NetworkInputClientPump(INetworkInputClient client)
            =>_client=client??throw new ArgumentNullException(nameof(client));

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
            _client.Dispose();
        }

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
