using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using ZeroPrimitives.Internal;

namespace ZeroPrimitives.Buffers
{
    /// <summary>
    /// High-performance circular byte buffer (Ring Buffer) for streaming TCP sockets and serial COM ports.
    /// Eliminates memory shifting (Array.Copy) when accumulating and parsing fragmented binary frames.
    /// </summary>
    public sealed class ByteRingBuffer : IDisposable
    {
        private byte[]? _buffer;
        private readonly bool _fromPool;
        private readonly int _capacity;
        private int _head; // Read index
        private int _tail; // Write index
        private int _count;

        /// <summary>
        /// Initializes a new instance of ByteRingBuffer with the specified capacity.
        /// </summary>
        /// <param name="capacity">The maximum number of bytes the ring buffer can hold.</param>
        /// <param name="useArrayPool">Whether to rent the underlying buffer from ArrayPool.Shared.</param>
        public ByteRingBuffer(int capacity, bool useArrayPool = false)
        {
            if (capacity <= 0)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than zero.");
            }

            _capacity = capacity;
            _fromPool = useArrayPool;

            if (_fromPool)
            {
                _buffer = ArrayPool<byte>.Shared.Rent(capacity);
            }
            else
            {
                _buffer = new byte[capacity];
            }

            _head = 0;
            _tail = 0;
            _count = 0;
        }

        /// <summary>
        /// Gets the maximum capacity of the ring buffer.
        /// </summary>
        public int Capacity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _capacity;
        }

        /// <summary>
        /// Gets the number of bytes currently stored and available to read.
        /// </summary>
        public int AvailableRead
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _count;
        }

        /// <summary>
        /// Gets the free space remaining in bytes available to write.
        /// </summary>
        public int AvailableWrite
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _capacity - _count;
        }

        /// <summary>
        /// Gets a value indicating whether the buffer is empty.
        /// </summary>
        public bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _count == 0;
        }

        /// <summary>
        /// Gets a value indicating whether the buffer is full.
        /// </summary>
        public bool IsFull
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _count == _capacity;
        }

        /// <summary>
        /// Writes bytes into the ring buffer. Returns the actual number of bytes written.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Write(ReadOnlySpan<byte> source)
        {
            ThrowIfDisposed();

            int toWrite = Math.Min(source.Length, AvailableWrite);
            if (toWrite == 0) return 0;

            int firstChunk = Math.Min(toWrite, _capacity - _tail);
            source.Slice(0, firstChunk).CopyTo(_buffer.AsSpan(_tail, firstChunk));

            int secondChunk = toWrite - firstChunk;
            if (secondChunk > 0)
            {
                source.Slice(firstChunk, secondChunk).CopyTo(_buffer.AsSpan(0, secondChunk));
                _tail = secondChunk;
            }
            else
            {
                _tail = (_tail + firstChunk) % _capacity;
            }

            _count += toWrite;
            return toWrite;
        }

        /// <summary>
        /// Attempts to write the entire source span into the ring buffer.
        /// Returns false if there is not enough free space.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryWrite(ReadOnlySpan<byte> source)
        {
            ThrowIfDisposed();

            if (source.Length > AvailableWrite)
            {
                return false;
            }

            Write(source);
            return true;
        }

        /// <summary>
        /// Reads bytes from the ring buffer into the destination span and advances the read position.
        /// Returns the actual number of bytes read.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Read(Span<byte> destination)
        {
            int peeked = Peek(destination);
            if (peeked > 0)
            {
                Advance(peeked);
            }
            return peeked;
        }

        /// <summary>
        /// Attempts to read exactly destination.Length bytes from the ring buffer.
        /// Returns false and does not advance the read pointer if insufficient bytes are available.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryRead(Span<byte> destination)
        {
            ThrowIfDisposed();

            if (destination.Length > _count)
            {
                return false;
            }

            Read(destination);
            return true;
        }

        /// <summary>
        /// Peeks bytes from the ring buffer into destination without advancing the read position.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Peek(Span<byte> destination)
        {
            ThrowIfDisposed();

            int toRead = Math.Min(destination.Length, _count);
            if (toRead == 0) return 0;

            int firstChunk = Math.Min(toRead, _capacity - _head);
            _buffer.AsSpan(_head, firstChunk).CopyTo(destination.Slice(0, firstChunk));

            int secondChunk = toRead - firstChunk;
            if (secondChunk > 0)
            {
                _buffer.AsSpan(0, secondChunk).CopyTo(destination.Slice(firstChunk, secondChunk));
            }

            return toRead;
        }

        /// <summary>
        /// Advances the read position by the specified count without copying data.
        /// Useful when consuming a packet frame that was inspected via Peek.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Advance(int count)
        {
            ThrowIfDisposed();

            if (count < 0 || count > _count)
            {
                ThrowHelper.ThrowCannotAdvanceBeyondBounds(nameof(count));
            }

            _head = (_head + count) % _capacity;
            _count -= count;
        }

        /// <summary>
        /// Clears all stored bytes in the buffer and resets head and tail pointers.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            _head = 0;
            _tail = 0;
            _count = 0;
        }

        /// <summary>
        /// Releases any rented buffer back to the ArrayPool.
        /// </summary>
        public void Dispose()
        {
            if (_buffer != null)
            {
                if (_fromPool)
                {
                    ArrayPool<byte>.Shared.Return(_buffer);
                }
                _buffer = null;
                _count = 0;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ThrowIfDisposed()
        {
            if (_buffer == null)
            {
                ThrowHelper.ThrowObjectDisposedException(nameof(ByteRingBuffer));
            }
        }
    }
}
