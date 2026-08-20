using ECSFrameWork;
using FrameWork.NetworkSync;
using FrameWork.RollBackSystem;
using System;
using System.Collections;
using System.Diagnostics;
using System.Net;
using UnityEngine;

namespace FrameWork.NetworkSync.Tests
{
    /// <summary>
    /// Unity 公网 Authority Smoke 验证入口。
    /// </summary>
    public static class PublicAuthorityUnityClientValidationTestBootstrap
    {
        private const string ServerAddress = "8.137.83.229";
        private const int ServerPort = 28015;
        private const uint SessionId = 0x11223344u;
        private const int PlayerCount = 2;
        private const int FrameCount = 100;
        private const double TimeoutSeconds = 3.0;

        /// <summary>
        /// 使用两个真实 LocalNetworkInputClient 完成公网 Authority RoundTrip。
        /// </summary>
        public static IEnumerator Run()
        {
            var serverEndPoint = new IPEndPoint(IPAddress.Parse(ServerAddress), ServerPort);
            LocalNetworkInputClient player1 = null;
            LocalNetworkInputClient player2 = null;
            var stopwatch = Stopwatch.StartNew();
            int authorityFrameCount = 0;
            uint lastSequence1 = 0, lastSequence2 = 0;
            bool hasSequence1 = false, hasSequence2 = false;

            try
            {
                var config1 = new UdpTransportConfig("0.0.0.0", 0, NetworkProtocolConstants.MaxDatagramSize);
                var config2 = new UdpTransportConfig("0.0.0.0", 0, NetworkProtocolConstants.MaxDatagramSize);

                player1 = new LocalNetworkInputClient(config1, serverEndPoint, SessionId, 1);
                player2 = new LocalNetworkInputClient(config2, serverEndPoint, SessionId, 2);

                UnityEngine.Debug.Log($"PublicAuthorityUnityClientValidationTestBootstrap Run Log: Server={serverEndPoint}, Session=0x{SessionId:X8}, Player1={player1.LocalEndPoint}, Player2={player2.LocalEndPoint}");

                for (int frame = 1; frame <= FrameCount; frame++)
                {
                    PlayerInputSnapshot input1 = CreateInput(frame, 1);
                    PlayerInputSnapshot input2 = CreateInput(frame, 2);

                    player1.SendInput(in input1);
                    player2.SendInput(in input2);

                    bool received1 = false, received2 = false;
                    ServerAuthorityFramePacket authority1 = default, authority2 = default;
                    double deadline = Time.realtimeSinceStartupAsDouble + TimeoutSeconds;

                    while (!received1 || !received2)
                    {
                        if (!received1 && player1.TryReceiveAuthority(out ServerAuthorityFramePacket packet1))
                        {
                            int authorityFrame = packet1.InputSet.frameNumber;

                            if (authorityFrame > frame)
                                throw new InvalidOperationException($"PublicAuthorityUnityClientValidationTestBootstrap Run Error: Category=FutureAuthority, Frame={frame}, PlayerID=1, AuthorityFrame={authorityFrame}, Sequence={packet1.Sequence}, Local={player1.LocalEndPoint}, Server={serverEndPoint}");

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
                                throw new InvalidOperationException($"PublicAuthorityUnityClientValidationTestBootstrap Run Error: Category=FutureAuthority, Frame={frame}, PlayerID=2, AuthorityFrame={authorityFrame}, Sequence={packet2.Sequence}, Local={player2.LocalEndPoint}, Server={serverEndPoint}");

                            if (authorityFrame == frame)
                            {
                                authority2 = packet2;
                                received2 = true;
                            }
                        }

                        if (received1 && received2) break;

                        if (Time.realtimeSinceStartupAsDouble >= deadline)
                        {
                            string state1 = GetClientState(player1);
                            string state2 = GetClientState(player2);

                            throw new TimeoutException(
                                $"PublicAuthorityUnityClientValidationTestBootstrap Run Error: Category=Timeout, Frame={frame}, Server={serverEndPoint}, " +
                                $"Player1Local={player1.LocalEndPoint}, Player1Received={received1}, Player1State=[{state1}], " +
                                $"Player2Local={player2.LocalEndPoint}, Player2Received={received2}, Player2State=[{state2}]");
                        }

                        yield return null;
                    }

                    ValidateAuthority(in authority1, frame, 1, in input1, in input2, ref lastSequence1, ref hasSequence1);
                    ValidateAuthority(in authority2, frame, 2, in input1, in input2, ref lastSequence2, ref hasSequence2);
                    AssertInputSetBitExact(authority1.InputSet, authority2.InputSet, frame, "ClientAuthorityComparison");

                    authorityFrameCount++;

                    if (frame <= 5 || frame % 10 == 0)
                        UnityEngine.Debug.Log($"PublicAuthorityUnityClientValidationTestBootstrap Run Log: Frame={frame}/{FrameCount}, SequenceP1={authority1.Sequence}, SequenceP2={authority2.Sequence}");
                }

                stopwatch.Stop();

                Expect(authorityFrameCount == FrameCount, $"PublicAuthorityUnityClientValidationTestBootstrap Run Error: AuthorityFrameCount Expected={FrameCount}, Actual={authorityFrameCount}");

                UnityEngine.Debug.Log(
                    $"PublicAuthorityUnityClientValidationTestBootstrap Run Log: [UNITY PUBLIC AUTHORITY SMOKE] " +
                    $"Server={serverEndPoint}, Session=0x{SessionId:X8}, Players={PlayerCount}, Frames={FrameCount}, " +
                    $"AuthorityFrames={authorityFrameCount}, Elapsed={stopwatch.Elapsed.TotalMilliseconds:F2}ms, Result=PASS");
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

        private static void ValidateAuthority(in ServerAuthorityFramePacket packet, int frame, int receiverPlayerID, in PlayerInputSnapshot expected1, in PlayerInputSnapshot expected2, ref uint lastSequence, ref bool hasSequence)
        {
            Expect(packet.SessionId == SessionId, $"PublicAuthorityUnityClientValidationTestBootstrap ValidateAuthority Error: Category=SessionMismatch, Frame={frame}, PlayerID={receiverPlayerID}, Expected=0x{SessionId:X8}, Actual=0x{packet.SessionId:X8}");
            Expect(packet.Sequence != 0, $"PublicAuthorityUnityClientValidationTestBootstrap ValidateAuthority Error: Category=InvalidSequence, Frame={frame}, PlayerID={receiverPlayerID}, Sequence=0");

            if (hasSequence)
                Expect(packet.Sequence > lastSequence, $"PublicAuthorityUnityClientValidationTestBootstrap ValidateAuthority Error: Category=SequenceNotIncreasing, Frame={frame}, PlayerID={receiverPlayerID}, Previous={lastSequence}, Actual={packet.Sequence}");

            lastSequence = packet.Sequence;
            hasSequence = true;

            FrameInputSet inputSet = packet.InputSet;

            Expect(inputSet.IsCreated, $"PublicAuthorityUnityClientValidationTestBootstrap ValidateAuthority Error: Category=InputSetNotCreated, Frame={frame}, PlayerID={receiverPlayerID}, Sequence={packet.Sequence}");
            Expect(inputSet.frameNumber == frame, $"PublicAuthorityUnityClientValidationTestBootstrap ValidateAuthority Error: Category=FrameMismatch, Frame={frame}, PlayerID={receiverPlayerID}, AuthorityFrame={inputSet.frameNumber}, Sequence={packet.Sequence}");
            Expect(inputSet.Count == PlayerCount, $"PublicAuthorityUnityClientValidationTestBootstrap ValidateAuthority Error: Category=PlayerCountMismatch, Frame={frame}, PlayerID={receiverPlayerID}, Expected={PlayerCount}, Actual={inputSet.Count}, Sequence={packet.Sequence}");

            Expect(inputSet.TryGetInput(1, out PlayerInputSnapshot actual1), $"PublicAuthorityUnityClientValidationTestBootstrap ValidateAuthority Error: Category=MissingInput, Frame={frame}, PlayerID=1, Receiver={receiverPlayerID}, Sequence={packet.Sequence}");
            Expect(inputSet.TryGetInput(2, out PlayerInputSnapshot actual2), $"PublicAuthorityUnityClientValidationTestBootstrap ValidateAuthority Error: Category=MissingInput, Frame={frame}, PlayerID=2, Receiver={receiverPlayerID}, Sequence={packet.Sequence}");

            AssertInputBitExact(in expected1, in actual1, frame, 1, $"Receiver{receiverPlayerID}");
            AssertInputBitExact(in expected2, in actual2, frame, 2, $"Receiver{receiverPlayerID}");
        }

        private static void AssertInputSetBitExact(FrameInputSet expected, FrameInputSet actual, int frame, string stage)
        {
            Expect(expected.IsCreated == actual.IsCreated, $"PublicAuthorityUnityClientValidationTestBootstrap AssertInputSetBitExact Error: Stage={stage}, Frame={frame}, IsCreatedA={expected.IsCreated}, IsCreatedB={actual.IsCreated}");
            Expect(expected.frameNumber == actual.frameNumber, $"PublicAuthorityUnityClientValidationTestBootstrap AssertInputSetBitExact Error: Stage={stage}, Frame={frame}, FrameA={expected.frameNumber}, FrameB={actual.frameNumber}");
            Expect(expected.Count == actual.Count, $"PublicAuthorityUnityClientValidationTestBootstrap AssertInputSetBitExact Error: Stage={stage}, Frame={frame}, CountA={expected.Count}, CountB={actual.Count}");

            for (int playerID = 1; playerID <= PlayerCount; playerID++)
            {
                Expect(expected.TryGetInput(playerID, out PlayerInputSnapshot inputA), $"PublicAuthorityUnityClientValidationTestBootstrap AssertInputSetBitExact Error: Stage={stage}, Frame={frame}, PlayerID={playerID}, InputA=Missing");
                Expect(actual.TryGetInput(playerID, out PlayerInputSnapshot inputB), $"PublicAuthorityUnityClientValidationTestBootstrap AssertInputSetBitExact Error: Stage={stage}, Frame={frame}, PlayerID={playerID}, InputB=Missing");
                AssertInputBitExact(in inputA, in inputB, frame, playerID, stage);
            }
        }

        private static void AssertInputBitExact(in PlayerInputSnapshot expected, in PlayerInputSnapshot actual, int frame, int playerID, string stage)
        {
            Expect(expected.frameNumber == actual.frameNumber, $"PublicAuthorityUnityClientValidationTestBootstrap AssertInputBitExact Error: Stage={stage}, Frame={frame}, PlayerID={playerID}, Field=FrameNumber, Expected={expected.frameNumber}, Actual={actual.frameNumber}");
            Expect(expected.playerID == actual.playerID, $"PublicAuthorityUnityClientValidationTestBootstrap AssertInputBitExact Error: Stage={stage}, Frame={frame}, PlayerID={playerID}, Field=PlayerID, Expected={expected.playerID}, Actual={actual.playerID}");

            ExpectFloatBits(expected.moveX, actual.moveX, frame, playerID, stage, "MoveX");
            ExpectFloatBits(expected.moveY, actual.moveY, frame, playerID, stage, "MoveY");
            ExpectFloatBits(expected.mouseX, actual.mouseX, frame, playerID, stage, "MouseX");
            ExpectFloatBits(expected.mouseY, actual.mouseY, frame, playerID, stage, "MouseY");
            ExpectFloatBits(expected.mouseDeltaX, actual.mouseDeltaX, frame, playerID, stage, "MouseDeltaX");
            ExpectFloatBits(expected.mouseDeltaY, actual.mouseDeltaY, frame, playerID, stage, "MouseDeltaY");
            ExpectFloatBits(expected.scrollX, actual.scrollX, frame, playerID, stage, "ScrollX");
            ExpectFloatBits(expected.scrollY, actual.scrollY, frame, playerID, stage, "ScrollY");

            Expect(expected.pressedButtons == actual.pressedButtons, $"PublicAuthorityUnityClientValidationTestBootstrap AssertInputBitExact Error: Stage={stage}, Frame={frame}, PlayerID={playerID}, Field=PressedButtons, Expected={expected.pressedButtons}, Actual={actual.pressedButtons}");
            Expect(expected.heldButtons == actual.heldButtons, $"PublicAuthorityUnityClientValidationTestBootstrap AssertInputBitExact Error: Stage={stage}, Frame={frame}, PlayerID={playerID}, Field=HeldButtons, Expected={expected.heldButtons}, Actual={actual.heldButtons}");
            Expect(expected.releasedButtons == actual.releasedButtons, $"PublicAuthorityUnityClientValidationTestBootstrap AssertInputBitExact Error: Stage={stage}, Frame={frame}, PlayerID={playerID}, Field=ReleasedButtons, Expected={expected.releasedButtons}, Actual={actual.releasedButtons}");
        }

        private static void ExpectFloatBits(float expected, float actual, int frame, int playerID, string stage, string field)
        {
            int expectedBits = BitConverter.SingleToInt32Bits(expected);
            int actualBits = BitConverter.SingleToInt32Bits(actual);

            Expect(expectedBits == actualBits,
                $"PublicAuthorityUnityClientValidationTestBootstrap ExpectFloatBits Error: Stage={stage}, Frame={frame}, PlayerID={playerID}, Field={field}, Expected={expected}({expectedBits:X8}), Actual={actual}({actualBits:X8})");
        }

        private static string GetClientState(LocalNetworkInputClient client)
            => $"LastSentSequence={client.LastSentSequence}, LastRejectReason={client.LastRejectReason}, LastDecodeError={client.LastDecodeError}";

        private static void Expect(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}