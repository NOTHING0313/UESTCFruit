using FrameWork.NetworkSync;

namespace UESTCFruit.NetworkSyncAuthorityHost
{
    internal sealed class KcpAuthorityServerApp
    {
        private readonly Program.AuthorityHostOptions _options;
        private bool _running=true;
        private int _kcpErrorCount;

        public KcpAuthorityServerApp(Program.AuthorityHostOptions options)=>_options=options;

        public int Run()
        {
            if(!string.Equals(_options.BindAddress,"0.0.0.0",StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException("kcp2k High-Level Server Currently Binds 0.0.0.0 In IPv4 Mode. Use --bind 0.0.0.0.");

            using var server=new KcpNetworkInputServer(
                _options.Port,
                _options.PlayerCount,
                _options.SessionId);

            server.PlayerBound+=(playerID,endPoint)=>
                Console.WriteLine($"BIND Player={playerID} Endpoint={endPoint}");

            server.AuthorityGenerated+=packet=>
            {
                if(server.AuthorityFrameCount<=5||server.AuthorityFrameCount%10==0)
                    Console.WriteLine($"AUTH Frame={packet.InputSet.frameNumber} Seq={packet.Sequence} Players={packet.InputSet.Count} Transport=KCP");
            };

            server.KcpError+=(connectionId,error,message)=>
            {
                _kcpErrorCount++;
                Console.WriteLine($"KCP ERROR #{_kcpErrorCount}: ConnectionID={connectionId} Error={error} Message={message}");
            };

            Console.CancelKeyPress+=(_,eventArgs)=>
            {
                eventArgs.Cancel=true;
                _running=false;
            };

            Console.WriteLine("[NETWORK SYNC KCP AUTHORITY SERVER]");
            Console.WriteLine($"Bind        = {server.LocalEndPoint}");
            Console.WriteLine($"Session     = 0x{_options.SessionId:X8}");
            Console.WriteLine($"Players     = {_options.PlayerCount}");
            Console.WriteLine($"Protocol    = V{NetworkProtocolConstants.Version}");
            Console.WriteLine("Transport   = kcp2k Reliable");
            Console.WriteLine("Status      = LISTENING");
            Console.WriteLine("Press Ctrl+C To Stop");

            while(_running)
            {
                server.Tick();
                Thread.Sleep(1);
            }

            Console.WriteLine();
            Console.WriteLine("Status      = STOPPED");
            Console.WriteLine($"Processed   = {server.ProcessedMessageCount}");
            Console.WriteLine($"Rejected    = {server.RejectedMessageCount}");
            Console.WriteLine($"Authorities = {server.AuthorityFrameCount}");
            Console.WriteLine($"Bound       = {server.BoundPlayerCount}/{_options.PlayerCount}");
            Console.WriteLine($"KcpErrors   = {_kcpErrorCount}");

            if(server.RejectedMessageCount>0)
                Console.WriteLine($"LastReject  = {server.LastRejectMessage}");

            return server.RejectedMessageCount==0&&_kcpErrorCount==0?0:2;
        }
    }
}
