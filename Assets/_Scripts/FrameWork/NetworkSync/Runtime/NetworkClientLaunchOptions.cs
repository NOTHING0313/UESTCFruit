using System;
using System.Globalization;

namespace FrameWork.NetworkSync
{
    /// <summary>
    /// 双客户端 Scene Smoke 的命令行覆盖参数。
    /// 未提供的字段继续使用 Inspector 配置。
    /// </summary>
    public sealed class NetworkClientLaunchOptions
    {
        public int? PlayerID { get; private set; }
        public int? PlayerCount { get; private set; }
        public string ServerAddress { get; private set; }
        public int? ServerPort { get; private set; }
        public uint? SessionId { get; private set; }

        public bool HasAnyOverride
            =>PlayerID.HasValue||PlayerCount.HasValue||ServerAddress!=null||ServerPort.HasValue||SessionId.HasValue;

        /// <summary>解析 Unity/Player 命令行；未知参数直接忽略。</summary>
        public static NetworkClientLaunchOptions Parse(string[] args)
        {
            var result=new NetworkClientLaunchOptions();
            if(args==null||args.Length==0) return result;

            for(int i=0;i<args.Length;i++)
            {
                if(TryRead(args,ref i,"--network-player-id",out string playerIDText))
                {
                    result.PlayerID=ParsePositiveInt(playerIDText,"network-player-id");
                    continue;
                }

                if(TryRead(args,ref i,"--network-player-count",out string playerCountText))
                {
                    result.PlayerCount=ParsePositiveInt(playerCountText,"network-player-count");
                    continue;
                }

                if(TryRead(args,ref i,"--network-server",out string server))
                {
                    if(string.IsNullOrWhiteSpace(server)) throw new ArgumentException("network-server Is Empty");
                    result.ServerAddress=server.Trim();
                    continue;
                }

                if(TryRead(args,ref i,"--network-port",out string portText))
                {
                    int port=ParsePositiveInt(portText,"network-port");
                    if(port>ushort.MaxValue) throw new ArgumentOutOfRangeException("network-port",port,"Network Port Must Be <= 65535");
                    result.ServerPort=port;
                    continue;
                }

                if(TryRead(args,ref i,"--network-session",out string sessionText))
                {
                    result.SessionId=ParseUInt(sessionText,"network-session");
                    continue;
                }
            }

            return result;
        }

        private static bool TryRead(string[] args,ref int index,string key,out string value)
        {
            string arg=args[index];
            string prefix=key+"=";

            if(arg.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))
            {
                value=arg.Substring(prefix.Length);
                return true;
            }

            if(string.Equals(arg,key,StringComparison.OrdinalIgnoreCase))
            {
                if(index+1>=args.Length) throw new ArgumentException($"{key} Missing Value");
                value=args[++index];
                return true;
            }

            value=null;
            return false;
        }

        private static int ParsePositiveInt(string value,string name)
        {
            if(!int.TryParse(value,NumberStyles.Integer,CultureInfo.InvariantCulture,out int result)||result<=0)
                throw new ArgumentException($"{name} Invalid Positive Integer: {value}");
            return result;
        }

        private static uint ParseUInt(string value,string name)
        {
            if(string.IsNullOrWhiteSpace(value)) throw new ArgumentException($"{name} Is Empty");

            string text=value.Trim();
            NumberStyles style=NumberStyles.Integer;

            if(text.StartsWith("0x",StringComparison.OrdinalIgnoreCase))
            {
                text=text.Substring(2);
                style=NumberStyles.HexNumber;
            }

            if(!uint.TryParse(text,style,CultureInfo.InvariantCulture,out uint result))
                throw new ArgumentException($"{name} Invalid UInt32: {value}");

            return result;
        }
    }
}
