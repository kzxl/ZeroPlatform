using System;

namespace ZeroStorage.Core.Gorilla
{
    /// <summary>
    /// Represents a single time-series measurement point: Unix millisecond timestamp and 64-bit IEEE 754 value.
    /// </summary>
    public readonly struct TimeSeriesPoint : IEquatable<TimeSeriesPoint>
    {
        public long TimestampMs { get; }
        public double Value { get; }

        public TimeSeriesPoint(long timestampMs, double value)
        {
            TimestampMs = timestampMs;
            Value = value;
        }

        public bool Equals(TimeSeriesPoint other) =>
            TimestampMs == other.TimestampMs && Value.Equals(other.Value);

        public override bool Equals(object? obj) =>
            obj is TimeSeriesPoint other && Equals(other);

        public override int GetHashCode() =>
            (TimestampMs.GetHashCode() * 397) ^ Value.GetHashCode();

        public override string ToString() =>
            $"[{TimestampMs} ms: {Value}]";
    }
}
