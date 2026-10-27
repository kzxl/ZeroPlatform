using System.IO;

namespace ZeroCompression.Core
{
    /// <summary>
    /// Contract for compression codecs operating in the ZeroUniverse ecosystem.
    /// </summary>
    public interface ICompressionCodec
    {
        /// <summary>Identifies the compression method implemented by this codec.</summary>
        CompressionMethod Method { get; }

        /// <summary>
        /// Wraps the destination stream with a compression stream.
        /// Disposing the returned stream finalizes compression and leaves destination open.
        /// </summary>
        Stream WrapCompress(Stream destination, CompressionOptions options);

        /// <summary>
        /// Wraps the source stream with a decompression stream.
        /// Leaves source open on dispose.
        /// </summary>
        Stream WrapDecompress(Stream source, int windowLog = 0);
    }
}
