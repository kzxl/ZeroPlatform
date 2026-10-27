using System;
using System.Collections.Generic;
using System.Linq;
using ZeroOcr.Core.Models;

namespace ZeroOcr.Core.Analysis;

/// <summary>
/// Spatial layout analyzer for grouping unorganized OCR words into structured lines, columns, and reading order.
/// </summary>
public static class OcrLayoutAnalyzer
{
    /// <summary>
    /// Groups loose recognized words into ordered lines based on vertical overlap and horizontal sorting.
    /// </summary>
    public static IReadOnlyList<OcrLine> GroupIntoLines(IEnumerable<OcrWord> words, float verticalTolerance = 0.5f)
    {
        var wordList = words
            .OrderBy(w => w.BoundingBox.Top)
            .ThenBy(w => w.BoundingBox.Left)
            .ToList();

        if (wordList.Count == 0) return Array.Empty<OcrLine>();

        var lineGroups = new List<List<OcrWord>>();

        foreach (var word in wordList)
        {
            var wb = word.BoundingBox;
            float wordMidY = wb.Top + (wb.Height / 2f);

            // Find matching line where vertical overlap is satisfied
            List<OcrWord>? matchedLine = null;
            foreach (var line in lineGroups)
            {
                var lineBounds = CalculateBoundingBox(line);
                float lineMidY = lineBounds.Top + (lineBounds.Height / 2f);
                float allowedOverlap = Math.Min(wb.Height, lineBounds.Height) * verticalTolerance;

                if (Math.Abs(wordMidY - lineMidY) <= allowedOverlap)
                {
                    matchedLine = line;
                    break;
                }
            }

            if (matchedLine != null)
            {
                matchedLine.Add(word);
            }
            else
            {
                lineGroups.Add(new List<OcrWord> { word });
            }
        }

        // Sort words within each line from left to right, and sort lines from top to bottom
        var resultLines = new List<OcrLine>();
        foreach (var group in lineGroups.OrderBy(g => CalculateBoundingBox(g).Top))
        {
            var sortedWords = group.OrderBy(w => w.BoundingBox.Left).ToList();
            string lineText = string.Join(" ", sortedWords.Select(w => w.Text));
            resultLines.Add(new OcrLine(lineText, sortedWords));
        }

        return resultLines;
    }

    private static OcrRect CalculateBoundingBox(IReadOnlyList<OcrWord> words)
    {
        if (words.Count == 0) return OcrRect.Empty;
        var r = words[0].BoundingBox;
        for (int i = 1; i < words.Count; i++)
            r = r.Union(words[i].BoundingBox);
        return r;
    }
}
