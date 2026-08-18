using ECSFrameWork;
using FrameWork.RollBackSystem;
using System;
using System.Diagnostics;
using System.Net;
using System.Threading;

namespace FrameWork.NetworkSync
{
    /// <summary>
    /// 两个本地客户端与单个本地权威服务器的输入交换验证。
    /// </summary>
    public static class MultiClientServerInputExchangeValidationTestBootstrap
    {
        private const string LoopbackAddress = "127.0.0.1";
        private const uint SessionId = 0x11223344u;
        private const int Player1ID = 1;
        private const int Player2ID = 2;
        private const int TimeoutMs = 2000;
        private const int NoAuthorityCheckMs = 50;

        /// <summary>
        /// 验证 Player1 先到时服务器必须等待 Player2，随后向两个客户端广播相同 Authority。
        /// </summary>
        public static void RunPlayer1ThenPlayer2Static()
        {
            using var server = CreateServer();
            using var client1 = CreateClient(server.LocalEndPoint, Player1ID);
            using var client2 = CreateClient(server.LocalEndPoint, Player2ID);

            RegisterPlayers(server, client1, client2);

            PlayerInputSnapshot input1 = CreateInput(100, Player1ID, 1f, 0f);
            PlayerInputSnapshot input2 = CreateInput(100, Player2ID, -1f, 0f);

            client1.SendInput(in input1);
            WaitServerProcessedCount(server, 1);

            Expect(server.AuthorityFrameCount == 0,
                $"04C Early Authority Error: Expected=0, Actual={server.AuthorityFrameCount}");

            ExpectNoAuthority(client1);
            ExpectNoAuthority(client2);

            client2.SendInput(in input2);

            ServerAuthorityFramePacket serverAuthority = WaitServerAuthority(server, 1);
            ServerAuthorityFramePacket client1Authority = WaitClientAuthority(client1);
            ServerAuthorityFramePacket client2Authority = WaitClientAuthority(client2);

            FrameInputSet expected = CreateExpectedFrame(100, input1, input2);

            AssertAuthority(serverAuthority, expected, 1u, "Server");
            AssertAuthority(client1Authority, expected, 1u, "Client1");
            AssertAuthority(client2Authority, expected, 1u, "Client2");

            Expect(new FrameInputSetComparer().IsEqual(client1Authority.InputSet, client2Authority.InputSet),
                "04C Client Authority Mismatch Error");

            Expect(server.ProcessedDatagramCount == 2,
                $"04C Processed Count Error: Expected=2, Actual={server.ProcessedDatagramCount}");

            Expect(server.AuthorityFrameCount == 1,
                $"04C Authority Count Error: Expected=1, Actual={server.AuthorityFrameCount}");

            Expect(server.RejectedDatagramCount == 0,
                $"04C Reject Count Error: Expected=0, Actual={server.RejectedDatagramCount}");
        }

        /// <summary>
        /// 验证 Player2 先到时结果完全相同，Authority 不依赖玩家输入到达顺序。
        /// </summary>
        public static void RunPlayer2ThenPlayer1Static()
        {
            using var server = CreateServer();
            using var client1 = CreateClient(server.LocalEndPoint, Player1ID);
            using var client2 = CreateClient(server.LocalEndPoint, Player2ID);

            RegisterPlayers(server, client1, client2);

            PlayerInputSnapshot input1 = CreateInput(200, Player1ID, 0f, 1f);
            PlayerInputSnapshot input2 = CreateInput(200, Player2ID, 0f, -1f);

            client2.SendInput(in input2);
            WaitServerProcessedCount(server, 1);

            Expect(server.AuthorityFrameCount == 0,
                $"04C Reverse Early Authority Error: Expected=0, Actual={server.AuthorityFrameCount}");

            client1.SendInput(in input1);

            ServerAuthorityFramePacket authority = WaitServerAuthority(server, 1);
            ServerAuthorityFramePacket authority1 = WaitClientAuthority(client1);
            ServerAuthorityFramePacket authority2 = WaitClientAuthority(client2);

            FrameInputSet expected = CreateExpectedFrame(200, input1, input2);

            AssertAuthority(authority, expected, 1u, "Reverse Server");
            AssertAuthority(authority1, expected, 1u, "Reverse Client1");
            AssertAuthority(authority2, expected, 1u, "Reverse Client2");
        }

