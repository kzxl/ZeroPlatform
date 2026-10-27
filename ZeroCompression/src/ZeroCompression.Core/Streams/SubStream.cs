using System;
using System.IO;

namespace ZeroCompression.Core.Streams
{
    /// <summary>
    /// A read-only view over a contiguous region [offset, offset+length) of an
    /// underlying seekable stream. Used by extractors to isolate payloads.
    /// </summary>
    public sealed class SubStream : Stream
    {
        private readonly Stream _inner;
        private readonly long _start;
        private readonly long _length;
        private long _position;
        private readonly bool _leaveOpen;

        public SubStream(Stream inner, long start, long length, bool leaveOpen = false)
        {
            if (!inner.CanSeek) throw new ArgumentException("Inner stream must be seekable.", nameof(inner));
            _inner = inner;
            _start = start;
            _length = length;
            _leaveOpen = leaveOpen;
            _inner.Seek(_start, SeekOrigin.Begin);
            _position = 0;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            long remaining = _length - _position;
            if (remaining <= 0) return 0;
            if (count > remaining) count = (int)remaining;

            if (_inner.Position != _start + _position)
                _inner.Seek(_start + _position, SeekOrigin.Begin);

            int read = _inner.Read(buffer, offset, count);
            _position += read;
            return read;
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => _length;

        public override long Position
        {
            get => _position;
            set
            {
                if (value < 0 || value > _length) throw new ArgumentOutOfRangeException(nameof(value));
                _position = value;
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            long target = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => _position + offset,
                SeekOrigin.End => _length + offset,
                _ => throw new ArgumentOutOfRangeException(nameof(origin)),
            };
            if (target < 0 || target > _length) throw new IOException("Seek outside SubStream bounds.");
            _position = target;
            return _position;
        }

        public override void Flush() { }
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_leaveOpen) _inner.Dispose();
            base.Dispose(disposing);
        }
    }
}
