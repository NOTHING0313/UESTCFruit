using ECSFrameWork;
using FrameWork.NetworkSync;
using FrameWork.RollBackSystem;
using System;
using System.Collections;
using System.Diagnostics;
using UnityEngine;

namespace FrameWork.NetworkSync.Tests
{
    /// <summary>
    /// Unity Runtime 公网 KCP Authority RoundTrip 验证入口。
    /// </summary>
    public static class PublicKcpAuthorityUnityClientValidationTestBootstrap
    {
        private const string ServerAddress = "8.137.83.229";
        private const int ServerPort = 28015;
        private const uint SessionId = 0x11223344u;
        private const int PlayerCount = 2;
        private const int FrameCount = 100;
        private const double ConnectTimeoutSeconds = 5.0;
        private const double AuthorityTimeoutSeconds = 5.0;

        public static IEnumerator Run()
        {
            KcpNetworkInputClient player1 = null, player2 = null;
            var stopwatch = Stopwatch.StartNew();
            uint lastSequence1 = 0, lastSequence2 = 0;

            try
            {
                player1 = new KcpNetworkInputClient(ServerAddress, ServerPort, SessionId, 1);
                player2 = new KcpNetworkInputClient(ServerAddress, ServerPort, SessionId, 2);

                double connectDeadline = Time.realtimeSinceStartupAsDouble + ConnectTimeoutSeconds;

                while (!player1.IsConnected || !player2.IsConnected)
                {
                    player1.Tick();
                    player2.Tick();

                    ThrowIfClientError(player1);
                    ThrowIfClientError(player2);

                    if (Time.realtimeSinceStartupAsDouble >= connectDeadline)
                    {
                        throw new TimeoutException(
                            $"PublicKcpAuthorityUnityClientValidationTestBootstrap Run Error: Category=ConnectTimeout, " +
                            $"Server={ServerAddress}:{ServerPort}, P1Connected={player1.IsConnected}, P2Connected={player2.IsConnected}, " +
                            $"P1=[{GetClientState(player1)}], P2=[{GetClientState(player2)}]");
                    }

                    yield return null;
                }

                UnityEngine.Debug.Log(
                    $"PublicKcpAuthorityUnityClientValidationTestBootstrap Run Log: [UNITY PUBLIC KCP AUTHORITY] " +
                    $"Server={ServerAddress}:{ServerPort}, Session=0x{SessionId:X8}, Players={PlayerCount}, " +
                    $"Player1Local={player1.LocalEndPoint}, Player2Local={player2.LocalEndPoint}");

                for (int frame = 1; frame <= FrameCount; frame++)
                {
                    PlayerInputSnapshot input1 = CreateInput(frame, 1);
                    PlayerInputSnapshot input2 = CreateInput(frame, 2);

                    player1.SendInput(in input1);
                    player2.SendInput(in input2);

                    ServerAuthorityFramePacket authority1 = default, authority2 = default;
                    bool received1 = false, received2 = false;
                    double deadline = Time.realtimeSinceStartupAsDouble + AuthorityTimeoutSeconds;

                    while (!received1 || !received2)
                    {
                        if (!received1 && player1.TryReceiveAuthority(out ServerAuthorityFramePacket packet1))
                        {
                            int authorityFrame = packet1.InputSet.frameNumber;

                            if (authorityFrame > frame)
                                throw new InvalidOperationException(
                                    $"PublicKcpAuthorityUnityClientValidationTestBootstrap Run Error: Category=FutureAuthority, " +
                                    $"Receiver=1, ExpectedFrame={frame}, ActualFrame={authorityFrame}, Sequence={packet1.Sequence}");

                            if (authorityFrame == frame)
                            {
                                authority1 = packet1;
                                received1 = true;
                            }
                        }

                        if (!received2 && player2.TryReceiveAuthority(out ServerAuthorityFramePacket packet2))
                        {
                            int authorityFrame = packet2.InputSet.frameNumber;

                            if (authorityFrame > frame)
                                throw new InvalidOperationException(
                                    $"PublicKcpAuthorityUnityClientValidationTestBootstrap Run Error: Category=FutureAuthority, " +
                                    $"Receiver=2, ExpectedFrame={frame}, ActualFrame={authorityFrame}, Sequence={packet2.Sequence}");

                            if (authorityFrame == frame)
                            {
                                authority2 = packet2;
                                received2 = true;
                            }
                        }

                        ThrowIfClientError(player1);
                        ThrowIfClientError(player2);

                        if (received1 && received2) break;

                        if (Time.realtimeSinceStartupAsDouble >= deadline)
                        {
                            throw new TimeoutException(
                                $"PublicKcpAuthorityUnityClientValidationTestBootstrap Run Error: Category=AuthorityTimeout, " +
                                $"Frame={frame}, P1Received={received1}, P2Received={received2}, " +
                                $"P1=[{GetClientState(player1)}], P2=[{GetClientState(player2)}]");
                        }

                        yield return null;
                    }

                    ValidateAuthority(in authority1, frame, in input1, in input2, ref lastSequence1, 1);
                    ValidateAuthority(in authority2, frame, in input1, in input2, ref lastSequence2, 2);
                    AssertInputSetBitExact(authority1.InputSet, authority2.InputSet, frame, "ClientAuthorityComparison");

                    if (frame <= 5 || frame % 10 == 0)
                    {
                        UnityEngine.Debug.Log(
                            $"PublicKcpAuthorityUnityClientValidationTestBootstrap Run Log: " +
                            $"Frame={frame}/{FrameCount}, SequenceP1={authority1.Sequence}, SequenceP2={authority2.Sequence}");
                    }
                }

                stopwatch.Stop();

                UnityEngine.Debug.Log(
                    $"PublicKcpAuthorityUnityClientValidationTestBootstrap Run Log: [UNITY PUBLIC KCP AUTHORITY] " +
                    $"Server={ServerAddress}:{ServerPort}, Session=0x{SessionId:X8}, Players={PlayerCount}, Frames={FrameCount}, " +
                    $"LastSequenceP1={lastSequence1}, LastSequenceP2={lastSequence2}, " +
                    $"Elapsed={stopwatch.Elapsed.TotalMilliseconds:F2}ms, Result=PASS");
            }
            finally
            {
                player1?.Dispose();
                player2?.Dispose();
            }
        }

