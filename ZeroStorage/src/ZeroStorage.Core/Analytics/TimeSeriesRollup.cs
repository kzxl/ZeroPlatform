using System;
using System.Collections.Generic;
using ZeroStorage.Core.Gorilla;
using ZeroStorage.Core.TimeSeries;

namespace ZeroStorage.Core.Analytics
{
    /// <summary>
    /// Pure C# high-performance time-series rollup aggregator.
    /// Condenses high-frequency sensor streams (10Hz/1Hz) into statistical summary buckets (1m, 5m, 1h).
    /// </summary>
    public static class TimeSeriesRollup
    {
        /// <summary>
        /// Aggregates a sequence of points into fixed-width rollup buckets.
        /// Points are expected to be in ascending chronological order.
        /// </summary>
        /// <param name="points">Chronologically sorted collection of points.</param>
        /// <param name="intervalMs">Duration of each bucket in milliseconds.</param>
        public static List<RollupBucket> ComputeRollup(IReadOnlyList<TimeSeriesPoint> points, long intervalMs)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (intervalMs <= 0) throw new ArgumentOutOfRangeException(nameof(intervalMs), "Interval must be positive.");

            var buckets = new List<RollupBucket>();
            if (points.Count == 0) return buckets;

            long currentBucketStart = (points[0].TimestampMs / intervalMs) * intervalMs;
            double min = points[0].Value;
            double max = points[0].Value;
            double sum = points[0].Value;
            double first = points[0].Value;
            double last = points[0].Value;
            int count = 1;

            for (int i = 1; i < points.Count; i++)
            {
                var pt = points[i];
                long ptBucketStart = (pt.TimestampMs / intervalMs) * intervalMs;

                if (ptBucketStart == currentBucketStart)
                {
                    if (pt.Value < min) min = pt.Value;
                    if (pt.Value > max) max = pt.Value;
                    sum += pt.Value;
                    last = pt.Value;
                    count++;
                }
                else
                {
                    // Emit completed bucket
                    buckets.Add(new RollupBucket(currentBucketStart, min, max, sum / count, first, last, count));

                    // Start new bucket
                    currentBucketStart = ptBucketStart;
                    min = pt.Value;
                    max = pt.Value;
                    sum = pt.Value;
                    first = pt.Value;
                    last = pt.Value;
                    count = 1;
                }
            }

            // Emit final bucket
            if (count > 0)
            {
                buckets.Add(new RollupBucket(currentBucketStart, min, max, sum / count, first, last, count));
            }

            return buckets;
        }

        /// <summary>
        /// Aggregates points using a typed <see cref="RollupInterval"/>.
        /// </summary>
        public static List<RollupBucket> ComputeRollup(IReadOnlyList<TimeSeriesPoint> points, RollupInterval interval) =>
            ComputeRollup(points, (long)interval);
    }
}