        /// <summary>
        /// 连续 100 帧交替改变两玩家输入到达顺序，两个客户端必须始终收到相同 Authority。
        /// </summary>
        public static void Run100FramesAlternatingArrivalOrderStatic()
        {
            using var server = CreateServer();
            using var client1 = CreateClient(server.LocalEndPoint, Player1ID);
            using var client2 = CreateClient(server.LocalEndPoint, Player2ID);

            RegisterPlayers(server, client1, client2);

            var comparer = new FrameInputSetComparer();

            for (int frame = 1; frame <= 100; frame++)
            {
                PlayerInputSnapshot input1 = CreateInput(
                    frame,
                    Player1ID,
                    frame % 3 - 1,
                    frame % 2 == 0 ? 1f : -1f);

                PlayerInputSnapshot input2 = CreateInput(
                    frame,
                    Player2ID,
                    1 - (frame % 3),
                    frame % 2 == 0 ? -1f : 1f);

                int expectedProcessedBeforeSecond = frame * 2 - 1;
                int authorityBefore = frame - 1;

                if ((frame & 1) == 1)
                {
                    client1.SendInput(in input1);
                    WaitServerProcessedCount(server, expectedProcessedBeforeSecond);

                    Expect(server.AuthorityFrameCount == authorityBefore,
                        $"04C 100Frames Early Authority Error: Frame={frame}, First=P1, Expected={authorityBefore}, Actual={server.AuthorityFrameCount}");

                    client2.SendInput(in input2);
                }
                else
                {
                    client2.SendInput(in input2);
                    WaitServerProcessedCount(server, expectedProcessedBeforeSecond);

                    Expect(server.AuthorityFrameCount == authorityBefore,
                        $"04C 100Frames Early Authority Error: Frame={frame}, First=P2, Expected={authorityBefore}, Actual={server.AuthorityFrameCount}");

                    client1.SendInput(in input1);
                }

                ServerAuthorityFramePacket serverAuthority = WaitServerAuthority(server, frame);
                ServerAuthorityFramePacket authority1 = WaitClientAuthority(client1);
                ServerAuthorityFramePacket authority2 = WaitClientAuthority(client2);

                FrameInputSet expected = CreateExpectedFrame(frame, input1, input2);

                AssertAuthority(serverAuthority, expected, (uint)frame, $"Frame={frame} Server");
                AssertAuthority(authority1, expected, (uint)frame, $"Frame={frame} Client1");
                AssertAuthority(authority2, expected, (uint)frame, $"Frame={frame} Client2");

                Expect(comparer.IsEqual(authority1.InputSet, authority2.InputSet),
                    $"04C 100Frames Client Authority Mismatch Error: Frame={frame}");

                Expect(client1.LastSentSequence == (uint)frame,
                    $"04C Client1 Sequence Error: Frame={frame}, Actual={client1.LastSentSequence}");

                Expect(client2.LastSentSequence == (uint)frame,
                    $"04C Client2 Sequence Error: Frame={frame}, Actual={client2.LastSentSequence}");
            }

            Expect(server.ProcessedDatagramCount == 200,
                $"04C Final Processed Count Error: Expected=200, Actual={server.ProcessedDatagramCount}");

            Expect(server.AuthorityFrameCount == 100,
                $"04C Final Authority Count Error: Expected=100, Actual={server.AuthorityFrameCount}");

            Expect(server.RejectedDatagramCount == 0,
                $"04C Final Reject Count Error: Expected=0, Actual={server.RejectedDatagramCount}");
        }

