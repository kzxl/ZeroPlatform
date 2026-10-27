using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;

namespace ZeroPlatform.Concurrency
{
    // Padding class to avoid false sharing on 64-bit enqueue position
    [EditorBrowsable(EditorBrowsableState.Never)]
    public abstract class ZeroMpmc64Pad0
    {
        protected long _p0, _p1, _p2, _p3, _p4, _p5, _p6;
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public abstract class ZeroMpmc64Producer<T> : ZeroMpmc64Pad0
    {
        protected long _enqueuePos;
        protected long _p7, _p8, _p9, _p10, _p11, _p12, _p13;
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public abstract class ZeroMpmc64Pad1<T> : ZeroMpmc64Producer<T>
    {
        protected long _p14, _p15, _p16, _p17, _p18, _p19, _p20;
    }

    /// <summary>
    /// Pure C# Multi-Producer Multi-Consumer (MPMC) lock-free bounded queue using 64-bit sequence counters.
    /// Implemented using Dmitry Vyukov's bounded MPMC queue algorithm.
    /// Employs 64-bit sequence numbers to mathematically guarantee perpetual non-wrapping operation
    /// (over 5,800 years of continuous operation at 100M ops/sec).
    /// </summary>
    /// <typeparam name="T">The type of elements in the ring buffer.</typeparam>
    public sealed class ZeroMpmcRingBuffer64<T> : ZeroMpmc64Pad1<T>
    {
        private long _dequeuePos;
        private long _p21, _p22, _p23, _p24, _p25, _p26, _p27;

        private struct Cell
        {
            public long Sequence;
            public T Value;
        }

        private readonly Cell[] _buffer;
        private readonly long _mask;
        private readonly int _capacity;

        /// <summary>
        /// Initializes a new instance of the <see cref="ZeroMpmcRingBuffer64{T}"/> class with a power-of-two capacity.
        /// </summary>
        /// <param name="capacityPowerOfTwo">The maximum capacity, which must be a power of two.</param>
        public ZeroMpmcRingBuffer64(int capacityPowerOfTwo)
        {
            if (capacityPowerOfTwo < 2 || (capacityPowerOfTwo & (capacityPowerOfTwo - 1)) != 0)
            {
                throw new ArgumentException($"Capacity ({capacityPowerOfTwo}) must be a power of two.", nameof(capacityPowerOfTwo));
            }

            _capacity = capacityPowerOfTwo;
            _mask = capacityPowerOfTwo - 1;
            _buffer = new Cell[capacityPowerOfTwo];

            for (long i = 0; i < capacityPowerOfTwo; i++)
            {
                _buffer[i].Sequence = i;
            }

            _enqueuePos = 0;
            _dequeuePos = 0;
        }

        /// <summary>
        /// Gets the maximum capacity of the ring buffer.
        /// </summary>
        public int Capacity => _capacity;

        /// <summary>
        /// Gets an approximate snapshot of current item count.
        /// </summary>
        public int Count
        {
            get
            {
                long enq = Volatile.Read(ref _enqueuePos);
                long deq = Volatile.Read(ref _dequeuePos);
                long count = enq - deq;
                return count < 0 ? 0 : (count > _capacity ? _capacity : (int)count);
            }
        }

        /// <summary>
        /// Gets a value indicating whether the ring buffer is empty.
        /// </summary>
        public bool IsEmpty => Volatile.Read(ref _dequeuePos) >= Volatile.Read(ref _enqueuePos);

        /// <summary>
        /// Attempts to enqueue an item into the buffer without locking.
        /// </summary>
        /// <param name="item">The item to enqueue.</param>
        /// <returns><c>true</c> if successfully enqueued; <c>false</c> if the buffer is full.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryEnqueue(T item)
        {
            Cell[] buffer = _buffer;
            long mask = _mask;

            while (true)
            {
                long pos = Volatile.Read(ref _enqueuePos);
                long index = pos & mask;
                long seq = Volatile.Read(ref buffer[index].Sequence);
                long dif = seq - pos;

                if (dif == 0)
                {
                    // Empty cell available for writing, attempt to claim slot
                    if (Interlocked.CompareExchange(ref _enqueuePos, pos + 1, pos) == pos)
                    {
                        buffer[index].Value = item;
                        // Advance sequence to pos + 1 to signal consumer readiness
                        Volatile.Write(ref buffer[index].Sequence, pos + 1);
                        return true;
                    }
                }
                else if (dif < 0)
                {
                    // Buffer is full
                    return false;
                }
                // dif > 0: Another producer is writing, continue spin-wait
            }
        }

        /// <summary>
        /// Attempts to dequeue an item from the buffer without locking.
        /// </summary>
        /// <param name="item">The dequeued item.</param>
        /// <returns><c>true</c> if an item was successfully dequeued; <c>false</c> if the buffer is empty.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryDequeue(out T item)
        {
            Cell[] buffer = _buffer;
            long mask = _mask;

            while (true)
            {
                long pos = Volatile.Read(ref _dequeuePos);
                long index = pos & mask;
                long seq = Volatile.Read(ref buffer[index].Sequence);
                long dif = seq - (pos + 1);

                if (dif == 0)
                {
                    // Slot contains data, attempt to claim slot for reading
                    if (Interlocked.CompareExchange(ref _dequeuePos, pos + 1, pos) == pos)
                    {
                        item = buffer[index].Value;
                        buffer[index].Value = default!; // Clear slot for GC
                        // Advance sequence to pos + mask + 1 to signal slot availability for next wrap-around
                        Volatile.Write(ref buffer[index].Sequence, pos + mask + 1);
                        return true;
                    }
                }
                else if (dif < 0)
                {
                    // Buffer is empty
                    item = default!;
                    return false;
                }
                // dif > 0: Another consumer is reading, continue spin-wait
            }
        }

        /// <summary>
        /// Clears all elements currently stored in the buffer.
        /// </summary>
        public void Clear()
        {
            while (TryDequeue(out _)) { }
        }
    }
}
