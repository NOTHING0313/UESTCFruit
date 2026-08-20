using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace FrameWork.NetworkSync
{
    /// <summary>按配置创建 Raw UDP 或 KCP 网络输入客户端。</summary>
    public static class NetworkInputClientFactory
    {
        public static INetworkInputClient Create(NetworkInputClientOptions options)
        {
            if(options==null) throw new ArgumentNullException(nameof(options));

            return options.TransportMode switch
            {
                NetworkInputTransportMode.RawUdp=>CreateRawUdp(options),
                NetworkInputTransportMode.Kcp=>new KcpNetworkInputClient(options.ServerAddress,options.ServerPort,options.SessionId,options.PlayerID),
                _=>throw new ArgumentOutOfRangeException(nameof(options.TransportMode),options.TransportMode,"Unsupported Network Input Transport")
            };
        }

        private static INetworkInputClient CreateRawUdp(NetworkInputClientOptions options)
        {
            IPAddress address=ResolveIPv4(options.ServerAddress);
            var serverEndPoint=new IPEndPoint(address,options.ServerPort);
            var transportConfig=new UdpTransportConfig(options.BindAddress,options.BindPort,options.MaxDatagramSize);
            return new LocalNetworkInputClient(transportConfig,serverEndPoint,options.SessionId,options.PlayerID);
        }

        private static IPAddress ResolveIPv4(string host)
        {
            if(IPAddress.TryParse(host,out IPAddress address))
            {
                if(address.AddressFamily!=AddressFamily.InterNetwork)
                    throw new NotSupportedException($"IPv4 Required: {address}");

                return address;
            }

            IPAddress result=Dns.GetHostAddresses(host).FirstOrDefault(value=>value.AddressFamily==AddressFamily.InterNetwork);
            return result??throw new InvalidOperationException($"No IPv4 Address Found: {host}");
        }
    }
}
