using System;
using System.Threading;
using System.Threading.Tasks;
using ZeroOcr.Core.Imaging;

namespace ZeroOcr.Inference.Abstractions;

/// <summary>
/// Abstraction for text line direction classifier models.
/// Distinguishes between upright text (0°) and upside-down text (180°).
/// </summary>
public interface ITextDirectionClassifier : IDisposable
{
    /// <summary>
    /// Gets a value indicating whether the classifier model is loaded and ready for inference.
    /// </summary>
    bool IsReady { get; }

    /// <summary>
    /// Predicts the orientation angle of the text patch (returns 0 or 180 degrees).
    /// </summary>
    Task<int> ClassifyAngleAsync(OcrImageBuffer linePatch, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rotates the image patch 180 degrees in-place if an inverted angle is detected.
    /// </summary>
    void RectifyInPlace(OcrImageBuffer linePatch);
}
