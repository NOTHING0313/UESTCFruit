using ECSFrameWork;
using FrameWork.RollBackSystem;
using NUnit.Framework;
using System.Diagnostics;
using System.Net;
using System.Threading;

namespace FrameWork.NetworkSync.Tests
{
    [TestFixture]
    public sealed class LocalClientServerInputExchangeNUnitTests
    {
        private const string LoopbackAddress = "127.0.0.1";
        private const uint SessionId = 0x11223344u;
        private const int Player1ID = 1;
        private const int TimeoutMs = 2000;

        [Test]
        public void Collector_TwoPlayers_CompletesOnlyAfterAllInputsArrive()
        {
            var collector = new ServerInputFrameCollector();
            collector.RegisterPlayer(1);
            collector.RegisterPlayer(2);

            PlayerInputSnapshot player2 = CreateInput(100, 2, -1f, 0f);
            PlayerInputSnapshot player1 = CreateInput(100, 1, 1f, 0f);

            Assert.IsFalse(collector.TryAddInput(in player2, out _));

            Assert.IsTrue(
                collector.TryAddInput(
                    in player1,
                    out FrameInputSet completed));

            Assert.AreEqual(100, completed.frameNumber);
            Assert.AreEqual(2, completed.Count);
            Assert.AreEqual(1, completed.GetInputAt(0).playerID);
            Assert.AreEqual(2, completed.GetInputAt(1).playerID);
        }

        [Test]
        public void OneClient_SingleFrame_InputToAuthority_RoundTrip()
        {
            using var server = CreateServer(SessionId);
            using var client = CreateClient(server.LocalEndPoint, SessionId, Player1ID);

            server.RegisterPlayer(Player1ID, client.LocalEndPoint);

            PlayerInputSnapshot source = CreateInput(100, Player1ID, 1f, -1f);
            client.SendInput(in source);

            ServerAuthorityFramePacket serverAuthority = WaitServerAuthority(server);
            ServerAuthorityFramePacket clientAuthority = WaitClientAuthority(client);

            Assert.AreEqual(1u, client.LastSentSequence);
            Assert.AreEqual(SessionId, serverAuthority.SessionId);
            Assert.AreEqual(1u, serverAuthority.Sequence);
            Assert.IsTrue(new FrameInputSetComparer().IsEqual(serverAuthority.InputSet, clientAuthority.InputSet));

            Assert.IsTrue(clientAuthority.InputSet.TryGetInput(Player1ID, out PlayerInputSnapshot result));
            Assert.IsTrue(new PlayerInputSnapshotComparer().IsEqual(source, result));

            Assert.AreEqual(1, server.ProcessedDatagramCount);
            Assert.AreEqual(1, server.AuthorityFrameCount);
            Assert.AreEqual(0, server.RejectedDatagramCount);
        }

        [Test]
        public void OneClient_100Frames_AllAuthorityFramesMatchInputs()
        {
            using var server = CreateServer(SessionId);
            using var client = CreateClient(server.LocalEndPoint, SessionId, Player1ID);

            server.RegisterPlayer(Player1ID, client.LocalEndPoint);

            var comparer = new PlayerInputSnapshotComparer();

            for (int frame = 1; frame <= 100; frame++)
            {
                PlayerInputSnapshot source = CreateInput(
                    frame,
                    Player1ID,
                    frame % 3 - 1,
                    frame % 2 == 0 ? 1f : -1f);

                client.SendInput(in source);

                ServerAuthorityFramePacket serverAuthority = WaitServerAuthority(server);
                ServerAuthorityFramePacket clientAuthority = WaitClientAuthority(client);

                Assert.AreEqual(frame, serverAuthority.InputSet.frameNumber);
                Assert.AreEqual((uint)frame, serverAuthority.Sequence);
                Assert.AreEqual(frame, clientAuthority.InputSet.frameNumber);

                Assert.IsTrue(
                    clientAuthority.InputSet.TryGetInput(
                        Player1ID,
                        out PlayerInputSnapshot result));

                Assert.IsTrue(comparer.IsEqual(source, result), $"Frame={frame}");
            }

            Assert.AreEqual(100, server.ProcessedDatagramCount);
            Assert.AreEqual(100, server.AuthorityFrameCount);
            Assert.AreEqual(0, server.RejectedDatagramCount);
        }

        [Test]
        public void Server_WrongSession_IsRejectedAndProducesNoAuthority()
        {
            using var server = CreateServer(SessionId);
            using var client = CreateClient(server.LocalEndPoint, 0x55667788u, Player1ID);

            server.RegisterPlayer(Player1ID, client.LocalEndPoint);

            PlayerInputSnapshot input = CreateInput(100, Player1ID, 1f, 0f);
            client.SendInput(in input);

            WaitUntilServerProcessed(server, 1);

            Assert.AreEqual(NetworkInputExchangeRejectReason.SessionMismatch, server.LastRejectReason);
            Assert.AreEqual(1, server.RejectedDatagramCount);
            Assert.AreEqual(0, server.AuthorityFrameCount);
            AssertNoAuthority(client, 100);
        }

