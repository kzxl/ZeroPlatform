using System;
using System.Text;
using System.Text.RegularExpressions;

namespace ZeroOcr.Inference.PostProcessing;

/// <summary>
/// Industrial packaging & logistics lexicon processor.
/// Applies fuzzy Levenshtein alignment and date-code digit disambiguation (O->0, I->1, B->8).
/// </summary>
public static class IndustrialLexiconMatcher
{
    private static readonly string[] IndustrialPrefixes =
    {
        "HSD", "NSX", "EXP", "MFG", "LOT", "BATCH", "SERIAL", "S/N", "QTY", "VND", "USD"
    };

    /// <summary>
    /// Computes Levenshtein edit distance with zero heap allocation using stackalloc.
    /// </summary>
    public static int LevenshteinDistance(ReadOnlySpan<char> a, ReadOnlySpan<char> b)
    {
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;

        int lenA = a.Length;
        int lenB = b.Length;

        // Allocate row buffer on stack (max 128 chars for standard OCR words)
        Span<int> prevRow = stackalloc int[lenB + 1];
        Span<int> currRow = stackalloc int[lenB + 1];

        for (int j = 0; j <= lenB; j++) prevRow[j] = j;

        for (int i = 1; i <= lenA; i++)
        {
            currRow[0] = i;
            char charA = a[i - 1];

            for (int j = 1; j <= lenB; j++)
            {
                int cost = (charA == b[j - 1]) ? 0 : 1;
                currRow[j] = Math.Min(
                    Math.Min(currRow[j - 1] + 1, prevRow[j] + 1),
                    prevRow[j - 1] + cost);
            }

            currRow.CopyTo(prevRow);
        }

        return prevRow[lenB];
    }

    /// <summary>
    /// Disambiguates common visual OCR character collisions in numeric date/LOT sequences.
    /// Converts letter 'O'/'o' to '0', 'I'/'l' to '1', 'B' to '8' when flanked by numbers and slashes/dots/dashes.
    /// </summary>
    public static string DisambiguateDateCode(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;

        // Look for date-like sequences containing digits mixed with common letter substitutions
        // Example: "EXP: 2O26/I2/3I" -> "EXP: 2026/12/31"
        var sb = new StringBuilder(text.Length);

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (IsInDateNumericContext(text, i))
            {
                switch (c)
                {
                    case 'O':
                    case 'o':
                        sb.Append('0');
                        continue;
                    case 'I':
                    case 'l':
                    case '|':
                        sb.Append('1');
                        continue;
                    case 'Z':
                        sb.Append('2');
                        continue;
                    case 'S':
                    case 's':
                        sb.Append('5');
                        continue;
                    case 'B':
                        sb.Append('8');
                        continue;
                }
            }

            sb.Append(c);
        }

        return sb.ToString();
    }

    private static bool IsInDateNumericContext(string s, int index)
    {
        // Find word boundary around index
        int start = index;
        while (start > 0 && !char.IsWhiteSpace(s[start - 1])) start--;

        int end = index;
        while (end < s.Length && !char.IsWhiteSpace(s[end])) end++;

        // Must contain at least one date delimiter (/ or - or .)
        var word = s.AsSpan(start, end - start);
        bool hasSeparator = false;
        for (int i = 0; i < word.Length; i++)
        {
            char wc = word[i];
            if (wc == '/' || wc == '-' || wc == '.')
            {
                hasSeparator = true;
                break;
            }
        }

        if (!hasSeparator) return false;

        // Check if neighboring characters in the word are digits or date separators
        bool prevIsDateChar = (index > start) && (char.IsDigit(s[index - 1]) || s[index - 1] == '/' || s[index - 1] == '-' || s[index - 1] == '.' || s[index - 1] == ':');
        bool nextIsDateChar = (index < end - 1) && (char.IsDigit(s[index + 1]) || s[index + 1] == '/' || s[index + 1] == '-' || s[index + 1] == '.');

        return prevIsDateChar || nextIsDateChar;
    }

    /// <summary>
    /// Attempts fuzzy match against standard manufacturing keywords (NSX, HSD, EXP, LOT).
    /// </summary>
    public static string? TryFuzzyMatchPrefix(string word, int maxEditDistance = 1)
    {
        if (string.IsNullOrEmpty(word)) return null;

        string cleanWord = word.Trim().TrimEnd(':', '.', '-').ToUpperInvariant();

        foreach (var prefix in IndustrialPrefixes)
        {
            if (LevenshteinDistance(cleanWord.AsSpan(), prefix.AsSpan()) <= maxEditDistance)
            {
                return prefix;
            }
        }

        return null;
    }
}
