using System;

namespace FrameWork.NetworkSync
{
    /// <summary>UDP Transport V1 绑定配置。</summary>
    public readonly struct UdpTransportConfig
    {
        public readonly string BindAddress;
        public readonly int BindPort;
        public readonly int MaxDatagramSize;

        public UdpTransportConfig(string bindAddress, int bindPort, int maxDatagramSize = NetworkProtocolConstants.MaxDatagramSize)
        {
            if (string.IsNullOrWhiteSpace(bindAddress)) throw new ArgumentException("Bind Address Is Empty", nameof(bindAddress));
            if (bindPort < 0 || bindPort > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(bindPort));
            if (maxDatagramSize <= 0 || maxDatagramSize > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(maxDatagramSize));

            BindAddress = bindAddress;
            BindPort = bindPort;
            MaxDatagramSize = maxDatagramSize;
        }
    }
}