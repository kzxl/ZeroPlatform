using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ZeroStorage.Core.Gorilla;
using ZeroStorage.Core.TimeSeries;

namespace ZeroStorage.Core.Persistence
{
    /// <summary>
    /// Supported aggregation functions for downsampling time-series buckets.
    /// </summary>
    public enum AggregationType
    {
        Mean,
        Min,
        Max,
        Sum,
        First,
        Last,
        Count
    }

    /// <summary>
    /// Comprehensive statistical summary for a temporal rollup window.
    /// </summary>
    public sealed class RollupSummary
    {
        public long BucketStartMs { get; }
        public long BucketEndMs { get; }
        public int Count { get; }
        public double Min { get; }
        public double Max { get; }
        public double Mean => Count > 0 ? Sum / Count : 0.0;
        public double Sum { get; }
        public double First { get; }
        public double Last { get; }

        public RollupSummary(
            long bucketStartMs,
            long bucketEndMs,
            int count,
            double min,
            double max,
            double sum,
            double first,
            double last)
        {
            BucketStartMs = bucketStartMs;
            BucketEndMs = bucketEndMs;
            Count = count;
            Min = min;
            Max = max;
            Sum = sum;
            First = first;
            Last = last;
        }
    }

    /// <summary>
    /// Configuration rule defining TTL duration and downsampling resolution for a metric.
    /// </summary>
    public sealed class RetentionRule
    {
        public int? MetricId { get; set; }
        public long RawRetentionDurationMs { get; set; }
        public long RollupBucketSizeMs { get; set; }
        public AggregationType RollupAggregation { get; set; } = AggregationType.Mean;
        public long ArchiveRetentionDurationMs { get; set; }

        public RetentionRule() { }

        public RetentionRule(
            long rawRetentionDurationMs,
            long rollupBucketSizeMs,
            AggregationType rollupAggregation = AggregationType.Mean,
            long archiveRetentionDurationMs = long.MaxValue,
            int? metricId = null)
        {
            RawRetentionDurationMs = rawRetentionDurationMs;
            RollupBucketSizeMs = rollupBucketSizeMs;
            RollupAggregation = rollupAggregation;
            ArchiveRetentionDurationMs = archiveRetentionDurationMs;
            MetricId = metricId;
        }
    }

    /// <summary>
    /// Result metrics returned after executing retention lifecycle policies.
    /// </summary>
    public sealed class RetentionExecutionResult
    {
        public int RawBlocksExamined { get; set; }
        public int RawBlocksRetained { get; set; }
        public int RawBlocksPurged { get; set; }
        public int RawPointsAggregated { get; set; }
        public int RollupBlocksCreated { get; set; }
        public int ArchiveBlocksPurged { get; set; }
    }

    /// <summary>
    /// Autonomous lifecycle storage engine that enforces Time-to-Live (TTL) retention policies
    /// and performs temporal rollup downsampling on industrial high-frequency telemetry.
    /// </summary>
    public static class RetentionPolicyEngine
    {
        /// <summary>
        /// Downsamples a collection of time-series points into fixed-width buckets using the specified aggregation method.
        /// </summary>
        public static List<TimeSeriesPoint> Downsample(
            IReadOnlyList<TimeSeriesPoint> points,
            long bucketSizeMs,
            AggregationType aggregation = AggregationType.Mean)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (bucketSizeMs <= 0) throw new ArgumentOutOfRangeException(nameof(bucketSizeMs), "Bucket size must be positive.");
            if (points.Count == 0) return new List<TimeSeriesPoint>();

            // Group points by bucket start: bucketStart = (timestamp / bucketSizeMs) * bucketSizeMs
            var buckets = new Dictionary<long, List<TimeSeriesPoint>>();
            for (int i = 0; i < points.Count; i++)
            {
                var pt = points[i];
                long bucketKey = (pt.TimestampMs / bucketSizeMs) * bucketSizeMs;
                if (!buckets.TryGetValue(bucketKey, out var list))
                {
                    list = new List<TimeSeriesPoint>();
                    buckets[bucketKey] = list;
                }
                list.Add(pt);
            }

