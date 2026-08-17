using ECSFrameWork;
using FrameWork.RollBackSystem;
using System;

namespace FrameWork.NetworkSync
{
    /// <summary>网络协议包类型。</summary>
    public enum NetworkPacketType : byte
    {
        ClientInput = 1,
        ServerAuthorityFrame = 2
    }

    /// <summary>网络协议解码错误。</summary>
    public enum NetworkPacketDecodeError
    {
        None,
        NullData,
        TooShort,
        InvalidMagic,
        UnsupportedVersion,
        UnknownPacketType,
        InvalidFlags,
        InvalidReserved,
        TruncatedPayload,
        TrailingData,
        WrongPacketType,
        InvalidPayloadLength,
        InvalidPlayerCount,
        InvalidFrameInput
    }

    /// <summary>网络协议固定常量。</summary>
    public static class NetworkProtocolConstants
    {
        public const uint Magic = 0x54534555u;
        public const ushort Version = 1;
        public const int HeaderSize = 20;
        public const int MaxPlayerCount = 16;
        public const int MaxDatagramSize = 1200;
        public const int AuthorityPayloadPrefixSize = 8;
    }

    /// <summary>所有网络数据包共享的固定头。</summary>
    public readonly struct NetworkPacketHeader
    {
        public readonly uint Magic;
        public readonly ushort Version;
        public readonly NetworkPacketType PacketType;
        public readonly byte Flags;
        public readonly uint SessionId;
        public readonly uint Sequence;
        public readonly ushort PayloadLength;
        public readonly ushort Reserved;

        public NetworkPacketHeader(uint magic, ushort version, NetworkPacketType packetType, byte flags, uint sessionId, uint sequence, ushort payloadLength, ushort reserved)
        {
            Magic = magic;
            Version = version;
            PacketType = packetType;
            Flags = flags;
            SessionId = sessionId;
            Sequence = sequence;
            PayloadLength = payloadLength;
            Reserved = reserved;
        }
    }

    /// <summary>客户端向服务器发送的单玩家单帧输入。</summary>
    public readonly struct ClientInputPacket
    {
        public readonly uint SessionId;
        public readonly uint Sequence;
        public readonly PlayerInputSnapshot Input;

        public ClientInputPacket(uint sessionId, uint sequence, PlayerInputSnapshot input)
        {
            if (input.frameNumber <= 0) throw new ArgumentOutOfRangeException(nameof(input), input.frameNumber, "Input Frame Must Be Greater Than Zero");
            if (input.playerID <= 0) throw new ArgumentOutOfRangeException(nameof(input), input.playerID, "Player ID Must Be Greater Than Zero");

            SessionId = sessionId;
            Sequence = sequence;
            Input = input;
        }
    }

    /// <summary>服务器广播的一帧完整权威玩家输入集合。</summary>
    public readonly struct ServerAuthorityFramePacket
    {
        public readonly uint SessionId;
        public readonly uint Sequence;
        public readonly FrameInputSet InputSet;

        public ServerAuthorityFramePacket(uint sessionId, uint sequence, FrameInputSet inputSet)
        {
            if (!inputSet.IsCreated) throw new ArgumentException("Authority Frame Input Set Is Not Created", nameof(inputSet));
            if (inputSet.Count <= 0 || inputSet.Count > NetworkProtocolConstants.MaxPlayerCount)
                throw new ArgumentOutOfRangeException(nameof(inputSet), inputSet.Count, $"Player Count Must Be 1~{NetworkProtocolConstants.MaxPlayerCount}");

            SessionId = sessionId;
            Sequence = sequence;
            InputSet = inputSet;
        }
    }
}