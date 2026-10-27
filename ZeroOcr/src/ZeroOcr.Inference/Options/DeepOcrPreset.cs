namespace ZeroOcr.Inference.Options;

/// <summary>
/// Pre-configured operational profiles for sovereign deep OCR execution.
/// </summary>
public enum DeepOcrPreset
{
    /// <summary>
    /// Standard general-purpose recognition for documents, receipts, and signs.
    /// </summary>
    Standard,

    /// <summary>
    /// Industrial manufacturing preset: activates morphological dot-matrix bridging
    /// and strict date/LOT regex pattern enforcement for packaging inspection.
    /// </summary>
    IndustrialDotMatrix,

    /// <summary>
    /// High-accuracy Vietnamese document reading: ensures comprehensive 134-glyph
    /// vocabulary with automatic Unicode NFC tone-mark normalization.
    /// </summary>
    VietnameseDocument,

    /// <summary>
    /// Specialized 7-segment LED/LCD digital instrument meter reading.
    /// Constrains dictionary to numerals, decimal points, and measurement symbols.
    /// </summary>
    DigitalDisplay
}