            // Sort bucket keys chronologically
            var sortedKeys = new List<long>(buckets.Keys);
            sortedKeys.Sort();

            var result = new List<TimeSeriesPoint>(sortedKeys.Count);
            for (int k = 0; k < sortedKeys.Count; k++)
            {
                long bucketStart = sortedKeys[k];
                var pts = buckets[bucketStart];
                double aggregatedValue = ComputeAggregate(pts, aggregation);
                result.Add(new TimeSeriesPoint(bucketStart, aggregatedValue));
            }

            return result;
        }

        /// <summary>
        /// Computes complete RollupSummary statistics for each fixed-width temporal window.
        /// </summary>
        public static List<RollupSummary> GenerateRollups(
            IReadOnlyList<TimeSeriesPoint> points,
            long bucketSizeMs)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (bucketSizeMs <= 0) throw new ArgumentOutOfRangeException(nameof(bucketSizeMs), "Bucket size must be positive.");
            if (points.Count == 0) return new List<RollupSummary>();

            var buckets = new Dictionary<long, List<TimeSeriesPoint>>();
            for (int i = 0; i < points.Count; i++)
            {
                var pt = points[i];
                long bucketKey = (pt.TimestampMs / bucketSizeMs) * bucketSizeMs;
                if (!buckets.TryGetValue(bucketKey, out var list))
                {
                    list = new List<TimeSeriesPoint>();
                    buckets[bucketKey] = list;
                }
                list.Add(pt);
            }

            var sortedKeys = new List<long>(buckets.Keys);
            sortedKeys.Sort();

            var result = new List<RollupSummary>(sortedKeys.Count);
            for (int k = 0; k < sortedKeys.Count; k++)
            {
                long bucketStart = sortedKeys[k];
                long bucketEnd = bucketStart + bucketSizeMs - 1;
                var pts = buckets[bucketStart];

                double min = double.MaxValue;
                double max = double.MinValue;
                double sum = 0.0;
                double first = pts[0].Value;
                double last = pts[pts.Count - 1].Value;

                for (int i = 0; i < pts.Count; i++)
                {
                    double v = pts[i].Value;
                    if (v < min) min = v;
                    if (v > max) max = v;
                    sum += v;
                }

                result.Add(new RollupSummary(bucketStart, bucketEnd, pts.Count, min, max, sum, first, last));
            }