        [Test]
        public void Server_CorrectPlayerFromWrongEndpoint_IsRejected()
        {
            using var server = CreateServer(SessionId);
            using var realClient = CreateClient(server.LocalEndPoint, SessionId, Player1ID);
            using var intruder = new UdpTransport(
                new UdpTransportConfig(
                    LoopbackAddress,
                    0,
                    NetworkProtocolConstants.MaxDatagramSize));

            server.RegisterPlayer(Player1ID, realClient.LocalEndPoint);

            PlayerInputSnapshot input = CreateInput(100, Player1ID, 1f, 0f);
            var packet = new ClientInputPacket(SessionId, 1u, input);
            byte[] bytes = NetworkPacketSerializer.SerializeClientInput(in packet);

            intruder.Send(bytes, server.LocalEndPoint);

            WaitUntilServerProcessed(server, 1);

            Assert.AreEqual(NetworkInputExchangeRejectReason.EndpointMismatch, server.LastRejectReason);
            Assert.AreEqual(1, server.RejectedDatagramCount);
            Assert.AreEqual(0, server.AuthorityFrameCount);
        }

        [Test]
        public void DuplicateCompletedInput_IsIdempotentAndDoesNotBroadcastSecondAuthority()
        {
            using var server = CreateServer(SessionId);
            using var client = CreateClient(server.LocalEndPoint, SessionId, Player1ID);

            server.RegisterPlayer(Player1ID, client.LocalEndPoint);

            PlayerInputSnapshot input = CreateInput(100, Player1ID, 1f, 0f);

            client.SendInput(in input);
            WaitServerAuthority(server);
            WaitClientAuthority(client);

            client.SendInput(in input);
            WaitUntilServerProcessed(server, 2);

            Assert.AreEqual(1, server.AuthorityFrameCount);
            Assert.AreEqual(0, server.RejectedDatagramCount);
            AssertNoAuthority(client, 100);
        }

        private static LocalNetworkInputServer CreateServer(uint sessionId)
        {
            return new LocalNetworkInputServer(
                new UdpTransportConfig(
                    LoopbackAddress,
                    0,
                    NetworkProtocolConstants.MaxDatagramSize),
                sessionId);
        }

        private static LocalNetworkInputClient CreateClient(IPEndPoint serverEndPoint, uint sessionId, int playerID)
        {
            return new LocalNetworkInputClient(
                new UdpTransportConfig(
                    LoopbackAddress,
                    0,
                    NetworkProtocolConstants.MaxDatagramSize),
                serverEndPoint,
                sessionId,
                playerID);
        }

        private static ServerAuthorityFramePacket WaitServerAuthority(LocalNetworkInputServer server)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            while (stopwatch.ElapsedMilliseconds < TimeoutMs)
            {
                if (server.TryProcessOneDatagram(out ServerAuthorityFramePacket authority))
                    return authority;

                Thread.Sleep(1);
            }

            Assert.Fail(
                $"Local Server Authority Timeout: Endpoint={server.LocalEndPoint}, Processed={server.ProcessedDatagramCount}, Rejected={server.RejectedDatagramCount}, Reason={server.LastRejectReason}");

            return default;
        }

        private static ServerAuthorityFramePacket WaitClientAuthority(LocalNetworkInputClient client)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            while (stopwatch.ElapsedMilliseconds < TimeoutMs)
            {
                if (client.TryReceiveAuthority(out ServerAuthorityFramePacket authority))
                    return authority;

                Thread.Sleep(1);
            }

            Assert.Fail(
                $"Local Client Authority Timeout: Endpoint={client.LocalEndPoint}, Reject={client.LastRejectReason}, Decode={client.LastDecodeError}");

            return default;
        }

        private static void WaitUntilServerProcessed(LocalNetworkInputServer server, int expectedCount)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            while (stopwatch.ElapsedMilliseconds < TimeoutMs)
            {
                server.TryProcessOneDatagram(out _);

                if (server.ProcessedDatagramCount >= expectedCount) return;

                Thread.Sleep(1);
            }

            Assert.Fail(
                $"Local Server Process Timeout: Expected={expectedCount}, Actual={server.ProcessedDatagramCount}");
        }

        private static void AssertNoAuthority(LocalNetworkInputClient client, int timeoutMs)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            while (stopwatch.ElapsedMilliseconds < timeoutMs)
            {
                if (client.TryReceiveAuthority(out _))
                    Assert.Fail("Unexpected Server Authority Packet Received");

                Thread.Sleep(1);
            }
        }

        private static PlayerInputSnapshot CreateInput(int frame, int playerID, float moveX, float moveY)
        {
            return new PlayerInputSnapshot(frame, playerID)
            {
                moveX = moveX,
                moveY = moveY,
                mouseX = 10f + frame * 0.01f,
                mouseY = -20f - playerID,
                mouseDeltaX = 0.5f,
                mouseDeltaY = -0.25f,
                scrollX = 1f,
                scrollY = -1f
            };
        }
    }
}