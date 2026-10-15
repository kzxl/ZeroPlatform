using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using ZeroPrimitives.Buffers;

namespace ZeroPrimitives.Memory
{
    /// <summary>
    /// Thread-safe, multi-bucket off-heap memory pool for zero-allocation native buffers.
    /// Manages power-of-two size buckets ranging from 4KB up to 64MB.
    /// Completely bypasses the .NET Garbage Collector (GC), preventing Gen-2 heap fragmentation and LOH contention.
    /// </summary>
    public sealed unsafe class NativeMemoryPool : IDisposable
    {
        public const int MinBucketShift = 12; // 2^12 = 4096 bytes (4KB)
        public const int MaxBucketShift = 26; // 2^26 = 67,108,864 bytes (64MB)
        public const int MinBucketSize = 1 << MinBucketShift; // 4096
        public const int MaxBucketSize = 1 << MaxBucketShift; // 64MB
        public const int NumBuckets = MaxBucketShift - MinBucketShift + 1; // 15 buckets

        private static readonly NativeMemoryPool _shared = new NativeMemoryPool(maxPerBucket: 32);

        /// <summary>
        /// Gets the system-wide shared instance of <see cref="NativeMemoryPool"/>.
        /// </summary>
        public static NativeMemoryPool Shared => _shared;

        private readonly ZeroPoolStack<IntPtr>[] _buckets;
        private readonly ZeroPoolStack<NativeMemoryBlock>[] _blockShells;
        private readonly int[] _bucketSizes;
        private readonly int _maxPerBucket;
        private int _disposed;

        /// <summary>
        /// Gets whether this memory pool has been disposed.
        /// </summary>
        public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

        /// <summary>
        /// Initializes a new instance of <see cref="NativeMemoryPool"/>.
        /// </summary>
        /// <param name="maxPerBucket">Maximum number of pooled blocks retained per size bucket before freeing excess.</param>
        public NativeMemoryPool(int maxPerBucket = 32)
        {
            if (maxPerBucket <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxPerBucket), "Max per bucket must be positive.");

            _maxPerBucket = maxPerBucket;
            _buckets = new ZeroPoolStack<IntPtr>[NumBuckets];
            _blockShells = new ZeroPoolStack<NativeMemoryBlock>[NumBuckets];
            _bucketSizes = new int[NumBuckets];

            for (int i = 0; i < NumBuckets; i++)
            {
                _buckets[i] = new ZeroPoolStack<IntPtr>(maxPerBucket, canGrow: false, maxCapacity: maxPerBucket);
                _blockShells[i] = new ZeroPoolStack<NativeMemoryBlock>(maxPerBucket, canGrow: false, maxCapacity: maxPerBucket);
                _bucketSizes[i] = 1 << (MinBucketShift + i);
            }
        }

        /// <summary>
        /// Selects the appropriate bucket index for the requested minimum size.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SelectBucketIndex(int minimumBytes)
        {
            if (minimumBytes <= MinBucketSize)
                return 0;

            if (minimumBytes >= MaxBucketSize)
                return NumBuckets - 1;

            int v = minimumBytes - 1;
            v |= v >> 1;
            v |= v >> 2;
            v |= v >> 4;
            v |= v >> 8;
            v |= v >> 16;
            v++;

            int log2 = 31 - BitOps.LeadingZeroCount((uint)v);
            int index = log2 - MinBucketShift;
            return index < NumBuckets ? index : NumBuckets - 1;
        }

        /// <summary>
        /// Gets the capacity in bytes of a specific bucket index.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetBucketCapacity(int bucketIndex)
        {
            if ((uint)bucketIndex >= (uint)NumBuckets)
                throw new ArgumentOutOfRangeException(nameof(bucketIndex));
            return _bucketSizes[bucketIndex];
        }

        /// <summary>
        /// Gets the count of currently pooled blocks available for reuse in a given bucket.
        /// </summary>
        public int GetAvailableCount(int bucketIndex)
        {
            if ((uint)bucketIndex >= (uint)NumBuckets)
                throw new ArgumentOutOfRangeException(nameof(bucketIndex));
            return _buckets[bucketIndex].Count;
        }

