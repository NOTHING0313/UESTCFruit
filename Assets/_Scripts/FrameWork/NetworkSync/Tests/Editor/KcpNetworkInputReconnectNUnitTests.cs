using ECSFrameWork;
using kcp2k;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.Threading;

namespace FrameWork.NetworkSync.Tests
{
    [TestFixture]
    public sealed class KcpNetworkInputReconnectNUnitTests
    {
        private const uint SessionId=0x11223344u;
        private const int TimeoutMs=3000;

        [Test]
        public void FaultedClient_ReconnectsSameInstance_RebindsAndContinuesSequence()
        {
            KcpConfig serverConfig=CreateShortTimeoutConfig();
            KcpConfig clientConfig=CreateShortTimeoutConfig();

            using var server=new KcpNetworkInputServer(0,1,SessionId,512,serverConfig);
            int port=server.LocalEndPoint.Port;
            using var client=new KcpNetworkInputClient("127.0.0.1",port,SessionId,1,clientConfig);

            WaitConnected(server,client);

            SendInput(client,1);
            WaitAuthority(server,client,1);
            Assert.AreEqual(1u,client.LastSentSequence);
            Assert.AreEqual(1,server.BoundPlayerCount);

            // 静默停止 Server Tick，让 Client 走真实 KCP Timeout -> Faulted。
            WaitClientFaulted(client);

            Assert.AreEqual(NetworkInputClientConnectionState.Faulted,client.ConnectionState);
            Assert.IsTrue(client.CanReconnect);
            Assert.AreEqual(ErrorCode.Timeout,client.LastKcpError);

            // Server 恢复 Tick 后先清掉旧超时 Connection / Player Binding。
            WaitServerUnbound(server);
            Assert.AreEqual(0,server.ConnectedConnectionCount);
            Assert.AreEqual(0,server.BoundPlayerCount);

            client.Reconnect();

            Assert.AreEqual(NetworkInputClientConnectionState.Connecting,client.ConnectionState);
            Assert.IsFalse(client.HasTransportError);
            Assert.AreEqual(NetworkInputExchangeRejectReason.None,client.LastRejectReason);

            WaitConnected(server,client);

            // 同一 Client 对象保留 Session / PlayerID / Sequence。
            Assert.AreEqual(SessionId,client.SessionId);
            Assert.AreEqual(1,client.PlayerID);

            SendInput(client,2);
            WaitAuthority(server,client,2);

            Assert.AreEqual(2u,client.LastSentSequence,
                "04A-4A In-Process Reconnect Must Preserve Application Sequence");
            Assert.AreEqual(1,server.BoundPlayerCount);
            Assert.AreEqual(1,server.ConnectedConnectionCount);
            Assert.AreEqual(0,server.RejectedMessageCount);
            Assert.AreEqual(NetworkInputClientConnectionState.Connected,client.ConnectionState);
        }

        [Test]
        public void ConnectedClient_ReconnectIsRejected()
        {
            using var server=new KcpNetworkInputServer(0,1,SessionId);
            int port=server.LocalEndPoint.Port;
            using var client=new KcpNetworkInputClient("127.0.0.1",port,SessionId,1);

            WaitConnected(server,client);

            InvalidOperationException exception=Assert.Throws<InvalidOperationException>(client.Reconnect);
            StringAssert.Contains("Cannot Reconnect",exception.Message);
        }

        [Test]
        public void ReconnectCapability_IsKcpOnly()
        {
            Assert.IsTrue(typeof(IReconnectableNetworkInputClient).IsAssignableFrom(typeof(KcpNetworkInputClient)));
            Assert.IsFalse(typeof(IReconnectableNetworkInputClient).IsAssignableFrom(typeof(LocalNetworkInputClient)));
        }

        private static KcpConfig CreateShortTimeoutConfig()
        {
            KcpConfig config=KcpNetworkConfigFactory.Create();
            config.Timeout=250;
            config.MaxRetransmits=5;
            return config;
        }

        private static void SendInput(KcpNetworkInputClient client,int frame)
        {
            var input=new PlayerInputSnapshot(frame,1){moveX=frame%2==0?1f:-1f};
            client.SendInput(in input);
        }

        private static void WaitConnected(KcpNetworkInputServer server,KcpNetworkInputClient client)
        {
            Stopwatch stopwatch=Stopwatch.StartNew();

            while(stopwatch.ElapsedMilliseconds<TimeoutMs)
            {
                server.Tick();
                client.Tick();

                if(client.ConnectionState==NetworkInputClientConnectionState.Connected&&
                   server.ConnectedConnectionCount==1)
                    return;

                Thread.Sleep(1);
            }

            Assert.Fail(
                $"04A-4A Connect Timeout: State={client.ConnectionState}, " +
                $"Connections={server.ConnectedConnectionCount}, Bound={server.BoundPlayerCount}, " +
                $"Error={client.LastKcpError}:{client.LastKcpErrorMessage}");
        }

        private static void WaitClientFaulted(KcpNetworkInputClient client)
        {
            Stopwatch stopwatch=Stopwatch.StartNew();

            while(stopwatch.ElapsedMilliseconds<TimeoutMs&&
                  client.ConnectionState!=NetworkInputClientConnectionState.Faulted)
            {
                client.Tick();
                Thread.Sleep(5);
            }

            Assert.AreEqual(
                NetworkInputClientConnectionState.Faulted,
                client.ConnectionState,
                $"04A-4A Client Fault Timeout: Error={client.LastKcpError}:{client.LastKcpErrorMessage}");
        }

        private static void WaitServerUnbound(KcpNetworkInputServer server)
        {
            Stopwatch stopwatch=Stopwatch.StartNew();

            while(stopwatch.ElapsedMilliseconds<TimeoutMs)
            {
                server.Tick();

                if(server.ConnectedConnectionCount==0&&server.BoundPlayerCount==0)
                    return;

                Thread.Sleep(5);
            }

            Assert.Fail(
                $"04A-4A Server Old Connection Did Not Clear: " +
                $"Connections={server.ConnectedConnectionCount}, Bound={server.BoundPlayerCount}, " +
                $"Error={server.LastKcpError}:{server.LastKcpErrorMessage}");
        }

        private static void WaitAuthority(KcpNetworkInputServer server,KcpNetworkInputClient client,int expectedCount)
        {
            Stopwatch stopwatch=Stopwatch.StartNew();

            while(stopwatch.ElapsedMilliseconds<TimeoutMs)
            {
                server.Tick();
                client.Tick();

                while(client.TryReceiveAuthority(out _)) { }

                if(server.AuthorityFrameCount>=expectedCount) return;
                Thread.Sleep(1);
            }

            Assert.Fail(
                $"04A-4A Authority Timeout: Expected={expectedCount}, Actual={server.AuthorityFrameCount}, " +
                $"Rejected={server.RejectedMessageCount}, LastReject={server.LastRejectMessage}");
        }
    }
}
