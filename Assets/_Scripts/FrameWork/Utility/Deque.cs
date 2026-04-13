using System;
using System.Collections;
using System.Collections.Generic;
namespace Utility
{
    public sealed class Deque<T> : IEnumerable<T>
    {
        private T[] _buffer;
        private int _head;
        private int _count;
        public int Count => _count;
        public int Capacity => _buffer.Length;
        public bool IsEmpty => _count == 0;
        public Deque(int capacity = 8)
        {
            if (capacity < 1) capacity = 1;
            // 保持 2 的幂有利于取模优化
            capacity = NextPowerOfTwo(capacity);
            _buffer = new T[capacity];
            _head = 0;
            _count = 0;
        }
        public T this[int index]
        {
            get
            {
                if (index > _count)
                    throw new ArgumentOutOfRangeException(nameof(index));
                return _buffer[Index(index)];
            }
            set
            {
                if (index > _count)
                    throw new ArgumentOutOfRangeException(nameof(index));
                _buffer[Index(index)] = value;
            }
        }
        public void PushHead(T value)
        {
            EnsureCapacity(_count + 1);
            _head = Dec(_head);
            _buffer[_head] = value;
            _count++;
        }
        public void PushTail(T value)
        {
            EnsureCapacity(_count + 1);
            int index = Index(_count);
            _buffer[index] = value;
            _count++;
        }
        public T PopHead()
        {
            if (_count == 0)
                return default;
            T value = _buffer[_head];
            _buffer[_head] = default;
            _head = Inc(_head);
            _count--;
            return value;
        }
        public T PopTail()
        {
            if (_count == 0)
                return default;
            int index = Index(--_count);
            T value = _buffer[index];
            _buffer[index] = default;
            return value;
        }
        public bool TryPopHead(out T item)
        {
            if (_count == 0) { item = default; return false; }
            item = PopHead();
            return true;
        }
        public bool TryPopTail(out T item)
        {
            if (_count == 0) { item = default; return false; }
            item = PopTail();
            return true;
        }
        public T PeekHead() => _count == 0 ? default : _buffer[_head];
        public T PeekTail() => _count == 0 ? default : _buffer[Index(_count - 1)];
        public bool TryPeekHead(out T item)
        {
            if (_count == 0) { item = default; return false; }
            item = _buffer[_head];
            return true;
        }
        public bool TryPeekTail(out T item)
        {
            if (_count == 0) { item = default; return false; }
            item = _buffer[Index(_count - 1)];
            return true;
        }
        public void Clear()
        {
            if (_count == 0) return;
            // 仅在引用类型时清理可减少开销；但泛型无法直接判断，统一清理更安全
            for (int i = 0; i < _count; i++)
                _buffer[Index(i)] = default;
            _head = 0;
            _count = 0;
        }
        public T[] ToArray()
        {
            var arr = new T[_count];
            for (int i = 0; i < _count; i++)
                arr[i] = _buffer[Index(i)];
            return arr;
        }
        private int Inc(int num) => (num + 1) & (Capacity - 1);
        private int Dec(int num) => (num - 1) & (Capacity - 1);
        private int Index(int offset)
        {
            // offset 相对 head 的偏移
            int idx = _head + offset;
            // capacity 是 2 的幂，可以用 mask 替代取模
            return idx & (_buffer.Length - 1);
        }
        private void EnsureCapacity(int count)
        {
            if (count > _buffer.Length)
            {
                int index = NextPowerOfTwo(count);
                T[] newList = new T[index];
                for (int i = 0; i < _count; i++)
                    newList[i] = _buffer[Index(i)];
                _head = 0;
                _buffer = newList;
            }
        }
        private static int NextPowerOfTwo(int num)
        {
            if (num < 2)
                return 2;
            num--;
            num |= num >> 1;
            num |= num >> 2;
            num |= num >> 4;
            num |= num >> 8;
            num |= num >> 16;
            return ++num;
        }
        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; i < _count; i++)
                yield return _buffer[Index(i)];
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
