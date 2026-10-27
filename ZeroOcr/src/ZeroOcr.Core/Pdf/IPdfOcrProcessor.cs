using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ZeroOcr.Core.Imaging;

namespace ZeroOcr.Core.Pdf;

/// <summary>
/// Abstraction for PDF page rasterization into in-memory OCR image buffers.
/// Implementations may leverage Windows.Data.Pdf (Windows native), PDFium (cross-platform), or pure C# rasterizers.
/// </summary>
public interface IPdfPageRenderer : IDisposable
{
    /// <summary>
    /// Gets a value indicating whether this renderer is operational on the current operating system.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Returns the total number of pages in the given PDF document.
    /// </summary>
    Task<int> GetPageCountAsync(Stream pdfStream, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rasterizes a specific PDF page into an uncompressed, zero-overhead OcrImageBuffer at the requested DPI.
    /// </summary>
    /// <param name="pdfStream">Input PDF stream.</param>
    /// <param name="pageIndex">0-based page index.</param>
    /// <param name="dpi">Rendering resolution (default 250).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Rendered OcrImageBuffer in 32-bit BGRA format.</returns>
    Task<OcrImageBuffer> RenderPageAsync(
        Stream pdfStream,
        int pageIndex,
        int dpi = 250,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Unified interface for processing multi-page PDF documents through the adaptive hybrid OCR pipeline.
/// </summary>
public interface IPdfOcrProcessor
{
    /// <summary>
    /// Processes a PDF stream using the configured adaptive fast-path / deep-path routing.
    /// </summary>
    Task<PdfDocumentOcrResult> ProcessPdfAsync(
        Stream pdfStream,
        PdfOcrOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes a local PDF file from disk.
    /// </summary>
    Task<PdfDocumentOcrResult> ProcessPdfAsync(
        string filePath,
        PdfOcrOptions? options = null,
        CancellationToken cancellationToken = default);
}
