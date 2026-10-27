using System;
using System.Collections.Generic;

namespace ZeroUI.Core.Data
{
    /// <summary>
    /// Represents a 2D time-series data point (X: Timestamp or Index, Y: Value).
    /// </summary>
    public readonly struct TimePoint : IEquatable<TimePoint>
    {
        public double X { get; }
        public double Y { get; }

        public TimePoint(double x, double y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(TimePoint other) => X.Equals(other.X) && Y.Equals(other.Y);
        public override bool Equals(object? obj) => obj is TimePoint other && Equals(other);
        public override int GetHashCode() => unchecked((X.GetHashCode() * 397) ^ Y.GetHashCode());
        public static bool operator ==(TimePoint left, TimePoint right) => left.Equals(right);
        public static bool operator !=(TimePoint left, TimePoint right) => !left.Equals(right);

        public override string ToString() => $"({X:0.##}, {Y:0.##})";
    }

    /// <summary>
    /// High-performance, zero-GC implementation of the Largest-Triangle-Three-Buckets (LTTB) algorithm.
    /// Downsamples massive time-series telemetry streams (from 100k up to 10M+ points) down to screen pixel resolution
    /// while strictly preserving visual peaks, valleys, and waveform morphology without UI lag.
    /// Supports direct ReadOnlySpan&lt;TimePoint&gt; execution for maximum cache locality and JIT bounds-check elimination.
    /// </summary>
    public static class LttbDecimation
    {
        /// <summary>
        /// Downsamples the input span down to the target threshold count using LTTB.
        /// Writes results directly into the destination span without heap allocations.
        /// </summary>
        public static int Downsample(ReadOnlySpan<TimePoint> data, Span<TimePoint> destination, int targetCount)
        {
            int dataLength = data.Length;
            if (targetCount >= dataLength || targetCount <= 2 || dataLength <= 2)
            {
                int copyCount = Math.Min(dataLength, destination.Length);
                data.Slice(0, copyCount).CopyTo(destination);
                return copyCount;
            }

            int sampledIndex = 0;
            double every = (double)(dataLength - 2) / (targetCount - 2);

            int a = 0;
            destination[sampledIndex++] = data[a]; // Always add the first point

            for (int i = 0; i < targetCount - 2; i++)
            {
                double avgX = 0;
                double avgY = 0;
                int avgRangeStart = (int)Math.Floor((i + 1) * every) + 1;
                int avgRangeEnd = (int)Math.Floor((i + 2) * every) + 1;
                if (avgRangeEnd > dataLength) avgRangeEnd = dataLength;

                int avgRangeLength = avgRangeEnd - avgRangeStart;
                if (avgRangeLength > 0)
                {
                    for (int j = avgRangeStart; j < avgRangeEnd; j++)
                    {
                        ref readonly var pt = ref data[j];
                        avgX += pt.X;
                        avgY += pt.Y;
                    }
                    avgX /= avgRangeLength;
                    avgY /= avgRangeLength;
                }
                else
                {
                    ref readonly var last = ref data[dataLength - 1];
                    avgX = last.X;
                    avgY = last.Y;
                }

                int rangeOffs = (int)Math.Floor(i * every) + 1;
                int rangeTo = (int)Math.Floor((i + 1) * every) + 1;
                if (rangeTo > dataLength) rangeTo = dataLength;

                ref readonly var pointA = ref data[a];
                double pointAx = pointA.X;
                double pointAy = pointA.Y;

                double maxArea = -1;
                int maxAreaIndex = rangeOffs;

                for (int j = rangeOffs; j < rangeTo; j++)
                {
                    ref readonly var pt = ref data[j];
                    double area = Math.Abs(
                        (pointAx - avgX) * (pt.Y - pointAy) -
                        (pointAx - pt.X) * (avgY - pointAy)
                    ) * 0.5;

                    if (area > maxArea)
                    {
                        maxArea = area;
                        maxAreaIndex = j;
                    }
                }

                destination[sampledIndex++] = data[maxAreaIndex];
                a = maxAreaIndex;
            }

            destination[sampledIndex++] = data[dataLength - 1]; // Always add the last point
            return sampledIndex;
        }

        /// <summary>
        /// Dedicated array overload for Downsample avoiding Span/IReadOnlyList resolution ambiguity.
        /// </summary>
        public static int Downsample(TimePoint[] data, TimePoint[] destination, int targetCount)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            return Downsample(data.AsSpan(), destination.AsSpan(), targetCount);
        }

        /// <summary>
        /// Backward-compatible overload accepting IReadOnlyList&lt;TimePoint&gt; and TimePoint[] destination.
        /// </summary>
        public static int Downsample(IReadOnlyList<TimePoint> data, TimePoint[] destination, int targetCount)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (destination == null) throw new ArgumentNullException(nameof(destination));

            if (data is TimePoint[] array)
            {
                return Downsample(new ReadOnlySpan<TimePoint>(array), destination.AsSpan(), targetCount);
            }

            int dataLength = data.Count;
            if (targetCount >= dataLength || targetCount <= 2 || dataLength <= 2)
            {
                int copyCount = Math.Min(dataLength, destination.Length);
                for (int i = 0; i < copyCount; i++)
                {
                    destination[i] = data[i];
                }
                return copyCount;
            }

            int sampledIndex = 0;
            double every = (double)(dataLength - 2) / (targetCount - 2);

            int a = 0;
            destination[sampledIndex++] = data[a];

            for (int i = 0; i < targetCount - 2; i++)
            {
                double avgX = 0;
                double avgY = 0;
                int avgRangeStart = (int)Math.Floor((i + 1) * every) + 1;
                int avgRangeEnd = (int)Math.Floor((i + 2) * every) + 1;
                avgRangeEnd = Math.Min(avgRangeEnd, dataLength);

                int avgRangeLength = avgRangeEnd - avgRangeStart;
                if (avgRangeLength > 0)
                {
                    for (int j = avgRangeStart; j < avgRangeEnd; j++)
                    {
                        var pt = data[j];
                        avgX += pt.X;
                        avgY += pt.Y;
                    }
                    avgX /= avgRangeLength;
                    avgY /= avgRangeLength;
                }
                else
                {
                    var last = data[dataLength - 1];
                    avgX = last.X;
                    avgY = last.Y;
                }

                int rangeOffs = (int)Math.Floor(i * every) + 1;
                int rangeTo = (int)Math.Floor((i + 1) * every) + 1;
                rangeTo = Math.Min(rangeTo, dataLength);

                var pointA = data[a];
                double pointAx = pointA.X;
                double pointAy = pointA.Y;

                double maxArea = -1;
                int maxAreaIndex = rangeOffs;

                for (int j = rangeOffs; j < rangeTo; j++)
                {
                    var pt = data[j];
                    double area = Math.Abs(
                        (pointAx - avgX) * (pt.Y - pointAy) -
                        (pointAx - pt.X) * (avgY - pointAy)
                    ) * 0.5;

                    if (area > maxArea)
                    {
                        maxArea = area;
                        maxAreaIndex = j;
                    }
                }

                destination[sampledIndex++] = data[maxAreaIndex];
                a = maxAreaIndex;
            }

            destination[sampledIndex++] = data[dataLength - 1];
            return sampledIndex;
        }
    }
}