        private static PlayerInputSnapshot CreateInput(int frame, int playerID)
        {
            ulong pressed = 1UL << ((frame + playerID) % 8);
            ulong held = (1UL << ((frame + playerID + 1) % 8)) | (1UL << ((frame + playerID + 3) % 8));
            ulong released = 1UL << ((frame + playerID + 5) % 8);

            return new PlayerInputSnapshot(frame, playerID)
            {
                moveX = (frame + playerID) % 3 - 1,
                moveY = (frame * 2 + playerID) % 3 - 1,
                mouseX = frame * 0.25f + playerID,
                mouseY = -frame * 0.125f - playerID,
                mouseDeltaX = (frame % 7) * 0.03125f + playerID * 0.5f,
                mouseDeltaY = -(frame % 5) * 0.0625f - playerID * 0.25f,
                scrollX = (frame % 3 - 1) * 0.5f,
                scrollY = ((frame + playerID) % 3 - 1) * 0.25f,
                pressedButtons = (InputButtonFlags)pressed,
                heldButtons = (InputButtonFlags)held,
                releasedButtons = (InputButtonFlags)released
            };
        }

        private static void ValidateAuthority(
            in ServerAuthorityFramePacket packet,
            int frame,
            in PlayerInputSnapshot expected1,
            in PlayerInputSnapshot expected2,
            ref uint lastSequence,
            int receiverPlayerID)
        {
            Expect(packet.SessionId == SessionId,
                $"PublicKcpAuthorityUnityClientValidationTestBootstrap ValidateAuthority Error: Category=SessionMismatch, Receiver={receiverPlayerID}, Frame={frame}");

            Expect(packet.Sequence != 0,
                $"PublicKcpAuthorityUnityClientValidationTestBootstrap ValidateAuthority Error: Category=InvalidSequence, Receiver={receiverPlayerID}, Frame={frame}");

            Expect(packet.Sequence > lastSequence,
                $"PublicKcpAuthorityUnityClientValidationTestBootstrap ValidateAuthority Error: Category=SequenceNotIncreasing, Receiver={receiverPlayerID}, Frame={frame}, Previous={lastSequence}, Actual={packet.Sequence}");

            lastSequence = packet.Sequence;

            FrameInputSet inputSet = packet.InputSet;

            Expect(inputSet.IsCreated,
                $"PublicKcpAuthorityUnityClientValidationTestBootstrap ValidateAuthority Error: InputSet Not Created, Frame={frame}");

            Expect(inputSet.frameNumber == frame,
                $"PublicKcpAuthorityUnityClientValidationTestBootstrap ValidateAuthority Error: FrameMismatch, Expected={frame}, Actual={inputSet.frameNumber}");

            Expect(inputSet.Count == PlayerCount,
                $"PublicKcpAuthorityUnityClientValidationTestBootstrap ValidateAuthority Error: PlayerCount Expected={PlayerCount}, Actual={inputSet.Count}");

            Expect(inputSet.TryGetInput(1, out PlayerInputSnapshot actual1),
                $"PublicKcpAuthorityUnityClientValidationTestBootstrap ValidateAuthority Error: P1 Missing, Frame={frame}");

            Expect(inputSet.TryGetInput(2, out PlayerInputSnapshot actual2),
                $"PublicKcpAuthorityUnityClientValidationTestBootstrap ValidateAuthority Error: P2 Missing, Frame={frame}");

            AssertInputBitExact(in expected1, in actual1, frame, 1, $"Receiver{receiverPlayerID}");
            AssertInputBitExact(in expected2, in actual2, frame, 2, $"Receiver{receiverPlayerID}");
        }

