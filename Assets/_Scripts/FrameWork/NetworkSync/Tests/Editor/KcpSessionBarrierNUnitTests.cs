using ECSFrameWork;
using NUnit.Framework;
using System.Diagnostics;
using System.Threading;

namespace FrameWork.NetworkSync.Tests
{
    [TestFixture]
    public sealed class KcpSessionBarrierNUnitTests
    {
        private const uint SessionId=0x11223344u;
        private const int TimeoutMs=3000;

        [Test]
        public void Disconnect_ClearsPendingAndDropsNewInputUntilAllPlayersRebound()
        {
            using var server=new KcpNetworkInputServer(0,2,SessionId);
            int port=server.LocalEndPoint.Port;

            using var client1=new KcpNetworkInputClient("127.0.0.1",port,SessionId,1);
            var client2=new KcpNetworkInputClient("127.0.0.1",port,SessionId,2);

            try
            {
                WaitConnected(server,client1,client2);

                SendFrame(client1,client2,1);
                WaitAuthority(server,client1,client2,1);

                Assert.AreEqual(1,server.LastAuthorityFrame);
                Assert.AreEqual(0,server.PendingFrameCount);

                // 在双方都在线时制造一个未完成 F2。
                // KCP 是异步 Pump，不允许假设一次 Tick + 5 ms 就已经送达 Server。
                SendInput(client1,2);
                WaitPendingFrameCount(server,client1,client2,1);

                client2.Dispose();
                WaitServerState(server,client1,1,1);

                Assert.AreEqual(0,server.PendingFrameCount,
                    "04A-4B-1 Disconnect Must Clear Old Pending Frames");
                Assert.AreEqual(1,server.DroppedPendingFrameCount,
                    "04A-4B-1 Old Pending Drop Count");

                // 缺员期间 surviving client 的新输入不能再进入 pending collector。
                SendInput(client1,2);
                SendInput(client1,3);
                WaitDroppedIncompleteCount(server,client1,2);

                Assert.AreEqual(0,server.PendingFrameCount);
                Assert.AreEqual(2,server.DroppedIncompleteSessionInputCount);
                Assert.AreEqual(1,server.AuthorityFrameCount);

                using var reconnectedClient2=new KcpNetworkInputClient("127.0.0.1",port,SessionId,2);
                WaitConnected(server,client1,reconnectedClient2);

                // Socket 已连，但 Player2 要靠第一条有效输入重新 BIND。
                Assert.AreEqual(1,server.BoundPlayerCount);

                SendInput(reconnectedClient2,2);
                WaitBoundAndPending(server,client1,reconnectedClient2,2,1);

                SendInput(client1,2);
                WaitAuthority(server,client1,reconnectedClient2,2);

                SendInput(client1,3);
                SendInput(reconnectedClient2,3);
                WaitAuthority(server,client1,reconnectedClient2,3);

                Assert.AreEqual(3,server.LastAuthorityFrame);
                Assert.AreEqual(3,server.AuthorityFrameCount);
                Assert.AreEqual(0,server.PendingFrameCount);
                Assert.AreEqual(0,server.RejectedMessageCount);
            }
            finally
            {
                client2.Dispose();
            }
        }

        [Test]
        public void Disconnect_PreservesCompletedAuthorityBoundary()
        {
            using var server=new KcpNetworkInputServer(0,2,SessionId);
            int port=server.LocalEndPoint.Port;

            using var client1=new KcpNetworkInputClient("127.0.0.1",port,SessionId,1);
            var client2=new KcpNetworkInputClient("127.0.0.1",port,SessionId,2);

            try
            {
                WaitConnected(server,client1,client2);

                for(int frame=1;frame<=5;frame++)
                {
                    SendFrame(client1,client2,frame);
                    WaitAuthority(server,client1,client2,frame);
                }

                Assert.AreEqual(5,server.LastAuthorityFrame);
                Assert.AreEqual(5,server.AuthorityFrameCount);

                SendInput(client1,6);
                WaitPendingFrameCount(server,client1,client2,1);

                client2.Dispose();
                WaitServerState(server,client1,1,1);

                Assert.AreEqual(5,server.LastAuthorityFrame,
                    "04A-4B-1 Completed Authority Boundary Must Survive Disconnect");
                Assert.AreEqual(5,server.AuthorityFrameCount);
                Assert.AreEqual(0,server.PendingFrameCount);
                Assert.AreEqual(1,server.DroppedPendingFrameCount);
            }
            finally
            {
                client2.Dispose();
            }
        }

        private static void SendFrame(KcpNetworkInputClient client1,KcpNetworkInputClient client2,int frame)
        {
            SendInput(client1,frame);
            SendInput(client2,frame);
        }

        private static void SendInput(KcpNetworkInputClient client,int frame)
        {
            var input=new PlayerInputSnapshot(frame,client.PlayerID)
            {
                moveX=((frame+client.PlayerID)%3)-1,
                moveY=((frame*2+client.PlayerID)%3)-1
            };
            client.SendInput(in input);
        }

        private static void WaitConnected(
            KcpNetworkInputServer server,
            KcpNetworkInputClient client1,
            KcpNetworkInputClient client2)
        {
            Stopwatch stopwatch=Stopwatch.StartNew();

            while(stopwatch.ElapsedMilliseconds<TimeoutMs)
            {
                Pump(server,client1,client2,1);

                int expected=client2==null?1:2;
                if(client1.IsConnected&&(client2==null||client2.IsConnected)&&server.ConnectedConnectionCount==expected)
                    return;
            }

            Assert.Fail(
                $"04A-4B-1 Connect Timeout: Connections={server.ConnectedConnectionCount}, " +
                $"P1={client1.ConnectionState}, P2={client2?.ConnectionState}");
        }

