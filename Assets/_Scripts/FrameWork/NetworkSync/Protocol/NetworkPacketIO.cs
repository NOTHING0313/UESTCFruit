using System;

namespace FrameWork.NetworkSync
{
    /// <summary>显式 Little Endian 网络包写入器。</summary>
    public sealed class NetworkPacketWriter
    {
        private readonly byte[] _buffer;
        private int _offset;

        public int Length => _offset;

        public NetworkPacketWriter(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _buffer = new byte[capacity];
        }

        public void WriteByte(byte value)
        {
            Ensure(1);
            _buffer[_offset++] = value;
        }

        public void WriteUInt16(ushort value)
        {
            Ensure(2);
            _buffer[_offset++] = (byte)value;
            _buffer[_offset++] = (byte)(value >> 8);
        }

        public void WriteUInt32(uint value)
        {
            Ensure(4);
            _buffer[_offset++] = (byte)value;
            _buffer[_offset++] = (byte)(value >> 8);
            _buffer[_offset++] = (byte)(value >> 16);
            _buffer[_offset++] = (byte)(value >> 24);
        }

        public void WriteUInt64(ulong value)
        {
            Ensure(8);
            WriteUInt32((uint)value);
            WriteUInt32((uint)(value >> 32));
        }

        public void WriteInt32(int value) => WriteUInt32(unchecked((uint)value));

        public void WriteSingle(float value) => WriteInt32(BitConverter.SingleToInt32Bits(value));

        public byte[] ToArray()
        {
            var result = new byte[_offset];
            Buffer.BlockCopy(_buffer, 0, result, 0, _offset);
            return result;
        }

        private void Ensure(int count)
        {
            if (_offset + count > _buffer.Length)
                throw new InvalidOperationException($"NetworkPacketWriter Capacity Exceeded: Capacity={_buffer.Length}, Offset={_offset}, Write={count}");
        }
    }

    /// <summary>显式 Little Endian 网络包读取器。</summary>
    public sealed class NetworkPacketReader
    {
        private readonly byte[] _buffer;
        private readonly int _end;
        private int _offset;

        public int Remaining => _end - _offset;

        public NetworkPacketReader(byte[] buffer, int offset, int length)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || length < 0 || offset + length > buffer.Length) throw new ArgumentOutOfRangeException();

            _buffer = buffer;
            _offset = offset;
            _end = offset + length;
        }

        public byte ReadByte()
        {
            Ensure(1);
            return _buffer[_offset++];
        }

        public ushort ReadUInt16()
        {
            Ensure(2);
            ushort value = (ushort)(_buffer[_offset] | (_buffer[_offset + 1] << 8));
            _offset += 2;
            return value;
        }

        public uint ReadUInt32()
        {
            Ensure(4);

            uint value =
                _buffer[_offset] |
                ((uint)_buffer[_offset + 1] << 8) |
                ((uint)_buffer[_offset + 2] << 16) |
                ((uint)_buffer[_offset + 3] << 24);

            _offset += 4;
            return value;
        }

        public ulong ReadUInt64()
        {
            ulong low = ReadUInt32();
            ulong high = ReadUInt32();
            return low | (high << 32);
        }

        public int ReadInt32() => unchecked((int)ReadUInt32());

        public float ReadSingle() => BitConverter.Int32BitsToSingle(ReadInt32());

        private void Ensure(int count)
        {
            if (_offset + count > _end)
                throw new InvalidOperationException($"NetworkPacketReader Out Of Data: Remaining={Remaining}, Read={count}");
        }
    }
}