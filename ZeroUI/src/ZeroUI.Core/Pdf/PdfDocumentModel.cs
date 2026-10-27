using System;
using System.Collections.Generic;
using System.Text;

namespace ZeroUI.Core.Pdf
{
    /// <summary>
    /// Represents an in-memory parsed or generated multi-page vector PDF document.
    /// Provides zero-dependency document metadata, page collection, and full-text search index.
    /// </summary>
    public sealed class PdfDocumentModel
    {
        public string Title { get; set; } = "Technical Document";
        public string Author { get; set; } = "ZeroUI Engineering System";
        public string Subject { get; set; } = "Industrial CAD / SOP";
        public string Producer { get; set; } = "ZeroUI Vector PDF Engine";
        public string Version { get; set; } = "1.7";
        public List<PdfPageModel> Pages { get; } = new List<PdfPageModel>();
        public List<PdfBookmarkModel> Bookmarks { get; } = new List<PdfBookmarkModel>();

        public int PageCount => Pages.Count;

        public PdfPageModel? GetPage(int pageIndex)
        {
            if (pageIndex >= 0 && pageIndex < Pages.Count)
                return Pages[pageIndex];
            return null;
        }
    }

    /// <summary>
    /// Represents an individual page in a PDF document with physical dimensions, orientation, and vector primitives.
    /// </summary>
    public sealed class PdfPageModel
    {
        public int PageIndex { get; set; }
        public int PageNumber => PageIndex + 1;

        /// <summary>
        /// Page width in typographical points (1 pt = 1/72 inch). Standard A4 Portrait: 595.28 pt.
        /// </summary>
        public double Width { get; set; } = 595.28;

        /// <summary>
        /// Page height in typographical points (1 pt = 1/72 inch). Standard A4 Portrait: 841.89 pt.
        /// </summary>
        public double Height { get; set; } = 841.89;

        /// <summary>
        /// Clockwise page rotation angle in degrees (0, 90, 180, 270).
        /// </summary>
        public int Rotation { get; set; } = 0;

        /// <summary>
        /// All vector elements to render on this page.
        /// </summary>
        public List<PdfVectorElement> Elements { get; } = new List<PdfVectorElement>();

        /// <summary>
        /// Text tokens extracted from this page for keyword indexing and bounding-box hit testing.
        /// </summary>
        public List<PdfTextRun> TextRuns { get; } = new List<PdfTextRun>();

        private string? _cachedText;

        /// <summary>
        /// Full plain text representation of all text runs on the page.
        /// </summary>
        public string ExtractedText
        {
            get
            {
                if (_cachedText == null)
                {
                    var sb = new StringBuilder(TextRuns.Count * 16);
                    for (int i = 0; i < TextRuns.Count; i++)
                    {
                        sb.Append(TextRuns[i].Text).Append(' ');
                    }
                    _cachedText = sb.ToString();
                }
                return _cachedText;
            }
        }

        public void InvalidateTextCache() => _cachedText = null;
    }

    /// <summary>
    /// Type of vector element inside a PDF page.
    /// </summary>
    public enum PdfVectorElementType
    {
        Line,
        Rectangle,
        RoundedRectangle,
        Ellipse,
        Polygon,
        Path,
        Text,
        Symbol
    }

    /// <summary>
    /// Standard industrial electrical and mechanical schematic symbols.
    /// </summary>
    public enum PdfSymbolType
    {
        None,
        BreakerClosed,
        BreakerOpen,
        Motor3Phase,
        TransformerBay,
        ControlValve,
        PumpCentrifugal,
        EarthGround,
        WarningTriangle,
        QualityStamp
    }

    /// <summary>
    /// Represents an individual vector geometry or text drawing instruction inside a PDF page.
    /// </summary>
    public sealed class PdfVectorElement
    {
        public PdfVectorElementType ElementType { get; set; }

        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }

        // Secondary coordinate for lines
        public double X2 { get; set; }
        public double Y2 { get; set; }

        // Corner radius for rounded rectangles
        public double CornerRadius { get; set; }

        // Color representations in 32-bit ARGB (0xAARRGGBB)
        public uint StrokeColor { get; set; } = 0xFF1E293B; // Slate 800
        public uint FillColor { get; set; } = 0x00000000;   // Transparent
        public double StrokeWidth { get; set; } = 1.0;
        public bool IsStroked { get; set; } = true;
        public bool IsFilled { get; set; } = false;

        // Dash pattern (e.g. 4, 4 for dashed lines; null for solid)
        public float[]? DashPattern { get; set; }

        // Polygon / polyline point collections
        public List<PdfPoint>? Points { get; set; }

        // Text properties
        public string? Text { get; set; }
        public string FontFamily { get; set; } = "Segoe UI";
        public double FontSize { get; set; } = 11.0;
        public bool IsBold { get; set; }
        public bool IsItalic { get; set; }
        public uint TextColor { get; set; } = 0xFF0F172A;

        // Industrial vector symbol type
        public PdfSymbolType SymbolType { get; set; } = PdfSymbolType.None;
    }

    /// <summary>
    /// Represents a 2D vector coordinate point in PDF point space.
    /// </summary>
    public struct PdfPoint
    {
        public double X { get; set; }
        public double Y { get; set; }

        public PdfPoint(double x, double y)
        {
            X = x;
            Y = y;
        }
    }

    /// <summary>
    /// Positioned text run with bounding box for hit-testing and search highlights.
    /// </summary>
    public sealed class PdfTextRun
    {
        public string Text { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double FontSize { get; set; }
        public string FontFamily { get; set; }
        public uint Color { get; set; }

        public PdfTextRun(string text, double x, double y, double width, double height, double fontSize = 11.0, string fontFamily = "Segoe UI", uint color = 0xFF0F172A)
        {
            Text = text;
            X = x;
            Y = y;
            Width = width;
            Height = height;
            FontSize = fontSize;
            FontFamily = fontFamily;
            Color = color;
        }
    }

    /// <summary>
    /// Represents a document outline bookmark or table-of-contents entry pointing to a target page.
    /// </summary>
    public sealed class PdfBookmarkModel
    {
        public string Title { get; set; } = string.Empty;
        public int PageIndex { get; set; }
        public double YOffset { get; set; }
        public List<PdfBookmarkModel> Children { get; } = new List<PdfBookmarkModel>();

        public PdfBookmarkModel() { }

        public PdfBookmarkModel(string title, int pageIndex, double yOffset = 0)
        {
            Title = title;
            PageIndex = pageIndex;
            YOffset = yOffset;
        }
    }
}
