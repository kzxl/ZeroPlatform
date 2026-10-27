using System;
using System.Collections.Generic;
using System.Linq;
using ZeroOcr.Core.Models;

namespace ZeroOcr.Inference.PostProcessing;

/// <summary>
/// Geometric line aggregator and 2D reading-order normalizer.
/// Resolves line fragmentation issues by clustering bounding quads/lines that share the same horizontal baseline,
/// merging them into unified, left-to-right reading sequences.
/// </summary>
public static class OcrLineMerger
{
    /// <summary>
    /// Groups and merges horizontally aligned, fragmented text lines into unified full lines.
    /// </summary>
    /// <param name="lines">Collection of recognized OCR lines.</param>
    /// <param name="maxBaselineOffsetRatio">Max vertical center delta relative to mean line height (default 0.45).</param>
    /// <param name="maxHorizontalGapRatio">Max horizontal space relative to line height to allow line merging (default 3.0).</param>
    /// <returns>Merged and reading-order sorted OCR lines.</returns>
    public static IReadOnlyList<OcrLine> MergeHorizontalLines(
        IReadOnlyList<OcrLine> lines,
        float maxBaselineOffsetRatio = 0.45f,
        float maxHorizontalGapRatio = 3.0f)
    {
        if (lines == null || lines.Count <= 1)
            return lines ?? Array.Empty<OcrLine>();

        // 1. Filter out empty lines
        var validLines = new List<OcrLine>(lines.Count);
        for (int i = 0; i < lines.Count; i++)
        {
            var l = lines[i];
            if (!string.IsNullOrWhiteSpace(l.Text) && !l.BoundingBox.IsEmpty)
            {
                validLines.Add(l);
            }
        }

        if (validLines.Count <= 1)
            return validLines;

        // 2. Sort by Y baseline first to initialize clusters
        validLines.Sort((a, b) => a.BoundingBox.Y.CompareTo(b.BoundingBox.Y));

        var clusters = new List<List<OcrLine>>();

        foreach (var line in validLines)
        {
            float lineCenterY = line.BoundingBox.Y + (line.BoundingBox.Height * 0.5f);
            float lineHeight = Math.Max(1.0f, line.BoundingBox.Height);

            bool placed = false;
            foreach (var cluster in clusters)
            {
                // Calculate average center Y and height of current cluster
                float clusterCenterY = 0f;
                float clusterHeight = 0f;
                for (int i = 0; i < cluster.Count; i++)
                {
                    clusterCenterY += cluster[i].BoundingBox.Y + (cluster[i].BoundingBox.Height * 0.5f);
                    clusterHeight += cluster[i].BoundingBox.Height;
                }
                clusterCenterY /= cluster.Count;
                clusterHeight /= cluster.Count;

                float refHeight = Math.Max(lineHeight, clusterHeight);
                float yDiff = Math.Abs(lineCenterY - clusterCenterY);

                // Baseline alignment check: within threshold of line height
                if (yDiff <= refHeight * maxBaselineOffsetRatio)
                {
                    cluster.Add(line);
                    placed = true;
                    break;
                }
            }

            if (!placed)
            {
                clusters.Add(new List<OcrLine> { line });
            }
        }

        // 3. In each cluster, sort left-to-right and merge adjacent fragments
        var result = new List<OcrLine>(clusters.Count);

        foreach (var cluster in clusters)
        {
            // Sort left-to-right
            cluster.Sort((a, b) => a.BoundingBox.X.CompareTo(b.BoundingBox.X));

            OcrLine current = cluster[0];

            for (int i = 1; i < cluster.Count; i++)
            {
                OcrLine next = cluster[i];

                float currentRight = current.BoundingBox.Right;
                float nextLeft = next.BoundingBox.Left;
                float gap = nextLeft - currentRight;
                float refHeight = Math.Max(current.BoundingBox.Height, next.BoundingBox.Height);

                // Check horizontal proximity: allowed gap range
                if (gap <= refHeight * maxHorizontalGapRatio && gap >= -refHeight * 0.5f)
                {
                    // Merge next into current
                    bool needSpace = gap > 0 || (!current.Text.EndsWith(" ") && !next.Text.StartsWith(" "));
                    string mergedText = needSpace 
                        ? $"{current.Text.TrimEnd()} {next.Text.TrimStart()}" 
                        : $"{current.Text}{next.Text}";

                    var mergedRect = current.BoundingBox.Union(next.BoundingBox);

                    // Combine words
                    var mergedWords = new List<OcrWord>(current.Words.Count + next.Words.Count);
                    mergedWords.AddRange(current.Words);
                    mergedWords.AddRange(next.Words);

                    // Length-weighted confidence
                    int lenCurrent = Math.Max(1, current.Text.Length);
                    int lenNext = Math.Max(1, next.Text.Length);
                    float mergedConf = (current.Confidence * lenCurrent + next.Confidence * lenNext) / (lenCurrent + lenNext);

                    current = new OcrLine(mergedText, mergedWords, mergedRect, mergedConf);
                }
                else
                {
                    result.Add(current);
                    current = next;
                }
            }

            result.Add(current);
        }

        // 4. Final reading order sort: top-to-bottom, left-to-right
        return SortReadingOrder(result);
    }

    /// <summary>
    /// Sorts recognized lines by natural 2D reading order (Top-to-Bottom, Left-to-Right).
    /// </summary>
    public static IReadOnlyList<OcrLine> SortReadingOrder(List<OcrLine> lines)
    {
        if (lines.Count <= 1) return lines;

        lines.Sort((a, b) =>
        {
            float yDiff = a.BoundingBox.Y - b.BoundingBox.Y;
            float avgH = (a.BoundingBox.Height + b.BoundingBox.Height) * 0.5f;

            // If vertical delta is smaller than 40% line height, treat as same line level -> sort by X
            if (Math.Abs(yDiff) < avgH * 0.4f)
            {
                return a.BoundingBox.X.CompareTo(b.BoundingBox.X);
            }

            return yDiff.CompareTo(0f);
        });

        return lines;
    }
}
