using System;
using System.Buffers;

namespace ZeroCompression.Core.Buffers
{
    /// <summary>
    /// Pragmatic Dual-Tier buffer management wrapping <see cref="ArrayPool{T}.Shared"/>.
    /// Eliminates LOH and Gen2 garbage collection pressure during large stream operations.
    /// </summary>
    public static class BufferPool
    {
        public const int DefaultChunkSize = 64 * 1024;    // 64 KiB
        public const int DefaultCopySize = 1024 * 1024;   // 1 MiB

        /// <summary>
        /// Rents a byte array from the shared pool of at least <paramref name="minimumLength"/> bytes.
        /// </summary>
        public static byte[] Rent(int minimumLength = DefaultCopySize)
        {
            return ArrayPool<byte>.Shared.Rent(minimumLength);
        }

        /// <summary>
        /// Returns a previously rented byte array to the shared pool.
        /// </summary>
        public static void Return(byte[] buffer, bool clearArray = false)
        {
            if (buffer != null)
            {
                ArrayPool<byte>.Shared.Return(buffer, clearArray);
            }
        }

        /// <summary>
        /// Rents a buffer wrapped in a disposable struct for safe, clean scoped usage.
        /// </summary>
        public static PooledBuffer Scope(int minimumLength = DefaultCopySize, bool clearOnDispose = false)
        {
            return new PooledBuffer(Rent(minimumLength), clearOnDispose);
        }
    }

    /// <summary>
    /// Scoped disposable wrapper over a rented byte buffer.
    /// </summary>
    public readonly struct PooledBuffer : IDisposable
    {
        public byte[] Array { get; }
        private readonly bool _clearOnDispose;

        public PooledBuffer(byte[] array, bool clearOnDispose)
        {
            Array = array;
            _clearOnDispose = clearOnDispose;
        }

        public Span<byte> Span => Array.AsSpan();
        public Memory<byte> Memory => Array.AsMemory();

        public void Dispose()
        {
            if (Array != null)
            {
                BufferPool.Return(Array, _clearOnDispose);
            }
        }
    }
}
