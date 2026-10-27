using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ZeroOcr.Core.Imaging;
using ZeroOcr.Core.Models;

namespace ZeroOcr.Inference.Abstractions;

/// <summary>
/// Abstraction for deep learning text sequence recognition models (e.g., RepSVTR, CRNN, MobileNetV3-CTC).
/// Recognizes sequence text and authentic character-level/word-level confidence scores from an unskewed line patch.
/// </summary>
public interface ITextRecognizer : IDisposable
{
    /// <summary>
    /// Gets a value indicating whether the recognition model is loaded and ready for inference.
    /// </summary>
    bool IsReady { get; }

    /// <summary>
    /// Gets the list of supported BCP-47 language tags (e.g., "vi-VN", "en-US").
    /// </summary>
    IReadOnlyList<string> SupportedLanguages { get; }

    /// <summary>
    /// Recognizes text from a rectified line patch image.
    /// </summary>
    /// <param name="linePatch">The horizontal rectified image patch of the text line.</param>
    /// <param name="quad">The source 4-vertex quadrilateral in master image coordinates.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Recognized OcrLine with genuine confidence score, or null if below threshold.</returns>
    Task<OcrLine?> RecognizeLineAsync(
        OcrImageBuffer linePatch,
        OcrQuad quad,
        CancellationToken cancellationToken = default);
}
