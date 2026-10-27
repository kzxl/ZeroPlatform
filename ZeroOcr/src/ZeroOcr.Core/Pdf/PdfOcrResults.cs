using System;
using System.Collections.Generic;
using System.Text;
using ZeroOcr.Core.Models;

namespace ZeroOcr.Core.Pdf;

/// <summary>
/// OCR and text extraction result for a single PDF page.
/// </summary>
public sealed class PdfPageOcrResult
{
    public int PageIndex { get; }
    public OcrResult Ocr { get; }
    public bool IsScannedPage { get; }
    public PdfExtractionSource ExtractionSource { get; }
    public TimeSpan Elapsed { get; }

    public string Text => Ocr.Text;
    public IReadOnlyList<OcrLine> Lines => Ocr.Lines;
    public float Confidence => Ocr.MeanConfidence;

    public PdfPageOcrResult(
        int pageIndex,
        OcrResult ocr,
        bool isScannedPage,
        PdfExtractionSource extractionSource,
        TimeSpan elapsed)
    {
        PageIndex = pageIndex;
        Ocr = ocr ?? throw new ArgumentNullException(nameof(ocr));
        IsScannedPage = isScannedPage;
        ExtractionSource = extractionSource;
        Elapsed = elapsed;
    }
}

/// <summary>
/// Comprehensive multi-page OCR extraction result for an entire PDF document.
/// </summary>
public sealed class PdfDocumentOcrResult
{
    public bool Success { get; }
    public IReadOnlyList<PdfPageOcrResult> Pages { get; }
    public int PageCount => Pages.Count;
    public TimeSpan Elapsed { get; }
    public string? ErrorMessage { get; }

    private string? _fullText;
    public string FullText
    {
        get
        {
            if (_fullText == null)
            {
                var sb = new StringBuilder();
                for (int i = 0; i < Pages.Count; i++)
                {
                    if (sb.Length > 0) sb.AppendLine();
                    sb.Append(Pages[i].Text);
                }
                _fullText = sb.ToString();
            }
            return _fullText;
        }
    }

    public PdfDocumentOcrResult(
        bool success,
        IReadOnlyList<PdfPageOcrResult> pages,
        TimeSpan elapsed,
        string? errorMessage = null)
    {
        Success = success;
        Pages = pages ?? Array.Empty<PdfPageOcrResult>();
        Elapsed = elapsed;
        ErrorMessage = errorMessage;
    }

    public static PdfDocumentOcrResult Failed(string message, TimeSpan elapsed) =>
        new(false, Array.Empty<PdfPageOcrResult>(), elapsed, message);
}
