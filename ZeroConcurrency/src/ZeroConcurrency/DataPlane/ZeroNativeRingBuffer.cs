using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using ZeroPrimitives.Memory;

namespace ZeroPlatform.Concurrency
{
    [StructLayout(LayoutKind.Explicit, Size = 128)]
    internal struct NativeRingBufferSlot
    {
        [FieldOffset(64)]
        public long Value;
    }

    /// <summary>
    /// Represents a high-throughput off-heap Disruptor RingBuffer engineered for unmanaged memory frames.
    /// Manages pooled off-heap <see cref="NativeMemoryBlock"/> instances directly inside cache-line padded slots.
    /// </summary>
    public sealed class ZeroNativeRingBuffer : IDisposable
    {
        private readonly int _capacity;
        private readonly int _mask;
        private readonly NativeMemoryBlock?[] _slots;
        private readonly NativeMemoryPool _pool;
        private readonly bool _ownsPool;

        // Physical 128-byte cache-line isolation preventing false sharing across all CLRs
        private NativeRingBufferSlot _writeHead;
        private NativeRingBufferSlot _readTail;
        private int _disposed;

        /// <summary>
        /// Gets the capacity in number of slots (power of two).
        /// </summary>
        public int Capacity => _capacity;

        /// <summary>
        /// Gets the count of active frames waiting to be read.
        /// </summary>
        public int Count
        {
            get
            {
                long write = Volatile.Read(ref _writeHead.Value);
                long read = Volatile.Read(ref _readTail.Value);
                long diff = write - read;
                return diff < 0 ? 0 : (diff > _capacity ? _capacity : (int)diff);
            }
        }

        /// <summary>
        /// Gets whether the ring buffer has been disposed.
        /// </summary>
        public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="ZeroNativeRingBuffer"/> class.
        /// </summary>
        /// <param name="capacityPowerOfTwo">Number of slots (must be a power of two, e.g. 1024, 4096).</param>
        /// <param name="pool">Optional native memory pool for renting slots. Uses <see cref="NativeMemoryPool.Shared"/> if null.</param>
        public ZeroNativeRingBuffer(int capacityPowerOfTwo = 1024, NativeMemoryPool? pool = null)
        {
            if (capacityPowerOfTwo < 2 || (capacityPowerOfTwo & (capacityPowerOfTwo - 1)) != 0)
                throw new ArgumentException("Capacity must be a power of two.", nameof(capacityPowerOfTwo));

            _capacity = capacityPowerOfTwo;
            _mask = capacityPowerOfTwo - 1;
            _slots = new NativeMemoryBlock?[capacityPowerOfTwo];

            if (pool != null)
            {
                _pool = pool;
                _ownsPool = false;
            }
            else
            {
                _pool = NativeMemoryPool.Shared;
                _ownsPool = false;
            }
        }

        /// <summary>
        /// Attempts to write a binary payload into the ring buffer by copying it into a rented off-heap block.
        /// Thread-safe for a single writer.
        /// </summary>
        public bool TryWrite(ReadOnlySpan<byte> payload)
        {
            ThrowIfDisposed();

            long write = Volatile.Read(ref _writeHead.Value);
            long read = Volatile.Read(ref _readTail.Value);

            if (write - read >= _capacity)
                return false; // Buffer full

            // Rent an unmanaged block from the pool
            var block = _pool.Rent(payload.Length);
            payload.CopyTo(block.Span);

            int index = (int)(write & _mask);
            _slots[index] = block;

            // Advance write head atomically
            Volatile.Write(ref _writeHead.Value, write + 1);
            return true;
        }

        /// <summary>
        /// Attempts to peek the size of the next payload in the ring buffer without consuming it.
        /// Thread-safe for a single reader.
        /// </summary>
        public bool TryPeek(out int payloadLength)
        {
            ThrowIfDisposed();
            payloadLength = 0;

            long read = Volatile.Read(ref _readTail.Value);
            long write = Volatile.Read(ref _writeHead.Value);

            if (read >= write)
                return false; // Buffer empty

            int index = (int)(read & _mask);
            NativeMemoryBlock? block = _slots[index];
            if (block == null)
                return false;

            payloadLength = block.Length;
            return true;
        }

        /// <summary>
        /// Attempts to read the next binary payload frame from the ring buffer into the destination span.
        /// Automatically returns the unmanaged block back to the pool once read.
        /// Thread-safe for a single reader.
        /// </summary>
        public bool TryRead(Span<byte> destination, out int bytesRead)
        {
            ThrowIfDisposed();
            bytesRead = 0;

            long read = Volatile.Read(ref _readTail.Value);
            long write = Volatile.Read(ref _writeHead.Value);

            if (read >= write)
                return false; // Buffer empty

            int index = (int)(read & _mask);
            NativeMemoryBlock? block = _slots[index];

            if (block == null)
                return false;

            if (destination.Length < block.Length)
            {
                throw new ArgumentException($"Destination buffer size ({destination.Length} bytes) is smaller than payload block length ({block.Length} bytes).", nameof(destination));
            }

            block.Span.CopyTo(destination);
            bytesRead = block.Length;

            // Recycle off-heap block back to pool via disposal
            block.Dispose();
            _slots[index] = null;

            // Advance read tail atomically
            Volatile.Write(ref _readTail.Value, read + 1);
            return true;
        }

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref _disposed) != 0)
                throw new ObjectDisposedException(nameof(ZeroNativeRingBuffer));
        }

        /// <summary>
        /// Releases all allocated memory and frees cached slots.
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            // Free pending blocks in slots
            for (int i = 0; i < _capacity; i++)
            {
                var block = _slots[i];
                if (block != null)
                {
                    block.Dispose();
                    _slots[i] = null;
                }
            }

            if (_ownsPool)
            {
                _pool.Dispose();
            }
        }
    }
}