        /// <summary>
        /// 同一个未完成帧内 Player1 重复输入不能让服务器误认为帧已完成。
        /// </summary>
        public static void RunDuplicatePendingInputStatic()
        {
            using var server = CreateServer();
            using var client1 = CreateClient(server.LocalEndPoint, Player1ID);
            using var client2 = CreateClient(server.LocalEndPoint, Player2ID);

            RegisterPlayers(server, client1, client2);

            PlayerInputSnapshot input1 = CreateInput(300, Player1ID, 1f, 0f);
            PlayerInputSnapshot input2 = CreateInput(300, Player2ID, -1f, 0f);

            client1.SendInput(in input1);
            client1.SendInput(in input1);

            WaitServerProcessedCount(server, 2);

            Expect(server.AuthorityFrameCount == 0,
                $"04C Pending Duplicate Authority Error: Expected=0, Actual={server.AuthorityFrameCount}");

            Expect(server.RejectedDatagramCount == 0,
                $"04C Pending Duplicate Reject Error: Expected=0, Actual={server.RejectedDatagramCount}");

            client2.SendInput(in input2);

            ServerAuthorityFramePacket authority = WaitServerAuthority(server, 1);
            ServerAuthorityFramePacket authority1 = WaitClientAuthority(client1);
            ServerAuthorityFramePacket authority2 = WaitClientAuthority(client2);

            FrameInputSet expected = CreateExpectedFrame(300, input1, input2);

            AssertAuthority(authority, expected, 1u, "Duplicate Server");
            AssertAuthority(authority1, expected, 1u, "Duplicate Client1");
            AssertAuthority(authority2, expected, 1u, "Duplicate Client2");

            Expect(server.ProcessedDatagramCount == 3,
                $"04C Pending Duplicate Processed Error: Expected=3, Actual={server.ProcessedDatagramCount}");

            Expect(server.AuthorityFrameCount == 1,
                $"04C Pending Duplicate Final Authority Error: Expected=1, Actual={server.AuthorityFrameCount}");
        }

        /// <summary>
        /// Player2 永远不发送时服务器不得产生不完整 Authority。
        /// </summary>
        public static void RunMissingSecondPlayerStatic()
        {
            using var server = CreateServer();
            using var client1 = CreateClient(server.LocalEndPoint, Player1ID);
            using var client2 = CreateClient(server.LocalEndPoint, Player2ID);

            RegisterPlayers(server, client1, client2);

            PlayerInputSnapshot input1 = CreateInput(400, Player1ID, 1f, 1f);
            client1.SendInput(in input1);

            WaitServerProcessedCount(server, 1);

            Expect(server.AuthorityFrameCount == 0,
                $"04C Missing Player Authority Error: Expected=0, Actual={server.AuthorityFrameCount}");

            ExpectNoAuthority(client1);
            ExpectNoAuthority(client2);
        }

