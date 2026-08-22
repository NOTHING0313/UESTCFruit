namespace FrameWork.NetworkSync
{
    /// <summary>网络输入客户端传输连接生命周期。</summary>
    public enum NetworkInputClientConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
        Faulted
    }
}
