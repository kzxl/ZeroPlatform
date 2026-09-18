using System;
using System.IO;
using SharpCompress.Compressors;
using SharpCompress.Compressors.LZMA;
using LzmaMode = SharpCompress.Compressors.CompressionMode;

namespace ZeroCompression.Core.Codecs
{
    public sealed class LzmaCodec : ICompressionCodec
    {
        public CompressionMethod Method => CompressionMethod.Lzma;

        public Stream WrapCompress(Stream destination, CompressionOptions options)
        {
            ArgumentNullException.ThrowIfNull(destination);
            return LZipStream.Create(destination, LzmaMode.Compress, leaveOpen: true);
        }

        public Stream WrapDecompress(Stream source, int windowLog = 0)
        {
            ArgumentNullException.ThrowIfNull(source);
            return LZipStream.Create(source, LzmaMode.Decompress, leaveOpen: true);
        }
    }
}
