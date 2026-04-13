using BuffSystem;
using System;
using System.Collections.Generic;

namespace Utility
{
    public sealed class BitSet
    {
        private ulong[] _words;
        public int BitCapacity { get; private set; }
        public bool IsEmpty
        {
            get
            {
                if (_words == null) return true;
                for (int i = 0; i < _words.Length; i++)
                    if (_words[i] != 0) return false;
                return true;
            }
        }
        public BitSet(int capacity = 0) => EnsureCapacity(capacity);
        public void ClearAll()
        {
            if (_words == null)
                return;
            Array.Clear(_words, 0, _words.Length);
        }
        private void EnsureCapacity(int count)
        {
            if (count <= BitCapacity)
                return;
            BitCapacity = count;
            int capacity = (count + 63) >> 6;
            if (_words == null)
                _words = new ulong[capacity];
            else if (_words.Length < capacity)
                Array.Resize(ref _words, capacity);
        }
        public bool ContainsAny(BitSet other)
        {
            int len = Math.Min(_words?.Length ?? 0, other._words?.Length ?? 0);
            for (int i = 0; i < len; i++)
                if ((_words[i] & other._words[i]) != 0)
                    return true;
            return false;
        }
        public bool ContainsAll(BitSet other)
        {
            int len = Math.Min(_words?.Length ?? 0, other._words?.Length ?? 0);
            for (int i = 0; i < len; i++)
                if ((_words[i] & other._words[i]) != other._words[i])
                    return false;
            for (int i = len; i < (other._words?.Length ?? 0); i++)
                if (other._words[i] != 0)
                    return false;
            return true;
        }
        public void Set(int bitIndex)
        {
            if (bitIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(bitIndex));
            EnsureCapacity(bitIndex + 1);
            _words[bitIndex >> 6] |= 1UL << (bitIndex & 63);
        }
        public void Remove(int bitIndex)
        {
            if (bitIndex < 0 || bitIndex >= BitCapacity)
                throw new ArgumentOutOfRangeException(nameof(bitIndex));
            _words[bitIndex >> 6] &= ~(1UL << (bitIndex & 63));
        }
        public bool Contain(int bitIndex)
        {
            if (bitIndex < 0 || bitIndex >= BitCapacity)
                throw new ArgumentOutOfRangeException(nameof(bitIndex));
            return (_words[bitIndex >> 6] & (1UL << (bitIndex & 63))) != 0;
        }
        public void FillAll(int count)
        {
            EnsureCapacity(count);
            int fullWords = count >> 6;
            int remainBits = count & 63;

            for (int i = 0; i < fullWords; i++)
                _words[i] = ulong.MaxValue;

            if (remainBits > 0)
                _words[fullWords] = (1UL << remainBits) - 1UL;

            for (int i = fullWords + (remainBits > 0 ? 1 : 0); i < _words.Length; i++)
                _words[i] = 0;
        }
        public void AndWith(BitSet other)
        {
            int len = Math.Min(_words?.Length ?? 0, other._words?.Length ?? 0);
            for (int i = 0; i < len; i++)
                _words[i] &= other._words[i];
            // 其余高位直接清空
            for (int i = len; i < (_words?.Length ?? 0); i++)
                _words[i] = 0;
        }
        public void OrWith(BitSet other)
        {
            if (other._words == null) return;
            EnsureCapacity(other.BitCapacity);
            int len = other._words.Length;
            for (int i = 0; i < len; i++)
                _words[i] |= other._words[i];
        }

        // this = this AND (NOT other)
        public void AndNotWith(BitSet other)
        {
            int len = Math.Min(_words?.Length ?? 0, other._words?.Length ?? 0);
            for (int i = 0; i < len; i++)
                _words[i] &= ~other._words[i];
        }

        public BitSet Clone()
        {
            var bs = new BitSet(BitCapacity);
            if (_words != null)
            {
                bs._words = (ulong[])_words.Clone();
            }
            return bs;
        }

        // 枚举所有置位 bit，返回 bitIndex
        public IEnumerable<int> EnumerateSetBits()
        {
            if (_words == null) yield break;

            for (int w = 0; w < _words.Length; w++)
            {
                ulong x = _words[w];
                while (x != 0)
                {
                    // 取最低位的 1
                    ulong lsb = x & (~x + 1);
                    int bit = BitOperations.TrailingZeroCount(x);
                    yield return (w << 6) + bit;
                    x ^= lsb;
                }
            }
        }
        internal static class BitOperations
        {
            public static int TrailingZeroCount(ulong value)
            {
                if (value == 0) return 64;
                int count = 0;
                while ((value & 1UL) == 0) { value >>= 1; count++; }
                return count;
            }
            /// <summary>
            /// 统计 uint 中二进制 1 的个数
            /// </summary>
            public static int PopCount(uint value)
            {
                value = value - ((value >> 1) & 0x55555555u);
                value = (value & 0x33333333u) + ((value >> 2) & 0x33333333u);
                value = (value + (value >> 4)) & 0x0F0F0F0Fu;
                value = value + (value >> 8);
                value = value + (value >> 16);
                return (int)(value & 0x3Fu);
            }
            /// <summary>
            /// 统计 ulong 中二进制 1 的个数
            /// </summary>
            public static int PopCount(ulong value)
            {
                value = value - ((value >> 1) & 0x5555555555555555UL);
                value = (value & 0x3333333333333333UL) + ((value >> 2) & 0x3333333333333333UL);
                value = (value + (value >> 4)) & 0x0F0F0F0F0F0F0F0FUL;
                value = value + (value >> 8);
                value = value + (value >> 16);
                value = value + (value >> 32);
                return (int)(value & 0x7FUL);
            }
        }
    }
}