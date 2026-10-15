using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace ZeroPrimitives.Memory
{
    /// <summary>
    /// Zero-allocation, thread-safe LIFO pool stack backed by a pre-allocated contiguous array
    /// and a user-mode atomic spinlock. Eliminates the 24-byte GC heap Node allocation incurred
    /// by <see cref="System.Collections.Concurrent.ConcurrentStack{T}"/> on every push operation.
    /// </summary>
    /// <typeparam name="T">The pooled element type.</typeparam>
    internal sealed class ZeroPoolStack<T>
    {
        private T[] _items;
        private int _head;
        private int _lock;
        private readonly bool _canGrow;
        private readonly int _maxCapacity;

        public ZeroPoolStack(int initialCapacity, bool canGrow = true, int maxCapacity = int.MaxValue)
        {
            if (initialCapacity <= 0)
                initialCapacity = 16;

            _items = new T[initialCapacity];
            _head = 0;
            _lock = 0;
            _canGrow = canGrow;
            _maxCapacity = maxCapacity;
        }

        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Volatile.Read(ref _head);
        }

        public bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Volatile.Read(ref _head) == 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryPush(T item)
        {
            if (Interlocked.CompareExchange(ref _lock, 1, 0) != 0)
            {
                EnterLockSlow();
            }

            int head = _head;
            if (head >= _maxCapacity)
            {
                Volatile.Write(ref _lock, 0);
                return false;
            }

            T[] items = _items;
            if ((uint)head >= (uint)items.Length)
            {
                if (!_canGrow)
                {
                    Volatile.Write(ref _lock, 0);
                    return false;
                }

                items = Grow(items);
            }

            items[head] = item;
            _head = head + 1;
            Volatile.Write(ref _lock, 0);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Push(T item)
        {
            if (Interlocked.CompareExchange(ref _lock, 1, 0) != 0)
            {
                EnterLockSlow();
            }

            int head = _head;
            T[] items = _items;
            if ((uint)head >= (uint)items.Length)
            {
                items = Grow(items);
            }

            items[head] = item;
            _head = head + 1;
            Volatile.Write(ref _lock, 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryPop(out T item)
        {
            if (Volatile.Read(ref _head) == 0)
            {
                item = default!;
                return false;
            }

            if (Interlocked.CompareExchange(ref _lock, 1, 0) != 0)
            {
                EnterLockSlow();
            }

            int head = _head - 1;
            if (head >= 0)
            {
                T[] items = _items;
                item = items[head];
                items[head] = default!;
                _head = head;
                Volatile.Write(ref _lock, 0);
                return true;
            }

            Volatile.Write(ref _lock, 0);
            item = default!;
            return false;
        }

        public void Clear()
        {
            if (Interlocked.CompareExchange(ref _lock, 1, 0) != 0)
            {
                EnterLockSlow();
            }

            if (_head > 0)
            {
                Array.Clear(_items, 0, _head);
                _head = 0;
            }

            Volatile.Write(ref _lock, 0);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private T[] Grow(T[] current)
        {
            int newCapacity = current.Length * 2;
            if (newCapacity > _maxCapacity)
            {
                newCapacity = _maxCapacity;
            }
            if (newCapacity <= current.Length)
            {
                newCapacity = current.Length + 16;
            }

            var next = new T[newCapacity];
            Array.Copy(current, 0, next, 0, current.Length);
            _items = next;
            return next;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void EnterLockSlow()
        {
            var spinner = new SpinWait();
            do
            {
                spinner.SpinOnce();
            }
            while (Interlocked.CompareExchange(ref _lock, 1, 0) != 0);
        }
    }
}
