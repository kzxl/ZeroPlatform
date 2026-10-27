namespace ZeroDocuments.Excel.Models
{
    /// <summary>
    /// Configuration options for reading Excel (.xlsx) packages.
    /// </summary>
    public sealed class ExcelReaderOptions
    {
        /// <summary>
        /// Maximum allowed uncompressed size in bytes for any single ZIP entry (worksheet, shared strings, etc.).
        /// Protects against zip decompression bombs (Zip Bombs / CWE-409).
        /// Default is 500 MB (524,288,000 bytes). Set to 0 or negative for unlimited.
        /// </summary>
        public long MaxUncompressedEntryBytes { get; set; } = 500L * 1024 * 1024;
    }
}
