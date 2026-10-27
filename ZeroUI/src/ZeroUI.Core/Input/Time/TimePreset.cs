using System;

namespace ZeroUI.Core.Input.Time
{
    /// <summary>
    /// Represents a named time preset (such as a factory shift start or break interval).
    /// </summary>
    public readonly struct TimePreset : IEquatable<TimePreset>
    {
        public string Label { get; }
        public TimeSpan Time { get; }

        public TimePreset(string label, TimeSpan time)
        {
            Label = label ?? string.Empty;
            Time = time;
        }

        public bool Equals(TimePreset other) => Label == other.Label && Time == other.Time;
        public override bool Equals(object? obj) => obj is TimePreset other && Equals(other);
        public override int GetHashCode() => (Label, Time).GetHashCode();
        public override string ToString() => $"{Label} ({Time:hh\\:mm})";

        public static bool operator ==(TimePreset left, TimePreset right) => left.Equals(right);
        public static bool operator !=(TimePreset left, TimePreset right) => !left.Equals(right);
    }
}
