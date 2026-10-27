using System;
using ZeroUI.Core.Scada;

namespace ZeroUI.Core.Historian
{
    /// <summary>
    /// Represents an aggregated telemetry time-bucket storing industrial statistical metrics (Min, Max, Avg, Last, Count).
    /// Preserves critical waveform peaks and valleys across multi-resolution storage tiers.
    /// </summary>
    public sealed class RollupBucket
    {
        public string TagPath { get; }
        public TelemetryResolution Resolution { get; }
        public long BucketTimeMs { get; }
        public double MinVal { get; set; }
        public double MaxVal { get; set; }
        public double SumVal { get; set; }
        public double LastVal { get; set; }
        public int Count { get; set; }
        public ScadaQuality Quality { get; set; }

        public double AvgVal => Count > 0 ? SumVal / Count : 0;

        public RollupBucket(string tagPath, TelemetryResolution resolution, long bucketTimeMs, double initialVal, ScadaQuality quality)
        {
            TagPath = tagPath;
            Resolution = resolution;
            BucketTimeMs = bucketTimeMs;
            MinVal = initialVal;
            MaxVal = initialVal;
            SumVal = initialVal;
            LastVal = initialVal;
            Count = 1;
            Quality = quality;
        }

        public void AddSample(double val, ScadaQuality quality)
        {
            if (val < MinVal) MinVal = val;
            if (val > MaxVal) MaxVal = val;
            SumVal += val;
            LastVal = val;
            Count++;
            if (quality != ScadaQuality.Good)
            {
                Quality = quality;
            }
        }
    }

    /// <summary>
    /// Struct key for zero-allocation grouping of rollup buckets across tags and resolutions.
    /// </summary>
    public readonly struct RollupKey : IEquatable<RollupKey>
    {
        public readonly string TagPath;
        public readonly TelemetryResolution Resolution;
        public readonly long BucketTimeMs;

        public RollupKey(string tagPath, TelemetryResolution resolution, long bucketTimeMs)
        {
            TagPath = tagPath;
            Resolution = resolution;
            BucketTimeMs = bucketTimeMs;
        }

        public bool Equals(RollupKey other) =>
            string.Equals(TagPath, other.TagPath, StringComparison.Ordinal) &&
            Resolution == other.Resolution &&
            BucketTimeMs == other.BucketTimeMs;

        public override bool Equals(object? obj) => obj is RollupKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (TagPath != null ? StringComparer.Ordinal.GetHashCode(TagPath) : 0);
                hash = hash * 31 + (byte)Resolution;
                hash = hash * 31 + BucketTimeMs.GetHashCode();
                return hash;
            }
        }
    }
}
