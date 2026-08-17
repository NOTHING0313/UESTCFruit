using ECSFrameWork;
using FrameWork.RollBackSystem;
using System;

namespace FrameWork.NetworkSync
{
    /// <summary>V1 网络协议序列化与反序列化入口。</summary>
    public static class NetworkPacketSerializer
    {
        /// <summary>序列化客户端输入包。</summary>
        public static byte[] SerializeClientInput(in ClientInputPacket packet)
        {
            const int payloadLength = PlayerInputSnapshotNetworkCodec.WireSize;
            int packetLength = NetworkProtocolConstants.HeaderSize + payloadLength;

            var writer = new NetworkPacketWriter(packetLength);

            WriteHeader(
                writer,
                NetworkPacketType.ClientInput,
                packet.SessionId,
                packet.Sequence,
                payloadLength);

            PlayerInputSnapshot input = packet.Input;
            PlayerInputSnapshotNetworkCodec.Write(writer, in input);

            ValidateSerializedLength(writer, packetLength);
            return writer.ToArray();
        }

        /// <summary>序列化服务器完整权威帧。</summary>
        public static byte[] SerializeServerAuthorityFrame(in ServerAuthorityFramePacket packet)
        {
            FrameInputSet inputSet = packet.InputSet;

            if (!inputSet.IsCreated) throw new InvalidOperationException("Authority Input Set Is Not Created");
            if (inputSet.Count <= 0 || inputSet.Count > NetworkProtocolConstants.MaxPlayerCount)
                throw new InvalidOperationException($"Authority Player Count Error: Count={inputSet.Count}");

            int payloadLength =
                NetworkProtocolConstants.AuthorityPayloadPrefixSize +
                inputSet.Count * PlayerInputSnapshotNetworkCodec.WireSize;

            int packetLength = NetworkProtocolConstants.HeaderSize + payloadLength;

            if (packetLength > NetworkProtocolConstants.MaxDatagramSize)
                throw new InvalidOperationException($"Authority Packet Too Large: Length={packetLength}, Max={NetworkProtocolConstants.MaxDatagramSize}");

            var writer = new NetworkPacketWriter(packetLength);

            WriteHeader(
                writer,
                NetworkPacketType.ServerAuthorityFrame,
                packet.SessionId,
                packet.Sequence,
                payloadLength);

            writer.WriteInt32(inputSet.frameNumber);
            writer.WriteUInt16((ushort)inputSet.Count);
            writer.WriteUInt16(0);

            for (int i = 0; i < inputSet.Count; i++)
            {
                PlayerInputSnapshot input = inputSet.GetInputAt(i);
                PlayerInputSnapshotNetworkCodec.Write(writer, in input);
            }

            ValidateSerializedLength(writer, packetLength);
            return writer.ToArray();
        }

        /// <summary>只解析并验证协议头，供未来 UDP Receiver 分发 PacketType。</summary>
        public static bool TryReadHeader(byte[] data, out NetworkPacketHeader header, out NetworkPacketDecodeError error)
        {
            header = default;

            if (data == null)
            {
                error = NetworkPacketDecodeError.NullData;
                return false;
            }

            if (data.Length < NetworkProtocolConstants.HeaderSize)
            {
                error = NetworkPacketDecodeError.TooShort;
                return false;
            }

            var reader = new NetworkPacketReader(data, 0, NetworkProtocolConstants.HeaderSize);

            uint magic = reader.ReadUInt32();
            ushort version = reader.ReadUInt16();
            byte rawPacketType = reader.ReadByte();
            byte flags = reader.ReadByte();
            uint sessionId = reader.ReadUInt32();
            uint sequence = reader.ReadUInt32();
            ushort payloadLength = reader.ReadUInt16();
            ushort reserved = reader.ReadUInt16();

            if (magic != NetworkProtocolConstants.Magic)
            {
                error = NetworkPacketDecodeError.InvalidMagic;
                return false;
            }

            if (version != NetworkProtocolConstants.Version)
            {
                error = NetworkPacketDecodeError.UnsupportedVersion;
                return false;
            }

            NetworkPacketType packetType = (NetworkPacketType)rawPacketType;

            if (packetType != NetworkPacketType.ClientInput && packetType != NetworkPacketType.ServerAuthorityFrame)
            {
                error = NetworkPacketDecodeError.UnknownPacketType;
                return false;
            }

            if (flags != 0)
            {
                error = NetworkPacketDecodeError.InvalidFlags;
                return false;
            }

            if (reserved != 0)
            {
                error = NetworkPacketDecodeError.InvalidReserved;
                return false;
            }

            int expectedLength = NetworkProtocolConstants.HeaderSize + payloadLength;

            if (data.Length < expectedLength)
            {
                error = NetworkPacketDecodeError.TruncatedPayload;
                return false;
            }

            if (data.Length > expectedLength)
            {
                error = NetworkPacketDecodeError.TrailingData;
                return false;
            }

            header = new NetworkPacketHeader(
                magic,
                version,
                packetType,
                flags,
                sessionId,
                sequence,
                payloadLength,
                reserved);

            error = NetworkPacketDecodeError.None;
            return true;
        }

