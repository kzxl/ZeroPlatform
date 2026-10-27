using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;
using ZeroOcr.Core.Imaging;
using ZeroOcr.Core.Interfaces;
using ZeroOcr.Core.Models;
using OcrResult = ZeroOcr.Core.Models.OcrResult;
using OcrLine = ZeroOcr.Core.Models.OcrLine;
using OcrWord = ZeroOcr.Core.Models.OcrWord;
using WinOcrEngine = Windows.Media.Ocr.OcrEngine;
using WinOcrResult = Windows.Media.Ocr.OcrResult;
using WinOcrLine = Windows.Media.Ocr.OcrLine;
using WinOcrWord = Windows.Media.Ocr.OcrWord;

namespace ZeroOcr.Windows;

/// <summary>
/// Sovereign Windows 10/11 native OCR engine using Windows.Media.Ocr.
/// Requires no external neural network files or third-party dependencies.
/// </summary>
public sealed class WindowsOcrEngine : IOcrEngine
{
    private readonly ConcurrentDictionary<string, WinOcrEngine?> _engineCache = new(StringComparer.OrdinalIgnoreCase);

    public string Name => "Windows.Media.Ocr";

    public bool IsAvailable => WinOcrEngine.AvailableRecognizerLanguages.Count > 0;

    public IReadOnlyList<string> SupportedLanguages =>
        WinOcrEngine.AvailableRecognizerLanguages
            .Select(l => l.LanguageTag)
            .ToList();

    public bool IsLanguageSupported(string languageTag)
    {
        if (string.IsNullOrWhiteSpace(languageTag)) return false;
        try
        {
            var lang = new global::Windows.Globalization.Language(languageTag);
            return WinOcrEngine.IsLanguageSupported(lang);
        }
        catch
        {
            return false;
        }
    }

    public async Task<OcrResult> RecognizeAsync(
        OcrImageBuffer image,
        OcrOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsAvailable)
                return OcrResult.Failed("Windows OCR is not available on this system.", sw.Elapsed);

            // Apply Region Of Interest (ROI) if specified
            if (options?.RegionOfInterest is { } roi && !roi.IsEmpty)
            {
                image = image.Crop(roi);
            }

            var engine = GetEngine(options?.LanguageTag);
            if (engine == null)
            {
                return OcrResult.Failed(
                    $"No Windows OCR engine could be initialized for language '{options?.LanguageTag ?? "Default"}'.",
                    sw.Elapsed);
            }

            using var softwareBitmap = ConvertToSoftwareBitmap(image);
            cancellationToken.ThrowIfCancellationRequested();

            var winResult = await engine.RecognizeAsync(softwareBitmap).AsTask(cancellationToken);
            sw.Stop();

