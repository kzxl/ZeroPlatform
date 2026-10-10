using System;
using System.Runtime.CompilerServices;
using System.Threading;

#pragma warning disable CS0169 // Intentionally unused cache-line padding fields

namespace ZeroPrimitives.Concurrency
{
    /// <summary>
    /// High-throughput, lock-free Single-Producer Single-Consumer (SPSC) bounded FIFO queue.
    /// Incorporates CPU cache-line padding to prevent False Sharing (L1/L2 cache-line thrashing) between producer and consumer threads.
    /// Uses unsigned 32-bit (uint) modular sequence arithmetic to guarantee perpetual wrap-around safety (zero integer overflow bugs even after billions of operations).
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit, Size = 128)]
    internal struct SpscHeadSlot
    {
        [System.Runtime.InteropServices.FieldOffset(64)]
        public uint Value;
        [System.Runtime.InteropServices.FieldOffset(72)]
        public uint CachedTail;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit, Size = 128)]
    internal struct SpscTailSlot
    {
        [System.Runtime.InteropServices.FieldOffset(64)]
        public uint Value;
        [System.Runtime.InteropServices.FieldOffset(72)]
        public uint CachedHead;
    }

    public sealed class SpscQueue<T>
    {
        private readonly T[] _buffer;
        private readonly uint _mask;

        // Guaranteed physical 128-byte cache-line isolation preventing false sharing across all CLRs
        private SpscHeadSlot _head;
        private SpscTailSlot _tail;

        /// <summary>
        /// Initializes a new SPSC queue with a power-of-two capacity.
        /// </summary>
        /// <param name="capacityPowerOfTwo">Capacity (must be power of two: 16, 32, 64, 1024, 65536...).</param>
        public SpscQueue(int capacityPowerOfTwo)
        {
            if (capacityPowerOfTwo < 2) capacityPowerOfTwo = 2;

            // Round up to nearest power of two if not already
            if ((capacityPowerOfTwo & (capacityPowerOfTwo - 1)) != 0)
            {
                int p = 1;
                while (p < capacityPowerOfTwo) p <<= 1;
                capacityPowerOfTwo = p;
            }

            _buffer = new T[capacityPowerOfTwo];
            _mask = (uint)(capacityPowerOfTwo - 1);
            _head.Value = 0;
            _head.CachedTail = 0;
            _tail.Value = 0;
            _tail.CachedHead = 0;
        }

        public int Capacity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _buffer.Length;
        }

        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                uint count = Volatile.Read(ref _tail.Value) - Volatile.Read(ref _head.Value);
                return (int)count;
            }
        }

        public bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Volatile.Read(ref _head.Value) == Volatile.Read(ref _tail.Value);
        }

        /// <summary>
        /// Attempts to enqueue an item. MUST be called by only ONE producer thread.
        /// Zero allocation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryEnqueue(T item)
        {
            uint tail = _tail.Value;
            if ((tail - _tail.CachedHead) >= (uint)_buffer.Length)
            {
                _tail.CachedHead = Volatile.Read(ref _head.Value);
                if ((tail - _tail.CachedHead) >= (uint)_buffer.Length)
                {
                    return false;
                }
            }

            _buffer[tail & _mask] = item;
            Volatile.Write(ref _tail.Value, tail + 1);
            return true;
        }

        /// <summary>
        /// Attempts to dequeue an item. MUST be called by only ONE consumer thread.
        /// Zero allocation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryDequeue(out T item)
        {
            uint head = _head.Value;
            if (head == _head.CachedTail)
            {
                _head.CachedTail = Volatile.Read(ref _tail.Value);
                if (head == _head.CachedTail)
                {
                    item = default!;
                    return false;
                }
            }

            uint index = head & _mask;
            item = _buffer[index];
            _buffer[index] = default!; // Allow GC collection of reference types
            Volatile.Write(ref _head.Value, head + 1);
            return true;
        }

        /// <summary>
        /// Peeks at the item at the head of the queue without removing it.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryPeek(out T item)
        {
            uint head = _head.Value;
            uint tail = Volatile.Read(ref _tail.Value);

            if (head != tail)
            {
                item = _buffer[head & _mask];
                return true;
            }

            item = default!;
            return false;
        }
    }
}

