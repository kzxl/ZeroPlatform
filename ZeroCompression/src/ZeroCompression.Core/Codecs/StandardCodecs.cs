using System;
using System.IO;
using System.IO.Compression;

namespace ZeroCompression.Core.Codecs
{
    public sealed class BrotliCodec : ICompressionCodec
    {
        public CompressionMethod Method => CompressionMethod.Brotli;

        public Stream WrapCompress(Stream destination, CompressionOptions options)
        {
            ArgumentNullException.ThrowIfNull(destination);
            ArgumentNullException.ThrowIfNull(options);

            var level = MapBrotliLevel(options.Level);
            return new BrotliStream(destination, level, leaveOpen: true);
        }

        public Stream WrapDecompress(Stream source, int windowLog = 0)
        {
            ArgumentNullException.ThrowIfNull(source);
            return new BrotliStream(source, System.IO.Compression.CompressionMode.Decompress, leaveOpen: true);
        }

        private static CompressionLevel MapBrotliLevel(int level)
        {
            if (level <= 1) return CompressionLevel.Fastest;
            if (level >= 18) return CompressionLevel.SmallestSize;
            return CompressionLevel.Optimal;
        }
    }

    public sealed class DeflateCodec : ICompressionCodec
    {
        public CompressionMethod Method => CompressionMethod.Deflate;

        public Stream WrapCompress(Stream destination, CompressionOptions options)
        {
            ArgumentNullException.ThrowIfNull(destination);
            ArgumentNullException.ThrowIfNull(options);

            var level = options.Level <= 3 ? CompressionLevel.Fastest :
                        options.Level >= 15 ? CompressionLevel.SmallestSize : CompressionLevel.Optimal;

            return new DeflateStream(destination, level, leaveOpen: true);
        }

        public Stream WrapDecompress(Stream source, int windowLog = 0)
        {
            ArgumentNullException.ThrowIfNull(source);
            return new DeflateStream(source, System.IO.Compression.CompressionMode.Decompress, leaveOpen: true);
        }
    }

    public sealed class GZipCodec : ICompressionCodec
    {
        public CompressionMethod Method => CompressionMethod.GZip;

        public Stream WrapCompress(Stream destination, CompressionOptions options)
        {
            ArgumentNullException.ThrowIfNull(destination);
            ArgumentNullException.ThrowIfNull(options);

            var level = options.Level <= 3 ? CompressionLevel.Fastest :
                        options.Level >= 15 ? CompressionLevel.SmallestSize : CompressionLevel.Optimal;

            return new GZipStream(destination, level, leaveOpen: true);
        }

        public Stream WrapDecompress(Stream source, int windowLog = 0)
        {
            ArgumentNullException.ThrowIfNull(source);
            return new GZipStream(source, System.IO.Compression.CompressionMode.Decompress, leaveOpen: true);
        }
    }

    public sealed class StoreCodec : ICompressionCodec
    {
        public CompressionMethod Method => CompressionMethod.Store;

        public Stream WrapCompress(Stream destination, CompressionOptions options)
        {
            ArgumentNullException.ThrowIfNull(destination);
            return new LeaveOpenWrapper(destination);
        }

        public Stream WrapDecompress(Stream source, int windowLog = 0)
        {
            ArgumentNullException.ThrowIfNull(source);
            return new LeaveOpenWrapper(source);
        }

        private sealed class LeaveOpenWrapper : Stream
        {
            private readonly Stream _inner;
            public LeaveOpenWrapper(Stream inner) => _inner = inner;
            public override bool CanRead => _inner.CanRead;
            public override bool CanSeek => false;
            public override bool CanWrite => _inner.CanWrite;
            public override long Length => _inner.Length;
            public override long Position { get => _inner.Position; set => throw new NotSupportedException(); }
            public override void Flush() => _inner.Flush();
            public override int Read(byte[] b, int o, int c) => _inner.Read(b, o, c);
            public override void Write(byte[] b, int o, int c) => _inner.Write(b, o, c);
            public override long Seek(long o, SeekOrigin s) => throw new NotSupportedException();
            public override void SetLength(long v) => throw new NotSupportedException();
            protected override void Dispose(bool disposing) { /* leave inner open */ }
        }
    }
}
