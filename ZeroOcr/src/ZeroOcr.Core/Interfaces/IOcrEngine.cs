using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ZeroOcr.Core.Imaging;
using ZeroOcr.Core.Models;

namespace ZeroOcr.Core.Interfaces;

/// <summary>
/// Sovereign interface for OCR engine implementations across desktop, edge, and cloud.
/// </summary>
public interface IOcrEngine
{
    /// <summary>
    /// Friendly identifier or brand of the OCR engine (e.g. "Windows.Media.Ocr", "ZeroInference.Onnx", "Mock").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Indicates whether the engine is initialized and available on the current OS/runtime environment.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// List of available language tags (BCP-47) installed and supported by this engine.
    /// </summary>
    IReadOnlyList<string> SupportedLanguages { get; }

    /// <summary>
    /// Determines whether the specified language is installed and recognized.
    /// </summary>
    bool IsLanguageSupported(string languageTag);

    /// <summary>
    /// Performs asynchronous optical character recognition on an uncompressed image buffer.
    /// </summary>
    Task<OcrResult> RecognizeAsync(
        OcrImageBuffer image,
        OcrOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs asynchronous optical character recognition on encoded image bytes (PNG, JPEG, BMP).
    /// </summary>
    Task<OcrResult> RecognizeAsync(
        ReadOnlyMemory<byte> encodedImageBytes,
        OcrOptions? options = null,
        CancellationToken cancellationToken = default);
}
