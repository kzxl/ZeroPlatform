using System;

namespace ZeroStorage.Core.TimeSeries
{
    /// <summary>
    /// Represents summary statistics computed over a time range for a metric.
    /// Enables O(1) header-only aggregate push-down without decompressing block payloads.
    /// </summary>
    public sealed class BlockAggregateSummary
    {
        public int MetricId { get; }
        public int Count { get; }
        public double Min { get; }
        public double Max { get; }
        public double Sum { get; }
        public double Mean => Count > 0 ? Sum / Count : double.NaN;
        public long FromTimeMs { get; }
        public long ToTimeMs { get; }

        public BlockAggregateSummary(
            int metricId,
            int count,
            double min,
            double max,
            double sum,
            long fromTimeMs,
            long toTimeMs)
        {
            MetricId = metricId;
            Count = count;
            Min = min;
            Max = max;
            Sum = sum;
            FromTimeMs = fromTimeMs;
            ToTimeMs = toTimeMs;
        }

        public static BlockAggregateSummary Empty(int metricId, long fromTimeMs, long toTimeMs)
        {
            return new BlockAggregateSummary(metricId, 0, double.NaN, double.NaN, 0.0, fromTimeMs, toTimeMs);
        }

        public override string ToString()
        {
            return $"MetricId={MetricId}, Count={Count}, Min={Min:F3}, Max={Max:F3}, Mean={Mean:F3}, Sum={Sum:F3}, Window=[{FromTimeMs}..{ToTimeMs}]";
        }
    }
}
