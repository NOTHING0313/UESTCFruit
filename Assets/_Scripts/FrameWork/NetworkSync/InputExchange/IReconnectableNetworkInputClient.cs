namespace FrameWork.NetworkSync
{
    /// <summary>支持原地重新建立传输连接的网络输入客户端。</summary>
    public interface IReconnectableNetworkInputClient
    {
        bool CanReconnect { get; }
        void Reconnect();
    }
}
