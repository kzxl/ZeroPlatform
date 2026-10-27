using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;

namespace ZeroPlatform.Concurrency
{
    // Base padding classes to enforce cache-line separation (64 bytes) without false sharing.
    [EditorBrowsable(EditorBrowsableState.Never)]
    public abstract class ZeroRingBufferPad0
    {
        protected long _p0, _p1, _p2, _p3, _p4, _p5, _p6;
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public abstract class ZeroRingBufferProducer<T> : ZeroRingBufferPad0
    {
        // Tail is written only by the single producer.
        protected int _tail;
        // Cached copy of head to avoid cross-core bus invalidation on every enqueue.
        protected int _cachedHead;
        protected long _p8, _p9, _p10, _p11, _p12, _p13;
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public abstract class ZeroRingBufferPad1<T> : ZeroRingBufferProducer<T>
    {
        protected long _p14, _p15, _p16, _p17, _p18, _p19, _p20;
    }

    /// <summary>
    /// Pure C# Single-Producer Single-Consumer (SPSC) lock-free ring buffer.
    /// Utilizes cache-line padding, cached head/tail indexes, and bitmask indexing
    /// to achieve sub-microsecond latency, tens of millions ops/sec, and zero GC allocations.
    /// </summary>
    /// <typeparam name="T">The type of elements in the ring buffer.</typeparam>
    public sealed class ZeroRingBuffer<T> : ZeroRingBufferPad1<T>
    {
        // Head is written only by the single consumer.
        private int _head;
        // Cached copy of tail to avoid cross-core bus invalidation on every dequeue.
        private int _cachedTail;
        private long _p22, _p23, _p24, _p25, _p26, _p27;

        private readonly T[] _buffer;
        private readonly int _mask;
        private readonly int _capacity;

        /// <summary>
        /// Initializes a new instance of the <see cref="ZeroRingBuffer{T}"/> class with a power-of-two capacity.
        /// </summary>
        /// <param name="capacityPowerOfTwo">The maximum capacity, which must be a power of two (e.g. 64, 128, 1024, 4096...).</param>
        public ZeroRingBuffer(int capacityPowerOfTwo)
        {
            if (capacityPowerOfTwo < 2 || (capacityPowerOfTwo & (capacityPowerOfTwo - 1)) != 0)
            {
                throw new ArgumentException($"Capacity ({capacityPowerOfTwo}) must be a power of two (e.g. 64, 128, 1024, 4096...).", nameof(capacityPowerOfTwo));
            }

            _capacity = capacityPowerOfTwo;
            _mask = capacityPowerOfTwo - 1;
            _buffer = new T[capacityPowerOfTwo];
            _head = 0;
            _tail = 0;
            _cachedHead = 0;
            _cachedTail = 0;
        }

        /// <summary>
        /// Gets the maximum capacity of the ring buffer.
        /// </summary>
        public int Capacity => _capacity;

        /// <summary>
        /// Gets the current number of elements in the ring buffer.
        /// </summary>
        public int Count
        {
            get
            {
                int snapTail = Volatile.Read(ref _tail);
                int snapHead = Volatile.Read(ref _head);
                int count = snapTail - snapHead;
                return count < 0 ? 0 : count;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the ring buffer is empty.
        /// </summary>
        public bool IsEmpty => Volatile.Read(ref _head) == Volatile.Read(ref _tail);

        /// <summary>
        /// Gets a value indicating whether the ring buffer is full.
        /// </summary>
        public bool IsFull => (Volatile.Read(ref _tail) - Volatile.Read(ref _head)) >= _capacity;

        /// <summary>
        /// Enqueues an item into the buffer. Must only be invoked from the single producer thread.
        /// Leverages cached consumer head to eliminate inter-core cache-line invalidation on the hot path.
        /// </summary>
        /// <param name="item">The item to enqueue.</param>
        /// <returns><c>true</c> if successfully enqueued; <c>false</c> if the buffer is full.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryEnqueue(T item)
        {
            int currentTail = _tail;

            // Fast path: check against local cached head (avoids inter-core cache line traffic)
            if ((currentTail - _cachedHead) >= _capacity)
            {
                // Slow path: update cached head from consumer's memory
                _cachedHead = Volatile.Read(ref _head);
                if ((currentTail - _cachedHead) >= _capacity)
                {
                    return false; // Buffer is genuinely full
                }
            }

            _buffer[currentTail & _mask] = item;
            // Write barrier guarantees data is committed before advancing the tail index.
            Volatile.Write(ref _tail, currentTail + 1);
            return true;
        }

        /// <summary>
        /// Dequeues an item from the buffer. Must only be invoked from the single consumer thread.
        /// Leverages cached producer tail to eliminate inter-core cache-line invalidation on the hot path.
        /// </summary>
        /// <param name="item">The dequeued item.</param>
        /// <returns><c>true</c> if an item was successfully dequeued; <c>false</c> if the buffer is empty.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryDequeue(out T item)
        {
            int currentHead = _head;

            // Fast path: check against local cached tail (difference preserves distance across wraparound)
            if ((_cachedTail - currentHead) <= 0)
            {
                // Slow path: update cached tail from producer's memory
                _cachedTail = Volatile.Read(ref _tail);
                if ((_cachedTail - currentHead) <= 0)
                {
                    item = default!;
                    return false; // Buffer is genuinely empty
                }
            }

            int index = currentHead & _mask;
            item = _buffer[index];
            _buffer[index] = default!; // Clear slot to facilitate GC collection for reference types.

            // Write barrier guarantees slot read is completed before advancing the head index.
            Volatile.Write(ref _head, currentHead + 1);
            return true;
        }

        /// <summary>
        /// Enqueues a batch of items from a <see cref="ReadOnlySpan{T}"/>.
        /// Updates the tail pointer and memory barrier only once for the entire batch.
        /// </summary>
        /// <param name="items">The items to enqueue.</param>
        /// <returns>The actual number of items enqueued.</returns>
        public int TryEnqueueBatch(ReadOnlySpan<T> items)
        {
            int count = items.Length;
            if (count == 0) return 0;

            int currentTail = _tail;
            int available = _capacity - (currentTail - _cachedHead);

            if (available < count)
            {
                _cachedHead = Volatile.Read(ref _head);
                available = _capacity - (currentTail - _cachedHead);
                if (available <= 0) return 0;
                if (count > available) count = available;
            }

            int mask = _mask;
            T[] buffer = _buffer;
            for (int i = 0; i < count; i++)
            {
                buffer[(currentTail + i) & mask] = items[i];
            }

            Volatile.Write(ref _tail, currentTail + count);
            return count;
        }

        /// <summary>
        /// Dequeues a batch of items into a destination <see cref="Span{T}"/>.
        /// Updates the head pointer and memory barrier only once for the entire batch.
        /// </summary>
        /// <param name="destination">The destination span.</param>
        /// <returns>The actual number of items dequeued.</returns>
        public int TryDequeueBatch(Span<T> destination)
        {
            int count = destination.Length;
            if (count == 0) return 0;

            int currentHead = _head;
            int available = _cachedTail - currentHead;

            if (available < count)
            {
                _cachedTail = Volatile.Read(ref _tail);
                available = _cachedTail - currentHead;
                if (available <= 0) return 0;
                if (count > available) count = available;
            }

            int mask = _mask;
            T[] buffer = _buffer;
            for (int i = 0; i < count; i++)
            {
                int index = (currentHead + i) & mask;
                destination[i] = buffer[index];
                buffer[index] = default!;
            }

            Volatile.Write(ref _head, currentHead + count);
            return count;
        }

        /// <summary>
        /// Peeks at the item at the head of the buffer without removing it. Must only be invoked from the consumer thread.
        /// </summary>
        /// <param name="item">The item at the head.</param>
        /// <returns><c>true</c> if an item was peeked; <c>false</c> if the buffer is empty.</returns>
        public bool TryPeek(out T item)
        {
            int currentHead = _head;
            if ((_cachedTail - currentHead) <= 0)
            {
                _cachedTail = Volatile.Read(ref _tail);
                if ((_cachedTail - currentHead) <= 0)
                {
                    item = default!;
                    return false;
                }
            }

            item = _buffer[currentHead & _mask];
            return true;
        }

        /// <summary>
        /// Clears all elements from the ring buffer.
        /// </summary>
        public void Clear()
        {
            while (TryDequeue(out _)) { }
        }
    }
}