        private static void WaitServerState(
            KcpNetworkInputServer server,
            KcpNetworkInputClient remainingClient,
            int expectedConnections,
            int expectedBound)
        {
            Stopwatch stopwatch=Stopwatch.StartNew();

            while(stopwatch.ElapsedMilliseconds<TimeoutMs)
            {
                Pump(server,remainingClient,null,1);

                if(server.ConnectedConnectionCount==expectedConnections&&server.BoundPlayerCount==expectedBound)
                    return;
            }

            Assert.Fail(
                $"04A-4B-1 Server State Timeout: Connections={server.ConnectedConnectionCount}/{expectedConnections}, " +
                $"Bound={server.BoundPlayerCount}/{expectedBound}, Pending={server.PendingFrameCount}, " +
                $"Barrier={server.IsSessionBarrierActive}");
        }

        private static void WaitPendingFrameCount(
            KcpNetworkInputServer server,
            KcpNetworkInputClient client1,
            KcpNetworkInputClient client2,
            int expected)
        {
            Stopwatch stopwatch=Stopwatch.StartNew();

            while(stopwatch.ElapsedMilliseconds<TimeoutMs)
            {
                Pump(server,client1,client2,1);
                if(server.PendingFrameCount==expected) return;
            }

            Assert.Fail(
                $"04A-4B-1 Pending Timeout: Expected={expected}, Actual={server.PendingFrameCount}, " +
                $"Bound={server.BoundPlayerCount}, Barrier={server.IsSessionBarrierActive}, " +
                $"DroppedPending={server.DroppedPendingFrameCount}, DroppedIncomplete={server.DroppedIncompleteSessionInputCount}, " +
                $"Rejected={server.RejectedMessageCount}, LastReject={server.LastRejectMessage}");
        }

        private static void WaitDroppedIncompleteCount(
            KcpNetworkInputServer server,
            KcpNetworkInputClient client,
            int expected)
        {
            Stopwatch stopwatch=Stopwatch.StartNew();

            while(stopwatch.ElapsedMilliseconds<TimeoutMs)
            {
                Pump(server,client,null,1);
                if(server.DroppedIncompleteSessionInputCount>=expected) return;
            }

            Assert.Fail(
                $"04A-4B-1 Dropped Incomplete Timeout: Expected={expected}, Actual={server.DroppedIncompleteSessionInputCount}, " +
                $"Pending={server.PendingFrameCount}, Bound={server.BoundPlayerCount}, Barrier={server.IsSessionBarrierActive}, " +
                $"Rejected={server.RejectedMessageCount}, LastReject={server.LastRejectMessage}");
        }

        private static void WaitBoundAndPending(
            KcpNetworkInputServer server,
            KcpNetworkInputClient client1,
            KcpNetworkInputClient client2,
            int expectedBound,
            int expectedPending)
        {
            Stopwatch stopwatch=Stopwatch.StartNew();

            while(stopwatch.ElapsedMilliseconds<TimeoutMs)
            {
                Pump(server,client1,client2,1);

                if(server.BoundPlayerCount==expectedBound&&server.PendingFrameCount==expectedPending)
                    return;
            }

            Assert.Fail(
                $"04A-4B-1 Rebind Pending Timeout: Bound={server.BoundPlayerCount}/{expectedBound}, " +
                $"Pending={server.PendingFrameCount}/{expectedPending}, Barrier={server.IsSessionBarrierActive}, " +
                $"DroppedIncomplete={server.DroppedIncompleteSessionInputCount}, Rejected={server.RejectedMessageCount}");
        }

        private static void WaitAuthority(
            KcpNetworkInputServer server,
            KcpNetworkInputClient client1,
            KcpNetworkInputClient client2,
            int expectedCount)
        {
            Stopwatch stopwatch=Stopwatch.StartNew();

            while(stopwatch.ElapsedMilliseconds<TimeoutMs)
            {
                Pump(server,client1,client2,1);

                while(client1.TryReceiveAuthority(out _)) { }
                if(client2!=null) while(client2.TryReceiveAuthority(out _)) { }

                if(server.AuthorityFrameCount>=expectedCount) return;
            }

            Assert.Fail(
                $"04A-4B-1 Authority Timeout: Expected={expectedCount}, Actual={server.AuthorityFrameCount}, " +
                $"LastAuthorityFrame={server.LastAuthorityFrame}, Pending={server.PendingFrameCount}, Bound={server.BoundPlayerCount}, " +
                $"Barrier={server.IsSessionBarrierActive}, DroppedPending={server.DroppedPendingFrameCount}, " +
                $"DroppedIncomplete={server.DroppedIncompleteSessionInputCount}, Rejected={server.RejectedMessageCount}, " +
                $"LastReject={server.LastRejectMessage}");
        }

        private static void Pump(
            KcpNetworkInputServer server,
            KcpNetworkInputClient client1,
            KcpNetworkInputClient client2,
            int sleepMs)
        {
            server.Tick();
            client1?.Tick();
            client2?.Tick();
            if(sleepMs>0) Thread.Sleep(sleepMs);
        }
    }
}
