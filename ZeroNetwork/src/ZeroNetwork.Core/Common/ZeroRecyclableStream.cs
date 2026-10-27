using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroNetwork.Common
{
    /// <summary>
    /// High-performance recyclable memory stream backed by rented chunks from <see cref="ArrayPool{T}"/>.
    /// Eliminates Large Object Heap (LOH) allocations and buffer resizing GC pauses compared to standard <see cref="MemoryStream"/>.
    /// </summary>
    public sealed class ZeroRecyclableStream : Stream
    {
        private const int DefaultBlockSize = 4096;

        private readonly int _blockSize;
        private readonly List<byte[]> _blocks = new List<byte[]>();
        private long _position;
        private long _length;
        private int _disposed;

        /// <inheritdoc />
        public override bool CanRead => Volatile.Read(ref _disposed) == 0;

        /// <inheritdoc />
        public override bool CanSeek => Volatile.Read(ref _disposed) == 0;

        /// <inheritdoc />
        public override bool CanWrite => Volatile.Read(ref _disposed) == 0;

        /// <inheritdoc />
        public override long Length
        {
            get
            {
                ThrowIfDisposed();
                return _length;
            }
        }

        /// <inheritdoc />
        public override long Position
        {
            get
            {
                ThrowIfDisposed();
                return _position;
            }
            set
            {
                ThrowIfDisposed();
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
                _position = value;
            }
        }

        /// <summary>
        /// Initializes a new instance of <see cref="ZeroRecyclableStream"/>.
        /// </summary>
        /// <param name="blockSize">Size in bytes for each rented chunk (default 4KB).</param>
        public ZeroRecyclableStream(int blockSize = DefaultBlockSize)
        {
            if (blockSize <= 0) throw new ArgumentOutOfRangeException(nameof(blockSize));
            _blockSize = blockSize;
        }

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count)
        {
            ThrowIfDisposed();
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || count < 0 || offset + count > buffer.Length) throw new ArgumentOutOfRangeException();
            if (count == 0) return;

            int remaining = count;
            int srcOffset = offset;

            while (remaining > 0)
            {
                int blockIndex = (int)(_position / _blockSize);
                int blockOffset = (int)(_position % _blockSize);

                while (_blocks.Count <= blockIndex)
                {
                    _blocks.Add(ArrayPool<byte>.Shared.Rent(_blockSize));
                }

                int toWrite = Math.Min(remaining, _blockSize - blockOffset);
                Buffer.BlockCopy(buffer, srcOffset, _blocks[blockIndex], blockOffset, toWrite);

                _position += toWrite;
                if (_position > _length) _length = _position;

                srcOffset += toWrite;
                remaining -= toWrite;
            }
        }

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count)
        {
            ThrowIfDisposed();
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || count < 0 || offset + count > buffer.Length) throw new ArgumentOutOfRangeException();
            if (count == 0 || _position >= _length) return 0;

            int toReadTotal = (int)Math.Min(count, _length - _position);
            int remaining = toReadTotal;
            int dstOffset = offset;

            while (remaining > 0)
            {
                int blockIndex = (int)(_position / _blockSize);
                int blockOffset = (int)(_position % _blockSize);

                int toRead = Math.Min(remaining, _blockSize - blockOffset);
                Buffer.BlockCopy(_blocks[blockIndex], blockOffset, buffer, dstOffset, toRead);

                _position += toRead;
                dstOffset += toRead;
                remaining -= toRead;
            }

            return toReadTotal;
        }

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin)
        {
            ThrowIfDisposed();
            long newPos = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => _position + offset,
                SeekOrigin.End => _length + offset,
                _ => throw new ArgumentOutOfRangeException(nameof(origin))
            };

            if (newPos < 0) throw new ArgumentOutOfRangeException(nameof(offset));
            _position = newPos;
            return _position;
        }

        /// <inheritdoc />
        public override void SetLength(long value)
        {
            ThrowIfDisposed();
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            _length = value;
            if (_position > _length) _position = _length;
        }

        /// <inheritdoc />
        public override void Flush() { }

        /// <summary>
        /// Copies all contents from the beginning directly to another stream without large intermediate buffers.
        /// </summary>
        public async Task WriteToStreamAsync(Stream destination, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (destination == null) throw new ArgumentNullException(nameof(destination));

            long written = 0;
            for (int i = 0; i < _blocks.Count && written < _length; i++)
            {
                int count = (int)Math.Min(_blockSize, _length - written);
                await destination.WriteAsync(_blocks[i], 0, count, cancellationToken).ConfigureAwait(false);
                written += count;
            }
        }

        /// <summary>
        /// Materializes all written bytes into a contiguous array.
        /// </summary>
        public byte[] ToArray()
        {
            ThrowIfDisposed();
            if (_length == 0) return Array.Empty<byte>();

            byte[] result = new byte[_length];
            long copied = 0;
            for (int i = 0; i < _blocks.Count && copied < _length; i++)
            {
                int count = (int)Math.Min(_blockSize, _length - copied);
                Buffer.BlockCopy(_blocks[i], 0, result, (int)copied, count);
                copied += count;
            }
            return result;
        }

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref _disposed) != 0)
                throw new ObjectDisposedException(nameof(ZeroRecyclableStream));
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0 && disposing)
            {
                for (int i = 0; i < _blocks.Count; i++)
                {
                    ArrayPool<byte>.Shared.Return(_blocks[i]);
                }
                _blocks.Clear();
                _length = 0;
                _position = 0;
            }
            base.Dispose(disposing);
        }
    }
}
