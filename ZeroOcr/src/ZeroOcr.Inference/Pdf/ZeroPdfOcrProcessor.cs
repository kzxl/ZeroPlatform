using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ZeroOcr.Core.Imaging;
using ZeroOcr.Core.Interfaces;
using ZeroOcr.Core.Models;
using ZeroOcr.Core.Pdf;

namespace ZeroOcr.Inference.Pdf;

/// <summary>
/// Sovereign adaptive PDF OCR processor implementing the industrial two-tier hybrid architecture.
/// Combines microsecond native digital text extraction (fast-path ~2ms) with deep neural OCR (deep-path)
/// for scanned, image-only, or corrupted PDF pages.
/// </summary>
public sealed class ZeroPdfOcrProcessor : IPdfOcrProcessor
{
    private readonly IOcrEngine _ocrEngine;
    private readonly IPdfPageRenderer? _renderer;

    public ZeroPdfOcrProcessor(IOcrEngine ocrEngine, IPdfPageRenderer? renderer = null)
    {
        _ocrEngine = ocrEngine ?? throw new ArgumentNullException(nameof(ocrEngine));
        _renderer = renderer;
    }

    public async Task<PdfDocumentOcrResult> ProcessPdfAsync(
        string filePath,
        PdfOcrOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("PDF file not found.", filePath);

        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return await ProcessPdfAsync(fs, options, cancellationToken).ConfigureAwait(false);
    }

    public async Task<PdfDocumentOcrResult> ProcessPdfAsync(
        Stream pdfStream,
        PdfOcrOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (pdfStream == null) throw new ArgumentNullException(nameof(pdfStream));
        var sw = Stopwatch.StartNew();
        options ??= new PdfOcrOptions();

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            // 1. Read stream into byte buffer for stream inspection
            byte[] pdfBytes;
            if (pdfStream is MemoryStream ms)
            {
                pdfBytes = ms.ToArray();
            }
            else
            {
                using var copyMs = new MemoryStream();
                await pdfStream.CopyToAsync(copyMs).ConfigureAwait(false);
                pdfBytes = copyMs.ToArray();
            }

            // 2. Discover total pages
            int totalPages = await ResolvePageCountAsync(pdfBytes, cancellationToken).ConfigureAwait(false);
            if (totalPages <= 0)
                totalPages = 1;

            var pageResults = new List<PdfPageOcrResult>(totalPages);

            // Determine target pages to process
            var targetPages = options.TargetPageIndices ?? GetSequentialPages(totalPages);

            foreach (int pageIndex in targetPages)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (pageIndex < 0 || pageIndex >= totalPages) continue;

                var pageSw = Stopwatch.StartNew();

                // 3. Adaptive Branching
                bool tryFastPath = options.Mode != PdfOcrMode.ForceOcr;
                bool fastPathSucceeded = false;
                PdfPageOcrResult? pageResult = null;

                if (tryFastPath)
                {
                    // Attempt fast-path: inspect native text stream
                    int charCount = PdfStreamTextExtractor.CountApproximateNativeCharacters(pdfBytes);
                    if (charCount >= options.MinNativeCharsForFastPath)
                    {
                        // Extract lines
                        PdfStreamTextExtractor.ExtractTextFromDecodedStream(pdfBytes, out var lines);
                        if (lines.Count > 0)
                        {
                            var ocrLines = new List<OcrLine>(lines.Count);
                            for (int li = 0; li < lines.Count; li++)
                            {
                                var rect = new OcrRect(0, li * 20, 600, 20);
                                ocrLines.Add(new OcrLine(lines[li], Array.Empty<OcrWord>(), rect, 1.0f));
                            }

                            pageSw.Stop();
                            var nativeOcr = OcrResult.Create(ocrLines, pageSw.Elapsed, options.EngineOptions?.LanguageTag);
                            pageResult = new PdfPageOcrResult(
                                pageIndex,
                                nativeOcr,
                                isScannedPage: false,
                                PdfExtractionSource.NativeDigitalFastPath,
                                pageSw.Elapsed);

                            fastPathSucceeded = true;
                        }
                    }
                }

                // 4. Fallback to Deep OCR (Rasterization + Deep Perception)
                if (!fastPathSucceeded && options.Mode != PdfOcrMode.ExtractTextOnly)
                {
                    if (_renderer == null || !_renderer.IsAvailable)
                    {
                        // In environments without rasterizer, return fast-path message or graceful indicator
                        pageSw.Stop();
                        var dummyResult = OcrResult.Failed(
                            "Scanned PDF page requires an IPdfPageRenderer (such as WindowsPdfPageRenderer). No renderer registered.",
                            pageSw.Elapsed);

                        pageResult = new PdfPageOcrResult(
                            pageIndex,
                            dummyResult,
                            isScannedPage: true,
                            PdfExtractionSource.RasterizedDeepOcr,
                            pageSw.Elapsed);
                    }
                    else
                    {
                        using var memStream = new MemoryStream(pdfBytes);
                        using var renderedPage = await _renderer.RenderPageAsync(
                            memStream,
                            pageIndex,
                            options.RenderDpi,
                            cancellationToken).ConfigureAwait(false);

                        var deepResult = await _ocrEngine.RecognizeAsync(
                            renderedPage,
                            options.EngineOptions,
                            cancellationToken).ConfigureAwait(false);

                        pageSw.Stop();
                        pageResult = new PdfPageOcrResult(
                            pageIndex,
                            deepResult,
                            isScannedPage: true,
                            PdfExtractionSource.RasterizedDeepOcr,
                            pageSw.Elapsed);
                    }
                }

                if (pageResult != null)
                {
                    pageResults.Add(pageResult);
                }
            }

            sw.Stop();
            return new PdfDocumentOcrResult(true, pageResults, sw.Elapsed);
        }
        catch (OperationCanceledException)
        {
            return PdfDocumentOcrResult.Failed("PDF OCR processing was canceled.", sw.Elapsed);
        }
        catch (Exception ex)
        {
            return PdfDocumentOcrResult.Failed($"PDF processing failed: {ex.Message}", sw.Elapsed);
        }
    }

    private async Task<int> ResolvePageCountAsync(byte[] pdfBytes, CancellationToken cancellationToken)
    {
        if (_renderer != null && _renderer.IsAvailable)
        {
            using var ms = new MemoryStream(pdfBytes);
            return await _renderer.GetPageCountAsync(ms, cancellationToken).ConfigureAwait(false);
        }

        // Pure C# page count estimation from PDF trailer/tree
        string ascii = Encoding.ASCII.GetString(pdfBytes);
        int pageCount = 0;
        int idx = 0;

        while ((idx = ascii.IndexOf("/Type /Page", idx, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            // Verify it is not /Pages
            int after = idx + 11;
            if (after < ascii.Length && ascii[after] != 's' && ascii[after] != 'S')
            {
                pageCount++;
            }
            idx += 11;
        }

        return Math.Max(1, pageCount);
    }

    private static int[] GetSequentialPages(int count)
    {
        var arr = new int[count];
        for (int i = 0; i < count; i++) arr[i] = i;
        return arr;
    }
}
