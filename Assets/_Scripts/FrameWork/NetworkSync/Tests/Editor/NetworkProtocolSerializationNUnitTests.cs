using ECSFrameWork;
using FrameWork.RollBackSystem;
using NUnit.Framework;
using System;

namespace FrameWork.NetworkSync.Tests
{
    [TestFixture]
    public sealed class NetworkProtocolSerializationNUnitTests
    {
        [Test]
        public void ClientInput_RoundTrip_PreservesCompleteSnapshot()
        {
            PlayerInputSnapshot input = CreateFullInput(120, 2);
            var source = new ClientInputPacket(0x11223344u, 99u, input);

            byte[] data = NetworkPacketSerializer.SerializeClientInput(in source);

            Assert.AreEqual(
                NetworkProtocolConstants.HeaderSize + PlayerInputSnapshotNetworkCodec.WireSize,
                data.Length);

            Assert.IsTrue(
                NetworkPacketSerializer.TryDeserializeClientInput(
                    data,
                    out ClientInputPacket result,
                    out NetworkPacketDecodeError error),
                error.ToString());

            Assert.AreEqual(source.SessionId, result.SessionId);
            Assert.AreEqual(source.Sequence, result.Sequence);
            Assert.IsTrue(new PlayerInputSnapshotComparer().IsEqual(source.Input, result.Input));
        }

        [Test]
        public void ClientInput_WireFormat_IsStableLittleEndian()
        {
            PlayerInputSnapshot input = CreateFullInput(0x01020304, 0x05060708);
            var packet = new ClientInputPacket(0x11223344u, 0x55667788u, input);

            byte[] data = NetworkPacketSerializer.SerializeClientInput(in packet);

            Assert.AreEqual(0x55, data[0]);
            Assert.AreEqual(0x45, data[1]);
            Assert.AreEqual(0x53, data[2]);
            Assert.AreEqual(0x54, data[3]);

            Assert.AreEqual(0x01, data[4]);
            Assert.AreEqual(0x00, data[5]);

            Assert.AreEqual((byte)NetworkPacketType.ClientInput, data[6]);
            Assert.AreEqual(0, data[7]);

            Assert.AreEqual(0x44, data[8]);
            Assert.AreEqual(0x33, data[9]);
            Assert.AreEqual(0x22, data[10]);
            Assert.AreEqual(0x11, data[11]);

            Assert.AreEqual(0x88, data[12]);
            Assert.AreEqual(0x77, data[13]);
            Assert.AreEqual(0x66, data[14]);
            Assert.AreEqual(0x55, data[15]);

            Assert.AreEqual(64, data[16]);
            Assert.AreEqual(0, data[17]);
            Assert.AreEqual(0, data[18]);
            Assert.AreEqual(0, data[19]);

            Assert.AreEqual(0x04, data[20]);
            Assert.AreEqual(0x03, data[21]);
            Assert.AreEqual(0x02, data[22]);
            Assert.AreEqual(0x01, data[23]);

            Assert.AreEqual(0x08, data[24]);
            Assert.AreEqual(0x07, data[25]);
            Assert.AreEqual(0x06, data[26]);
            Assert.AreEqual(0x05, data[27]);
        }

        [Test]
        public void ServerAuthorityFrame_FourPlayers_RoundTripPreservesFrameInputSet()
        {
            FrameInputSet set = new FrameInputSet(300, new[]
            {
                CreateFullInput(300,4),
                CreateFullInput(300,2),
                CreateFullInput(300,1),
                CreateFullInput(300,3)
            });

            var source = new ServerAuthorityFramePacket(0xAABBCCDDu, 1234u, set);

            byte[] data = NetworkPacketSerializer.SerializeServerAuthorityFrame(in source);

            Assert.AreEqual(284, data.Length);

            Assert.IsTrue(
                NetworkPacketSerializer.TryDeserializeServerAuthorityFrame(
                    data,
                    out ServerAuthorityFramePacket result,
                    out NetworkPacketDecodeError error),
                error.ToString());

            Assert.AreEqual(source.SessionId, result.SessionId);
            Assert.AreEqual(source.Sequence, result.Sequence);
            Assert.IsTrue(new FrameInputSetComparer().IsEqual(source.InputSet, result.InputSet));

            Assert.AreEqual(1, result.InputSet.GetInputAt(0).playerID);
            Assert.AreEqual(2, result.InputSet.GetInputAt(1).playerID);
            Assert.AreEqual(3, result.InputSet.GetInputAt(2).playerID);
            Assert.AreEqual(4, result.InputSet.GetInputAt(3).playerID);
        }

