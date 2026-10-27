using System;
using System.Runtime.InteropServices;

namespace ZeroUI.Core.Scada
{
    /// <summary>
    /// Unboxed telemetry sample update payload (TagId, Value, TimestampUtcMs).
    /// Pure value-type with zero heap allocation on hot communication and ingestion paths.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct TagUpdate : IEquatable<TagUpdate>
    {
        public readonly int TagId;
        public readonly ScadaValue Value;
        public readonly long TimestampUtcMs;

        public TagUpdate(int tagId, in ScadaValue value, long timestampUtcMs)
        {
            TagId = tagId;
            Value = value;
            TimestampUtcMs = timestampUtcMs;
        }

        public TagUpdate(int tagId, in ScadaValue value)
        {
            TagId = tagId;
            Value = value;
            TimestampUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        public bool Equals(TagUpdate other) =>
            TagId == other.TagId && TimestampUtcMs == other.TimestampUtcMs && Value.Equals(other.Value);

        public override bool Equals(object? obj) => obj is TagUpdate other && Equals(other);
        public override int GetHashCode() => (TagId, TimestampUtcMs).GetHashCode();
        public override string ToString() => $"Tag[{TagId}] = {Value} @ {TimestampUtcMs}";

        public static bool operator ==(TagUpdate left, TagUpdate right) => left.Equals(right);
        public static bool operator !=(TagUpdate left, TagUpdate right) => !left.Equals(right);
    }
}