        /// <summary>反序列化客户端输入包。</summary>
        public static bool TryDeserializeClientInput(byte[] data, out ClientInputPacket packet, out NetworkPacketDecodeError error)
        {
            packet = default;

            if (!TryReadHeader(data, out NetworkPacketHeader header, out error)) return false;

            if (header.PacketType != NetworkPacketType.ClientInput)
            {
                error = NetworkPacketDecodeError.WrongPacketType;
                return false;
            }

            if (header.PayloadLength != PlayerInputSnapshotNetworkCodec.WireSize)
            {
                error = NetworkPacketDecodeError.InvalidPayloadLength;
                return false;
            }

            var reader = new NetworkPacketReader(
                data,
                NetworkProtocolConstants.HeaderSize,
                header.PayloadLength);

            PlayerInputSnapshot input = PlayerInputSnapshotNetworkCodec.Read(reader);

            if (input.frameNumber <= 0 || input.playerID <= 0 || reader.Remaining != 0)
            {
                error = NetworkPacketDecodeError.InvalidFrameInput;
                return false;
            }

            packet = new ClientInputPacket(header.SessionId, header.Sequence, input);
            error = NetworkPacketDecodeError.None;
            return true;
        }

        /// <summary>反序列化服务器完整权威帧。</summary>
        public static bool TryDeserializeServerAuthorityFrame(byte[] data, out ServerAuthorityFramePacket packet, out NetworkPacketDecodeError error)
        {
            packet = default;

            if (!TryReadHeader(data, out NetworkPacketHeader header, out error)) return false;

            if (header.PacketType != NetworkPacketType.ServerAuthorityFrame)
            {
                error = NetworkPacketDecodeError.WrongPacketType;
                return false;
            }

            if (header.PayloadLength < NetworkProtocolConstants.AuthorityPayloadPrefixSize)
            {
                error = NetworkPacketDecodeError.InvalidPayloadLength;
                return false;
            }

            var reader = new NetworkPacketReader(
                data,
                NetworkProtocolConstants.HeaderSize,
                header.PayloadLength);

            int frameNumber = reader.ReadInt32();
            ushort playerCount = reader.ReadUInt16();
            ushort reserved = reader.ReadUInt16();

            if (reserved != 0)
            {
                error = NetworkPacketDecodeError.InvalidReserved;
                return false;
            }

            if (playerCount == 0 || playerCount > NetworkProtocolConstants.MaxPlayerCount)
            {
                error = NetworkPacketDecodeError.InvalidPlayerCount;
                return false;
            }

            int expectedPayloadLength =
                NetworkProtocolConstants.AuthorityPayloadPrefixSize +
                playerCount * PlayerInputSnapshotNetworkCodec.WireSize;

            if (header.PayloadLength != expectedPayloadLength)
            {
                error = NetworkPacketDecodeError.InvalidPayloadLength;
                return false;
            }

            var inputs = new PlayerInputSnapshot[playerCount];

            for (int i = 0; i < playerCount; i++)
            {
                PlayerInputSnapshot input = PlayerInputSnapshotNetworkCodec.Read(reader);

                if (input.frameNumber != frameNumber || input.frameNumber <= 0 || input.playerID <= 0)
                {
                    error = NetworkPacketDecodeError.InvalidFrameInput;
                    return false;
                }

                inputs[i] = input;
            }

            if (reader.Remaining != 0)
            {
                error = NetworkPacketDecodeError.InvalidPayloadLength;
                return false;
            }

            try
            {
                FrameInputSet inputSet = new FrameInputSet(frameNumber, inputs);
                packet = new ServerAuthorityFramePacket(header.SessionId, header.Sequence, inputSet);
            }
            catch (ArgumentException)
            {
                error = NetworkPacketDecodeError.InvalidFrameInput;
                return false;
            }

            error = NetworkPacketDecodeError.None;
            return true;
        }

        private static void WriteHeader(NetworkPacketWriter writer, NetworkPacketType packetType, uint sessionId, uint sequence, int payloadLength)
        {
            if (payloadLength < 0 || payloadLength > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(payloadLength));

            writer.WriteUInt32(NetworkProtocolConstants.Magic);
            writer.WriteUInt16(NetworkProtocolConstants.Version);
            writer.WriteByte((byte)packetType);
            writer.WriteByte(0);
            writer.WriteUInt32(sessionId);
            writer.WriteUInt32(sequence);
            writer.WriteUInt16((ushort)payloadLength);
            writer.WriteUInt16(0);
        }

        private static void ValidateSerializedLength(NetworkPacketWriter writer, int expected)
        {
            if (writer.Length != expected)
                throw new InvalidOperationException($"Network Packet Length Error: Expected={expected}, Actual={writer.Length}");
        }
    }
}