        private static void AssertInputSetBitExact(FrameInputSet a, FrameInputSet b, int frame, string stage)
        {
            Expect(a.IsCreated && b.IsCreated,
                $"PublicKcpAuthorityUnityClientValidationTestBootstrap AssertInputSetBitExact Error: Stage={stage}, Frame={frame}");

            Expect(a.frameNumber == b.frameNumber && a.Count == b.Count,
                $"PublicKcpAuthorityUnityClientValidationTestBootstrap AssertInputSetBitExact Error: Stage={stage}, Frame={frame}, FrameA={a.frameNumber}, FrameB={b.frameNumber}, CountA={a.Count}, CountB={b.Count}");

            for (int playerID = 1; playerID <= PlayerCount; playerID++)
            {
                Expect(a.TryGetInput(playerID, out PlayerInputSnapshot inputA),
                    $"PublicKcpAuthorityUnityClientValidationTestBootstrap AssertInputSetBitExact Error: Stage={stage}, Frame={frame}, P{playerID} A Missing");

                Expect(b.TryGetInput(playerID, out PlayerInputSnapshot inputB),
                    $"PublicKcpAuthorityUnityClientValidationTestBootstrap AssertInputSetBitExact Error: Stage={stage}, Frame={frame}, P{playerID} B Missing");

                AssertInputBitExact(in inputA, in inputB, frame, playerID, stage);
            }
        }

        private static void AssertInputBitExact(in PlayerInputSnapshot expected, in PlayerInputSnapshot actual, int frame, int playerID, string stage)
        {
            Expect(expected.frameNumber == actual.frameNumber,
                $"{stage} FrameNumber Error: Frame={frame}, PlayerID={playerID}");

            Expect(expected.playerID == actual.playerID,
                $"{stage} PlayerID Error: Frame={frame}, PlayerID={playerID}");

            ExpectFloatBits(expected.moveX, actual.moveX, frame, playerID, stage, "MoveX");
            ExpectFloatBits(expected.moveY, actual.moveY, frame, playerID, stage, "MoveY");
            ExpectFloatBits(expected.mouseX, actual.mouseX, frame, playerID, stage, "MouseX");
            ExpectFloatBits(expected.mouseY, actual.mouseY, frame, playerID, stage, "MouseY");
            ExpectFloatBits(expected.mouseDeltaX, actual.mouseDeltaX, frame, playerID, stage, "MouseDeltaX");
            ExpectFloatBits(expected.mouseDeltaY, actual.mouseDeltaY, frame, playerID, stage, "MouseDeltaY");
            ExpectFloatBits(expected.scrollX, actual.scrollX, frame, playerID, stage, "ScrollX");
            ExpectFloatBits(expected.scrollY, actual.scrollY, frame, playerID, stage, "ScrollY");

            Expect(expected.pressedButtons == actual.pressedButtons,
                $"{stage} PressedButtons Error: Frame={frame}, PlayerID={playerID}");

            Expect(expected.heldButtons == actual.heldButtons,
                $"{stage} HeldButtons Error: Frame={frame}, PlayerID={playerID}");

            Expect(expected.releasedButtons == actual.releasedButtons,
                $"{stage} ReleasedButtons Error: Frame={frame}, PlayerID={playerID}");
        }

        private static void ExpectFloatBits(float expected, float actual, int frame, int playerID, string stage, string field)
        {
            int expectedBits = BitConverter.SingleToInt32Bits(expected);
            int actualBits = BitConverter.SingleToInt32Bits(actual);

            Expect(expectedBits == actualBits,
                $"{stage} {field} Error: Frame={frame}, PlayerID={playerID}, Expected={expected}({expectedBits:X8}), Actual={actual}({actualBits:X8})");
        }

        private static void ThrowIfClientError(KcpNetworkInputClient client)
        {
            if (client.LastKcpError.HasValue)
            {
                throw new InvalidOperationException(
                    $"PublicKcpAuthorityUnityClientValidationTestBootstrap Client Error: PlayerID={client.PlayerID}, " +
                    $"KcpError={client.LastKcpError}, Message={client.LastKcpErrorMessage}");
            }

            if (client.LastRejectReason != NetworkInputExchangeRejectReason.None)
            {
                throw new InvalidOperationException(
                    $"PublicKcpAuthorityUnityClientValidationTestBootstrap Client Error: PlayerID={client.PlayerID}, " +
                    $"Reject={client.LastRejectReason}, Decode={client.LastDecodeError}");
            }
        }

        private static string GetClientState(KcpNetworkInputClient client)
            => $"Connected={client.IsConnected}, Local={client.LocalEndPoint}, LastSentSequence={client.LastSentSequence}, " +
              $"LastRejectReason={client.LastRejectReason}, LastDecodeError={client.LastDecodeError}, " +
              $"LastKcpError={client.LastKcpError}, LastKcpErrorMessage={client.LastKcpErrorMessage}";

        private static void Expect(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}