            return result;
        }

        /// <summary>
        /// Downsamples a TimeSeriesBlock into a newly compressed TimeSeriesBlock of specified aggregation resolution.
        /// </summary>
        public static TimeSeriesBlock DownsampleBlock(
            TimeSeriesBlock block,
            long bucketSizeMs,
            AggregationType aggregation = AggregationType.Mean)
        {
            if (block == null) throw new ArgumentNullException(nameof(block));

            var rawPoints = block.Decompress();
            var downsampled = Downsample(rawPoints, bucketSizeMs, aggregation);
            if (downsampled.Count == 0)
                throw new InvalidOperationException("Downsampling produced 0 points.");

            return TimeSeriesBlock.FromPoints(block.MetricId, downsampled);
        }

        /// <summary>
        /// Purges blocks and individual points strictly older than the given cutoff timestamp from a log file.
        /// Retained data is written to targetFilePath.
        /// </summary>
        public static int PurgeExpired(
            MemoryMappedTimeSeriesLog sourceLog,
            string targetFilePath,
            long cutoffTimestampMs)
        {
            if (sourceLog == null) throw new ArgumentNullException(nameof(sourceLog));
            if (string.IsNullOrEmpty(targetFilePath)) throw new ArgumentNullException(nameof(targetFilePath));

            if (File.Exists(targetFilePath))
                File.Delete(targetFilePath);

            var allBlocks = sourceLog.ReadAllBlocks();
            int retainedCount = 0;

            using (var targetLog = new MemoryMappedTimeSeriesLog(targetFilePath))
            {
                for (int i = 0; i < allBlocks.Count; i++)
                {
                    var block = allBlocks[i];

                    if (block.EndTimeMs < cutoffTimestampMs)
                    {
                        // Completely expired - skip
                        continue;
                    }
                    else if (block.StartTimeMs >= cutoffTimestampMs)
                    {
                        // Completely valid - retain directly
                        targetLog.AppendBlock(block);
                        retainedCount++;
                    }
                    else
                    {
                        // Straddles cutoff: decompress, filter, recompress
                        var pts = block.Decompress();
                        var filtered = new List<TimeSeriesPoint>();
                        for (int p = 0; p < pts.Count; p++)
                        {
                            if (pts[p].TimestampMs >= cutoffTimestampMs)
                            {
                                filtered.Add(pts[p]);
                            }
                        }

                        if (filtered.Count > 0)
                        {
                            var newBlock = TimeSeriesBlock.FromPoints(block.MetricId, filtered);
                            targetLog.AppendBlock(newBlock);
                            retainedCount++;
                        }
                    }
                }
            }

            return retainedCount;
        }

        /// <summary>
        /// Executes full retention lifecycle:
        /// 1. Finds raw points older than TTL cutoff.
        /// 2. Downsamples expired raw points into rollup buckets and appends to rollup log.
        /// 3. Retains non-expired raw points in compacted raw log.
        /// 4. Purges expired rollups older than archive retention cutoff.
        /// </summary>
        public static RetentionExecutionResult ExecuteLifecycle(
            string rawLogPath,
            string rollupLogPath,
            long currentTimestampMs,
            RetentionRule rule)
        {
            if (string.IsNullOrEmpty(rawLogPath)) throw new ArgumentNullException(nameof(rawLogPath));
            if (string.IsNullOrEmpty(rollupLogPath)) throw new ArgumentNullException(nameof(rollupLogPath));
            if (rule == null) throw new ArgumentNullException(nameof(rule));

            var result = new RetentionExecutionResult();
            if (!File.Exists(rawLogPath)) return result;

            long rawCutoffMs = currentTimestampMs - rule.RawRetentionDurationMs;
            long archiveCutoffMs = currentTimestampMs - rule.ArchiveRetentionDurationMs;

            string tempRawPath = rawLogPath + ".tmp." + Guid.NewGuid().ToString("N");

            // Process Raw Log
            var pointsToRollup = new Dictionary<int, List<TimeSeriesPoint>>();
            int rawExamined = 0;
            int rawRetained = 0;
            int rawPurged = 0;

            using (var rawLog = new MemoryMappedTimeSeriesLog(rawLogPath))
            using (var tempRawLog = new MemoryMappedTimeSeriesLog(tempRawPath))
            {
                var blocks = rawLog.ReadAllBlocks();
                rawExamined = blocks.Count;

                for (int b = 0; b < blocks.Count; b++)
                {
                    var block = blocks[b];

                    // Check if rule applies to this metric
                    if (rule.MetricId.HasValue && rule.MetricId.Value != block.MetricId)
                    {
                        // Unaffected metric: retain block directly
                        tempRawLog.AppendBlock(block);
                        rawRetained++;
                        continue;
                    }

                    if (block.EndTimeMs < rawCutoffMs)
                    {
                        // Entire block expired -> extract points for rollup
                        var pts = block.Decompress();
                        AddPoints(pointsToRollup, block.MetricId, pts);
                        rawPurged++;
                    }
                    else if (block.StartTimeMs >= rawCutoffMs)
                    {
                        // Entire block is fresh -> retain directly
                        tempRawLog.AppendBlock(block);
                        rawRetained++;
                    }
                    else
                    {
                        // Straddles cutoff: split into expired (for rollup) and active (for retention)
                        var pts = block.Decompress();
                        var freshPts = new List<TimeSeriesPoint>();
                        var expiredPts = new List<TimeSeriesPoint>();

                        for (int i = 0; i < pts.Count; i++)
                        {
                            if (pts[i].TimestampMs < rawCutoffMs)
                                expiredPts.Add(pts[i]);
                            else
                                freshPts.Add(pts[i]);
                        }

                        if (expiredPts.Count > 0)
                        {
                            AddPoints(pointsToRollup, block.MetricId, expiredPts);
                            rawPurged++;
                        }

                        if (freshPts.Count > 0)
                        {
                            tempRawLog.AppendBlock(TimeSeriesBlock.FromPoints(block.MetricId, freshPts));
                            rawRetained++;
                        }
                    }
                }
            }

            result.RawBlocksExamined = rawExamined;
            result.RawBlocksRetained = rawRetained;
            result.RawBlocksPurged = rawPurged;

            // Replace raw log atomically
            if (File.Exists(rawLogPath))
                File.Delete(rawLogPath);
            File.Move(tempRawPath, rawLogPath);

            // Write Rollups to Rollup Log
            int totalPointsAggregated = 0;
            int rollupsCreated = 0;

            using (var rollupLog = new MemoryMappedTimeSeriesLog(rollupLogPath))
            {
                foreach (var kvp in pointsToRollup)
                {
                    int metricId = kvp.Key;
                    var pts = kvp.Value;
                    if (pts.Count == 0) continue;

                    pts.Sort((a, b) => a.TimestampMs.CompareTo(b.TimestampMs));
                    totalPointsAggregated += pts.Count;

                    var downsampled = Downsample(pts, rule.RollupBucketSizeMs, rule.RollupAggregation);
                    if (downsampled.Count > 0)
                    {
                        var rollupBlock = TimeSeriesBlock.FromPoints(metricId, downsampled);
                        rollupLog.AppendBlock(rollupBlock);
                        rollupsCreated++;
                    }
                }
            }

            result.RawPointsAggregated = totalPointsAggregated;
            result.RollupBlocksCreated = rollupsCreated;

            // Purge Archive Rollups older than ArchiveRetentionDurationMs if specified
            if (rule.ArchiveRetentionDurationMs < long.MaxValue && File.Exists(rollupLogPath))
            {
                string tempRollupPath = rollupLogPath + ".tmp." + Guid.NewGuid().ToString("N");
                int initialArchiveCount = 0;
                int retainedArchiveCount = 0;

                using (var currentRollupLog = new MemoryMappedTimeSeriesLog(rollupLogPath))
                {
                    initialArchiveCount = currentRollupLog.BlockCount;
                    retainedArchiveCount = PurgeExpired(currentRollupLog, tempRollupPath, archiveCutoffMs);
                }

                if (File.Exists(rollupLogPath))
                    File.Delete(rollupLogPath);
                File.Move(tempRollupPath, rollupLogPath);

                result.ArchiveBlocksPurged = initialArchiveCount - retainedArchiveCount;
            }

            return result;
        }

        private static void AddPoints(Dictionary<int, List<TimeSeriesPoint>> dict, int metricId, List<TimeSeriesPoint> points)
        {
            if (!dict.TryGetValue(metricId, out var list))
            {
                list = new List<TimeSeriesPoint>();
                dict[metricId] = list;
            }
            list.AddRange(points);
        }

        private static double ComputeAggregate(List<TimeSeriesPoint> pts, AggregationType aggregation)
        {
            if (pts.Count == 0) return 0.0;

            switch (aggregation)
            {
                case AggregationType.First:
                    return pts[0].Value;

                case AggregationType.Last:
                    return pts[pts.Count - 1].Value;

                case AggregationType.Min:
                    double min = double.MaxValue;
                    for (int i = 0; i < pts.Count; i++)
                        if (pts[i].Value < min) min = pts[i].Value;
                    return min;

                case AggregationType.Max:
                    double max = double.MinValue;
                    for (int i = 0; i < pts.Count; i++)
                        if (pts[i].Value > max) max = pts[i].Value;
                    return max;

                case AggregationType.Sum:
                    double sum = 0.0;
                    for (int i = 0; i < pts.Count; i++)
                        sum += pts[i].Value;
                    return sum;

                case AggregationType.Count:
                    return pts.Count;

                case AggregationType.Mean:
                default:
                    double total = 0.0;
                    for (int i = 0; i < pts.Count; i++)
                        total += pts[i].Value;
                    return total / pts.Count;
            }
        }
    }
}
