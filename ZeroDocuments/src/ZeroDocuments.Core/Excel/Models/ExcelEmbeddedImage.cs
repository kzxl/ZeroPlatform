using System;

namespace ZeroDocuments.Excel.Models
{
    /// <summary>
    /// Represents an image extracted from an OpenXML Excel (.xlsx) workbook.
    /// </summary>
    public sealed class ExcelEmbeddedImage
    {
        /// <summary>
        /// Gets or sets the image filename or identifier within the package.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the file format extension (e.g. "png", "jpeg", "jpg").
        /// </summary>
        public string Format { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the binary payload of the image.
        /// </summary>
        public byte[] Data { get; set; } = Array.Empty<byte>();
    }
}
