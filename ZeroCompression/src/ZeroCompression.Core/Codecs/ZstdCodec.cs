using System;
using System.IO;
using ZstdSharp;
using ZstdSharp.Unsafe;

namespace ZeroCompression.Core.Codecs
{
    public sealed class ZstdCodec : ICompressionCodec
    {
        public CompressionMethod Method => CompressionMethod.Zstd;

        public Stream WrapCompress(Stream destination, CompressionOptions options)
        {
            ArgumentNullException.ThrowIfNull(destination);
            ArgumentNullException.ThrowIfNull(options);

            int level = Math.Clamp(options.Level, 1, Compressor.MaxCompressionLevel);
            var compressor = new Compressor(level);

            if (options.LongDistanceMatching)
            {
                compressor.SetParameter(ZSTD_cParameter.ZSTD_c_enableLongDistanceMatching, 1);
            }

            int wlog = options.ResolvedWindowLog;
            if (wlog > 0)
            {
                compressor.SetParameter(ZSTD_cParameter.ZSTD_c_windowLog, wlog);
            }

            compressor.SetParameter(ZSTD_cParameter.ZSTD_c_checksumFlag, options.ContentChecksum ? 1 : 0);

            int workers = options.ResolvedWorkers;
            if (workers > 0)
            {
                compressor.SetParameter(ZSTD_cParameter.ZSTD_c_nbWorkers, workers);
            }

            return new CompressionStream(destination, compressor, bufferSize: 0, preserveCompressor: false, leaveOpen: true);
        }

        public Stream WrapDecompress(Stream source, int windowLog = 0)
        {
            ArgumentNullException.ThrowIfNull(source);

            var decompressor = new Decompressor();
            if (windowLog > CompressionOptions.MaxSafeWindowLog)
            {
                decompressor.SetParameter(ZSTD_dParameter.ZSTD_d_windowLogMax,
                    Math.Min(windowLog, CompressionOptions.MaxLongWindowLog));
            }

            return new DecompressionStream(source, decompressor, bufferSize: 0,
                checkEndOfStream: false, preserveDecompressor: false, leaveOpen: true);
        }
    }
}
