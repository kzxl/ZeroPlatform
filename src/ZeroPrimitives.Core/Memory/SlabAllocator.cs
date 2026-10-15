using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace ZeroPrimitives.Memory
{
    /// <summary>
    /// Represents a high-throughput, off-heap unmanaged memory allocator engineered for fixed-size blocks
    /// (e.g., camera 4K video frames, LiDAR point clouds, neural tensor inputs).
    /// Eliminates OS heap allocation overhead and prevents Garbage Collector LOH fragmentation.
    /// </summary>
    public sealed unsafe class SlabAllocator : IDisposable
    {
        private readonly int _blockSize;
        private readonly int _alignedBlockSize;
        private readonly int _blocksPerSlab;
        private readonly int _alignment;
        private readonly bool _canGrow;
        private readonly ZeroPoolStack<IntPtr> _freePointers;
        private readonly List<IntPtr> _rawSlabPointers = new();
        private readonly List<int> _rawSlabSizes = new();
        private readonly object _growLock = new();

        private int _totalBlocks;
        private int _activeBlocks;
        private int _disposed;

        /// <summary>
        /// Gets the usable payload size in bytes of each block.
        /// </summary>
        public int BlockSize => _blockSize;

        /// <summary>
        /// Legacy alias for <see cref="BlockSize"/>.
        /// </summary>
        public int SlabSize => _blockSize;

        /// <summary>
        /// Gets the alignment boundary in bytes (e.g., 32 for AVX2, 64 for AVX-512).
        /// </summary>
        public int Alignment => _alignment;

        /// <summary>
        /// Gets the number of blocks contained in each allocated unmanaged slab.
        /// </summary>
        public int BlocksPerSlab => _blocksPerSlab;

        /// <summary>
        /// Gets the total number of blocks managed across all slabs.
        /// </summary>
        public int TotalBlocks => Volatile.Read(ref _totalBlocks);

        /// <summary>
        /// Legacy alias for <see cref="TotalBlocks"/>.
        /// </summary>
        public int AllocatedSlabs => Volatile.Read(ref _totalBlocks);

        /// <summary>
        /// Gets the count of currently rented blocks.
        /// </summary>
        public int ActiveBlocks => Volatile.Read(ref _activeBlocks);

        /// <summary>
        /// Gets the count of available blocks ready for immediate leasing.
        /// </summary>
        public int AvailableBlocks => _freePointers.Count;

        /// <summary>
        /// Legacy alias for <see cref="AvailableBlocks"/>.
        /// </summary>
        public int AvailableSlabs => _freePointers.Count;

        /// <summary>
        /// Gets whether this allocator has been disposed.
        /// </summary>
        public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="SlabAllocator"/> with pre-allocated unmanaged slabs.
        /// </summary>
        /// <param name="slabSize">The size in bytes of each individual block.</param>
        /// <param name="initialSlabs">The initial number of blocks to allocate off-heap.</param>
        /// <param name="maxSlabs">The maximum number of blocks that can be allocated off-heap.</param>
        /// <param name="alignment">Byte alignment boundary (defaults to 64 bytes for cache line and AVX-512).</param>
        public SlabAllocator(int slabSize, int initialSlabs = 16, int maxSlabs = 1024, int alignment = 64)
        {
            if (slabSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(slabSize), "Slab size must be positive.");
            if (initialSlabs < 0)
                throw new ArgumentOutOfRangeException(nameof(initialSlabs), "Initial slabs cannot be negative.");
            if (maxSlabs < initialSlabs)
                throw new ArgumentOutOfRangeException(nameof(maxSlabs), "Max slabs cannot be less than initial slabs.");
            if (alignment < 8 || (alignment & (alignment - 1)) != 0)
                throw new ArgumentException("Alignment must be a power of two and at least 8 bytes.", nameof(alignment));

            _blockSize = slabSize;
            _alignment = alignment;
            _blocksPerSlab = Math.Max(1, Math.Min(initialSlabs > 0 ? initialSlabs : 16, maxSlabs));
            _canGrow = maxSlabs > initialSlabs;
            _freePointers = new ZeroPoolStack<IntPtr>(Math.Max(16, maxSlabs), canGrow: true);

            // Compute block size rounded up to the required alignment boundary
            _alignedBlockSize = (slabSize + (alignment - 1)) & ~(alignment - 1);

            if (initialSlabs > 0)
            {
                AllocateNewSlab(initialSlabs);
            }
        }

        /// <summary>
        /// Rents a fixed-size unmanaged memory block wrapped in a <see cref="NativeMemoryBlock"/>.
        /// Disposing the returned block will automatically return its pointer back to this slab allocator.
        /// </summary>
        /// <param name="length">Optional active length. Defaults to full slab capacity if 0.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NativeMemoryBlock Rent(int length = 0)
        {
            if (length > _blockSize)
                throw new ArgumentOutOfRangeException(nameof(length), "Length cannot exceed slab capacity.");

            byte* ptr = RentPointer();
            int activeLength = length > 0 ? length : _blockSize;
            return new NativeMemoryBlock(ptr, _blockSize, activeLength, RecycleBlock);
        }

        /// <summary>
        /// Rents a raw unmanaged pointer with zero managed heap allocations.
        /// Must be returned via <see cref="Return(byte*)"/> when processing completes.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte* RentPointer()
        {
            ThrowIfDisposed();

            while (true)
            {
                if (_freePointers.TryPop(out IntPtr ptr))
                {
                    Interlocked.Increment(ref _activeBlocks);
                    return (byte*)ptr;
                }

                if (!_canGrow)
                {
                    throw new InvalidOperationException($"SlabAllocator exhausted and cannot grow. Active: {_activeBlocks}, Total: {_totalBlocks}");
                }

                lock (_growLock)
                {
                    ThrowIfDisposed();
                    if (_freePointers.IsEmpty)
                    {
                        AllocateNewSlab(_blocksPerSlab);
                    }
                }
            }
        }

        /// <summary>
        /// Attempts to rent a raw unmanaged pointer without throwing an exception if exhausted.
        /// </summary>
        public bool TryRentPointer(out byte* pointer)
        {
            ThrowIfDisposed();

            if (_freePointers.TryPop(out IntPtr ptr))
            {
                Interlocked.Increment(ref _activeBlocks);
                pointer = (byte*)ptr;
                return true;
            }

            if (!_canGrow)
            {
                pointer = null;
                return false;
            }

            lock (_growLock)
            {
                ThrowIfDisposed();
                if (_freePointers.TryPop(out ptr))
                {
                    Interlocked.Increment(ref _activeBlocks);
                    pointer = (byte*)ptr;
                    return true;
                }

                AllocateNewSlab(_blocksPerSlab);
                if (_freePointers.TryPop(out ptr))
                {
                    Interlocked.Increment(ref _activeBlocks);
                    pointer = (byte*)ptr;
                    return true;
                }
            }

            pointer = null;
            return false;
        }

        /// <summary>
        /// Returns a rented raw pointer back to the slab allocator for immediate reuse.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Return(byte* pointer)
        {
            if (pointer == null)
                return;

            if (Volatile.Read(ref _disposed) != 0)
                return;

            _freePointers.Push((IntPtr)pointer);
            Interlocked.Decrement(ref _activeBlocks);
        }

        /// <summary>
        /// Rents a scoped lease that automatically returns the pointer to this allocator upon exiting a using statement.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SlabRentScope RentScoped()
        {
            byte* ptr = RentPointer();
            return new SlabRentScope(this, ptr, _blockSize);
        }

        private void RecycleBlock(NativeMemoryBlock block)
        {
            if (block != null)
            {
                Return(block.DangerousGetPointer());
            }
        }

        private void AllocateNewSlab(int blockCount)
        {
            // Total bytes required for this slab including alignment padding
            int totalBytes = (blockCount * _alignedBlockSize) + _alignment;

            IntPtr rawPtr;
#if NET8_0_OR_GREATER
            rawPtr = (IntPtr)NativeMemory.Alloc((nuint)totalBytes);
#else
            rawPtr = Marshal.AllocHGlobal(totalBytes);
#endif

            NativeMemoryTracker.TrackAlloc(totalBytes);
            _rawSlabPointers.Add(rawPtr);
            _rawSlabSizes.Add(totalBytes);

            // Compute aligned base pointer
            byte* basePtr = (byte*)rawPtr;
            nuint rawAddr = (nuint)basePtr;
            nuint alignedAddr = (rawAddr + (nuint)(_alignment - 1)) & ~(nuint)(_alignment - 1);
            byte* alignedBase = (byte*)alignedAddr;

            // Carve blocks from the aligned slab and push into the free stack
            for (int i = 0; i < blockCount; i++)
            {
                byte* blockPtr = alignedBase + (i * _alignedBlockSize);
                _freePointers.Push((IntPtr)blockPtr);
            }

            Interlocked.Add(ref _totalBlocks, blockCount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref _disposed) != 0)
                throw new ObjectDisposedException(nameof(SlabAllocator));
        }

        ~SlabAllocator()
        {
            DisposeInternal();
        }

        /// <summary>
        /// Releases all off-heap unmanaged memory allocated by this allocator.
        /// </summary>
        public void Dispose()
        {
            DisposeInternal();
            GC.SuppressFinalize(this);
        }

        private void DisposeInternal()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;


            lock (_growLock)
            {
                _freePointers.Clear();

                for (int i = 0; i < _rawSlabPointers.Count; i++)
                {
                    IntPtr rawPtr = _rawSlabPointers[i];
                    if (rawPtr != IntPtr.Zero)
                    {
#if NET8_0_OR_GREATER
                        NativeMemory.Free((void*)rawPtr);
#else
                        Marshal.FreeHGlobal(rawPtr);
#endif
                        if (i < _rawSlabSizes.Count)
                        {
                            NativeMemoryTracker.TrackFree(_rawSlabSizes[i]);
                        }
                    }
                }
                _rawSlabPointers.Clear();
                _rawSlabSizes.Clear();
                Volatile.Write(ref _totalBlocks, 0);
                Volatile.Write(ref _activeBlocks, 0);
            }
        }
    }

    /// <summary>
    /// A ref struct lease representing a rented unmanaged slab block.
    /// Guarantees return of the memory pointer when exiting scope.
    /// </summary>
    public readonly unsafe ref struct SlabRentScope
    {
        private readonly SlabAllocator _allocator;
        private readonly byte* _pointer;
        private readonly int _length;

        /// <summary>
        /// Gets the raw unmanaged pointer.
        /// </summary>
        public byte* Pointer => _pointer;

        /// <summary>
        /// Gets the block size in bytes.
        /// </summary>
        public int Length => _length;

        /// <summary>
        /// Gets a writable span view over the rented unmanaged memory.
        /// </summary>
        public Span<byte> Span => new(_pointer, _length);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal SlabRentScope(SlabAllocator allocator, byte* pointer, int length)
        {
            _allocator = allocator;
            _pointer = pointer;
            _length = length;
        }

        /// <summary>
        /// Returns the rented pointer back to the owning <see cref="SlabAllocator"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            if (_allocator != null && _pointer != null)
            {
                _allocator.Return(_pointer);
            }
        }
    }
}