        /// <summary>
        /// Rents a zero-allocation value-type <see cref="NativeMemoryLease"/> with at least the specified minimum byte capacity.
        /// When disposed, the unmanaged memory returns directly to this pool with zero GC heap allocations.
        /// </summary>
        /// <param name="minSize">Minimum required payload size in bytes.</param>
        /// <returns>A value-type <see cref="NativeMemoryLease"/> wrapping the unmanaged buffer.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NativeMemoryLease RentLease(int minSize)
        {
            ThrowIfDisposed();

            if (minSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(minSize), "Minimum bytes must be positive.");

            if (minSize > MaxBucketSize)
            {
                NativeMemoryTracker.TrackAlloc(minSize);
                IntPtr unpooledPtr = AllocateRaw(minSize);
                return new NativeMemoryLease(this, unpooledPtr, minSize, minSize);
            }

            int bucketIndex = SelectBucketIndex(minSize);
            int capacity = _bucketSizes[bucketIndex];

            if (!_buckets[bucketIndex].TryPop(out IntPtr ptr))
            {
                NativeMemoryTracker.TrackAlloc(capacity);
                ptr = AllocateRaw(capacity);
            }

            return new NativeMemoryLease(this, ptr, minSize, capacity);
        }

        /// <summary>
        /// Rents an off-heap memory block with at least the specified minimum byte capacity.
        /// When disposed, the block automatically returns to this pool.
        /// </summary>
        /// <param name="minimumBytes">Minimum required payload size.</param>
        /// <returns>A recycled or freshly allocated <see cref="NativeMemoryBlock"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NativeMemoryBlock Rent(int minimumBytes)
        {
            ThrowIfDisposed();

            if (minimumBytes <= 0)
                throw new ArgumentOutOfRangeException(nameof(minimumBytes), "Minimum bytes must be positive.");

            // For ultra-large allocations exceeding MaxBucketSize, allocate standalone unpooled
            if (minimumBytes > MaxBucketSize)
            {
                NativeMemoryTracker.TrackAlloc(minimumBytes);
                return NativeMemoryBlock.Allocate(minimumBytes);
            }

            int bucketIndex = SelectBucketIndex(minimumBytes);
            int capacity = _bucketSizes[bucketIndex];

            if (!_buckets[bucketIndex].TryPop(out IntPtr ptr))
            {
                NativeMemoryTracker.TrackAlloc(capacity);
                ptr = AllocateRaw(capacity);
            }

            if (_blockShells[bucketIndex].TryPop(out var block))
            {
                block.Reset((byte*)ptr, minimumBytes);
                return block;
            }

            return new NativeMemoryBlock(
                pointer: (byte*)ptr,
                capacity: capacity,
                length: minimumBytes,
                recycleCallback: ReturnBlock,
                ownsMemory: false
            );
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ReturnLease(IntPtr pointer, int capacity)
        {
            if (pointer == IntPtr.Zero)
                return;

            if (IsDisposed || capacity > MaxBucketSize)
            {
                FreeRaw(pointer, capacity);
                return;
            }

            int bucketIndex = SelectBucketIndex(capacity);
            if (!_buckets[bucketIndex].TryPush(pointer))
            {
                FreeRaw(pointer, capacity);
            }
        }

        private void ReturnBlock(NativeMemoryBlock block)
        {
            byte* rawPtr = block.DetachPointer();
            int capacity = block.Capacity;

            if (rawPtr != null)
            {
                ReturnLease((IntPtr)rawPtr, capacity);
            }

            if (!IsDisposed && capacity <= MaxBucketSize)
            {
                int bucketIndex = SelectBucketIndex(capacity);
                _blockShells[bucketIndex].TryPush(block);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static IntPtr AllocateRaw(int capacity)
        {
#if NET8_0_OR_GREATER
            return (IntPtr)NativeMemory.Alloc((nuint)capacity);
#else
            return Marshal.AllocHGlobal(capacity);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void FreeRaw(IntPtr pointer, int capacity)
        {
            if (pointer == IntPtr.Zero)
                return;

            NativeMemoryTracker.TrackFree(capacity);
#if NET8_0_OR_GREATER
            NativeMemory.Free((void*)pointer);
#else
            Marshal.FreeHGlobal(pointer);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ThrowIfDisposed()
        {
            if (IsDisposed)
                throw new ObjectDisposedException(nameof(NativeMemoryPool));
        }

        /// <summary>
        /// Disposes all pooled unmanaged memory blocks and frees OS native resources.
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            for (int i = 0; i < NumBuckets; i++)
            {
                int capacity = _bucketSizes[i];
                while (_buckets[i].TryPop(out IntPtr ptr))
                {
                    FreeRaw(ptr, capacity);
                }
                _blockShells[i].Clear();
            }
        }
    }
}
