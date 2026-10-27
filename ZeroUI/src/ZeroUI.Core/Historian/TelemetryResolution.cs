using System;

namespace ZeroUI.Core.Historian
{
    /// <summary>
    /// Multi-resolution telemetry storage tiers for industrial historian continuous aggregation.
    /// Decouples chart query performance from raw data volume: O(screen pixels) instead of O(raw history).
    /// </summary>
    public enum TelemetryResolution : byte
    {
        /// <summary>
        /// Level 0: Raw high-speed telemetry (10 ms / native PLC sample rate).
        /// </summary>
        Raw = 0,

        /// <summary>
        /// Level 1: 100 ms aggregation (10 Hz). Ideal for short-range transient analysis (&lt; 1 hour).
        /// </summary>
        L1_100ms = 1,

        /// <summary>
        /// Level 2: 1 second aggregation (1 Hz). Ideal for hourly and daily shift charts (&lt; 24 hours).
        /// </summary>
        L2_1s = 2,

        /// <summary>
        /// Level 3: 10 second aggregation. Ideal for weekly trends (&lt; 7 days).
        /// </summary>
        L3_10s = 3,

        /// <summary>
        /// Level 4: 1 minute aggregation. Ideal for monthly overview (&lt; 90 days).
        /// </summary>
        L4_1m = 4,

        /// <summary>
        /// Level 5: 10 minute aggregation. Ideal for quarterly/annual multi-year historical auditing.
        /// </summary>
        L5_10m = 5
    }

    /// <summary>
    /// Extension methods for TelemetryResolution tier selection and bucket calculations.
    /// </summary>
    public static class TelemetryResolutionExtensions
    {
        /// <summary>
        /// Returns the bucket duration in milliseconds for the given resolution level.
        /// </summary>
        public static long GetBucketDurationMs(this TelemetryResolution resolution)
        {
            switch (resolution)
            {
                case TelemetryResolution.L1_100ms: return 100;
                case TelemetryResolution.L2_1s: return 1000;
                case TelemetryResolution.L3_10s: return 10000;
                case TelemetryResolution.L4_1m: return 60000;
                case TelemetryResolution.L5_10m: return 600000;
                default: return 10;
            }
        }

        /// <summary>
        /// Selects the optimal telemetry resolution level based on the requested chart time span window.
        /// Aligned with industrial telemetry specifications:
        /// - &lt;= 5 minutes: Level 0 (Raw - 10ms)
        /// - &lt;= 15 minutes: Level 1 (100ms)
        /// - &lt;= 2 hours (1 hour zoom): Level 2 (1 sec ~ 3,600 buckets)
        /// - &lt;= 2 days (1 day zoom): Level 3 (10 sec ~ 8,640 buckets)
        /// - &lt;= 14 days (1 week zoom): Level 4 (1 min ~ 10,080 buckets)
        /// - &gt; 14 days (1 month zoom): Level 5 (10 min ~ 4,320 buckets)
        /// Guarantees query size is always bounded by O(screen pixels) &lt;= 10k points.
        /// </summary>
        public static TelemetryResolution SelectOptimalResolution(TimeSpan window)
        {
            if (window <= TimeSpan.FromMinutes(5)) return TelemetryResolution.Raw;
            if (window <= TimeSpan.FromMinutes(15)) return TelemetryResolution.L1_100ms;
            if (window <= TimeSpan.FromHours(2)) return TelemetryResolution.L2_1s;
            if (window <= TimeSpan.FromDays(2)) return TelemetryResolution.L3_10s;
            if (window <= TimeSpan.FromDays(14)) return TelemetryResolution.L4_1m;
            return TelemetryResolution.L5_10m;
        }
    }
}
