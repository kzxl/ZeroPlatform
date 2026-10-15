using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;

namespace ZeroPlatform.Concurrency
{
    // Padding class to avoid false sharing on enqueue position
    [EditorBrowsable(EditorBrowsableState.Never)]
    public abstract class ZeroMpmcPad0
    {
        protected long _p0, _p1, _p2, _p3, _p4, _p5, _p6;
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public abstract class ZeroMpmcProducer<T> : ZeroMpmcPad0
    {
        protected int _enqueuePos;
        protected long _p7, _p8, _p9, _p10, _p11, _p12, _p13;
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public abstract class ZeroMpmcPad1<T> : ZeroMpmcProducer<T>
    {
        protected long _p14, _p15, _p16, _p17, _p18, _p19, _p20;
    }

    /// <summary>
    /// Pure C# Multi-Producer Multi-Consumer (MPMC) lock-free bounded queue.
    /// Implemented using Dmitry Vyukov's bounded MPMC queue algorithm with monotonic sequence barriers.
    /// Enables concurrent enqueues and dequeues across multiple threads with zero GC allocations.
    /// </summary>
    /// <typeparam name="T">The type of elements in the ring buffer.</typeparam>
    public sealed class ZeroMpmcRingBuffer<T> : ZeroMpmcPad1<T>
    {
        private int _dequeuePos;
        private long _p21, _p22, _p23, _p24, _p25, _p26, _p27;

        private struct Cell
        {
            public int Sequence;
            public T Value;
        }

        private readonly Cell[] _buffer;
        private readonly int _mask;
        private readonly int _capacity;

        /// <summary>
        /// Initializes a new instance of the <see cref="ZeroMpmcRingBuffer{T}"/> class with a power-of-two capacity.
        /// </summary>
        /// <param name="capacityPowerOfTwo">The maximum capacity, which must be a power of two.</param>
        public ZeroMpmcRingBuffer(int capacityPowerOfTwo)
        {
            if (capacityPowerOfTwo < 2 || (capacityPowerOfTwo & (capacityPowerOfTwo - 1)) != 0)
            {
                throw new ArgumentException($"Capacity ({capacityPowerOfTwo}) must be a power of two.", nameof(capacityPowerOfTwo));
            }

            _capacity = capacityPowerOfTwo;
            _mask = capacityPowerOfTwo - 1;
            _buffer = new Cell[capacityPowerOfTwo];

            for (int i = 0; i < capacityPowerOfTwo; i++)
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
                int enq = Volatile.Read(ref _enqueuePos);
                int deq = Volatile.Read(ref _dequeuePos);
                int count = enq - deq;
                return count < 0 ? 0 : (count > _capacity ? _capacity : count);
            }
        }

        /// <summary>
        /// Gets a value indicating whether the ring buffer is empty.
        /// </summary>
        public bool IsEmpty => (Volatile.Read(ref _enqueuePos) - Volatile.Read(ref _dequeuePos)) <= 0;

        /// <summary>
        /// Attempts to enqueue an item into the buffer without locking.
        /// </summary>
        /// <param name="item">The item to enqueue.</param>
        /// <returns><c>true</c> if successfully enqueued; <c>false</c> if the buffer is full.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryEnqueue(T item)
        {
            Cell[] buffer = _buffer;
            int mask = _mask;

            while (true)
            {
                int pos = Volatile.Read(ref _enqueuePos);
                int index = pos & mask;
                int seq = Volatile.Read(ref buffer[index].Sequence);
                int dif = seq - pos;

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
            int mask = _mask;

            while (true)
            {
                int pos = Volatile.Read(ref _dequeuePos);
                int index = pos & mask;
                int seq = Volatile.Read(ref buffer[index].Sequence);
                int dif = seq - (pos + 1);

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