        [Test]
        public void Header_InvalidMagic_IsRejected()
        {
            byte[] data = CreateClientPacketBytes();
            data[0] ^= 0xFF;

            Assert.IsFalse(NetworkPacketSerializer.TryReadHeader(data, out _, out NetworkPacketDecodeError error));
            Assert.AreEqual(NetworkPacketDecodeError.InvalidMagic, error);
        }

        [Test]
        public void Header_UnsupportedVersion_IsRejected()
        {
            byte[] data = CreateClientPacketBytes();
            data[4] = 0xFF;
            data[5] = 0x7F;

            Assert.IsFalse(NetworkPacketSerializer.TryReadHeader(data, out _, out NetworkPacketDecodeError error));
            Assert.AreEqual(NetworkPacketDecodeError.UnsupportedVersion, error);
        }

        [Test]
        public void Header_UnknownPacketType_IsRejected()
        {
            byte[] data = CreateClientPacketBytes();
            data[6] = 255;

            Assert.IsFalse(NetworkPacketSerializer.TryReadHeader(data, out _, out NetworkPacketDecodeError error));
            Assert.AreEqual(NetworkPacketDecodeError.UnknownPacketType, error);
        }

        [Test]
        public void Packet_TruncatedPayload_IsRejected()
        {
            byte[] source = CreateClientPacketBytes();
            var truncated = new byte[source.Length - 1];
            Buffer.BlockCopy(source, 0, truncated, 0, truncated.Length);

            Assert.IsFalse(NetworkPacketSerializer.TryReadHeader(truncated, out _, out NetworkPacketDecodeError error));
            Assert.AreEqual(NetworkPacketDecodeError.TruncatedPayload, error);
        }

        [Test]
        public void Packet_TrailingData_IsRejected()
        {
            byte[] source = CreateClientPacketBytes();
            var trailing = new byte[source.Length + 1];
            Buffer.BlockCopy(source, 0, trailing, 0, source.Length);
            trailing[trailing.Length - 1] = 0x7F;

            Assert.IsFalse(NetworkPacketSerializer.TryReadHeader(trailing, out _, out NetworkPacketDecodeError error));
            Assert.AreEqual(NetworkPacketDecodeError.TrailingData, error);
        }

        [Test]
        public void ServerAuthorityFrame_InvalidPlayerCount_IsRejected()
        {
            FrameInputSet set = new FrameInputSet(300, new[]
            {
                CreateFullInput(300,1),
                CreateFullInput(300,2)
            });

            var packet = new ServerAuthorityFramePacket(1u, 1u, set);
            byte[] data = NetworkPacketSerializer.SerializeServerAuthorityFrame(in packet);

            // Payload:
            // Header 20
            // FrameNumber 4
            // PlayerCount 2
            data[24] = 0;
            data[25] = 0;

            Assert.IsFalse(
                NetworkPacketSerializer.TryDeserializeServerAuthorityFrame(
                    data,
                    out _,
                    out NetworkPacketDecodeError error));

            Assert.AreEqual(NetworkPacketDecodeError.InvalidPlayerCount, error);
        }

        private static byte[] CreateClientPacketBytes()
        {
            var packet = new ClientInputPacket(1u, 1u, CreateFullInput(100, 1));
            return NetworkPacketSerializer.SerializeClientInput(in packet);
        }

        private static PlayerInputSnapshot CreateFullInput(int frame, int playerID)
        {
            var input = new PlayerInputSnapshot(frame, playerID)
            {
                moveX = 0.75f,
                moveY = -0.5f,
                mouseX = 123.25f,
                mouseY = -456.5f,
                mouseDeltaX = 2.5f,
                mouseDeltaY = -3.75f,
                scrollX = 1.25f,
                scrollY = -2.25f
            };

            input.pressedButtons = SetMask(input.pressedButtons, 0x05UL);
            input.heldButtons = SetMask(input.heldButtons, 0x12UL);
            input.releasedButtons = SetMask(input.releasedButtons, 0x20UL);

            return input;
        }

        private static byte SetMask(byte _, ulong value) => (byte)value;
        private static sbyte SetMask(sbyte _, ulong value) => unchecked((sbyte)value);
        private static ushort SetMask(ushort _, ulong value) => (ushort)value;
        private static short SetMask(short _, ulong value) => unchecked((short)value);
        private static uint SetMask(uint _, ulong value) => (uint)value;
        private static int SetMask(int _, ulong value) => unchecked((int)value);
        private static ulong SetMask(ulong _, ulong value) => value;
        private static long SetMask(long _, ulong value) => unchecked((long)value);
        private static T SetMask<T>(T _, ulong value) where T : struct, Enum => (T)Enum.ToObject(typeof(T), value);
    }
}