            return MapWinResult(winResult, sw.Elapsed, options?.LanguageTag, options?.MinConfidence ?? 0.0f);
        }
        catch (OperationCanceledException)
        {
            return OcrResult.Failed("OCR operation was canceled.", sw.Elapsed);
        }
        catch (Exception ex)
        {
            return OcrResult.Failed($"Windows OCR failed: {ex.Message}", sw.Elapsed);
        }
    }

    public async Task<OcrResult> RecognizeAsync(
        ReadOnlyMemory<byte> encodedImageBytes,
        OcrOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsAvailable)
                return OcrResult.Failed("Windows OCR is not available on this system.", sw.Elapsed);

            var engine = GetEngine(options?.LanguageTag);
            if (engine == null)
            {
                return OcrResult.Failed(
                    $"No Windows OCR engine could be initialized for language '{options?.LanguageTag ?? "Default"}'.",
                    sw.Elapsed);
            }

            using var randomAccessStream = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter(randomAccessStream.GetOutputStreamAt(0)))
            {
                writer.WriteBytes(encodedImageBytes.ToArray());
                await writer.StoreAsync().AsTask(cancellationToken);
            }

            var decoder = await BitmapDecoder.CreateAsync(randomAccessStream).AsTask(cancellationToken);
            using var softwareBitmap = await decoder.GetSoftwareBitmapAsync().AsTask(cancellationToken);

            var winResult = await engine.RecognizeAsync(softwareBitmap).AsTask(cancellationToken);
            sw.Stop();

            return MapWinResult(winResult, sw.Elapsed, options?.LanguageTag, options?.MinConfidence ?? 0.0f);
        }
        catch (OperationCanceledException)
        {
            return OcrResult.Failed("OCR operation was canceled.", sw.Elapsed);
        }
        catch (Exception ex)
        {
            return OcrResult.Failed($"Windows OCR failed: {ex.Message}", sw.Elapsed);
        }
    }

    private WinOcrEngine? GetEngine(string? languageTag)
    {
        var key = languageTag?.Trim() ?? string.Empty;

        return _engineCache.GetOrAdd(key, static k =>
        {
            if (!string.IsNullOrEmpty(k))
            {
                var lang = new global::Windows.Globalization.Language(k);
                if (WinOcrEngine.IsLanguageSupported(lang))
                {
                    var fromLang = WinOcrEngine.TryCreateFromLanguage(lang);
                    if (fromLang != null)
                        return fromLang;
                }
            }

            return WinOcrEngine.TryCreateFromUserProfileLanguages();
        });
    }

    private static SoftwareBitmap ConvertToSoftwareBitmap(OcrImageBuffer image)
    {
        int width = image.Width;
        int height = image.Height;

        // If not BGRA32, normalize to BGRA32 for Windows.Media.Ocr
        byte[] bgraBytes;
        if (image.Format == OcrPixelFormat.Bgra32)
        {
            bgraBytes = image.Memory.ToArray();
        }
        else
        {
            // Convert to Bgra32
            int stride = width * 4;
            bgraBytes = new byte[stride * height];
            var srcSpan = image.Span;

            for (int y = 0; y < height; y++)
            {
                int srcRow = y * image.Stride;
                int dstRow = y * stride;

                for (int x = 0; x < width; x++)
                {
                    int dstOffset = dstRow + (x * 4);
                    switch (image.Format)
                    {
                        case OcrPixelFormat.Gray8:
                            byte g = srcSpan[srcRow + x];
                            bgraBytes[dstOffset] = g;
                            bgraBytes[dstOffset + 1] = g;
                            bgraBytes[dstOffset + 2] = g;
                            bgraBytes[dstOffset + 3] = 255;
                            break;
                        case OcrPixelFormat.Rgb24:
                            int rgbOffset = srcRow + (x * 3);
                            bgraBytes[dstOffset] = srcSpan[rgbOffset + 2];     // B
                            bgraBytes[dstOffset + 1] = srcSpan[rgbOffset + 1]; // G
                            bgraBytes[dstOffset + 2] = srcSpan[rgbOffset];     // R
                            bgraBytes[dstOffset + 3] = 255;
                            break;
                        case OcrPixelFormat.Bgr24:
                            int bgrOffset = srcRow + (x * 3);
                            bgraBytes[dstOffset] = srcSpan[bgrOffset];         // B
                            bgraBytes[dstOffset + 1] = srcSpan[bgrOffset + 1]; // G
                            bgraBytes[dstOffset + 2] = srcSpan[bgrOffset + 2]; // R
                            bgraBytes[dstOffset + 3] = 255;
                            break;
                        case OcrPixelFormat.Rgba32:
                            int rgbaOffset = srcRow + (x * 4);
                            bgraBytes[dstOffset] = srcSpan[rgbaOffset + 2];     // B
                            bgraBytes[dstOffset + 1] = srcSpan[rgbaOffset + 1]; // G
                            bgraBytes[dstOffset + 2] = srcSpan[rgbaOffset];     // R
                            bgraBytes[dstOffset + 3] = srcSpan[rgbaOffset + 3]; // A
                            break;
                    }
                }
            }
        }

        return SoftwareBitmap.CreateCopyFromBuffer(
            bgraBytes.AsBuffer(),
            BitmapPixelFormat.Bgra8,
            width,
            height,
            BitmapAlphaMode.Premultiplied);
    }

    private static OcrResult MapWinResult(
        WinOcrResult winResult,
        TimeSpan elapsed,
        string? languageTag,
        float minConfidence)
    {
        var lines = new List<OcrLine>();
        float? angle = winResult.TextAngle.HasValue ? (float)winResult.TextAngle.Value : null;

        foreach (var winLine in winResult.Lines)
        {
            var words = new List<OcrWord>();
            foreach (var winWord in winLine.Words)
            {
                var r = winWord.BoundingRect;
                var rect = new OcrRect((float)r.X, (float)r.Y, (float)r.Width, (float)r.Height);

                // Windows.Media.Ocr does not expose per-word confidence float, default to 0.95f
                float conf = 0.95f;
                if (conf >= minConfidence)
                {
                    words.Add(new OcrWord(winWord.Text, rect, conf, OcrQuad.FromRect(rect)));
                }
            }

            if (words.Count > 0)
            {
                lines.Add(new OcrLine(winLine.Text, words, angleDegrees: angle));
            }
        }

        return OcrResult.Create(lines, elapsed, languageTag);
    }
}
