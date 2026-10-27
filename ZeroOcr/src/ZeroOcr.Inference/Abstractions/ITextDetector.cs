using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ZeroOcr.Core.Imaging;
using ZeroOcr.Core.Models;

namespace ZeroOcr.Inference.Abstractions;

/// <summary>
/// Abstraction for deep learning text detection models (e.g., DBNet, DBNet++).
/// Detects arbitrarily-oriented quadrilateral text bounding regions in an image.
/// </summary>
public interface ITextDetector : IDisposable
{
    /// <summary>
    /// Gets a value indicating whether the detector model is loaded and ready for inference.
    /// </summary>
    bool IsReady { get; }

    /// <summary>
    /// Detects oriented text bounding quadrilaterals from the input image buffer.
    /// </summary>
    /// <param name="image">Input raw image buffer.</param>
    /// <param name="minConfidence">Minimum probability threshold for text detection (typically 0.3 - 0.5).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of detected oriented 4-vertex bounding quadrilaterals.</returns>
    Task<IReadOnlyList<OcrQuad>> DetectQuadsAsync(
        OcrImageBuffer image,
        float minConfidence = 0.3f,
        CancellationToken cancellationToken = default);
}
