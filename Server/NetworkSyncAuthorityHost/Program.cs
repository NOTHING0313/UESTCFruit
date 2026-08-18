using System.Globalization;

namespace UESTCFruit.NetworkSyncAuthorityHost
{
    internal static class Program
    {
        private const uint DefaultSessionId=0x11223344u;

        private static int Main(string[] args)
        {
            try
            {
                if(args.Length==0) return ShowUsage();

                AuthorityHostOptions options=AuthorityHostOptions.Parse(args);

                return args[0].ToLowerInvariant() switch
                {
                    "server"=>new AuthorityServerApp(options).Run(),
                    "client"=>new AuthorityClientProbeApp(options).Run(),
                    _=>ShowUsage()
                };
            }
            catch(Exception exception)
            {
                Console.Error.WriteLine($"NetworkSyncAuthorityHost Main Error: {exception.GetType().Name}: {exception.Message}");
                return 1;
            }
        }

        private static int ShowUsage()
        {
            Console.WriteLine("NetworkSyncAuthorityHost");
            Console.WriteLine();
            Console.WriteLine("Server:");
            Console.WriteLine("  NetworkSyncAuthorityHost server --port 28015 --players 2 --session 0x11223344");
            Console.WriteLine();
            Console.WriteLine("Client:");
            Console.WriteLine("  NetworkSyncAuthorityHost client --host <IPv4> --port 28015 --players 2 --frames 100 --timeout 3000 --session 0x11223344");
            return 1;
        }

        internal sealed class AuthorityHostOptions
        {
            public string BindAddress { get; private set; }="0.0.0.0";
            public string? Host { get; private set; }
            public int Port { get; private set; }=28015;
            public int PlayerCount { get; private set; }=2;
            public int FrameCount { get; private set; }=100;
            public int TimeoutMs { get; private set; }=3000;
            public uint SessionId { get; private set; }=DefaultSessionId;

            public static AuthorityHostOptions Parse(string[] args)
            {
                var options=new AuthorityHostOptions
                {
                    BindAddress=GetStringArg(args,"--bind")??"0.0.0.0",
                    Host=GetStringArg(args,"--host"),
                    Port=GetIntArg(args,"--port",28015,1,ushort.MaxValue),
                    PlayerCount=GetIntArg(args,"--players",2,1,16),
                    FrameCount=GetIntArg(args,"--frames",100,1,100000),
                    TimeoutMs=GetIntArg(args,"--timeout",3000,100,60000),
                    SessionId=GetUIntArg(args,"--session",DefaultSessionId)
                };

                if(string.Equals(args[0],"client",StringComparison.OrdinalIgnoreCase)&&string.IsNullOrWhiteSpace(options.Host))
                    throw new ArgumentException("Client Requires --host");

                return options;
            }

            private static string? GetStringArg(string[] args,string name)
            {
                for(int i=1;i<args.Length-1;i++)
                    if(string.Equals(args[i],name,StringComparison.OrdinalIgnoreCase))
                        return args[i+1];

                return null;
            }

            private static int GetIntArg(string[] args,string name,int defaultValue,int min,int max)
            {
                string? raw=GetStringArg(args,name);
                if(raw==null) return defaultValue;

                if(!int.TryParse(raw,NumberStyles.Integer,CultureInfo.InvariantCulture,out int value)||value<min||value>max)
                    throw new ArgumentOutOfRangeException(name,$"Expected {min}~{max}, Actual={raw}");

                return value;
            }

            private static uint GetUIntArg(string[] args,string name,uint defaultValue)
            {
                string? raw=GetStringArg(args,name);
                if(raw==null) return defaultValue;

                if(raw.StartsWith("0x",StringComparison.OrdinalIgnoreCase))
                {
                    if(uint.TryParse(raw[2..],NumberStyles.HexNumber,CultureInfo.InvariantCulture,out uint hexValue))
                        return hexValue;
                }
                else if(uint.TryParse(raw,NumberStyles.Integer,CultureInfo.InvariantCulture,out uint value))
                {
                    return value;
                }

                throw new ArgumentException($"Invalid UInt32 Argument: {name}={raw}");
            }
        }
    }
}