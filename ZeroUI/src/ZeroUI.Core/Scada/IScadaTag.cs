using System;

namespace ZeroUI.Core.Scada
{
    /// <summary>
    /// Represents an immutable point of industrial telemetry.
    /// </summary>
    public interface IScadaTag
    {
        string TagPath { get; }
        object? Value { get; }
        DateTime Timestamp { get; }
        ScadaQuality Quality { get; }
        T? GetValue<T>();
    }

    /// <summary>
    /// High-performance read-only telemetry tag snapshot.
    /// </summary>
    public sealed class ScadaTag : IScadaTag
    {
        public string TagPath { get; }
        public object? Value { get; }
        public DateTime Timestamp { get; }
        public ScadaQuality Quality { get; }

        public ScadaTag(string tagPath, object? value, ScadaQuality quality = ScadaQuality.Good, DateTime? timestamp = null)
        {
            TagPath = tagPath ?? throw new ArgumentNullException(nameof(tagPath));
            Value = value;
            Quality = quality;
            Timestamp = timestamp ?? DateTime.UtcNow;
        }

        public T? GetValue<T>()
        {
            if (Value == null) return default;
            if (Value is T typed) return typed;
            try
            {
                return (T)Convert.ChangeType(Value, typeof(T));
            }
            catch
            {
                return default;
            }
        }

        public override string ToString() => $"{TagPath} = {Value} [{Quality}] @ {Timestamp:HH:mm:ss.fff}";
    }
}
