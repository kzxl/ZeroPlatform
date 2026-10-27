using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace ZeroPrimitives.Memory
{
    /// <summary>
    /// Auto-expanding off-heap chunked linear bump allocator.
    /// Eliminates rigid capacity limits by dynamically chaining unmanaged memory chunks (e.g., 4MB, 16MB)
    /// while maintaining O(1) pointer-bump allocation and instant bulk recycling via <see cref="Reset"/>.
    /// </summary>
    public sealed unsafe class PagingArenaAllocator : IDisposable
    {
        private sealed class ArenaChunk
        {
            public readonly byte* Pointer;
            public readonly int Capacity;
            public int Offset;
            public ArenaChunk? Next;

            public ArenaChunk(int capacity)
            {
                Capacity = capacity;
                Offset = 0;
                Next = null;
#if NET8_0_OR_GREATER
                Pointer = (byte*)NativeMemory.Alloc((nuint)capacity);
#else
                Pointer = (byte*)Marshal.AllocHGlobal(capacity).ToPointer();
#endif
                NativeMemoryTracker.TrackAlloc(capacity);
            }

            public void Free()
            {
                if (Pointer != null)
                {
                    NativeMemoryTracker.TrackFree(Capacity);
#if NET8_0_OR_GREATER
                    NativeMemory.Free(Pointer);
#else
                    Marshal.FreeHGlobal((IntPtr)Pointer);
#endif
                }
            }
        }

        private readonly int _chunkSize;
        private ArenaChunk _headChunk;
        private ArenaChunk _currentChunk;
        private int _totalAllocatedBytes;
        private int _disposed;

        /// <summary>
        /// Gets the default size in bytes of each allocated chunk (e.g. 4MB).
        /// </summary>
        public int ChunkSize => _chunkSize;

        /// <summary>
        /// Gets the total capacity in bytes of all linked chunks currently in this arena.
        /// </summary>
        public int TotalCapacity => Volatile.Read(ref _totalAllocatedBytes);

        /// <summary>
        /// Gets whether this arena has been disposed.
        /// </summary>
        public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

        /// <summary>
        /// Initializes a new instance of <see cref="PagingArenaAllocator"/>.
        /// </summary>
        /// <param name="chunkSize">Size in bytes for each memory chunk (default: 4MB).</param>
        public PagingArenaAllocator(int chunkSize = 4 * 1024 * 1024)
        {
            if (chunkSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(chunkSize), "Chunk size must be positive.");

            _chunkSize = chunkSize;
            _headChunk = new ArenaChunk(chunkSize);
            _currentChunk = _headChunk;
            _totalAllocatedBytes = chunkSize;
        }

        /// <summary>
        /// Allocates a contiguous span of bytes from the arena with the specified alignment.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<byte> Allocate(int size, int alignment = 16)
        {
            byte* ptr = AllocatePointer(size, alignment);
            return new Span<byte>(ptr, size);
        }

        /// <summary>
        /// Allocates a raw unmanaged memory pointer from the arena with the specified alignment.
        /// If the current chunk cannot satisfy the request, a new chunk is linked automatically.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte* AllocatePointer(int size, int alignment = 16)
        {
            ThrowIfDisposed();

            if (size <= 0)
                throw new ArgumentOutOfRangeException(nameof(size), "Allocation size must be positive.");

            if ((alignment & (alignment - 1)) != 0 || alignment <= 0)
                throw new ArgumentException("Alignment must be a positive power of two.", nameof(alignment));

            // Fast path: try to allocate from the current active chunk
            ArenaChunk chunk = _currentChunk;
            byte* basePtr = chunk.Pointer + chunk.Offset;
            byte* alignedPtr = (byte*)(((nuint)basePtr + (nuint)(alignment - 1)) & ~(nuint)(alignment - 1));
            int newOffset = (int)(alignedPtr - chunk.Pointer) + size;

            if (newOffset <= chunk.Capacity)
            {
                chunk.Offset = newOffset;
                return alignedPtr;
            }

            // Slow path: move to next existing chunk or allocate a new chunk
            return AllocateFromNextChunk(size, alignment);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private byte* AllocateFromNextChunk(int size, int alignment)
        {
            // If the next chunk already exists in the chain, reuse it
            if (_currentChunk.Next != null)
            {
                _currentChunk = _currentChunk.Next;
                _currentChunk.Offset = 0;
                byte* basePtr = _currentChunk.Pointer;
                byte* alignedPtr = (byte*)(((nuint)basePtr + (nuint)(alignment - 1)) & ~(nuint)(alignment - 1));
                int newOffset = (int)(alignedPtr - _currentChunk.Pointer) + size;
                if (newOffset <= _currentChunk.Capacity)
                {
                    _currentChunk.Offset = newOffset;
                    return alignedPtr;
                }
            }

            // Otherwise allocate a new chunk (at least _chunkSize, or larger if requested size is huge)
            int newCapacity = Math.Max(_chunkSize, size + (alignment * 2));
            var newChunk = new ArenaChunk(newCapacity);
            _totalAllocatedBytes += newCapacity;

            // Link chunk into the singly-linked list
            newChunk.Next = _currentChunk.Next;
            _currentChunk.Next = newChunk;
            _currentChunk = newChunk;

            byte* baseChunkPtr = _currentChunk.Pointer;
            byte* finalAlignedPtr = (byte*)(((nuint)baseChunkPtr + (nuint)(alignment - 1)) & ~(nuint)(alignment - 1));
            _currentChunk.Offset = (int)(finalAlignedPtr - baseChunkPtr) + size;
            return finalAlignedPtr;
        }

        /// <summary>
        /// Allocates an off-heap <see cref="NativeMemoryBlock"/> view over memory in this arena.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NativeMemoryBlock AllocateBlock(int size, int alignment = 16)
        {
            byte* ptr = AllocatePointer(size, alignment);
            return new NativeMemoryBlock(ptr, size, size, ownsMemory: false);
        }

        /// <summary>
        /// Resets the allocation offset across all linked chunks back to the head in O(1).
        /// All allocated chunks remain retained in memory for ultra-fast zero-allocation reuse in subsequent cycles.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            ThrowIfDisposed();

            ArenaChunk? curr = _headChunk;
            while (curr != null)
            {
                curr.Offset = 0;
                curr = curr.Next;
            }

            _currentChunk = _headChunk;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ThrowIfDisposed()
        {
            if (IsDisposed)
                throw new ObjectDisposedException(nameof(PagingArenaAllocator));
        }

        /// <summary>
        /// Frees all unmanaged memory chunks and releases native resources back to the OS.
        /// </summary>
        ~PagingArenaAllocator()
        {
            DisposeInternal();
        }

        /// <summary>
        /// Frees all unmanaged memory chunks and releases native resources back to the OS.
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

            ArenaChunk? curr = _headChunk;
            while (curr != null)
            {
                ArenaChunk? next = curr.Next;
                curr.Free();
                curr = next;
            }

            _headChunk = null!;
            _currentChunk = null!;
        }
    }
}

