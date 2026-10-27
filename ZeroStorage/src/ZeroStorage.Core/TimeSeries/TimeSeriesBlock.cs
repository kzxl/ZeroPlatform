using System;
using System.Collections.Generic;
using ZeroStorage.Core.Gorilla;

namespace ZeroStorage.Core.TimeSeries
{
    /// <summary>
    /// Encapsulates a compressed block of time-series points with precomputed summary statistics.
    /// </summary>
    public sealed class TimeSeriesBlock
    {
        public int MetricId { get; }
        public long StartTimeMs { get; }
        public long EndTimeMs { get; }
        public int Count { get; }
        public double Min { get; }
        public double Max { get; }
        public double Sum { get; }
        public double Mean => Count > 0 ? Sum / Count : 0.0;
        public byte[] CompressedData { get; }

        public TimeSeriesBlock(
            int metricId,
            long startTimeMs,
            long endTimeMs,
            int count,
            double min,
            double max,
            double sum,
            byte[] compressedData)
        {
            MetricId = metricId;
            StartTimeMs = startTimeMs;
            EndTimeMs = endTimeMs;
            Count = count;
            Min = min;
            Max = max;
            Sum = sum;
            CompressedData = compressedData ?? throw new ArgumentNullException(nameof(compressedData));
        }

        public static TimeSeriesBlock FromPoints(int metricId, IReadOnlyList<TimeSeriesPoint> points)
        {
            if (points == null || points.Count == 0)
                throw new ArgumentException("Points collection cannot be null or empty.", nameof(points));

            long start = points[0].TimestampMs;
            long end = points[points.Count - 1].TimestampMs;
            double min = double.MaxValue;
            double max = double.MinValue;
            double sum = 0.0;

            for (int i = 0; i < points.Count; i++)
            {
                double v = points[i].Value;
                if (v < min) min = v;
                if (v > max) max = v;
                sum += v;
            }

            byte[] compressed = GorillaEncoder.Encode(points);
            return new TimeSeriesBlock(metricId, start, end, points.Count, min, max, sum, compressed);
        }

        public List<TimeSeriesPoint> Decompress()
        {
            return GorillaDecoder.Decode(CompressedData);
        }
    }
}
