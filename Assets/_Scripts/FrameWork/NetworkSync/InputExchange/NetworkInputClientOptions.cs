using System;

namespace FrameWork.NetworkSync
{
    /// <summary>网络输入客户端创建参数。</summary>
    public sealed class NetworkInputClientOptions
    {
        public NetworkInputTransportMode TransportMode { get; }
        public string ServerAddress { get; }
        public int ServerPort { get; }
        public uint SessionId { get; }
        public int PlayerID { get; }
        public string BindAddress { get; }
        public int BindPort { get; }
        public int MaxDatagramSize { get; }

        public NetworkInputClientOptions(NetworkInputTransportMode transportMode,string serverAddress,int serverPort,uint sessionId,int playerID,string bindAddress="0.0.0.0",int bindPort=0,int maxDatagramSize=NetworkProtocolConstants.MaxDatagramSize)
        {
            if(string.IsNullOrWhiteSpace(serverAddress)) throw new ArgumentException("Server Address Is Empty",nameof(serverAddress));
            if(serverPort<=0||serverPort>ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(serverPort));
            if(playerID<=0) throw new ArgumentOutOfRangeException(nameof(playerID));
            if(string.IsNullOrWhiteSpace(bindAddress)) throw new ArgumentException("Bind Address Is Empty",nameof(bindAddress));
            if(bindPort<0||bindPort>ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(bindPort));
            if(maxDatagramSize<=0) throw new ArgumentOutOfRangeException(nameof(maxDatagramSize));

            TransportMode=transportMode;
            ServerAddress=serverAddress;
            ServerPort=serverPort;
            SessionId=sessionId;
            PlayerID=playerID;
            BindAddress=bindAddress;
            BindPort=bindPort;
            MaxDatagramSize=maxDatagramSize;
        }
    }
}
