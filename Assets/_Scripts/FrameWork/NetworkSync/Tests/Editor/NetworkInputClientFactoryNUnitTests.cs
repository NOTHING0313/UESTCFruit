using ECSFrameWork;
using FrameWork.RollBackSystem;
using NUnit.Framework;
using System.Diagnostics;
using System.Threading;

namespace FrameWork.NetworkSync.Tests
{
    [TestFixture]
    public sealed class NetworkInputClientFactoryNUnitTests
    {
        private const uint SessionId=0x11223344u;
        private const int TimeoutMs=3000;

        [Test]
        public void RawUdpFactory_OneFrame_AuthorityRoundTrip()
        {
            using var server=new LocalNetworkInputServer(new UdpTransportConfig("127.0.0.1",0,NetworkProtocolConstants.MaxDatagramSize),SessionId);
            using INetworkInputClient client=NetworkInputClientFactory.Create(
                new NetworkInputClientOptions(NetworkInputTransportMode.RawUdp,"127.0.0.1",server.LocalEndPoint.Port,SessionId,1,"127.0.0.1"));

            Assert.AreEqual(NetworkInputTransportMode.RawUdp,client.TransportMode);
            Assert.IsTrue(client.IsReady);
            Assert.IsFalse(client.HasTransportError);

            server.RegisterPlayer(1,client.LocalEndPoint);

            PlayerInputSnapshot input=CreateInput(1,1);
            client.SendInput(in input);

            Stopwatch stopwatch=Stopwatch.StartNew();
            while(stopwatch.ElapsedMilliseconds<TimeoutMs&&server.ProcessedDatagramCount<1)
            {
                server.TryProcessOneDatagram(out _);
                Thread.Sleep(1);
            }

            Assert.AreEqual(1,server.ProcessedDatagramCount);
            Assert.AreEqual(1,server.AuthorityFrameCount);

            ServerAuthorityFramePacket authority=WaitAuthority(client);
            AssertAuthority(in authority,in input);
        }

        [Test]
        public void KcpFactory_OneFrame_AuthorityRoundTrip()
        {
            using var server=new KcpNetworkInputServer(0,1,SessionId);
            using INetworkInputClient client=NetworkInputClientFactory.Create(
                new NetworkInputClientOptions(NetworkInputTransportMode.Kcp,"127.0.0.1",server.LocalEndPoint.Port,SessionId,1));

            Stopwatch connectStopwatch=Stopwatch.StartNew();
            while(connectStopwatch.ElapsedMilliseconds<TimeoutMs&&!client.IsReady)
            {
                server.Tick();
                client.Tick();
                Thread.Sleep(1);
            }

            Assert.IsTrue(client.IsReady,$"KCP Factory Connect Timeout: Error={client.LastTransportError}");
            Assert.AreEqual(NetworkInputTransportMode.Kcp,client.TransportMode);
            Assert.IsFalse(client.HasTransportError);

            PlayerInputSnapshot input=CreateInput(1,1);
            client.SendInput(in input);

            ServerAuthorityFramePacket authority=default;
            bool received=false;
            Stopwatch stopwatch=Stopwatch.StartNew();

            while(stopwatch.ElapsedMilliseconds<TimeoutMs)
            {
                server.Tick();

                if(client.TryReceiveAuthority(out authority))
                {
                    received=true;
                    break;
                }

                Thread.Sleep(1);
            }

            Assert.IsTrue(received,$"KCP Factory Authority Timeout: Error={client.LastTransportError}");
            Assert.AreEqual(1,server.ProcessedMessageCount);
            Assert.AreEqual(1,server.AuthorityFrameCount);
            AssertAuthority(in authority,in input);
        }

        private static ServerAuthorityFramePacket WaitAuthority(INetworkInputClient client)
        {
            Stopwatch stopwatch=Stopwatch.StartNew();

            while(stopwatch.ElapsedMilliseconds<TimeoutMs)
            {
                client.Tick();

                if(client.TryReceiveAuthority(out ServerAuthorityFramePacket authority))
                    return authority;

                Thread.Sleep(1);
            }

            throw new AssertionException($"Authority Timeout: Mode={client.TransportMode}, Reject={client.LastRejectReason}, Decode={client.LastDecodeError}, TransportError={client.LastTransportError}");
        }

        private static PlayerInputSnapshot CreateInput(int frame,int playerID)
        {
            return new PlayerInputSnapshot(frame,playerID)
            {
                moveX=1f,
                moveY=-1f,
                mouseX=12.5f,
                mouseY=-3.25f,
                heldButtons=(InputButtonFlags)3
            };
        }

        private static void AssertAuthority(in ServerAuthorityFramePacket authority,in PlayerInputSnapshot expected)
        {
            Assert.AreEqual(SessionId,authority.SessionId);
            Assert.AreNotEqual(0u,authority.Sequence);
            Assert.AreEqual(expected.frameNumber,authority.InputSet.frameNumber);
            Assert.AreEqual(1,authority.InputSet.Count);
            Assert.IsTrue(authority.InputSet.TryGetInput(expected.playerID,out PlayerInputSnapshot actual));
            Assert.IsTrue(new PlayerInputSnapshotComparer().IsEqual(expected,actual));
        }
    }
}
