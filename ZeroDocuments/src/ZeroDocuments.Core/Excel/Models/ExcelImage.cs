using System;

namespace ZeroDocuments.Excel.Models
{
    /// <summary>
    /// Represents an embedded image in an Excel worksheet using OpenXML DrawingML.
    /// Pure C# BCL implementation without external drawing libraries.
    /// </summary>
    public sealed class ExcelImage
    {
        /// <summary>
        /// Gets the raw binary image payload (PNG, JPEG, etc.).
        /// </summary>
        public byte[] Data { get; }

        /// <summary>
        /// Gets the image format extension ("png", "jpg", "jpeg").
        /// </summary>
        public string Format { get; }

        /// <summary>
        /// Gets or sets the 1-based anchor column index (e.g., 1 for Column A).
        /// </summary>
        public int ColumnIndex { get; set; }

        /// <summary>
        /// Gets or sets the 1-based anchor row index (e.g., 1 for Row 1).
        /// </summary>
        public int RowIndex { get; set; }

        /// <summary>
        /// Gets or sets the target display width in pixels.
        /// </summary>
        public int WidthPx { get; set; }

        /// <summary>
        /// Gets or sets the target display height in pixels.
        /// </summary>
        public int HeightPx { get; set; }

        /// <summary>
        /// Gets or sets the horizontal offset within the anchor cell in pixels.
        /// </summary>
        public int ColOffsetPx { get; set; }

        /// <summary>
        /// Gets or sets the vertical offset within the anchor cell in pixels.
        /// </summary>
        public int RowOffsetPx { get; set; }

        /// <summary>
        /// Gets or sets the descriptive name for the picture object.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExcelImage"/> class.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="data"/> is null.</exception>
        /// <exception cref="NotSupportedException"><paramref name="format"/> is not png, jpg, jpeg, gif, bmp, tif, tiff, emf or wmf.</exception>
        public ExcelImage(byte[] data, string format, int columnIndex, int rowIndex, int widthPx, int heightPx, string? name = null)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            Format = string.IsNullOrWhiteSpace(format) ? "png" : format.Trim().TrimStart('.').ToLowerInvariant();
            if (ZeroDocuments.Excel.Internal.SpreadsheetXml.GetImageContentType(Format) == null)
            {
                throw new NotSupportedException(
                    $"Image format '{format}' is not supported. Use png, jpg, jpeg, gif, bmp, tif, tiff, emf or wmf.");
            }
            ColumnIndex = Math.Max(1, columnIndex);
            RowIndex = Math.Max(1, rowIndex);
            WidthPx = Math.Max(1, widthPx);
            HeightPx = Math.Max(1, heightPx);
            Name = string.IsNullOrWhiteSpace(name) ? "Picture" : name!.Trim();
        }
    }
}
