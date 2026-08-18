using FrameWork.NetworkSync;
using FrameWork.RollBackSystem;
using System.Net;

namespace UESTCFruit.NetworkSyncAuthorityHost
{
    internal sealed class AuthorityServerApp
    {
        private readonly Program.AuthorityHostOptions _options;
        private readonly Dictionary<int,IPEndPoint> _playerEndPoints=new();
        private bool _running=true;
        private int _processedDatagramCount;
        private int _rejectedDatagramCount;
        private int _authorityFrameCount;
        private uint _nextAuthoritySequence=1;

        public AuthorityServerApp(Program.AuthorityHostOptions options)=>_options=options;

        public int Run()
        {
            using var transport=new UdpTransport(new UdpTransportConfig(_options.BindAddress,_options.Port,NetworkProtocolConstants.MaxDatagramSize));
            var collector=new ServerInputFrameCollector(512);

            for(int playerID=1;playerID<=_options.PlayerCount;playerID++)
                collector.RegisterPlayer(playerID);

            Console.CancelKeyPress+=(_,eventArgs)=>
            {
                eventArgs.Cancel=true;
                _running=false;
            };

            Console.WriteLine("[NETWORK SYNC AUTHORITY SERVER]");
            Console.WriteLine($"Bind        = {transport.LocalEndPoint}");
            Console.WriteLine($"Session     = 0x{_options.SessionId:X8}");
            Console.WriteLine($"Players     = {_options.PlayerCount}");
            Console.WriteLine($"Protocol    = V{NetworkProtocolConstants.Version}");
            Console.WriteLine("Status      = LISTENING");
            Console.WriteLine("Press Ctrl+C To Stop");

            while(_running)
            {
                if(!transport.TryReceive(out UdpReceivedDatagram datagram))
                {
                    Thread.Sleep(1);
                    continue;
                }

                _processedDatagramCount++;

                if(!NetworkPacketSerializer.TryDeserializeClientInput(datagram.Data,out ClientInputPacket packet,out NetworkPacketDecodeError decodeError))
                {
                    Reject($"DecodeFailed Error={decodeError} Remote={datagram.RemoteEndPoint}");
                    continue;
                }

                if(packet.SessionId!=_options.SessionId)
                {
                    Reject($"SessionMismatch Expected=0x{_options.SessionId:X8} Actual=0x{packet.SessionId:X8} Remote={datagram.RemoteEndPoint}");
                    continue;
                }

                int playerID=packet.Input.playerID;

                if(playerID<=0||playerID>_options.PlayerCount)
                {
                    Reject($"InvalidPlayer PlayerID={playerID} Remote={datagram.RemoteEndPoint}");
                    continue;
                }

                if(!TryBindPlayer(playerID,datagram.RemoteEndPoint))
                {
                    Reject($"EndpointMismatch PlayerID={playerID} Remote={datagram.RemoteEndPoint}");
                    continue;
                }

                FrameInputSet completedFrame;

                try
                {
                    if(!collector.TryAddInput(in packet.Input,out completedFrame)) continue;
                }
                catch(InvalidOperationException exception)
                {
                    Reject($"InputConflict PlayerID={playerID} Frame={packet.Input.frameNumber} Message={exception.Message}");
                    continue;
                }

                if(_playerEndPoints.Count!=_options.PlayerCount)
                    throw new InvalidOperationException($"Authority Completed Before All Player Endpoints Bound: Bound={_playerEndPoints.Count}, Expected={_options.PlayerCount}");

                var authorityPacket=new ServerAuthorityFramePacket(_options.SessionId,_nextAuthoritySequence++,completedFrame);
                byte[] bytes=NetworkPacketSerializer.SerializeServerAuthorityFrame(in authorityPacket);

                for(int id=1;id<=_options.PlayerCount;id++)
                    transport.Send(bytes,_playerEndPoints[id]);

                _authorityFrameCount++;

                if(_authorityFrameCount<=5||_authorityFrameCount%10==0)
                    Console.WriteLine($"AUTH Frame={completedFrame.frameNumber} Seq={authorityPacket.Sequence} Players={completedFrame.Count} Bytes={bytes.Length}");
            }

            Console.WriteLine();
            Console.WriteLine("Status      = STOPPED");
            Console.WriteLine($"Processed   = {_processedDatagramCount}");
            Console.WriteLine($"Rejected    = {_rejectedDatagramCount}");
            Console.WriteLine($"Authorities = {_authorityFrameCount}");
            Console.WriteLine($"Bound       = {_playerEndPoints.Count}/{_options.PlayerCount}");

            return _rejectedDatagramCount==0?0:2;
        }

        private bool TryBindPlayer(int playerID,IPEndPoint remoteEndPoint)
        {
            if(_playerEndPoints.TryGetValue(playerID,out IPEndPoint? existing))
                return EndPointEquals(existing,remoteEndPoint);

            foreach(KeyValuePair<int,IPEndPoint> pair in _playerEndPoints)
                if(pair.Key!=playerID&&EndPointEquals(pair.Value,remoteEndPoint))
                    return false;

            IPEndPoint clone=CloneEndPoint(remoteEndPoint);
            _playerEndPoints.Add(playerID,clone);

            Console.WriteLine($"BIND Player={playerID} Endpoint={clone}");
            return true;
        }

        private void Reject(string message)
        {
            _rejectedDatagramCount++;
            Console.WriteLine($"REJECT #{_rejectedDatagramCount}: {message}");
        }

        private static bool EndPointEquals(IPEndPoint a,IPEndPoint b)
            =>a.Port==b.Port&&Equals(a.Address,b.Address);

        private static IPEndPoint CloneEndPoint(IPEndPoint value)
            =>new(value.Address,value.Port);
    }
}