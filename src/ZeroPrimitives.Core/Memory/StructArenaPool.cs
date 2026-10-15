using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace ZeroPrimitives.Memory
{
    /// <summary>
    /// High-performance polymorphic struct arena pool.
    /// Provides zero-allocation, off-heap pooling for heterogeneous unmanaged structs
    /// (e.g., telemetry events, spatial 3D points, sensor readings, task descriptors)
    /// completely eliminating CLR object boxing and GC pressure.
    /// </summary>
    public sealed unsafe class StructArenaPool : IDisposable
    {
        private static readonly int[] DefaultBucketSizes = { 32, 64, 128, 256, 512, 1024 };

        private readonly int[] _bucketSizes;
        private readonly ZeroPoolStack<IntPtr>[] _freeSlots;
        private readonly List<IntPtr> _rawAllocations = new();
        private readonly int _slotsPerBlock;
        private readonly object _growLock = new();
        private int _disposed;

        private static readonly Lazy<StructArenaPool> _shared = new(() => new StructArenaPool(slotsPerBlock: 128));

        /// <summary>
        /// Gets the system-wide shared instance of <see cref="StructArenaPool"/>.
        /// </summary>
        public static StructArenaPool Shared => _shared.Value;

        /// <summary>
        /// Initializes a new instance of <see cref="StructArenaPool"/> with specified slot counts.
        /// </summary>
        /// <param name="slotsPerBlock">Number of slots pre-allocated per bucket block.</param>
        public StructArenaPool(int slotsPerBlock = 128)
            : this(DefaultBucketSizes, slotsPerBlock)
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="StructArenaPool"/> with custom bucket sizes.
        /// </summary>
        public StructArenaPool(int[] bucketSizes, int slotsPerBlock = 128)
        {
            if (bucketSizes == null || bucketSizes.Length == 0)
                throw new ArgumentException("Bucket sizes must not be empty.", nameof(bucketSizes));
            if (slotsPerBlock <= 0)
                throw new ArgumentOutOfRangeException(nameof(slotsPerBlock));

            _bucketSizes = (int[])bucketSizes.Clone();
            Array.Sort(_bucketSizes);

            _slotsPerBlock = slotsPerBlock;
            _freeSlots = new ZeroPoolStack<IntPtr>[_bucketSizes.Length];

            for (int i = 0; i < _bucketSizes.Length; i++)
            {
                _freeSlots[i] = new ZeroPoolStack<IntPtr>(slotsPerBlock * 2, canGrow: true);
                AllocateBlock(i);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetBucketIndex(int byteSize)
        {
            for (int i = 0; i < _bucketSizes.Length; i++)
            {
                if (byteSize <= _bucketSizes[i])
                    return i;
            }
            return -1;
        }

        private void AllocateBlock(int bucketIndex)
        {
            int slotSize = _bucketSizes[bucketIndex];
            int totalBytes = slotSize * _slotsPerBlock;

            IntPtr raw = Marshal.AllocHGlobal(totalBytes + 64);
            long rawAddr = raw.ToInt64();
            long alignedAddr = (rawAddr + 63) & ~63L;
            IntPtr aligned = new IntPtr(alignedAddr);

            lock (_growLock)
            {
                _rawAllocations.Add(raw);
            }

            for (int s = 0; s < _slotsPerBlock; s++)
            {
                IntPtr slotPtr = new IntPtr(alignedAddr + s * slotSize);
                _freeSlots[bucketIndex].Push(slotPtr);
            }
        }

        /// <summary>
        /// Rents a scoped lease for an unmanaged struct of type <typeparamref name="T"/>.
        /// Use within a 'using' statement for automatic recycling with zero GC allocation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StructLease<T> Rent<T>() where T : unmanaged
        {
            int size = sizeof(T);
            int bucketIdx = GetBucketIndex(size);
            if (bucketIdx < 0)
            {
                throw new ArgumentException($"Struct size {size} bytes exceeds maximum bucket capacity {_bucketSizes[_bucketSizes.Length - 1]} bytes.");
            }

            if (!_freeSlots[bucketIdx].TryPop(out IntPtr ptr))
            {
                lock (_growLock)
                {
                    AllocateBlock(bucketIdx);
                }
                if (!_freeSlots[bucketIdx].TryPop(out ptr))
                {
                    throw new OutOfMemoryException("Failed to acquire memory slot from struct arena.");
                }
            }

            // Zero-initialize the rented struct memory
            Unsafe.InitBlockUnaligned((void*)ptr, 0, (uint)size);
            return new StructLease<T>(this, ptr, bucketIdx);
        }

        /// <summary>
        /// Rents a non-ref pooled reference for type <typeparamref name="T"/> that can be stored in fields or collections.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public PooledStructRef<T> RentRef<T>() where T : unmanaged
        {
            int size = sizeof(T);
            int bucketIdx = GetBucketIndex(size);
            if (bucketIdx < 0)
            {
                throw new ArgumentException($"Struct size {size} bytes exceeds maximum bucket capacity {_bucketSizes[_bucketSizes.Length - 1]} bytes.");
            }

            if (!_freeSlots[bucketIdx].TryPop(out IntPtr ptr))
            {
                lock (_growLock)
                {
                    AllocateBlock(bucketIdx);
                }
                if (!_freeSlots[bucketIdx].TryPop(out ptr))
                {
                    throw new OutOfMemoryException("Failed to acquire memory slot from struct arena.");
                }
            }

            Unsafe.InitBlockUnaligned((void*)ptr, 0, (uint)size);
            return new PooledStructRef<T>(this, ptr, bucketIdx);
        }

        /// <summary>
        /// Rents a type-erased polymorphic envelope for arbitrary unmanaged payloads.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StructEnvelope RentEnvelope(int typeId, int byteSize)
        {
            if (byteSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(byteSize));

            int bucketIdx = GetBucketIndex(byteSize);
            if (bucketIdx < 0)
            {
                throw new ArgumentException($"Requested size {byteSize} bytes exceeds maximum bucket capacity {_bucketSizes[_bucketSizes.Length - 1]} bytes.");
            }

            if (!_freeSlots[bucketIdx].TryPop(out IntPtr ptr))
            {
                lock (_growLock)
                {
                    AllocateBlock(bucketIdx);
                }
                if (!_freeSlots[bucketIdx].TryPop(out ptr))
                {
                    throw new OutOfMemoryException("Failed to acquire memory slot from struct arena.");
                }
            }

            Unsafe.InitBlockUnaligned((void*)ptr, 0, (uint)byteSize);
            return new StructEnvelope(this, ptr, bucketIdx, typeId, byteSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Return(IntPtr ptr, int bucketIndex)
        {
            if (Volatile.Read(ref _disposed) != 0 || ptr == IntPtr.Zero)
                return;

            _freeSlots[bucketIndex].Push(ptr);
        }

        /// <summary>
        /// Disposes the arena pool and releases all underlying unmanaged blocks.
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            lock (_growLock)
            {
                for (int i = 0; i < _freeSlots.Length; i++)
                {
                    _freeSlots[i].Clear();
                }

                foreach (IntPtr raw in _rawAllocations)
                {
                    Marshal.FreeHGlobal(raw);
                }
                _rawAllocations.Clear();
            }
        }
    }

    /// <summary>
    /// Scoped, stack-only lease over pooled struct memory. Zero GC allocations, zero boxing.
    /// </summary>
    public readonly unsafe ref struct StructLease<T> where T : unmanaged
    {
        private readonly StructArenaPool _pool;
        private readonly IntPtr _ptr;
        private readonly int _bucketIndex;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal StructLease(StructArenaPool pool, IntPtr ptr, int bucketIndex)
        {
            _pool = pool;
            _ptr = ptr;
            _bucketIndex = bucketIndex;
        }

        /// <summary>
        /// Gets a direct unmanaged reference to the pooled struct.
        /// </summary>
        public ref T Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref Unsafe.AsRef<T>((void*)_ptr);
        }

        /// <summary>
        /// Gets a Span over the struct's raw memory bytes.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<byte> AsBytes() => new Span<byte>((void*)_ptr, sizeof(T));

        /// <summary>
        /// Returns the struct slot back to the arena pool.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            _pool.Return(_ptr, _bucketIndex);
        }
    }

    /// <summary>
    /// Heap-storable pooled reference for an unmanaged struct.
    /// </summary>
    public readonly unsafe struct PooledStructRef<T> : IDisposable, IEquatable<PooledStructRef<T>> where T : unmanaged
    {
        private readonly StructArenaPool _pool;
        private readonly IntPtr _ptr;
        private readonly int _bucketIndex;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal PooledStructRef(StructArenaPool pool, IntPtr ptr, int bucketIndex)
        {
            _pool = pool;
            _ptr = ptr;
            _bucketIndex = bucketIndex;
        }

        /// <summary>
        /// Gets a direct reference to the unmanaged struct.
        /// </summary>
        public ref T Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref Unsafe.AsRef<T>((void*)_ptr);
        }

        /// <summary>
        /// Gets raw pointer address.
        /// </summary>
        public IntPtr Pointer => _ptr;

        /// <summary>
        /// Recycles this struct memory slot back to the arena pool.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            _pool.Return(_ptr, _bucketIndex);
        }

        public bool Equals(PooledStructRef<T> other) => _ptr == other._ptr;
        public override bool Equals(object? obj) => obj is PooledStructRef<T> other && Equals(other);
        public override int GetHashCode() => _ptr.GetHashCode();
        public static bool operator ==(PooledStructRef<T> left, PooledStructRef<T> right) => left.Equals(right);
        public static bool operator !=(PooledStructRef<T> left, PooledStructRef<T> right) => !left.Equals(right);
    }

    /// <summary>
    /// Type-erased polymorphic envelope holding an unmanaged struct payload with type tagging.
    /// Eliminates boxing for heterogeneous event queues, actor mailboxes, and DSP pipelines.
    /// </summary>
    public readonly unsafe struct StructEnvelope : IDisposable
    {
        private readonly StructArenaPool _pool;
        private readonly IntPtr _ptr;
        private readonly int _bucketIndex;

        /// <summary>
        /// Gets the caller-defined discriminator / type ID.
        /// </summary>
        public int TypeId { get; }

        /// <summary>
        /// Gets the payload byte length.
        /// </summary>
        public int ByteSize { get; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal StructEnvelope(StructArenaPool pool, IntPtr ptr, int bucketIndex, int typeId, int byteSize)
        {
            _pool = pool;
            _ptr = ptr;
            _bucketIndex = bucketIndex;
            TypeId = typeId;
            ByteSize = byteSize;
        }

        /// <summary>
        /// Reinterprets the payload as a strongly-typed unmanaged struct reference.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T As<T>() where T : unmanaged
        {
            if (sizeof(T) > ByteSize)
                throw new InvalidCastException($"Requested type {typeof(T).Name} ({sizeof(T)} B) exceeds envelope payload ({ByteSize} B).");

            return ref Unsafe.AsRef<T>((void*)_ptr);
        }

        /// <summary>
        /// Returns the envelope slot back to the arena pool.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            _pool.Return(_ptr, _bucketIndex);
        }
    }
}
