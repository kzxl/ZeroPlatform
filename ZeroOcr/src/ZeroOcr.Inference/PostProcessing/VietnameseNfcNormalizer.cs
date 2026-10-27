using System;
using System.Text;

namespace ZeroOcr.Inference.PostProcessing;

/// <summary>
/// Normalizes recognized Vietnamese strings to standard Unicode Normalization Form C (NFC).
/// Prevents disintegrated combining accent marks that cause database lookup failures and equality mismatches.
/// </summary>
public static class VietnameseNfcNormalizer
{
    /// <summary>
    /// Normalizes text into canonical Unicode NFC and cleans common optical artifacts.
    /// </summary>
    public static string Normalize(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        // 1. Canonical Unicode Normalization Form C
        string normalized = input.Normalize(NormalizationForm.FormC);

        // 2. Fix known OCR space artifacts around diacritics
        if (normalized.Contains(" `") || normalized.Contains(" ~") || normalized.Contains(" ^"))
        {
            normalized = normalized
                .Replace(" `", "`")
                .Replace(" ~", "~")
                .Replace(" ^", "^");
        }

        return normalized.Trim();
    }
}
