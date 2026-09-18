namespace ZeroCompression.Core
{
    /// <summary>
    /// Supported compression methods across the ZeroUniverse ecosystem.
    /// Preserves binary compatibility with existing SFX footers.
    /// </summary>
    public enum CompressionMethod : byte
    {
        /// <summary>Zstandard. Ultra-fast, multi-threaded, long-distance matching. Default.</summary>
        Zstd = 0,
        /// <summary>Legacy / External LZMA container.</summary>
        Lzma = 1,
        /// <summary>Brotli (.NET BCL). High ratio for text/web payloads.</summary>
        Brotli = 2,
        /// <summary>Stored without compression.</summary>
        Store = 3,
        /// <summary>Deflate (.NET BCL). Standard deflate stream.</summary>
        Deflate = 4,
        /// <summary>GZip (.NET BCL). Standard GZip stream.</summary>
        GZip = 5,
        /// <summary>ZeroTelemetry: Specialized Gorilla/Chimp bit-packing XOR delta codec for time-series and numeric telemetry.</summary>
        ZeroTelemetry = 6
    }
}
