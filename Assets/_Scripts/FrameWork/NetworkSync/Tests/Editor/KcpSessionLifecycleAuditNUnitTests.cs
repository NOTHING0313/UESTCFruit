using ECSFrameWork;
using kcp2k;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;

namespace FrameWork.NetworkSync.Tests
{
    /// <summary>
    /// 04A-1：只审计当前 Disconnect / Rebind / Session 生命周期，不修改生产代码。
    /// </summary>
    [TestFixture]
    public sealed class KcpSessionLifecycleAuditNUnitTests
    {
        private const uint SessionId=0x11223344u;
        private const int TimeoutMs=3000;

        [Test]
        public void MissingPlayer_AuthorityStalls_WithoutAccumulatingNewPendingFrames()
        {
            using var server=new KcpNetworkInputServer(0,2,SessionId);
            int port=server.LocalEndPoint.Port;

            using var client1=new KcpNetworkInputClient("127.0.0.1",port,SessionId,1);
            var client2=new KcpNetworkInputClient("127.0.0.1",port,SessionId,2);

            try
            {
                WaitConnected(server,client1,client2);

                SendFrame(client1,client2,1);
                WaitAuthorityFrameCount(server,client1,client2,1);
                DrainAuthority(client1);
                DrainAuthority(client2);

                client2.Dispose();
                WaitServerConnectionState(server,client1,1,1);

                for(int frame=2;frame<=6;frame++)
                {
                    PlayerInputSnapshot input=CreateInput(frame,1);
                    client1.SendInput(in input);
                    Pump(server,client1,null,5);
                }

                Assert.AreEqual(1,server.AuthorityFrameCount,
                    "04A-4B-1 Authority Must Stall While One Player Is Missing");
                Assert.AreEqual(0,server.PendingFrameCount,
                    "04A-4B-1 Missing Player Window Must Not Accumulate New Pending Frames");
                Assert.AreEqual(5,server.DroppedIncompleteSessionInputCount,
                    "04A-4B-1 Missing Player Window Drop Count");
                Assert.AreEqual(0,server.RejectedMessageCount);
            }
            finally
            {
                client2.Dispose();
            }
        }

        [Test]
        public void ServerLoss_ClientTimesOut_BecomesNotReady_AndSendFails()
        {
            KcpConfig serverConfig=CreateShortTimeoutConfig();
            KcpConfig clientConfig=CreateShortTimeoutConfig();
            var server=new KcpNetworkInputServer(0,1,SessionId,512,serverConfig);
            int port=server.LocalEndPoint.Port;
            using var client=new KcpNetworkInputClient("127.0.0.1",port,SessionId,1,clientConfig);

            try
            {
                WaitConnected(server,client,null);
                Assert.IsTrue(client.IsReady,"04A-1 Client Must Be Ready Before Server Loss");

                // KcpServer.Stop 直接关闭 Socket；客户端只能依靠 KCP Timeout/DeadLink 感知远端消失。
                server.Dispose();

                Stopwatch stopwatch=Stopwatch.StartNew();
                while(stopwatch.ElapsedMilliseconds<TimeoutMs&&client.IsReady)
                {
                    client.Tick();
                    Thread.Sleep(5);
                }

                Assert.IsFalse(client.IsReady,"04A-1 Client Must Leave Ready State After Server Loss");
                Assert.IsFalse(client.IsConnected,"04A-1 Client Must Leave Connected State After Server Loss");
                Assert.IsTrue(client.LastKcpError.HasValue,"04A-1 Server Loss Must Produce KCP Error State");
                Assert.AreEqual(ErrorCode.Timeout,client.LastKcpError.Value,
                    $"04A-1 Expected Timeout Error, Actual={client.LastKcpError}:{client.LastKcpErrorMessage}");
                StringAssert.Contains("timed out",client.LastKcpErrorMessage.ToLowerInvariant());

                PlayerInputSnapshot input=CreateInput(2,1);
                InvalidOperationException exception=Assert.Throws<InvalidOperationException>(
                    ()=>client.SendInput(in input));
                StringAssert.Contains("not connected",exception.Message.ToLowerInvariant());
            }
            finally
            {
                server.Dispose();
            }
        }

