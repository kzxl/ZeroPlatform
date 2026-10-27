using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroNetwork.Http.Client
{
    /// <summary>
    /// Read-only stream that yields one already-consumed byte before delegating to the inner stream.
    /// Used to peek for empty bodies without buffering. Does not dispose the inner stream.
    /// </summary>
    internal sealed class PrefixedReadStream : Stream
    {
        private readonly Stream _inner;
        private readonly byte _prefix;
        private bool _prefixConsumed;

        public PrefixedReadStream(byte prefix, Stream inner)
        {
            _prefix = prefix;
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (TryReadPrefix(buffer, offset, count)) return 1;
            return _inner.Read(buffer, offset, count);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (TryReadPrefix(buffer, offset, count)) return Task.FromResult(1);
            return _inner.ReadAsync(buffer, offset, count, cancellationToken);
        }

#if NETCOREAPP2_1_OR_GREATER
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (!_prefixConsumed && buffer.Length > 0)
            {
                buffer.Span[0] = _prefix;
                _prefixConsumed = true;
                return new ValueTask<int>(1);
            }
            return _inner.ReadAsync(buffer, cancellationToken);
        }
#endif

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        private bool TryReadPrefix(byte[] buffer, int offset, int count)
        {
            if (_prefixConsumed || count <= 0) return false;
            buffer[offset] = _prefix;
            _prefixConsumed = true;
            return true;
        }
    }
}
