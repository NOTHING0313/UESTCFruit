using ECSFrameWork;
using FrameWork.NetworkSync;
using FrameWork.RollBackSystem;
using System.Diagnostics;

namespace UESTCFruit.NetworkSyncAuthorityHost
{
    internal sealed class KcpAuthorityClientProbeApp
    {
        private readonly Program.AuthorityHostOptions _options;

        public KcpAuthorityClientProbeApp(Program.AuthorityHostOptions options)=>_options=options;

        public int Run()
        {
            var clients=new KcpNetworkInputClient[_options.PlayerCount];

            try
            {
                for(int i=0;i<clients.Length;i++)
                {
                    int playerID=i+1;
                    clients[i]=new KcpNetworkInputClient(_options.Host!,_options.Port,_options.SessionId,playerID);
                }

                WaitConnected(clients);

                Console.WriteLine();
                Console.WriteLine("[NETWORK SYNC KCP AUTHORITY CLIENT PROBE]");
                Console.WriteLine($"Server      = {_options.Host}:{_options.Port}");
                Console.WriteLine($"Session     = 0x{_options.SessionId:X8}");
                Console.WriteLine($"Players     = {_options.PlayerCount}");
                Console.WriteLine($"Frames      = {_options.FrameCount}");
                Console.WriteLine($"Timeout     = {_options.TimeoutMs} ms");
                Console.WriteLine("Transport   = kcp2k Reliable");

                var lastSequences=new uint[clients.Length];
                Stopwatch total=Stopwatch.StartNew();

                for(int frame=1;frame<=_options.FrameCount;frame++)
                {
                    var inputs=new PlayerInputSnapshot[_options.PlayerCount];

                    for(int i=0;i<clients.Length;i++)
                    {
                        int playerID=i+1;
                        PlayerInputSnapshot input=CreateInput(frame,playerID);
                        inputs[i]=input;
                        clients[i].SendInput(in input);
                    }

                    var expected=new FrameInputSet(frame,inputs);
                    var received=new bool[clients.Length];
                    var authorities=new ServerAuthorityFramePacket[clients.Length];
                    Stopwatch stopwatch=Stopwatch.StartNew();

                    while(stopwatch.ElapsedMilliseconds<_options.TimeoutMs)
                    {
                        bool allReceived=true;

                        for(int i=0;i<clients.Length;i++)
                        {
                            if(received[i]) continue;

                            if(clients[i].TryReceiveAuthority(out ServerAuthorityFramePacket packet))
                            {
                                authorities[i]=packet;
                                received[i]=true;
                            }
                            else
                            {
                                allReceived=false;
                            }

                            ThrowIfClientError(clients[i]);
                        }

                        if(received.All(value=>value)) break;
                        Thread.Sleep(1);
                    }

                    for(int i=0;i<clients.Length;i++)
                    {
                        if(!received[i])
                            throw new TimeoutException($"KCP Authority Timeout: Player={i+1}, Frame={frame}, Local={clients[i].LocalEndPoint}");

                        ServerAuthorityFramePacket authority=authorities[i];

                        if(authority.SessionId!=_options.SessionId)
                            throw new InvalidOperationException($"Authority Session Error: Player={i+1}, Frame={frame}");

                        if(authority.Sequence==0||authority.Sequence<=lastSequences[i])
                            throw new InvalidOperationException($"Authority Sequence Error: Player={i+1}, Frame={frame}, Previous={lastSequences[i]}, Actual={authority.Sequence}");

                        lastSequences[i]=authority.Sequence;

                        if(!InputSetsBitEqual(expected,authority.InputSet))
                            throw new InvalidOperationException($"Authority InputSet Error: Player={i+1}, Frame={frame}");
                    }

                    if(frame<=5||frame%10==0||frame==_options.FrameCount)
                        Console.WriteLine($"[{frame}/{_options.FrameCount}] PASS Authority Frame={frame}");
                }

                total.Stop();

                Console.WriteLine();
                Console.WriteLine($"Result      = {_options.FrameCount}/{_options.FrameCount} KCP AUTHORITY FRAMES PASS");
                Console.WriteLine($"Elapsed     = {total.Elapsed.TotalMilliseconds:F2} ms");
                return 0;
            }
            finally
            {
                for(int i=0;i<clients.Length;i++)
                    clients[i]?.Dispose();
            }
        }

        private void WaitConnected(KcpNetworkInputClient[] clients)
        {
            Stopwatch stopwatch=Stopwatch.StartNew();

            while(stopwatch.ElapsedMilliseconds<_options.TimeoutMs)
            {
                bool allConnected=true;

                for(int i=0;i<clients.Length;i++)
                {
                    clients[i].Tick();
                    ThrowIfClientError(clients[i]);
                    if(!clients[i].IsConnected) allConnected=false;
                }

                if(allConnected)
                {
                    for(int i=0;i<clients.Length;i++)
                        Console.WriteLine($"CLIENT Player={i+1} Local={clients[i].LocalEndPoint}");

                    return;
                }

                Thread.Sleep(1);
            }

            throw new TimeoutException($"KCP Connect Timeout: Server={_options.Host}:{_options.Port}, Timeout={_options.TimeoutMs}ms");
        }

        private static void ThrowIfClientError(KcpNetworkInputClient client)
        {
            if(client.LastKcpError.HasValue)
                throw new InvalidOperationException($"KCP Client Error: PlayerID={client.PlayerID}, Error={client.LastKcpError}, Message={client.LastKcpErrorMessage}");

            if(client.LastRejectReason!=NetworkInputExchangeRejectReason.None)
                throw new InvalidOperationException($"KCP Client Authority Reject: PlayerID={client.PlayerID}, Reason={client.LastRejectReason}, Decode={client.LastDecodeError}");
        }

        private static PlayerInputSnapshot CreateInput(int frame,int playerID)
        {
            return new PlayerInputSnapshot(frame,playerID)
            {
                moveX=(frame+playerID)%3-1,
                moveY=(frame*3+playerID)%3-1,
                mouseX=frame*0.25f+playerID,
                mouseY=-frame*0.125f-playerID,
                mouseDeltaX=playerID*0.5f,
                mouseDeltaY=playerID*-0.25f,
                scrollX=playerID,
                scrollY=-playerID
            };
        }

        private static bool InputSetsBitEqual(FrameInputSet a,FrameInputSet b)
        {
            if(!a.IsCreated||!b.IsCreated||a.frameNumber!=b.frameNumber||a.Count!=b.Count) return false;

            for(int i=0;i<a.Count;i++)
            {
                PlayerInputSnapshot inputA=a.GetInputAt(i);
                PlayerInputSnapshot inputB=b.GetInputAt(i);

                var writerA=new NetworkPacketWriter(PlayerInputSnapshotNetworkCodec.WireSize);
                var writerB=new NetworkPacketWriter(PlayerInputSnapshotNetworkCodec.WireSize);

                PlayerInputSnapshotNetworkCodec.Write(writerA,in inputA);
                PlayerInputSnapshotNetworkCodec.Write(writerB,in inputB);

                if(!writerA.ToArray().AsSpan().SequenceEqual(writerB.ToArray())) return false;
            }

            return true;
        }
    }
}
