using System;
using System.Collections.Concurrent;
using System.IO;
using ZeroCompression.Core.Codecs;

namespace ZeroCompression.Core
{
    /// <summary>
    /// Thread-safe registry for compression codecs.
    /// Pre-registers standard sovereign codecs on initialization.
    /// </summary>
    public static class CodecRegistry
    {
        private static readonly ConcurrentDictionary<CompressionMethod, ICompressionCodec> Codecs = new();

        static CodecRegistry()
        {
            Register(new ZstdCodec());
            Register(new LzmaCodec());
            Register(new BrotliCodec());
            Register(new DeflateCodec());
            Register(new GZipCodec());
            Register(new StoreCodec());
            Register(new ZeroTelemetryCodec());
        }

        public static void Register(ICompressionCodec codec)
        {
            ArgumentNullException.ThrowIfNull(codec);
            Codecs[codec.Method] = codec;
        }

        public static ICompressionCodec Get(CompressionMethod method)
        {
            if (Codecs.TryGetValue(method, out var codec))
            {
                return codec;
            }
            throw new NotSupportedException($"Compression method '{method}' is not registered or supported.");
        }

        public static Stream WrapCompress(Stream destination, CompressionOptions options)
        {
            ArgumentNullException.ThrowIfNull(destination);
            ArgumentNullException.ThrowIfNull(options);
            return Get(options.Method).WrapCompress(destination, options);
        }

        public static Stream WrapDecompress(Stream source, CompressionMethod method, int windowLog = 0)
        {
            ArgumentNullException.ThrowIfNull(source);
            return Get(method).WrapDecompress(source, windowLog);
        }
    }
}
