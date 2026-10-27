using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace ZeroPrimitives.Memory
{
    /// <summary>
    /// High-performance off-heap linear bump allocator (Arena Allocator).
    /// Pre-allocates a contiguous block of unmanaged memory completely outside of the Garbage Collector (GC).
    /// Allocations advance a single offset pointer in O(1).
    /// All allocations in a frame, query, or tick can be reclaimed in a single CPU cycle via <see cref="Reset"/>.
    /// </summary>
    public sealed unsafe class ArenaAllocator : IDisposable
    {
        private byte* _basePointer;
        private readonly int _capacity;
        private int _offset;
        private int _disposed;

        /// <summary>
        /// Gets the total capacity in bytes of this arena.
        /// </summary>
        public int Capacity => _capacity;

        /// <summary>
        /// Gets the number of bytes currently allocated from this arena.
        /// </summary>
        public int UsedBytes => Volatile.Read(ref _offset);

        /// <summary>
        /// Gets the remaining available bytes in this arena.
        /// </summary>
        public int RemainingBytes => _capacity - UsedBytes;

        /// <summary>
        /// Gets whether this arena has been disposed.
        /// </summary>
        public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

        /// <summary>
        /// Initializes a new instance of <see cref="ArenaAllocator"/> with the specified byte capacity.
        /// </summary>
        /// <param name="capacity">Total bytes to allocate off-heap (e.g., 64MB, 256MB).</param>
        public ArenaAllocator(int capacity)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity), "Arena capacity must be positive.");

            _capacity = capacity;
#if NET8_0_OR_GREATER
            _basePointer = (byte*)NativeMemory.Alloc((nuint)capacity);
#else
            _basePointer = (byte*)Marshal.AllocHGlobal(capacity).ToPointer();
#endif
            _offset = 0;
        }

        /// <summary>
        /// Allocates a contiguous span of bytes from the arena with the specified alignment.
        /// </summary>
        /// <param name="size">Number of bytes required.</param>
        /// <param name="alignment">Memory alignment in bytes (default: 16 for SIMD/AVX).</param>
        /// <returns>A writable <see cref="Span{Byte}"/> over the allocated memory.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<byte> Allocate(int size, int alignment = 16)
        {
            byte* ptr = AllocatePointer(size, alignment);
            return new Span<byte>(ptr, size);
        }

        /// <summary>
        /// Allocates a contiguous unmanaged memory pointer from the arena with the specified alignment.
        /// </summary>
        /// <param name="size">Number of bytes required.</param>
        /// <param name="alignment">Memory alignment in bytes (default: 16 for SIMD/AVX).</param>
        /// <returns>Raw pointer to the allocated memory.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte* AllocatePointer(int size, int alignment = 16)
        {
            ThrowIfDisposed();

            if (size <= 0)
                throw new ArgumentOutOfRangeException(nameof(size), "Allocation size must be positive.");

            if ((alignment & (alignment - 1)) != 0 || alignment <= 0)
                throw new ArgumentException("Alignment must be a positive power of two.", nameof(alignment));

            byte* basePtr = _basePointer + _offset;
            byte* alignedPtr = (byte*)(((nuint)basePtr + (nuint)(alignment - 1)) & ~(nuint)(alignment - 1));
            int newOffset = (int)(alignedPtr - _basePointer) + size;

            if (newOffset > _capacity)
            {
                throw new OutOfMemoryException($"Arena capacity exceeded. Requested {size} bytes, but arena only has {_capacity - _offset} bytes remaining out of {_capacity}.");
            }

            _offset = newOffset;
            return alignedPtr;
        }

        /// <summary>
        /// Allocates an off-heap <see cref="NativeMemoryBlock"/> view from the arena.
        /// </summary>
        /// <param name="size">Number of bytes required.</param>
        /// <param name="alignment">Memory alignment in bytes (default: 16).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NativeMemoryBlock AllocateBlock(int size, int alignment = 16)
        {
            byte* ptr = AllocatePointer(size, alignment);
            return new NativeMemoryBlock(ptr, size, size, ownsMemory: false);
        }

        /// <summary>
        /// Resets the allocation offset to zero in a single CPU instruction.
        /// All previously allocated spans from this arena remain valid in memory but can be overwritten by subsequent allocations.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            ThrowIfDisposed();
            Volatile.Write(ref _offset, 0);
        }

        /// <summary>
        /// Resets the allocation offset to zero and zeroes out all arena memory.
        /// </summary>
        public void ResetAndClear()
        {
            ThrowIfDisposed();
            int used = _offset;
            Volatile.Write(ref _offset, 0);
            if (used > 0)
            {
#if NET8_0_OR_GREATER
                NativeMemory.Clear(_basePointer, (nuint)used);
#else
                new Span<byte>(_basePointer, used).Clear();
#endif
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ThrowIfDisposed()
        {
            if (IsDisposed)
                throw new ObjectDisposedException(nameof(ArenaAllocator), "The arena allocator has already been disposed.");
        }

        /// <summary>
        /// Disposes the arena and frees the underlying unmanaged memory block.
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            if (_basePointer != null)
            {
#if NET8_0_OR_GREATER
                NativeMemory.Free(_basePointer);
#else
                Marshal.FreeHGlobal((IntPtr)_basePointer);
#endif
                _basePointer = null;
            }
            GC.SuppressFinalize(this);
        }


        /// <summary>
        /// Finalizer ensures unmanaged memory is reclaimed if not disposed manually.
        /// </summary>
        ~ArenaAllocator()
        {
            if (_basePointer != null && Volatile.Read(ref _disposed) == 0)
            {
#if NET8_0_OR_GREATER
                NativeMemory.Free(_basePointer);
#else
                Marshal.FreeHGlobal((IntPtr)_basePointer);
#endif
                _basePointer = null;
            }
        }
    }
}
