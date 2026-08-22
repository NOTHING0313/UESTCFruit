using kcp2k;
using NUnit.Framework;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading;

namespace FrameWork.NetworkSync.Tests
{
    [TestFixture]
    public sealed class NetworkInputClientConnectionStateNUnitTests
    {
        private const uint SessionId=0x11223344u;
        private const int TimeoutMs=3000;

        [Test]
        public void RawUdp_OperationalLifetime_ReportsConnectedThenDisconnected()
        {
            var client=new LocalNetworkInputClient(
                new UdpTransportConfig("127.0.0.1",0),
                new IPEndPoint(IPAddress.Loopback,28015),
                SessionId,
                1);

            var states=new List<NetworkInputClientConnectionState>();
            client.ConnectionStateChanged+=states.Add;

            Assert.AreEqual(NetworkInputClientConnectionState.Connected,client.ConnectionState);
            Assert.IsTrue(client.IsReady);

            client.Dispose();

            Assert.AreEqual(NetworkInputClientConnectionState.Disconnected,client.ConnectionState);
            CollectionAssert.AreEqual(new[] { NetworkInputClientConnectionState.Disconnected },states);
        }

        [Test]
        public void Kcp_ConnectAndDispose_ReportsExplicitLifecycle()
        {
            using var server=new KcpNetworkInputServer(0,1,SessionId);
            int port=server.LocalEndPoint.Port;
            var client=new KcpNetworkInputClient("127.0.0.1",port,SessionId,1);
            var states=new List<NetworkInputClientConnectionState>();
            client.ConnectionStateChanged+=states.Add;

            try
            {
                Assert.AreEqual(NetworkInputClientConnectionState.Connecting,client.ConnectionState);
                WaitConnected(server,client);

                Assert.AreEqual(NetworkInputClientConnectionState.Connected,client.ConnectionState);
                CollectionAssert.Contains(states,NetworkInputClientConnectionState.Connected);

                client.Dispose();

                Assert.AreEqual(NetworkInputClientConnectionState.Disconnected,client.ConnectionState);
                CollectionAssert.Contains(states,NetworkInputClientConnectionState.Disconnected);
            }
            finally
            {
                client.Dispose();
            }
        }

        [Test]
        public void Kcp_ServerBlackhole_ReportsFaultedWithTimeout()
        {
            KcpConfig serverConfig=CreateShortTimeoutConfig();
            KcpConfig clientConfig=CreateShortTimeoutConfig();
            using var server=new KcpNetworkInputServer(0,1,SessionId,512,serverConfig);
            int port=server.LocalEndPoint.Port;
            using var client=new KcpNetworkInputClient("127.0.0.1",port,SessionId,1,clientConfig);
            var states=new List<NetworkInputClientConnectionState>();
            client.ConnectionStateChanged+=states.Add;

            WaitConnected(server,client);
            Assert.AreEqual(NetworkInputClientConnectionState.Connected,client.ConnectionState);

            // 不 Dispose Server。
            // Dispose/Stop 会尝试向 Client 发送 KCP Disconnect，因此测试结果可能是合法 Disconnected，
            // 与 Timeout/Faulted 产生竞态。这里故意停止 Server.Tick，让 socket 仍存在但完全不响应，
            // 稳定模拟 NAT/断网/进程卡死一类“静默黑洞”。
            Stopwatch stopwatch=Stopwatch.StartNew();
            while(stopwatch.ElapsedMilliseconds<TimeoutMs&&client.ConnectionState!=NetworkInputClientConnectionState.Faulted)
            {
                client.Tick();
                Thread.Sleep(5);
            }

            Assert.AreEqual(
                NetworkInputClientConnectionState.Faulted,
                client.ConnectionState,
                $"04A-2 Expected Silent Server Loss To Timeout. States={string.Join(" -> ",states)}, Error={client.LastKcpError}:{client.LastKcpErrorMessage}");

            Assert.IsFalse(client.IsReady);
            Assert.AreEqual(ErrorCode.Timeout,client.LastKcpError);
            CollectionAssert.Contains(states,NetworkInputClientConnectionState.Faulted);
        }

        private static KcpConfig CreateShortTimeoutConfig()
        {
            KcpConfig config=KcpNetworkConfigFactory.Create();
            config.Timeout=250;
            config.MaxRetransmits=5;
            return config;
        }

        private static void WaitConnected(KcpNetworkInputServer server,KcpNetworkInputClient client)
        {
            Stopwatch stopwatch=Stopwatch.StartNew();

            while(stopwatch.ElapsedMilliseconds<TimeoutMs)
            {
                server.Tick();
                client.Tick();

                if(client.ConnectionState==NetworkInputClientConnectionState.Connected&&server.ConnectedConnectionCount==1)
                    return;

                Thread.Sleep(1);
            }

            Assert.Fail(
                $"04A-2 Connect Timeout: ClientState={client.ConnectionState}, Connections={server.ConnectedConnectionCount}, " +
                $"Error={client.LastKcpError}:{client.LastKcpErrorMessage}");
        }
    }
}
