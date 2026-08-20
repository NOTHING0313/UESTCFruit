using kcp2k;

namespace FrameWork.NetworkSync
{
    /// <summary>
    /// UESTCFruit KCP 传输默认配置。
    /// </summary>
    public static class KcpNetworkConfigFactory
    {
        /// <summary>
        /// 创建公网低延迟可靠传输配置。
        /// </summary>
        public static KcpConfig Create()
        {
            return new KcpConfig(
                DualMode:false,
                Mtu:NetworkProtocolConstants.MaxDatagramSize,
                NoDelay:true,
                Interval:10,
                FastResend:2,
                CongestionWindow:false);
        }
    }
}