        /// <summary>
        /// 验证不同逻辑帧可以独立聚合，后一个 Frame 可以先于前一个 Frame 完成。
        /// </summary>
        public static void RunCrossFrameOutOfOrderCompletionStatic()
        {
            using var server = CreateServer();
            using var client1 = CreateClient(server.LocalEndPoint, Player1ID);
            using var client2 = CreateClient(server.LocalEndPoint, Player2ID);

            RegisterPlayers(server, client1, client2);

            PlayerInputSnapshot p1F100 = CreateInput(100, Player1ID, 1f, 0f);
            PlayerInputSnapshot p2F100 = CreateInput(100, Player2ID, -1f, 0f);

            PlayerInputSnapshot p1F101 = CreateInput(101, Player1ID, 0f, 1f);
            PlayerInputSnapshot p2F101 = CreateInput(101, Player2ID, 0f, -1f);

            // F100 只到 P1。
            client1.SendInput(in p1F100);
            WaitServerProcessedCount(server, 1);

            // F101 先收齐。
            client1.SendInput(in p1F101);
            WaitServerProcessedCount(server, 2);

            client2.SendInput(in p2F101);

            ServerAuthorityFramePacket firstAuthority = WaitServerAuthority(server, 1);
            ServerAuthorityFramePacket client1First = WaitClientAuthority(client1);
            ServerAuthorityFramePacket client2First = WaitClientAuthority(client2);

            FrameInputSet expected101 = CreateExpectedFrame(101, p1F101, p2F101);

            AssertAuthority(firstAuthority, expected101, 1u, "OutOfOrder First Server");
            AssertAuthority(client1First, expected101, 1u, "OutOfOrder First Client1");
            AssertAuthority(client2First, expected101, 1u, "OutOfOrder First Client2");

            // 最后补齐 F100。
            client2.SendInput(in p2F100);

            ServerAuthorityFramePacket secondAuthority = WaitServerAuthority(server, 2);
            ServerAuthorityFramePacket client1Second = WaitClientAuthority(client1);
            ServerAuthorityFramePacket client2Second = WaitClientAuthority(client2);

            FrameInputSet expected100 = CreateExpectedFrame(100, p1F100, p2F100);

            AssertAuthority(secondAuthority, expected100, 2u, "OutOfOrder Second Server");
            AssertAuthority(client1Second, expected100, 2u, "OutOfOrder Second Client1");
            AssertAuthority(client2Second, expected100, 2u, "OutOfOrder Second Client2");

            Expect(firstAuthority.InputSet.frameNumber == 101,
                $"04C OutOfOrder First Frame Error: Expected=101, Actual={firstAuthority.InputSet.frameNumber}");

            Expect(secondAuthority.InputSet.frameNumber == 100,
                $"04C OutOfOrder Second Frame Error: Expected=100, Actual={secondAuthority.InputSet.frameNumber}");
        }

        private static LocalNetworkInputServer CreateServer()
        {
            return new LocalNetworkInputServer(
                new UdpTransportConfig(
                    LoopbackAddress,
                    0,
                    NetworkProtocolConstants.MaxDatagramSize),
                SessionId);
        }

        private static LocalNetworkInputClient CreateClient(IPEndPoint serverEndPoint, int playerID)
        {
            return new LocalNetworkInputClient(
                new UdpTransportConfig(
                    LoopbackAddress,
                    0,
                    NetworkProtocolConstants.MaxDatagramSize),
                serverEndPoint,
                SessionId,
                playerID);
        }

        private static void RegisterPlayers(LocalNetworkInputServer server, LocalNetworkInputClient client1, LocalNetworkInputClient client2)
        {
            Expect(client1.LocalEndPoint.Port != client2.LocalEndPoint.Port,
                $"04C Client Port Collision Error: Port={client1.LocalEndPoint.Port}");

            server.RegisterPlayer(Player1ID, client1.LocalEndPoint);
            server.RegisterPlayer(Player2ID, client2.LocalEndPoint);

            Expect(server.PlayerCount == 2,
                $"04C Server PlayerCount Error: Expected=2, Actual={server.PlayerCount}");
        }

        private static FrameInputSet CreateExpectedFrame(int frame, PlayerInputSnapshot input1, PlayerInputSnapshot input2)
        {
            return new FrameInputSet(frame, new[]
            {
                input2,
                input1
            });
        }

        private static PlayerInputSnapshot CreateInput(int frame, int playerID, float moveX, float moveY)
        {
            return new PlayerInputSnapshot(frame, playerID)
            {
                moveX = moveX,
                moveY = moveY,
                mouseX = frame * 0.25f + playerID,
                mouseY = -frame * 0.125f - playerID,
                mouseDeltaX = 0.5f * playerID,
                mouseDeltaY = -0.25f * playerID,
                scrollX = playerID,
                scrollY = -playerID
            };
        }

