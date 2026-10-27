using System;
using ZeroOcr.Core.Interfaces;

namespace ZeroOcr.Core.Pdf;

/// <summary>
/// Configuration options for the adaptive PDF OCR pipeline.
/// </summary>
public sealed class PdfOcrOptions
{
    /// <summary>
    /// Gets or sets the PDF processing mode (Auto, ForceOcr, or ExtractTextOnly).
    /// Default is Auto.
    /// </summary>
    public PdfOcrMode Mode { get; set; } = PdfOcrMode.Auto;

    /// <summary>
    /// Gets or sets the target rendering resolution in Dots Per Inch (DPI) when rasterizing scanned pages.
    /// Default is 250 DPI (optimal balance of OCR recognition accuracy and inference speed).
    /// </summary>
    public int RenderDpi { get; set; } = 250;

    /// <summary>
    /// Minimum count of meaningful characters required to qualify a page as native digital in Auto mode.
    /// If fewer characters are found, the page is classified as scanned and routed to deep OCR.
    /// Default is 30 characters.
    /// </summary>
    public int MinNativeCharsForFastPath { get; set; } = 30;

    /// <summary>
    /// Optional underlying OCR engine options (e.g. language tag, confidence threshold, line merging).
    /// </summary>
    public OcrOptions? EngineOptions { get; set; }

    /// <summary>
    /// Specific 0-based page indices to process. If null or empty, processes all pages in the document.
    /// </summary>
    public int[]? TargetPageIndices { get; set; }

    /// <summary>
    /// Maximum number of pages to process concurrently. Default is 2.
    /// </summary>
    public int MaxDegreeOfParallelism { get; set; } = 2;
}
