using System;
using System.Collections;
using System.Collections.Generic;

namespace ZeroUI.Core.Collections
{
    /// <summary>
    /// High-performance, zero-allocation circular ring buffer.
    /// Uses power-of-two capacity for fast bitwise modulo operations.
    /// Thread-safe for concurrent read and write access.
    /// </summary>
    /// <typeparam name="T">Buffer element type.</typeparam>
    public sealed class RingBuffer<T> : IEnumerable<T>
    {
        private readonly T[] _buffer;
        private readonly int _capacity;
        private readonly int _mask;
        private readonly object _syncRoot = new object();

        private long _head; // Monotonically increasing write index
        private int _count;

        /// <summary>
        /// Gets the maximum capacity of the ring buffer (always a power of two).
        /// </summary>
        public int Capacity => _capacity;

        /// <summary>
        /// Gets the current number of valid items in the buffer.
        /// </summary>
        public int Count
        {
            get
            {
                lock (_syncRoot)
                {
                    return _count;
                }
            }
        }

        /// <summary>
        /// Gets whether the buffer is at maximum capacity.
        /// </summary>
        public bool IsFull
        {
            get
            {
                lock (_syncRoot)
                {
                    return _count == _capacity;
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of RingBuffer.
        /// </summary>
        /// <param name="capacity">Requested capacity (rounded up to nearest power of two, min 4).</param>
        public RingBuffer(int capacity = 1024)
        {
            _capacity = RoundUpToPowerOfTwo(Math.Max(4, capacity));
            _mask = _capacity - 1;
            _buffer = new T[_capacity];
        }

        /// <summary>
        /// Writes an item to the buffer. If the buffer is full, the oldest item is silently overwritten.
        /// </summary>
        public void Write(T item)
        {
            lock (_syncRoot)
            {
                int index = (int)(_head & _mask);
                _buffer[index] = item;
                _head++;
                if (_count < _capacity)
                {
                    _count++;
                }
            }
        }

        /// <summary>
        /// Writes a sequence of items in batch.
        /// </summary>
        public void WriteRange(ReadOnlySpan<T> items)
        {
            lock (_syncRoot)
            {
                for (int i = 0; i < items.Length; i++)
                {
                    int index = (int)(_head & _mask);
                    _buffer[index] = items[i];
                    _head++;
                    if (_count < _capacity)
                    {
                        _count++;
                    }
                }
            }
        }

        /// <summary>
        /// Gets the item at the specified chronological index (0 = oldest, Count - 1 = newest).
        /// </summary>
        public T GetAt(int index)
        {
            lock (_syncRoot)
            {
                if (index < 0 || index >= _count)
                    throw new ArgumentOutOfRangeException(nameof(index), "Index was out of range.");

                long physicalHead = _head;
                long oldestHead = physicalHead - _count;
                int bufferIndex = (int)((oldestHead + index) & _mask);
                return _buffer[bufferIndex];
            }
        }

        /// <summary>
        /// Gets the newest item written to the buffer, or default if empty.
        /// </summary>
        public bool TryGetLatest(out T item)
        {
            lock (_syncRoot)
            {
                if (_count == 0)
                {
                    item = default!;
                    return false;
                }

                int bufferIndex = (int)((_head - 1) & _mask);
                item = _buffer[bufferIndex];
                return true;
            }
        }

        /// <summary>
        /// Copies all elements from oldest to newest into the destination span without any heap allocations.
        /// </summary>
        /// <param name="destination">Destination span (must be at least Count in length).</param>
        /// <returns>Number of items copied.</returns>
        public int CopyTo(Span<T> destination)
        {
            lock (_syncRoot)
            {
                int toCopy = Math.Min(_count, destination.Length);
                if (toCopy == 0) return 0;

                long oldestHead = _head - _count;
                for (int i = 0; i < toCopy; i++)
                {
                    int bufferIndex = (int)((oldestHead + i) & _mask);
                    destination[i] = _buffer[bufferIndex];
                }
                return toCopy;
            }
        }

        /// <summary>
        /// Copies all elements from oldest to newest into a new array.
        /// </summary>
        public T[] ToArray()
        {
            lock (_syncRoot)
            {
                var result = new T[_count];
                long oldestHead = _head - _count;
                for (int i = 0; i < _count; i++)
                {
                    int bufferIndex = (int)((oldestHead + i) & _mask);
                    result[i] = _buffer[bufferIndex];
                }
                return result;
            }
        }

        /// <summary>
        /// Clears all elements from the buffer.
        /// </summary>
        public void Clear()
        {
            lock (_syncRoot)
            {
                Array.Clear(_buffer, 0, _capacity);
                _head = 0;
                _count = 0;
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            T[] snapshot = ToArray();
            for (int i = 0; i < snapshot.Length; i++)
            {
                yield return snapshot[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private static int RoundUpToPowerOfTwo(int v)
        {
            v--;
            v |= v >> 1;
            v |= v >> 2;
            v |= v >> 4;
            v |= v >> 8;
            v |= v >> 16;
            return v + 1;
        }
    }
}
