using System;
using System.Runtime.InteropServices;

namespace ZeroUI.Core.Scada
{
    /// <summary>
    /// Type descriptor for unboxed SCADA telemetry values.
    /// </summary>
    public enum ScadaValueType : byte
    {
        Empty = 0,
        Boolean = 1,
        Int64 = 2,
        Double = 3
    }

    /// <summary>
    /// High-performance 16-byte value-type union representing a SCADA telemetry value without heap allocation or boxing.
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    public readonly struct ScadaValue : IEquatable<ScadaValue>
    {
        [FieldOffset(0)]
        public readonly double DoubleVal;

        [FieldOffset(0)]
        public readonly long Int64Val;

        [FieldOffset(0)]
        public readonly bool BoolVal;

        [FieldOffset(8)]
        public readonly ScadaValueType Type;

        [FieldOffset(9)]
        public readonly ScadaQuality Quality;

        public static readonly ScadaValue Empty = new ScadaValue(0.0, ScadaValueType.Empty, ScadaQuality.Bad);

        public ScadaValue(double value, ScadaQuality quality = ScadaQuality.Good)
        {
            Int64Val = 0;
            BoolVal = false;
            DoubleVal = value;
            Type = ScadaValueType.Double;
            Quality = quality;
        }

        public ScadaValue(long value, ScadaQuality quality = ScadaQuality.Good)
        {
            DoubleVal = 0.0;
            BoolVal = false;
            Int64Val = value;
            Type = ScadaValueType.Int64;
            Quality = quality;
        }

        public ScadaValue(bool value, ScadaQuality quality = ScadaQuality.Good)
        {
            DoubleVal = 0.0;
            Int64Val = 0;
            BoolVal = value;
            Type = ScadaValueType.Boolean;
            Quality = quality;
        }

        private ScadaValue(double doubleVal, ScadaValueType type, ScadaQuality quality)
        {
            Int64Val = 0;
            BoolVal = false;
            DoubleVal = doubleVal;
            Type = type;
            Quality = quality;
        }

        public double AsDouble()
        {
            switch (Type)
            {
                case ScadaValueType.Double: return DoubleVal;
                case ScadaValueType.Int64: return Int64Val;
                case ScadaValueType.Boolean: return BoolVal ? 1.0 : 0.0;
                default: return 0.0;
            }
        }

        public long AsInt64()
        {
            switch (Type)
            {
                case ScadaValueType.Int64: return Int64Val;
                case ScadaValueType.Double: return (long)DoubleVal;
                case ScadaValueType.Boolean: return BoolVal ? 1 : 0;
                default: return 0;
            }
        }

        public bool AsBoolean()
        {
            switch (Type)
            {
                case ScadaValueType.Boolean: return BoolVal;
                case ScadaValueType.Double: return DoubleVal != 0.0;
                case ScadaValueType.Int64: return Int64Val != 0;
                default: return false;
            }
        }

        public object? ToObject()
        {
            switch (Type)
            {
                case ScadaValueType.Double: return DoubleVal;
                case ScadaValueType.Int64: return Int64Val;
                case ScadaValueType.Boolean: return BoolVal;
                default: return null;
            }
        }

        public bool Equals(ScadaValue other)
        {
            if (Type != other.Type || Quality != other.Quality) return false;
            switch (Type)
            {
                case ScadaValueType.Double: return DoubleVal.Equals(other.DoubleVal);
                case ScadaValueType.Int64: return Int64Val == other.Int64Val;
                case ScadaValueType.Boolean: return BoolVal == other.BoolVal;
                default: return true;
            }
        }

        public override bool Equals(object? obj) => obj is ScadaValue other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Type ^ ((int)Quality << 8);
                return hash ^ Int64Val.GetHashCode();
            }
        }

        public static bool operator ==(ScadaValue left, ScadaValue right) => left.Equals(right);
        public static bool operator !=(ScadaValue left, ScadaValue right) => !left.Equals(right);

        public override string ToString()
        {
            switch (Type)
            {
                case ScadaValueType.Double: return $"{DoubleVal} [{Quality}]";
                case ScadaValueType.Int64: return $"{Int64Val} [{Quality}]";
                case ScadaValueType.Boolean: return $"{BoolVal} [{Quality}]";
                default: return $"<Empty> [{Quality}]";
            }
        }
    }
}
