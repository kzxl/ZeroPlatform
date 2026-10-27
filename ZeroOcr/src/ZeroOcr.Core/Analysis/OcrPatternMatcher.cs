using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using ZeroOcr.Core.Models;

namespace ZeroOcr.Core.Analysis;

/// <summary>
/// A matched text token containing spatial location, raw text, and parsed value.
/// </summary>
public sealed class OcrPatternMatch<T>
{
    public string RawText { get; }
    public T Value { get; }
    public OcrRect BoundingBox { get; }
    public float Confidence { get; }

    public OcrPatternMatch(string rawText, T value, OcrRect boundingBox, float confidence)
    {
        RawText = rawText;
        Value = value;
        BoundingBox = boundingBox;
        Confidence = confidence;
    }

    public override string ToString() => $"\"{RawText}\" -> {Value} at {BoundingBox}";
}

/// <summary>
/// Sovereign pattern matcher for industrial dates, lot codes, tax identification numbers, and numerical amounts.
/// </summary>
public static class OcrPatternMatcher
{
    private static readonly Regex DateRegex = new(
        @"\b(\d{1,2}[\/\-\.]\d{1,2}[\/\-\.]\d{4}|\d{4}[\/\-\.]\d{1,2}[\/\-\.]\d{1,2})\b",
        RegexOptions.Compiled);

    private static readonly Regex TaxIdRegex = new(
        @"\b(\d{10}(?:-\d{3})?)\b",
        RegexOptions.Compiled);

    private static readonly Regex AmountRegex = new(
        @"(?:[\$₫€£]\s*)?(\d{1,3}(?:[.,]\d{3})*(?:[.,]\d{1,2})?)\s*(?:VND|VNĐ|USD|EUR|₫|\$)?\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Finds all calendar dates formatted as dd/MM/yyyy, yyyy-MM-dd, etc.
    /// </summary>
    public static IReadOnlyList<OcrPatternMatch<DateTime>> FindDates(OcrResult result)
    {
        var matches = new List<OcrPatternMatch<DateTime>>();
        string[] formats = { "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "yyyy/MM/dd", "dd-MM-yyyy", "dd.MM.yyyy" };

        foreach (var line in result.Lines)
        {
            var rxMatches = DateRegex.Matches(line.Text);
            foreach (Match m in rxMatches)
            {
                if (DateTime.TryParseExact(m.Value, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                {
                    var bounds = EstimateSubStringBounds(line, m.Index, m.Length);
                    matches.Add(new OcrPatternMatch<DateTime>(m.Value, dt, bounds, line.Confidence));
                }
            }
        }

        return matches;
    }

    /// <summary>
    /// Finds all 10-digit or 13-digit Enterprise Tax Identification Numbers (MST).
    /// </summary>
    public static IReadOnlyList<OcrPatternMatch<string>> FindTaxIds(OcrResult result)
    {
        var matches = new List<OcrPatternMatch<string>>();

        foreach (var line in result.Lines)
        {
            var rxMatches = TaxIdRegex.Matches(line.Text);
            foreach (Match m in rxMatches)
            {
                var bounds = EstimateSubStringBounds(line, m.Index, m.Length);
                matches.Add(new OcrPatternMatch<string>(m.Value, m.Value, bounds, line.Confidence));
            }
        }

        return matches;
    }

    /// <summary>
    /// Finds custom LOT/Batch numbers matching the given prefix (default: LOT, BATCH, PO, SO).
    /// </summary>
    public static IReadOnlyList<OcrPatternMatch<string>> FindLotCodes(
        OcrResult result,
        string prefixPattern = @"(?:LOT|BATCH|PO|SO|SER|SN)")
    {
        var regex = new Regex($@"\b({prefixPattern}[:\s\-_#]*[A-Za-z0-9\-]+)\b", RegexOptions.IgnoreCase);
        var matches = new List<OcrPatternMatch<string>>();

        foreach (var line in result.Lines)
        {
            var rxMatches = regex.Matches(line.Text);
            foreach (Match m in rxMatches)
            {
                var bounds = EstimateSubStringBounds(line, m.Index, m.Length);
                matches.Add(new OcrPatternMatch<string>(m.Value, m.Value, bounds, line.Confidence));
            }
        }

        return matches;
    }

    private static OcrRect EstimateSubStringBounds(OcrLine line, int charIndex, int length)
    {
        if (line.Words.Count == 0 || line.Text.Length == 0)
            return line.BoundingBox;

        // Find words spanning within [charIndex, charIndex + length]
        int currentPos = 0;
        OcrRect combined = OcrRect.Empty;

        foreach (var word in line.Words)
        {
            int wordStart = line.Text.IndexOf(word.Text, currentPos, StringComparison.OrdinalIgnoreCase);
            if (wordStart < 0) wordStart = currentPos;
            int wordEnd = wordStart + word.Text.Length;
            currentPos = wordEnd;

            // Check overlap
            if (wordEnd > charIndex && wordStart < charIndex + length)
            {
                combined = combined.Union(word.BoundingBox);
            }
        }

        return combined.IsEmpty ? line.BoundingBox : combined;
    }
}
