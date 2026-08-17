using ECSFrameWork;
using FrameWork.RollBackSystem;
using System;

namespace FrameWork.NetworkSync
{
    /// <summary>PlayerInputSnapshot 固定 64 字节网络编码器。</summary>
    public static class PlayerInputSnapshotNetworkCodec
    {
        public const int WireSize = 64;

        /// <summary>按固定字段顺序写入完整 PlayerInputSnapshot。</summary>
        public static void Write(NetworkPacketWriter writer, in PlayerInputSnapshot input)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));

            writer.WriteInt32(input.frameNumber);
            writer.WriteInt32(input.playerID);

            writer.WriteSingle(input.moveX);
            writer.WriteSingle(input.moveY);
            writer.WriteSingle(input.mouseX);
            writer.WriteSingle(input.mouseY);
            writer.WriteSingle(input.mouseDeltaX);
            writer.WriteSingle(input.mouseDeltaY);
            writer.WriteSingle(input.scrollX);
            writer.WriteSingle(input.scrollY);

            WriteMask(writer, input.pressedButtons);
            WriteMask(writer, input.heldButtons);
            WriteMask(writer, input.releasedButtons);
        }

        /// <summary>读取完整 PlayerInputSnapshot。</summary>
        public static PlayerInputSnapshot Read(NetworkPacketReader reader)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));

            int frameNumber = reader.ReadInt32();
            int playerID = reader.ReadInt32();

            var input = new PlayerInputSnapshot(frameNumber, playerID)
            {
                moveX = reader.ReadSingle(),
                moveY = reader.ReadSingle(),
                mouseX = reader.ReadSingle(),
                mouseY = reader.ReadSingle(),
                mouseDeltaX = reader.ReadSingle(),
                mouseDeltaY = reader.ReadSingle(),
                scrollX = reader.ReadSingle(),
                scrollY = reader.ReadSingle()
            };

            input.pressedButtons = ReadMask(reader, input.pressedButtons);
            input.heldButtons = ReadMask(reader, input.heldButtons);
            input.releasedButtons = ReadMask(reader, input.releasedButtons);

            return input;
        }

        // Wire 层统一把 Button Mask 固定为 uint64，
        // 与当前项目字段实际使用 uint / ulong / enum 解耦。
        private static void WriteMask(NetworkPacketWriter writer, byte value) => writer.WriteUInt64(value);
        private static void WriteMask(NetworkPacketWriter writer, sbyte value) => writer.WriteUInt64(unchecked((ulong)value));
        private static void WriteMask(NetworkPacketWriter writer, ushort value) => writer.WriteUInt64(value);
        private static void WriteMask(NetworkPacketWriter writer, short value) => writer.WriteUInt64(unchecked((ulong)value));
        private static void WriteMask(NetworkPacketWriter writer, uint value) => writer.WriteUInt64(value);
        private static void WriteMask(NetworkPacketWriter writer, int value) => writer.WriteUInt64(unchecked((ulong)value));
        private static void WriteMask(NetworkPacketWriter writer, ulong value) => writer.WriteUInt64(value);
        private static void WriteMask(NetworkPacketWriter writer, long value) => writer.WriteUInt64(unchecked((ulong)value));
        private static void WriteMask<T>(NetworkPacketWriter writer, T value) where T : struct, Enum => writer.WriteUInt64(Convert.ToUInt64(value));

        private static byte ReadMask(NetworkPacketReader reader, byte _) => (byte)reader.ReadUInt64();
        private static sbyte ReadMask(NetworkPacketReader reader, sbyte _) => unchecked((sbyte)reader.ReadUInt64());
        private static ushort ReadMask(NetworkPacketReader reader, ushort _) => (ushort)reader.ReadUInt64();
        private static short ReadMask(NetworkPacketReader reader, short _) => unchecked((short)reader.ReadUInt64());
        private static uint ReadMask(NetworkPacketReader reader, uint _) => (uint)reader.ReadUInt64();
        private static int ReadMask(NetworkPacketReader reader, int _) => unchecked((int)reader.ReadUInt64());
        private static ulong ReadMask(NetworkPacketReader reader, ulong _) => reader.ReadUInt64();
        private static long ReadMask(NetworkPacketReader reader, long _) => unchecked((long)reader.ReadUInt64());
        private static T ReadMask<T>(NetworkPacketReader reader, T _) where T : struct, Enum => (T)Enum.ToObject(typeof(T), reader.ReadUInt64());
    }
}