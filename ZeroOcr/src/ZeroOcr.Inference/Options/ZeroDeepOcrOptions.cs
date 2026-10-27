using System;
using ZeroOcr.Core.Interfaces;
using ZeroOcr.Core.Models;

namespace ZeroOcr.Inference.Options;

/// <summary>
/// Advanced operational options for the sovereign ZeroDeepOcrEngine.
/// </summary>
public sealed class ZeroDeepOcrOptions : OcrOptions
{
    /// <summary>
    /// Gets or sets the domain operational profile.
    /// </summary>
    public DeepOcrPreset Preset { get; set; } = DeepOcrPreset.Standard;

    /// <summary>
    /// Gets or sets the threshold for text detection probability map binarization.
    /// Default is 0.3f.
    /// </summary>
    public float DetectionThreshold { get; set; } = 0.3f;

    /// <summary>
    /// Gets or sets the polygon unclip expansion ratio for DBNet.
    /// Higher values expand the bounding quad to capture accent marks and ascenders/descenders.
    /// Default is 1.6f.
    /// </summary>
    public float UnclipRatio { get; set; } = 1.6f;

    /// <summary>
    /// Gets or sets a value indicating whether to run the orientation classifier (0° vs 180°).
    /// </summary>
    public bool EnableDirectionClassifier { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to apply morphological dot-matrix fusion prior to detection.
    /// If null, automatically determined by <see cref="Preset"/>.
    /// </summary>
    public bool? ApplyDotMatrixFusion { get; set; }

    /// <summary>
    /// Gets or sets the maximum image dimension for the detection network.
    /// Images are resized maintaining aspect ratio such that max dimension does not exceed this limit.
    /// Default is 960.
    /// </summary>
    public int MaxDetectionDimension { get; set; } = 960;

    /// <summary>
    /// Minimum consecutive blank CTC frames to trigger automatic word boundary space reconstruction.
    /// Set to 0 to disable automatic blank gap space insertion. Default is 8.
    /// </summary>
    public int BlankGapThreshold { get; set; } = 6;

    /// <summary>
    /// Gets or sets a value indicating whether to group and merge horizontally aligned text fragments on the same baseline.
    /// Default is true.
    /// </summary>
    public bool MergeHorizontalLines { get; set; } = true;

    /// <summary>
    /// Maximum vertical baseline offset relative to line height for horizontal line merging.
    /// Default is 0.45f.
    /// </summary>
    public float MaxBaselineOffsetRatio { get; set; } = 0.45f;

    /// <summary>
    /// Maximum horizontal gap between adjacent words relative to line height for horizontal line merging.
    /// Default is 3.0f.
    /// </summary>
    public float MaxHorizontalGapRatio { get; set; } = 3.0f;

    /// <summary>
    /// Creates options pre-configured for industrial date code and NSX/HSD inspection.
    /// </summary>
    public static ZeroDeepOcrOptions ForIndustrialPackaging(float minConfidence = 0.80f)
    {
        return new ZeroDeepOcrOptions
        {
            Preset = DeepOcrPreset.IndustrialDotMatrix,
            ApplyDotMatrixFusion = true,
            MinConfidence = minConfidence,
            DetectionThreshold = 0.25f,
            UnclipRatio = 1.8f
        };
    }

    /// <summary>
    /// Creates options pre-configured for Vietnamese document and invoice processing.
    /// </summary>
    public static ZeroDeepOcrOptions ForVietnameseDocument(float minConfidence = 0.85f)
    {
        return new ZeroDeepOcrOptions
        {
            Preset = DeepOcrPreset.VietnameseDocument,
            LanguageTag = "vi-VN",
            MinConfidence = minConfidence,
            DetectionThreshold = 0.30f,
            UnclipRatio = 1.8f,
            BlankGapThreshold = 6,
            MergeHorizontalLines = true
        };
    }
}
