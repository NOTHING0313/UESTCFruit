using FrameWork.NetworkSync;
using FrameWork.RollBackSystem;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using ECSFrameWork;

namespace UESTCFruit.NetworkSyncAuthorityHost
{
    internal sealed class AuthorityClientProbeApp
    {
        private readonly Program.AuthorityHostOptions _options;

        public AuthorityClientProbeApp(Program.AuthorityHostOptions options)=>_options=options;

        public int Run()
        {
            IPAddress serverAddress=ResolveIPv4(_options.Host!);
            IPEndPoint serverEndPoint=new(serverAddress,_options.Port);

            var clients=new UdpTransport[_options.PlayerCount];

            try
            {
                for(int i=0;i<clients.Length;i++)
                {
                    clients[i]=new UdpTransport(new UdpTransportConfig("0.0.0.0",0,NetworkProtocolConstants.MaxDatagramSize));
                    Console.WriteLine($"CLIENT Player={i+1} Local={clients[i].LocalEndPoint}");
                }

                Console.WriteLine();
                Console.WriteLine("[NETWORK SYNC AUTHORITY CLIENT PROBE]");
                Console.WriteLine($"Server      = {serverEndPoint}");
                Console.WriteLine($"Session     = 0x{_options.SessionId:X8}");
                Console.WriteLine($"Players     = {_options.PlayerCount}");
                Console.WriteLine($"Frames      = {_options.FrameCount}");
                Console.WriteLine($"Timeout     = {_options.TimeoutMs} ms");

                Stopwatch total=Stopwatch.StartNew();

                for(int frame=1;frame<=_options.FrameCount;frame++)
                {
                    var inputs=new PlayerInputSnapshot[_options.PlayerCount];

                    for(int i=0;i<_options.PlayerCount;i++)
                    {
                        int playerID=i+1;
                        PlayerInputSnapshot input=CreateInput(frame,playerID);
                        inputs[i]=input;

                        var packet=new ClientInputPacket(_options.SessionId,(uint)frame,input);
                        byte[] bytes=NetworkPacketSerializer.SerializeClientInput(in packet);
                        clients[i].Send(bytes,serverEndPoint);
                    }

                    var expected=new FrameInputSet(frame,inputs);

                    for(int i=0;i<clients.Length;i++)
                    {
                        ServerAuthorityFramePacket authority=WaitAuthority(clients[i],serverEndPoint);

                        if(authority.SessionId!=_options.SessionId)
                            throw new InvalidOperationException($"Authority Session Error: Player={i+1}, Frame={frame}");

                        if(authority.Sequence!=(uint)frame)
                            throw new InvalidOperationException($"Authority Sequence Error: Player={i+1}, Frame={frame}, Expected={frame}, Actual={authority.Sequence}");

                        if(!InputSetsBitEqual(expected,authority.InputSet))
                            throw new InvalidOperationException($"Authority InputSet Error: Player={i+1}, Frame={frame}");
                    }

                    if(frame<=5||frame%10==0||frame==_options.FrameCount)
                        Console.WriteLine($"[{frame}/{_options.FrameCount}] PASS Authority Frame={frame}");
                }

                total.Stop();

                Console.WriteLine();
                Console.WriteLine($"Result      = {_options.FrameCount}/{_options.FrameCount} AUTHORITY FRAMES PASS");
                Console.WriteLine($"Elapsed     = {total.Elapsed.TotalMilliseconds:F2} ms");

                return 0;
            }
            finally
            {
                for(int i=0;i<clients.Length;i++)
                    clients[i]?.Dispose();
            }
        }

        private ServerAuthorityFramePacket WaitAuthority(UdpTransport transport,IPEndPoint serverEndPoint)
        {
            Stopwatch stopwatch=Stopwatch.StartNew();

            while(stopwatch.ElapsedMilliseconds<_options.TimeoutMs)
            {
                if(!transport.TryReceive(out UdpReceivedDatagram datagram))
                {
                    Thread.Sleep(1);
                    continue;
                }

                if(!EndPointEquals(datagram.RemoteEndPoint,serverEndPoint))
                    throw new InvalidOperationException($"Authority Endpoint Error: Expected={serverEndPoint}, Actual={datagram.RemoteEndPoint}");

                if(!NetworkPacketSerializer.TryDeserializeServerAuthorityFrame(datagram.Data,out ServerAuthorityFramePacket packet,out NetworkPacketDecodeError error))
                    throw new InvalidOperationException($"Authority Decode Error: {error}");

                return packet;
            }

            throw new TimeoutException($"Authority Timeout: Local={transport.LocalEndPoint}, Server={serverEndPoint}, Timeout={_options.TimeoutMs}ms");
        }

        private static PlayerInputSnapshot CreateInput(int frame,int playerID)
        {
            float moveX=(frame+playerID)%3-1;
            float moveY=(frame*3+playerID)%3-1;

            return new PlayerInputSnapshot(frame,playerID)
            {
                moveX=moveX,
                moveY=moveY,
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

                if(!InputsBitEqual(in inputA,in inputB)) return false;
            }

            return true;
        }

        private static bool InputsBitEqual(in PlayerInputSnapshot a,in PlayerInputSnapshot b)
        {
            var writerA=new NetworkPacketWriter(PlayerInputSnapshotNetworkCodec.WireSize);
            var writerB=new NetworkPacketWriter(PlayerInputSnapshotNetworkCodec.WireSize);

            PlayerInputSnapshotNetworkCodec.Write(writerA,in a);
            PlayerInputSnapshotNetworkCodec.Write(writerB,in b);

            return writerA.ToArray().AsSpan().SequenceEqual(writerB.ToArray());
        }

        private static IPAddress ResolveIPv4(string host)
        {
            if(IPAddress.TryParse(host,out IPAddress? address))
            {
                if(address.AddressFamily!=AddressFamily.InterNetwork)
                    throw new NotSupportedException($"IPv4 Required: {address}");

                return address;
            }

            IPAddress? result=Dns.GetHostAddresses(host).FirstOrDefault(x=>x.AddressFamily==AddressFamily.InterNetwork);
            return result??throw new InvalidOperationException($"No IPv4 Address Found: {host}");
        }

        private static bool EndPointEquals(IPEndPoint a,IPEndPoint b)
            =>a.Port==b.Port&&Equals(a.Address,b.Address);
    }
}