using System;
using ZeroUI.Core.Scada;

namespace ZeroUI.Core.Historian
{
    /// <summary>
    /// Immutable telemetry sample for historical time-series logging.
    /// Packed for minimal memory footprint on high-frequency ingestion queues.
    /// </summary>
    public readonly struct HistorianRecord : IEquatable<HistorianRecord>
    {
        public long Id { get; }
        public string TagPath { get; }
        public double Value { get; }
        public ScadaQuality Quality { get; }
        public DateTime Timestamp { get; }

        public HistorianRecord(string tagPath, double value, ScadaQuality quality, DateTime timestamp, long id = 0)
        {
            Id = id;
            TagPath = tagPath ?? string.Empty;
            Value = value;
            Quality = quality;
            Timestamp = timestamp;
        }

        public bool Equals(HistorianRecord other)
            => Id == other.Id &&
               string.Equals(TagPath, other.TagPath, StringComparison.OrdinalIgnoreCase) &&
               Value.Equals(other.Value) &&
               Quality == other.Quality &&
               Timestamp.Equals(other.Timestamp);

        public override bool Equals(object? obj) => obj is HistorianRecord other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + StringComparer.OrdinalIgnoreCase.GetHashCode(TagPath);
                hash = hash * 31 + Value.GetHashCode();
                hash = hash * 31 + (int)Quality;
                hash = hash * 31 + Timestamp.GetHashCode();
                return hash;
            }
        }

        public override string ToString() => $"{Timestamp:yyyy-MM-dd HH:mm:ss.fff} | {TagPath} = {Value} [{Quality}]";
    }
}
