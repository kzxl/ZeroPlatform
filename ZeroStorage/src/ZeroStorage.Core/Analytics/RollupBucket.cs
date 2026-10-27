using System;

namespace ZeroStorage.Core.Analytics
{
    /// <summary>
    /// Standard predefined rollup aggregation intervals.
    /// </summary>
    public enum RollupInterval : long
    {
        Second10 = 10_000L,
        Minute1 = 60_000L,
        Minute5 = 300_000L,
        Minute15 = 900_000L,
        Hour1 = 3_600_000L,
        Hour6 = 21_600_000L,
        Day1 = 86_400_000L
    }

    /// <summary>
    /// Represents an aggregated summary bucket across a fixed time window.
    /// </summary>
    public readonly struct RollupBucket : IEquatable<RollupBucket>
    {
        /// <summary>
        /// Gets the start timestamp of the bucket in epoch milliseconds.
        /// </summary>
        public long TimestampMs { get; }

        /// <summary>
        /// Gets the minimum value observed in this bucket.
        /// </summary>
        public double Min { get; }

        /// <summary>
        /// Gets the maximum value observed in this bucket.
        /// </summary>
        public double Max { get; }

        /// <summary>
        /// Gets the arithmetic mean (average) of values in this bucket.
        /// </summary>
        public double Avg { get; }

        /// <summary>
        /// Gets the first (earliest) value in this bucket.
        /// </summary>
        public double First { get; }

        /// <summary>
        /// Gets the last (latest) value in this bucket.
        /// </summary>
        public double Last { get; }

        /// <summary>
        /// Gets the total count of raw sample points in this bucket.
        /// </summary>
        public int Count { get; }

        public RollupBucket(long timestampMs, double min, double max, double avg, double first, double last, int count)
        {
            TimestampMs = timestampMs;
            Min = min;
            Max = max;
            Avg = avg;
            First = first;
            Last = last;
            Count = count;
        }

        public bool Equals(RollupBucket other) =>
            TimestampMs == other.TimestampMs &&
            Min.Equals(other.Min) &&
            Max.Equals(other.Max) &&
            Avg.Equals(other.Avg) &&
            First.Equals(other.First) &&
            Last.Equals(other.Last) &&
            Count == other.Count;

        public override bool Equals(object? obj) => obj is RollupBucket other && Equals(other);

        public override int GetHashCode() =>
            (TimestampMs, Min, Max, Avg, First, Last, Count).GetHashCode();

        public override string ToString() =>
            $"Bucket({TimestampMs}: Min={Min:F2}, Max={Max:F2}, Avg={Avg:F2}, Count={Count})";
    }
}
