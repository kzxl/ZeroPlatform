using System;

namespace ZeroOcr.Core.Pdf;

/// <summary>
/// Operational mode for processing PDF documents in OCR pipelines.
/// </summary>
public enum PdfOcrMode
{
    /// <summary>
    /// Adaptive smart branching: automatically extracts native vector text directly if available (fast-path ~2ms),
    /// and seamlessly falls back to deep OCR rasterization for scanned/image pages.
    /// </summary>
    Auto,

    /// <summary>
    /// Enforces rasterizing every page into an image and executing the deep OCR pipeline.
    /// Recommended when native PDF text contains corrupted font encodings (CID/ToUnicode map errors) or when inspecting stamped/signed documents.
    /// </summary>
    ForceOcr,

    /// <summary>
    /// Only extracts native vector text. Skips rasterization and OCR entirely on scanned pages.
    /// </summary>
    ExtractTextOnly
}

/// <summary>
/// Indicates the method utilized to extract text from a specific PDF page.
/// </summary>
public enum PdfExtractionSource
{
    /// <summary>
    /// Extracted directly from native PDF content streams and embedded font glyphs (100% precision, zero GPU load).
    /// </summary>
    NativeDigitalFastPath,

    /// <summary>
    /// Rasterized to an image buffer and recognized via deep neural OCR (DBNet + SVTR).
    /// </summary>
    RasterizedDeepOcr,

    /// <summary>
    /// Hybrid composite: native digital text combined with deep OCR on embedded raster images/stamps.
    /// </summary>
    HybridComposite
}
