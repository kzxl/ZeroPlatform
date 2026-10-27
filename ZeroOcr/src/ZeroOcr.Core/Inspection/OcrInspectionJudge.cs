using System;
using System.Text.RegularExpressions;
using ZeroOcr.Core.Models;

namespace ZeroOcr.Core.Inspection;

/// <summary>
/// Verdict of an industrial OCR inspection evaluation.
/// </summary>
public sealed class OcrInspectionVerdict
{
    public bool IsPassed { get; }
    public string InspectionName { get; }
    public string Expected { get; }
    public string Actual { get; }
    public string Message { get; }
    public float Confidence { get; }
    public OcrRect? BoundingBox { get; }

    public OcrInspectionVerdict(
        bool isPassed,
        string inspectionName,
        string expected,
        string actual,
        string message,
        float confidence = 1.0f,
        OcrRect? boundingBox = null)
    {
        IsPassed = isPassed;
        InspectionName = inspectionName;
        Expected = expected;
        Actual = actual;
        Message = message;
        Confidence = confidence;
        BoundingBox = boundingBox;
    }

    public static OcrInspectionVerdict Pass(string name, string actual, string expected, float confidence, OcrRect? box = null) =>
        new(true, name, expected, actual, "OK - Inspection criteria satisfied.", confidence, box);

    public static OcrInspectionVerdict Fail(string name, string actual, string expected, string reason, float confidence, OcrRect? box = null) =>
        new(false, name, expected, actual, $"NG - {reason}", confidence, box);

    public override string ToString() => $"[{(IsPassed ? "PASS" : "FAIL")}] {InspectionName}: {Actual} (Exp: {Expected}) -> {Message}";
}

/// <summary>
/// Industrial evaluation judge for automated inline OCR inspection (Lot, Expiry Date, Serial verification).
/// </summary>
public static class OcrInspectionJudge
{
    /// <summary>
    /// Judges whether any recognized line contains an exact or partial expected string.
    /// </summary>
    public static OcrInspectionVerdict JudgeSubstring(
        OcrResult result,
        string expectedSubstring,
        string inspectionName = "TextPresenceCheck",
        bool caseSensitive = false)
    {
        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        foreach (var line in result.Lines)
        {
            if (line.Text.IndexOf(expectedSubstring, comparison) >= 0)
            {
                return OcrInspectionVerdict.Pass(inspectionName, line.Text, expectedSubstring, line.Confidence, line.BoundingBox);
            }
        }

        return OcrInspectionVerdict.Fail(
            inspectionName,
            result.Text,
            expectedSubstring,
            $"Expected text '{expectedSubstring}' not found.",
            result.MeanConfidence);
    }

    /// <summary>
    /// Judges whether any recognized text matches a required industrial regex pattern (e.g. LOT code format).
    /// </summary>
    public static OcrInspectionVerdict JudgeRegex(
        OcrResult result,
        string regexPattern,
        string inspectionName = "PatternFormatCheck")
    {
        var regex = new Regex(regexPattern, RegexOptions.Compiled);

        foreach (var line in result.Lines)
        {
            var match = regex.Match(line.Text);
            if (match.Success)
            {
                return OcrInspectionVerdict.Pass(inspectionName, match.Value, regexPattern, line.Confidence, line.BoundingBox);
            }

            foreach (var word in line.Words)
            {
                var wordMatch = regex.Match(word.Text);
                if (wordMatch.Success)
                {
                    return OcrInspectionVerdict.Pass(inspectionName, wordMatch.Value, regexPattern, word.Confidence, word.BoundingBox);
                }
            }
        }

        return OcrInspectionVerdict.Fail(
            inspectionName,
            result.Text,
            regexPattern,
            $"No token matched pattern '{regexPattern}'.",
            result.MeanConfidence);
    }

    /// <summary>
    /// Judges whether an expiry date is recognized and is strictly in the future.
    /// </summary>
    public static OcrInspectionVerdict JudgeExpiryDate(
        OcrResult result,
        DateTime? referenceDate = null,
        string inspectionName = "ExpiryDateCheck")
    {
        var now = referenceDate ?? DateTime.UtcNow.Date;
        var dates = Analysis.OcrPatternMatcher.FindDates(result);

        if (dates.Count == 0)
        {
            return OcrInspectionVerdict.Fail(
                inspectionName,
                result.Text,
                $"Date > {now:yyyy-MM-dd}",
                "No readable date found on the product.",
                result.MeanConfidence);
        }

        // Take the latest date found (typical for expiry dates)
        var latest = dates[0];
        for (int i = 1; i < dates.Count; i++)
        {
            if (dates[i].Value > latest.Value)
                latest = dates[i];
        }

        if (latest.Value >= now)
        {
            return OcrInspectionVerdict.Pass(
                inspectionName,
                latest.RawText,
                $">= {now:yyyy-MM-dd}",
                latest.Confidence,
                latest.BoundingBox);
        }

        return OcrInspectionVerdict.Fail(
            inspectionName,
            latest.RawText,
            $">= {now:yyyy-MM-dd}",
            $"Product expired on {latest.Value:yyyy-MM-dd}.",
            latest.Confidence,
            latest.BoundingBox);
    }
}