        [Test]
        public void CurrentNetworkClientContract_UsesOptionalReconnectCapability()
        {
            BindingFlags flags=BindingFlags.Instance|BindingFlags.Public;

            Assert.IsNull(typeof(INetworkInputClient).GetMethod("Reconnect",flags),
                "04A-4A Generic INetworkInputClient Must Not Force Connection-Oriented Semantics Onto Raw UDP");
            Assert.IsNotNull(typeof(IReconnectableNetworkInputClient).GetMethod("Reconnect",flags),
                "04A-4A Reconnect Capability Contract Missing");
            Assert.IsTrue(typeof(IReconnectableNetworkInputClient).IsAssignableFrom(typeof(KcpNetworkInputClient)),
                "04A-4A KCP Client Must Implement Reconnect Capability");
            Assert.IsFalse(typeof(IReconnectableNetworkInputClient).IsAssignableFrom(typeof(LocalNetworkInputClient)),
                "04A-4A Raw UDP Client Must Not Pretend To Support Connection Reconnect");
        }

        private static KcpConfig CreateShortTimeoutConfig()
        {
            KcpConfig config=KcpNetworkConfigFactory.Create();
            config.Timeout=250;
            config.MaxRetransmits=5;
            return config;
        }

        private static void SendFrame(KcpNetworkInputClient client1,KcpNetworkInputClient client2,int frame)
        {
            PlayerInputSnapshot input1=CreateInput(frame,1);
            PlayerInputSnapshot input2=CreateInput(frame,2);
            client1.SendInput(in input1);
            client2.SendInput(in input2);
        }

        private static PlayerInputSnapshot CreateInput(int frame,int playerID)
            =>new(frame,playerID)
            {
                moveX=((frame+playerID)%3)-1,
                moveY=((frame*2+playerID)%3)-1
            };

        private static void WaitConnected(
            KcpNetworkInputServer server,
            KcpNetworkInputClient client1,
            KcpNetworkInputClient client2)
        {
            Stopwatch stopwatch=Stopwatch.StartNew();

            while(stopwatch.ElapsedMilliseconds<TimeoutMs)
            {
                server.Tick();
                client1?.Tick();
                client2?.Tick();

                int expected=client2==null?1:2;
                bool ready1=client1==null||client1.IsConnected;
                bool ready2=client2==null||client2.IsConnected;

                if(ready1&&ready2&&server.ConnectedConnectionCount==expected) return;
                Thread.Sleep(1);
            }

            Assert.Fail(
                $"04A-1 Connect Timeout: Connections={server.ConnectedConnectionCount}, " +
                $"Client1={client1?.IsConnected}, Client2={client2?.IsConnected}, " +
                $"Client1Error={client1?.LastKcpError}:{client1?.LastKcpErrorMessage}, " +
                $"Client2Error={client2?.LastKcpError}:{client2?.LastKcpErrorMessage}");
        }

        private static void WaitServerConnectionState(
            KcpNetworkInputServer server,
            KcpNetworkInputClient remainingClient,
            int expectedConnections,
            int expectedBoundPlayers)
        {
            Stopwatch stopwatch=Stopwatch.StartNew();

            while(stopwatch.ElapsedMilliseconds<TimeoutMs)
            {
                server.Tick();
                remainingClient?.Tick();

                if(server.ConnectedConnectionCount==expectedConnections&&server.BoundPlayerCount==expectedBoundPlayers)
                    return;

                Thread.Sleep(1);
            }

            Assert.Fail(
                $"04A-1 Disconnect State Timeout: Connections={server.ConnectedConnectionCount}/{expectedConnections}, " +
                $"Bound={server.BoundPlayerCount}/{expectedBoundPlayers}");
        }

        private static void WaitAuthorityFrameCount(
            KcpNetworkInputServer server,
            KcpNetworkInputClient client1,
            KcpNetworkInputClient client2,
            int expected)
        {
            Stopwatch stopwatch=Stopwatch.StartNew();

            while(stopwatch.ElapsedMilliseconds<TimeoutMs)
            {
                Pump(server,client1,client2,1);
                if(server.AuthorityFrameCount>=expected) return;
            }

            Assert.Fail(
                $"04A-1 Authority Timeout: Expected={expected}, Actual={server.AuthorityFrameCount}, " +
                $"Rejected={server.RejectedMessageCount}, LastReject={server.LastRejectMessage}");
        }

        private static void Pump(
            KcpNetworkInputServer server,
            KcpNetworkInputClient client1,
            KcpNetworkInputClient client2,
            int sleepMilliseconds)
        {
            server.Tick();
            client1?.Tick();
            client2?.Tick();
            if(sleepMilliseconds>0) Thread.Sleep(sleepMilliseconds);
        }

        private static List<int> DrainAuthority(KcpNetworkInputClient client)
        {
            var frames=new List<int>();
            while(client.TryReceiveAuthority(out ServerAuthorityFramePacket packet))
                frames.Add(packet.InputSet.frameNumber);
            return frames;
        }
    }
}
