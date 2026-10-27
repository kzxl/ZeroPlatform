using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ZeroOcr.Core.Imaging;
using ZeroOcr.Core.Interfaces;
using ZeroOcr.Core.Models;

namespace ZeroOcr.Core.Engines;

/// <summary>
/// A mock OCR engine useful for unit testing, integration tests, and headless environments.
/// </summary>
public sealed class MockOcrEngine : IOcrEngine
{
    private readonly Func<OcrImageBuffer, OcrOptions?, OcrResult>? _responseFactory;

    public string Name => "MockOcrEngine";
    public bool IsAvailable { get; set; } = true;
    public List<string> Languages { get; } = new() { "en-US", "vi-VN" };
    public IReadOnlyList<string> SupportedLanguages => Languages;

    public MockOcrEngine(Func<OcrImageBuffer, OcrOptions?, OcrResult>? responseFactory = null)
    {
        _responseFactory = responseFactory;
    }

    public bool IsLanguageSupported(string languageTag)
    {
        for (int i = 0; i < Languages.Count; i++)
        {
            if (string.Equals(Languages[i], languageTag, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public Task<OcrResult> RecognizeAsync(
        OcrImageBuffer image,
        OcrOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!IsAvailable)
            return Task.FromResult(OcrResult.Failed("Mock OCR engine is unavailable."));

        if (options?.RegionOfInterest is { } roi && !roi.IsEmpty)
        {
            image = image.Crop(roi);
        }

        if (_responseFactory != null)
        {
            return Task.FromResult(_responseFactory(image, options));
        }

        var word1 = new OcrWord("MOCK", new OcrRect(10, 10, 50, 20), 0.99f);
        var word2 = new OcrWord("TEXT", new OcrRect(70, 10, 50, 20), 0.98f);
        var line = new OcrLine("MOCK TEXT", new[] { word1, word2 });
        var result = OcrResult.Create(new[] { line }, TimeSpan.FromMilliseconds(5), options?.LanguageTag ?? "en-US");

        return Task.FromResult(result);
    }

    public Task<OcrResult> RecognizeAsync(
        ReadOnlyMemory<byte> encodedImageBytes,
        OcrOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!IsAvailable)
            return Task.FromResult(OcrResult.Failed("Mock OCR engine is unavailable."));

        var word = new OcrWord("ENCODED_MOCK", new OcrRect(0, 0, 100, 20), 0.95f);
        var line = new OcrLine("ENCODED_MOCK", new[] { word });
        var result = OcrResult.Create(new[] { line }, TimeSpan.FromMilliseconds(5), options?.LanguageTag ?? "en-US");

        return Task.FromResult(result);
    }
}
