using System;
using System.Collections.Generic;
using ZeroStorage.Core.Gorilla;

namespace ZeroStorage.Core.Analytics
{
    /// <summary>
    /// High-performance implementation of the Largest-Triangle-Three-Buckets (LTTB) downsampling algorithm.
    /// Preserves critical visual waveform shapes, local extrema (vibration peaks, temperature drops),
    /// and overall trends while reducing millions of raw points down to display resolution (e.g. 2,000 px for ZeroCharts 144Hz).
    /// </summary>
    public static class LttbDecimator
    {
        /// <summary>
        /// Downsamples a chronologically sorted series of TimeSeriesPoint to exactly targetThreshold points using LTTB.
        /// </summary>
        /// <param name="points">The input chronologically sorted time-series points.</param>
        /// <param name="targetThreshold">The desired number of downsampled points (must be >= 2).</param>
        /// <returns>A downsampled list containing exactly targetThreshold points (or fewer if input count &lt; targetThreshold).</returns>
        public static List<TimeSeriesPoint> Decimate(IReadOnlyList<TimeSeriesPoint> points, int targetThreshold)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (targetThreshold <= 0) throw new ArgumentOutOfRangeException(nameof(targetThreshold), "Target threshold must be greater than zero.");

            int count = points.Count;
            if (count <= targetThreshold || targetThreshold <= 2)
            {
                if (count <= targetThreshold)
                {
                    return new List<TimeSeriesPoint>(points);
                }

                if (targetThreshold == 1)
                {
                    return new List<TimeSeriesPoint>(1) { points[0] };
                }

                // targetThreshold == 2
                return new List<TimeSeriesPoint>(2) { points[0], points[count - 1] };
            }

            var sampled = new List<TimeSeriesPoint>(targetThreshold);

            // Bucket size: remaining (count - 2) points divided into (targetThreshold - 2) buckets
            double every = (double)(count - 2) / (targetThreshold - 2);

            // Point A starts as the first point
            int a = 0;
            sampled.Add(points[a]);

            for (int i = 0; i < targetThreshold - 2; i++)
            {
                // Calculate average (centroid) of the next bucket (bucket i + 1)
                int nextBucketStart = (int)Math.Floor((i + 1) * every) + 1;
                int nextBucketEnd = (int)Math.Floor((i + 2) * every) + 1;
                if (nextBucketEnd > count) nextBucketEnd = count;

                double avgX = 0;
                double avgY = 0;
                int nextBucketCount = nextBucketEnd - nextBucketStart;

                if (nextBucketCount > 0)
                {
                    for (int k = nextBucketStart; k < nextBucketEnd; k++)
                    {
                        avgX += points[k].TimestampMs;
                        avgY += points[k].Value;
                    }
                    avgX /= nextBucketCount;
                    avgY /= nextBucketCount;
                }
                else
                {
                    avgX = points[count - 1].TimestampMs;
                    avgY = points[count - 1].Value;
                }

                // Range of current bucket (bucket i)
                int currBucketStart = (int)Math.Floor(i * every) + 1;
                int currBucketEnd = (int)Math.Floor((i + 1) * every) + 1;
                if (currBucketEnd > count) currBucketEnd = count;

                // Point A coordinates
                double pointAx = points[a].TimestampMs;
                double pointAy = points[a].Value;

                double maxArea = -1;
                int maxAreaIndex = currBucketStart;

                // Find point B in the current bucket that maximizes the triangle area with A and the average of bucket C
                for (int k = currBucketStart; k < currBucketEnd; k++)
                {
                    double currentX = points[k].TimestampMs;
                    double currentY = points[k].Value;

                    // Triangle area formula: 0.5 * | (Ax - Cx)(By - Ay) - (Ax - Bx)(Cy - Ay) |
                    double area = Math.Abs(
                        (pointAx - avgX) * (currentY - pointAy) -
                        (pointAx - currentX) * (avgY - pointAy)
                    );

                    if (area > maxArea)
                    {
                        maxArea = area;
                        maxAreaIndex = k;
                    }
                }

                sampled.Add(points[maxAreaIndex]);
                a = maxAreaIndex; // Next A is the selected point
            }

            // Always add the last point
            sampled.Add(points[count - 1]);

            return sampled;
        }
    }
}
