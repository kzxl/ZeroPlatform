using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ZeroTensor.Core
{
    /// <summary>
    /// Sovereign IEEE 754 16-bit Brain Floating Point (BFloat16) data structure.
    /// Features 1 sign bit, 8 exponent bits (identical dynamic range to FP32), and 7 mantissa bits.
    /// Provides zero-allocation, instant bitshift conversions and mixed-precision arithmetic.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct BFloat16 : IComparable, IFormattable, IComparable<BFloat16>, IEquatable<BFloat16>
    {
        private readonly ushort _value;

        [StructLayout(LayoutKind.Explicit)]
        private struct FloatIntUnion
        {
            [FieldOffset(0)] public float FloatValue;
            [FieldOffset(0)] public int IntValue;
            [FieldOffset(0)] public uint UIntValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float IntToFloat(int val)
        {
            var u = default(FloatIntUnion);
            u.IntValue = val;
            return u.FloatValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int FloatToInt(float val)
        {
            var u = default(FloatIntUnion);
            u.FloatValue = val;
            return u.IntValue;
        }

        public static BFloat16 MaxValue => new BFloat16(0x7F7F);
        public static BFloat16 MinValue => new BFloat16(0xFF7F);
        public static BFloat16 Epsilon => new BFloat16(0x0080);
        public static BFloat16 PositiveInfinity => new BFloat16(0x7F80);
        public static BFloat16 NegativeInfinity => new BFloat16(0xFF80);
        public static BFloat16 NaN => new BFloat16(0x7FC0);
        public static BFloat16 Zero => new BFloat16(0x0000);
        public static BFloat16 One => new BFloat16(0x3F80);

        internal BFloat16(ushort value) => _value = value;

        public ushort RawBits => _value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNaN(BFloat16 bf) => (bf._value & 0x7F80) == 0x7F80 && (bf._value & 0x007F) != 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsInfinity(BFloat16 bf) => (bf._value & 0x7FFF) == 0x7F80;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static explicit operator float(BFloat16 value)
        {
            return IntToFloat((int)value._value << 16);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static explicit operator BFloat16(float value)
        {
            int f = FloatToInt(value);

            // Handle NaN preservation
            if ((f & 0x7F800000) == 0x7F800000 && (f & 0x007FFFFF) != 0)
            {
                return new BFloat16((ushort)((f >> 16) | 0x0040));
            }

            // Round to nearest even
            int lsb = (f >> 16) & 1;
            int roundingBias = 0x7FFF + lsb;
            int temp = f + roundingBias;
            return new BFloat16((ushort)(temp >> 16));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static explicit operator double(BFloat16 value) => (double)(float)value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static explicit operator BFloat16(double value) => (BFloat16)(float)value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static explicit operator Half(BFloat16 value) => (Half)(float)value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static explicit operator BFloat16(Half value) => (BFloat16)(float)value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(BFloat16 left, BFloat16 right)
        {
            if (IsNaN(left) || IsNaN(right)) return false;
            if (((left._value | right._value) & 0x7FFF) == 0) return true; // +0 == -0
            return left._value == right._value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(BFloat16 left, BFloat16 right) => !(left == right);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator <(BFloat16 left, BFloat16 right) => (float)left < (float)right;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator >(BFloat16 left, BFloat16 right) => (float)left > (float)right;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator <=(BFloat16 left, BFloat16 right) => (float)left <= (float)right;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator >=(BFloat16 left, BFloat16 right) => (float)left >= (float)right;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BFloat16 operator +(BFloat16 left, BFloat16 right) => (BFloat16)((float)left + (float)right);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BFloat16 operator -(BFloat16 left, BFloat16 right) => (BFloat16)((float)left - (float)right);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BFloat16 operator *(BFloat16 left, BFloat16 right) => (BFloat16)((float)left * (float)right);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BFloat16 operator /(BFloat16 left, BFloat16 right) => (BFloat16)((float)left / (float)right);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BFloat16 operator -(BFloat16 value) => (BFloat16)(-(float)value);

        public bool Equals(BFloat16 other) => _value == other._value || (IsNaN(this) && IsNaN(other));

        public override bool Equals(object? obj) => obj is BFloat16 other && Equals(other);

        public override int GetHashCode() => _value.GetHashCode();

        public int CompareTo(BFloat16 other) => ((float)this).CompareTo((float)other);

        public int CompareTo(object? obj)
        {
            if (obj is null) return 1;
            if (obj is BFloat16 other) return CompareTo(other);
            throw new ArgumentException("Object must be of type BFloat16.");
        }

        public override string ToString() => ((float)this).ToString(CultureInfo.CurrentCulture);

        public string ToString(string? format, IFormatProvider? formatProvider) =>
            ((float)this).ToString(format, formatProvider);
    }
}
