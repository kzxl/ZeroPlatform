using System;

namespace ZeroIoT.SparkplugB
{
    /// <summary>
    /// Sparkplug B specification data types.
    /// </summary>
    public enum SparkplugDataType : uint
    {
        Unknown = 0,
        Int8 = 1,
        Int16 = 2,
        Int32 = 3,
        Int64 = 4,
        UInt8 = 5,
        UInt16 = 6,
        UInt32 = 7,
        UInt64 = 8,
        Float = 9,
        Double = 10,
        Boolean = 11,
        String = 12,
        DateTime = 13,
        Text = 14,
        UUID = 15,
        Bytes = 16
    }

    /// <summary>
    /// Represents an individual Sparkplug B metric.
    /// </summary>
    public class SparkplugMetric
    {
        public string Name { get; set; }
        public ulong? Alias { get; set; }
        public ulong Timestamp { get; set; }
        public SparkplugDataType DataType { get; set; }
        public object? Value { get; set; }
        public bool IsNull { get; set; }

        public SparkplugMetric(string name, SparkplugDataType dataType, object? value, ulong? timestamp = null)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            DataType = dataType;
            Value = value;
            IsNull = value == null;
            Timestamp = timestamp ?? (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        public static SparkplugMetric CreateDouble(string name, double val) =>
            new SparkplugMetric(name, SparkplugDataType.Double, val);

        public static SparkplugMetric CreateFloat(string name, float val) =>
            new SparkplugMetric(name, SparkplugDataType.Float, val);

        public static SparkplugMetric CreateInt32(string name, int val) =>
            new SparkplugMetric(name, SparkplugDataType.Int32, val);

        public static SparkplugMetric CreateInt64(string name, long val) =>
            new SparkplugMetric(name, SparkplugDataType.Int64, val);

        public static SparkplugMetric CreateBoolean(string name, bool val) =>
            new SparkplugMetric(name, SparkplugDataType.Boolean, val);

        public static SparkplugMetric CreateString(string name, string val) =>
            new SparkplugMetric(name, SparkplugDataType.String, val);

        public double AsDouble()
        {
            if (Value == null) return 0.0;
            if (Value is double d) return d;
            if (Value is float f) return f;
            if (Value is int i) return i;
            if (Value is long l) return l;
            if (Value is bool b) return b ? 1.0 : 0.0;
            return double.TryParse(Value.ToString(), out double res) ? res : 0.0;
        }
    }
}
