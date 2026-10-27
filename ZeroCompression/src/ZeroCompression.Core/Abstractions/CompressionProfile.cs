namespace ZeroCompression.Core
{
    /// <summary>
    /// Preset compression profiles surfaced to CLI and user interfaces.
    /// </summary>
    public enum CompressionProfile
    {
        /// <summary>Fast compression with low CPU effort.</summary>
        Fast,
        /// <summary>Balanced speed and compression ratio.</summary>
        Normal,
        /// <summary>Maximum ratio with multi-threading and long-distance matching.</summary>
        Ultra
    }
}
