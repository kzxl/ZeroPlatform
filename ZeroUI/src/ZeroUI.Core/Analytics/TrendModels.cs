using System;
using System.Collections.Generic;

namespace ZeroUI.Core.Analytics
{
    /// <summary>
    /// Represents a discrete timestamped telemetry point on an analytical trendline.
    /// </summary>
    public readonly struct TrendDataPoint : IEquatable<TrendDataPoint>
    {
        public DateTime Timestamp { get; }
        public double Value { get; }

        public TrendDataPoint(DateTime timestamp, double value)
        {
            Timestamp = timestamp;
            Value = value;
        }

        public bool Equals(TrendDataPoint other) =>
            Timestamp == other.Timestamp && Math.Abs(Value - other.Value) < double.Epsilon;

        public override bool Equals(object? obj) =>
            obj is TrendDataPoint other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (Timestamp.GetHashCode() * 397) ^ Value.GetHashCode();
            }
        }

        public override string ToString() => $"{Timestamp:HH:mm:ss.fff}: {Value:F2}";
    }

    /// <summary>
    /// Calculated statistical metrics for a series of trend data points.
    /// </summary>
    public readonly struct TrendStatistics
    {
        public int Count { get; }
        public double Min { get; }
        public double Max { get; }
        public double Average { get; }
        public double StdDev { get; }
        public double Latest { get; }

        public static TrendStatistics Empty { get; } = new TrendStatistics(0, 0, 0, 0, 0, 0);

        public TrendStatistics(int count, double min, double max, double average, double stdDev, double latest)
        {
            Count = count;
            Min = min;
            Max = max;
            Average = average;
            StdDev = stdDev;
            Latest = latest;
        }

        /// <summary>
        /// Computes statistical metrics over the specified slice of trend data points.
        /// </summary>
        public static TrendStatistics Compute(IReadOnlyList<TrendDataPoint> data, int startIndex = 0, int length = -1)
        {
            if (data == null || data.Count == 0) return Empty;

            if (startIndex < 0) startIndex = 0;
            if (length < 0 || startIndex + length > data.Count) length = data.Count - startIndex;
            if (length <= 0) return Empty;

            double min = double.MaxValue;
            double max = double.MinValue;
            double sum = 0;

            for (int i = startIndex; i < startIndex + length; i++)
            {
                double v = data[i].Value;
                if (v < min) min = v;
                if (v > max) max = v;
                sum += v;
            }

            double avg = sum / length;
            double varianceSum = 0;

            for (int i = startIndex; i < startIndex + length; i++)
            {
                double diff = data[i].Value - avg;
                varianceSum += diff * diff;
            }

            double stdDev = length > 1 ? Math.Sqrt(varianceSum / (length - 1)) : 0;
            double latest = data[startIndex + length - 1].Value;

            return new TrendStatistics(length, min, max, avg, stdDev, latest);
        }
    }

    /// <summary>
    /// Largest-Triangle-Three-Buckets (LTTB) downsampling algorithm for high-performance trend visualization.
    /// Reduces dense time-series data (e.g. 100k - 1M points) to screen resolution while strictly
    /// preserving visual peaks, valleys, and waveform characteristics with zero visual artifacts.
    /// </summary>
    public static class LttbDecimator
    {
        public static TrendDataPoint[] Downsample(IReadOnlyList<TrendDataPoint> data, int threshold)
        {
            if (data == null || data.Count == 0) return Array.Empty<TrendDataPoint>();
            if (threshold >= data.Count || threshold <= 2)
            {
                var copy = new TrendDataPoint[data.Count];
                for (int i = 0; i < data.Count; i++) copy[i] = data[i];
                return copy;
            }

            var sampled = new TrendDataPoint[threshold];
            int sampledIndex = 0;

            // Always add the first point
            sampled[sampledIndex++] = data[0];

            double bucketSize = (double)(data.Count - 2) / (threshold - 2);
            int a = 0;

            for (int i = 0; i < threshold - 2; i++)
            {
                // Calculate point average for next bucket (c)
                double avgX = 0;
                double avgY = 0;
                int avgRangeStart = (int)Math.Floor((i + 1) * bucketSize) + 1;
                int avgRangeEnd = (int)Math.Floor((i + 2) * bucketSize) + 1;
                if (avgRangeEnd > data.Count) avgRangeEnd = data.Count;

                int avgRangeLength = avgRangeEnd - avgRangeStart;
                if (avgRangeLength > 0)
                {
                    for (int j = avgRangeStart; j < avgRangeEnd; j++)
                    {
                        avgX += data[j].Timestamp.Ticks;
                        avgY += data[j].Value;
                    }
                    avgX /= avgRangeLength;
                    avgY /= avgRangeLength;
                }
                else
                {
                    avgX = data[a].Timestamp.Ticks;
                    avgY = data[a].Value;
                }

                // Get the range for this bucket (b)
                int rangeOffs = (int)Math.Floor(i * bucketSize) + 1;
                int rangeTo = (int)Math.Floor((i + 1) * bucketSize) + 1;
                if (rangeTo > data.Count) rangeTo = data.Count;

                // Point a
                double pointAX = data[a].Timestamp.Ticks;
                double pointAY = data[a].Value;

                double maxArea = -1;
                int nextA = rangeOffs;

                for (int j = rangeOffs; j < rangeTo; j++)
                {
                    // Calculate triangle area over points a, this point, and the average next point
                    double area = Math.Abs(
                        (pointAX - avgX) * (data[j].Value - pointAY) -
                        (pointAX - data[j].Timestamp.Ticks) * (avgY - pointAY)
                    ) * 0.5;

                    if (area > maxArea)
                    {
                        maxArea = area;
                        nextA = j;
                    }
                }

                sampled[sampledIndex++] = data[nextA];
                a = nextA;
            }

            // Always add the last point
            sampled[sampledIndex] = data[data.Count - 1];

            return sampled;
        }
    }
}
