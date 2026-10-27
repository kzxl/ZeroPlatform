using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ZeroNetwork.Common
{
    /// <summary>
    /// Ultra-fast monotonic bump allocator (Arena / Region Allocator) for request-scoped and frame-scoped memory.
    /// Replaces GC Heap allocations in HTTP request handling, SignalR RPC invocations, and packet deserialization.
    /// Allocations execute in 1 instruction via pointer addition, and <see cref="Reset"/> rewinds the entire arena in sub-1ns.
    /// </summary>
    public sealed class ZeroArenaAllocator : IDisposable
    {
        private readonly byte[] _buffer;
        private int _offset;
        private bool _disposed;

        [ThreadStatic]
        private static ZeroArenaAllocator? _threadLocal;

        /// <summary>
        /// Gets a thread-local 64KB pre-allocated <see cref="ZeroArenaAllocator"/> instance.
        /// </summary>
        public static ZeroArenaAllocator ThreadLocal => _threadLocal ??= new ZeroArenaAllocator(64 * 1024);

        /// <summary>
        /// Gets the total capacity of the arena in bytes.
        /// </summary>
        public int Capacity => _buffer.Length;

        /// <summary>
        /// Gets the number of bytes currently allocated in the arena.
        /// </summary>
        public int UsedBytes => _offset;

        /// <summary>
        /// Gets the remaining available capacity in bytes.
        /// </summary>
        public int AvailableBytes => _buffer.Length - _offset;

        /// <summary>
        /// Initializes a new instance of <see cref="ZeroArenaAllocator"/> with the specified byte capacity.
        /// </summary>
        /// <param name="capacityBytes">Total arena size in bytes (defaults to 64 KB).</param>
        public ZeroArenaAllocator(int capacityBytes = 64 * 1024)
        {
            if (capacityBytes <= 0) throw new ArgumentOutOfRangeException(nameof(capacityBytes));
            _buffer = new byte[capacityBytes];
            _offset = 0;
        }

        /// <summary>
        /// Allocates a contiguous block of bytes aligned to 8-byte boundaries.
        /// </summary>
        /// <param name="byteCount">Number of bytes to allocate.</param>
        /// <returns>A span pointing directly to the allocated arena region.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<byte> Allocate(int byteCount)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ZeroArenaAllocator));
            if (byteCount < 0) throw new ArgumentOutOfRangeException(nameof(byteCount));
            if (byteCount == 0) return Span<byte>.Empty;

            // 8-byte alignment
            int alignedSize = (byteCount + 7) & ~7;
            int newOffset = _offset + alignedSize;

            if (newOffset > _buffer.Length)
            {
                throw new OutOfMemoryException(
                    $"ZeroArenaAllocator capacity exceeded. Requested: {alignedSize} bytes, Available: {_buffer.Length - _offset} bytes.");
            }

            int currentOffset = _offset;
            _offset = newOffset;
            return _buffer.AsSpan(currentOffset, byteCount);
        }

        /// <summary>
        /// Allocates a typed span of unmanaged elements directly inside the arena.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe Span<T> AllocateSpan<T>(int count) where T : unmanaged
        {
            int totalBytes = checked(count * sizeof(T));
            Span<byte> byteSpan = Allocate(totalBytes);
            return MemoryMarshal.Cast<byte, T>(byteSpan);
        }

        /// <summary>
        /// Rewinds the arena offset back to zero in a single instruction without clearing underlying bytes.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            _offset = 0;
        }

        /// <summary>
        /// Creates a scoped checkpoint that automatically rewinds the arena to its previous offset when disposed.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArenaScope CreateScope()
        {
            return new ArenaScope(this, _offset);
        }

        public void Dispose()
        {
            _disposed = true;
            _offset = 0;
        }

        /// <summary>
        /// Disposable struct scope that restores the arena's previous allocation watermark upon disposal.
        /// </summary>
        public readonly struct ArenaScope : IDisposable
        {
            private readonly ZeroArenaAllocator _allocator;
            private readonly int _previousOffset;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal ArenaScope(ZeroArenaAllocator allocator, int previousOffset)
            {
                _allocator = allocator;
                _previousOffset = previousOffset;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose()
            {
                _allocator._offset = _previousOffset;
            }
        }
    }
}
