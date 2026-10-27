using System.Collections.Generic;
using ZeroOcr.Core.Models;

namespace ZeroOcr.Core.Interfaces;

/// <summary>
/// Configuration options passed to an OCR engine for recognition.
/// </summary>
public class OcrOptions
{
    /// <summary>
    /// BCP-47 language tag (e.g. "en-US", "vi-VN", "zh-Hans-CN").
    /// If null or empty, the engine uses the default system or model language.
    /// </summary>
    public string? LanguageTag { get; set; }

    /// <summary>
    /// Optional sub-region of interest (ROI). Only text within this box will be recognized.
    /// </summary>
    public OcrRect? RegionOfInterest { get; set; }

    /// <summary>
    /// Minimum recognition confidence threshold (0.0 to 1.0).
    /// Words below this score may be filtered out.
    /// </summary>
    public float MinConfidence { get; set; } = 0.0f;

    /// <summary>
    /// Whether to automatically detect text orientation and correct skew.
    /// </summary>
    public bool DetectOrientation { get; set; } = true;

    /// <summary>
    /// Engine-specific custom key-value configuration parameters.
    /// </summary>
    public IDictionary<string, object>? CustomProperties { get; set; }
}
