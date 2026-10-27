using System;
using System.Collections.Generic;
using System.Linq;
using ZeroOcr.Core.Models;

namespace ZeroOcr.Core.Analysis;

/// <summary>
/// Spatial layout analyzer for extracting Key-Value pairs from OCR tokens based on 2D proximity.
/// </summary>
public static class OcrKeyValueExtractor
{
    /// <summary>
    /// Extracts Key-Value pairs from an OCR result by matching candidate key strings and locating
    /// the geometrically nearest value either to the right or directly below.
    /// </summary>
    public static IReadOnlyList<OcrKeyValuePair> Extract(
        OcrResult result,
        IEnumerable<string> candidateKeys,
        float maxHorizontalDistance = 250.0f,
        float maxVerticalDistance = 80.0f)
    {
        if (result == null || !result.Success || result.Lines.Count == 0)
            return Array.Empty<OcrKeyValuePair>();

        var keysList = candidateKeys
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => k.Trim())
            .ToList();

        if (keysList.Count == 0)
            return Array.Empty<OcrKeyValuePair>();

        var pairs = new List<OcrKeyValuePair>();
        var allWords = result.Words;

        // 1. Identify words that match any candidate key (exact or prefix with colon)
        for (int i = 0; i < allWords.Count; i++)
        {
            var keyWord = allWords[i];
            string trimmedText = keyWord.Text.TrimEnd(':', '：', ' ', '\t');

            string? matchedKey = keysList.FirstOrDefault(k =>
                string.Equals(trimmedText, k, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(keyWord.Text, k, StringComparison.OrdinalIgnoreCase));

            if (matchedKey == null) continue;

            // 2. Search for the nearest value to the Right
            var rightCandidate = FindNearestToRight(keyWord, allWords, maxHorizontalDistance);

            // 3. Search for the nearest value Below
            var belowCandidate = FindNearestBelow(keyWord, allWords, maxVerticalDistance);

            if (rightCandidate != null)
            {
                float conf = (keyWord.Confidence + rightCandidate.Confidence) / 2f;
                pairs.Add(new OcrKeyValuePair(
                    matchedKey,
                    rightCandidate.Text,
                    keyWord.BoundingBox,
                    rightCandidate.BoundingBox,
                    conf,
                    SpatialDirection.Right));
            }
            else if (belowCandidate != null)
            {
                float conf = (keyWord.Confidence + belowCandidate.Confidence) / 2f;
                pairs.Add(new OcrKeyValuePair(
                    matchedKey,
                    belowCandidate.Text,
                    keyWord.BoundingBox,
                    belowCandidate.BoundingBox,
                    conf,
                    SpatialDirection.Below));
            }
        }

        return pairs;
    }

    private static OcrWord? FindNearestToRight(OcrWord key, IReadOnlyList<OcrWord> allWords, float maxDistance)
    {
        var kb = key.BoundingBox;
        float keyCenterY = kb.Top + (kb.Height / 2f);

        OcrWord? bestWord = null;
        float minDx = float.MaxValue;

        for (int i = 0; i < allWords.Count; i++)
        {
            var w = allWords[i];
            if (ReferenceEquals(w, key)) continue;

            var wb = w.BoundingBox;

            // Must be strictly to the right
            float dx = wb.Left - kb.Right;
            if (dx < -2f || dx > maxDistance) continue;

            // Vertical overlap: center Y must be within key's vertical bounds with margin
            float wordCenterY = wb.Top + (wb.Height / 2f);
            float dy = Math.Abs(wordCenterY - keyCenterY);
            if (dy > Math.Max(kb.Height, wb.Height) * 0.75f) continue;

            if (dx < minDx)
            {
                minDx = dx;
                bestWord = w;
            }
        }

        return bestWord;
    }

    private static OcrWord? FindNearestBelow(OcrWord key, IReadOnlyList<OcrWord> allWords, float maxDistance)
    {
        var kb = key.BoundingBox;
        float keyCenterX = kb.Left + (kb.Width / 2f);

        OcrWord? bestWord = null;
        float minDy = float.MaxValue;

        for (int i = 0; i < allWords.Count; i++)
        {
            var w = allWords[i];
            if (ReferenceEquals(w, key)) continue;

            var wb = w.BoundingBox;

            // Must be strictly below
            float dy = wb.Top - kb.Bottom;
            if (dy < -2f || dy > maxDistance) continue;

            // Horizontal alignment: center X must align within key width tolerance
            float wordCenterX = wb.Left + (wb.Width / 2f);
            float dx = Math.Abs(wordCenterX - keyCenterX);
            if (dx > Math.Max(kb.Width, wb.Width) * 1.5f) continue;

            if (dy < minDy)
            {
                minDy = dy;
                bestWord = w;
            }
        }

        return bestWord;
    }
}