        private static void AssertAuthority(ServerAuthorityFramePacket packet, FrameInputSet expected, uint expectedSequence, string stage)
        {
            Expect(packet.SessionId == SessionId,
                $"{stage} Session Error: Expected={SessionId}, Actual={packet.SessionId}");

            Expect(packet.Sequence == expectedSequence,
                $"{stage} Sequence Error: Expected={expectedSequence}, Actual={packet.Sequence}");

            Expect(new FrameInputSetComparer().IsEqual(packet.InputSet, expected),
                $"{stage} FrameInputSet Error: ExpectedFrame={expected.frameNumber}, ActualFrame={packet.InputSet.frameNumber}");

            Expect(packet.InputSet.Count == 2,
                $"{stage} PlayerCount Error: Expected=2, Actual={packet.InputSet.Count}");

            Expect(packet.InputSet.GetInputAt(0).playerID == Player1ID,
                $"{stage} Player Order Error: Index=0, Expected={Player1ID}, Actual={packet.InputSet.GetInputAt(0).playerID}");

            Expect(packet.InputSet.GetInputAt(1).playerID == Player2ID,
                $"{stage} Player Order Error: Index=1, Expected={Player2ID}, Actual={packet.InputSet.GetInputAt(1).playerID}");
        }

        private static void WaitServerProcessedCount(LocalNetworkInputServer server, int expectedCount)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            while (stopwatch.ElapsedMilliseconds < TimeoutMs)
            {
                server.TryProcessOneDatagram(out _);

                if (server.ProcessedDatagramCount >= expectedCount) return;

                Thread.Sleep(1);
            }

            throw new TimeoutException(
                $"04C Server Process Timeout: Expected={expectedCount}, Actual={server.ProcessedDatagramCount}, Authority={server.AuthorityFrameCount}, Reject={server.RejectedDatagramCount}, Reason={server.LastRejectReason}, Decode={server.LastDecodeError}");
        }

        private static ServerAuthorityFramePacket WaitServerAuthority(LocalNetworkInputServer server, int expectedAuthorityCount)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            while (stopwatch.ElapsedMilliseconds < TimeoutMs)
            {
                if (server.TryProcessOneDatagram(out ServerAuthorityFramePacket authority))
                {
                    if (server.AuthorityFrameCount == expectedAuthorityCount) return authority;

                    throw new InvalidOperationException(
                        $"04C Authority Count Error: Expected={expectedAuthorityCount}, Actual={server.AuthorityFrameCount}");
                }

                if (server.AuthorityFrameCount > expectedAuthorityCount)
                    throw new InvalidOperationException(
                        $"04C Authority Overflow Error: Expected={expectedAuthorityCount}, Actual={server.AuthorityFrameCount}");

                Thread.Sleep(1);
            }

            throw new TimeoutException(
                $"04C Server Authority Timeout: ExpectedAuthority={expectedAuthorityCount}, Actual={server.AuthorityFrameCount}, Processed={server.ProcessedDatagramCount}, Reject={server.RejectedDatagramCount}, Reason={server.LastRejectReason}");
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

            throw new TimeoutException(
                $"04C Client Authority Timeout: PlayerID={client.PlayerID}, Endpoint={client.LocalEndPoint}, Reject={client.LastRejectReason}, Decode={client.LastDecodeError}");
        }

        private static void ExpectNoAuthority(LocalNetworkInputClient client)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            while (stopwatch.ElapsedMilliseconds < NoAuthorityCheckMs)
            {
                if (client.TryReceiveAuthority(out ServerAuthorityFramePacket authority))
                    throw new InvalidOperationException(
                        $"04C Unexpected Authority Error: PlayerID={client.PlayerID}, Frame={authority.InputSet.frameNumber}, Sequence={authority.Sequence}");

                Thread.Sleep(1);
            }
        }

        private static void Expect(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}