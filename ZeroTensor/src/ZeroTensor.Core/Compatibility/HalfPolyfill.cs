#if !NET6_0_OR_GREATER
using System.Globalization;
using System.Runtime.InteropServices;

namespace System
{
    /// <summary>
    /// Sovereign IEEE 754 16-bit half-precision floating-point polyfill for .NET Standard 2.0 and .NET Framework 4.6.2.
    /// Provides transparent binary and arithmetic compatibility with .NET 8+ System.Half.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct Half : IComparable, IFormattable, IComparable<Half>, IEquatable<Half>
    {
        private readonly ushort _value;

        [StructLayout(LayoutKind.Explicit)]
        private struct FloatIntUnion
        {
            [FieldOffset(0)] public float FloatValue;
            [FieldOffset(0)] public int IntValue;
        }

        private static float IntToFloat(int val)
        {
            var u = default(FloatIntUnion);
            u.IntValue = val;
            return u.FloatValue;
        }

        private static int FloatToInt(float val)
        {
            var u = default(FloatIntUnion);
            u.FloatValue = val;
            return u.IntValue;
        }

        public static Half MaxValue => new Half(0x7BFF);
        public static Half MinValue => new Half(0xFBFF);
        public static Half Epsilon => new Half(0x0001);
        public static Half PositiveInfinity => new Half(0x7C00);
        public static Half NegativeInfinity => new Half(0xFC00);
        public static Half NaN => new Half(0xFE00);

        internal Half(ushort value) => _value = value;

        public static bool IsNaN(Half h) => (h._value & 0x7FFF) > 0x7C00;
        public static bool IsInfinity(Half h) => (h._value & 0x7FFF) == 0x7C00;
        public static bool IsPositiveInfinity(Half h) => h._value == 0x7C00;
        public static bool IsNegativeInfinity(Half h) => h._value == 0xFC00;

        public static explicit operator float(Half value)
        {
            ushort h = value._value;
            int sign = (h >> 15) & 0x0001;
            int exp = (h >> 10) & 0x001F;
            int frac = h & 0x03FF;

            if (exp == 0)
            {
                if (frac == 0)
                {
                    return IntToFloat(sign << 31);
                }
                while ((frac & 0x0400) == 0)
                {
                    frac <<= 1;
                    exp--;
                }
                exp++;
                frac &= ~0x0400;
            }
            else if (exp == 31)
            {
                if (frac == 0)
                {
                    return IntToFloat((sign << 31) | 0x7F800000);
                }
                return IntToFloat((sign << 31) | 0x7F800000 | (frac << 13));
            }

            exp = exp + (127 - 15);
            int f = (sign << 31) | (exp << 23) | (frac << 13);
            return IntToFloat(f);
        }

        public static explicit operator Half(float value)
        {
            int f = FloatToInt(value);
            int sign = (f >> 16) & 0x8000;
            int exp = ((f >> 23) & 0xFF) - (127 - 15);
            int frac = f & 0x007FFFFF;

            if (exp <= 0)
            {
                if (exp < -10)
                {
                    return new Half((ushort)sign);
                }
                frac |= 0x00800000;
                int shift = 14 - exp;
                frac >>= shift;
                return new Half((ushort)(sign | frac));
            }
            else if (exp >= 31)
            {
                if (exp == 255 - (127 - 15))
                {
                    return new Half((ushort)(sign | 0x7C00 | (frac != 0 ? 0x0200 : 0)));
                }
                return new Half((ushort)(sign | 0x7C00));
            }

            return new Half((ushort)(sign | (exp << 10) | (frac >> 13)));
        }

        public static explicit operator double(Half value) => (double)(float)value;
        public static explicit operator Half(double value) => (Half)(float)value;

        public static bool operator ==(Half left, Half right)
        {
            if (IsNaN(left) || IsNaN(right)) return false;
            if (((left._value | right._value) & 0x7FFF) == 0) return true;
            return left._value == right._value;
        }

        public static bool operator !=(Half left, Half right) => !(left == right);

        public static bool operator <(Half left, Half right) => (float)left < (float)right;
        public static bool operator >(Half left, Half right) => (float)left > (float)right;
        public static bool operator <=(Half left, Half right) => (float)left <= (float)right;
        public static bool operator >=(Half left, Half right) => (float)left >= (float)right;

        public static Half operator +(Half left, Half right) => (Half)((float)left + (float)right);
        public static Half operator -(Half left, Half right) => (Half)((float)left - (float)right);
        public static Half operator *(Half left, Half right) => (Half)((float)left * (float)right);
        public static Half operator /(Half left, Half right) => (Half)((float)left / (float)right);
        public static Half operator -(Half value) => (Half)(-(float)value);

        public bool Equals(Half other) => _value == other._value || (IsNaN(this) && IsNaN(other));

        public override bool Equals(object? obj) => obj is Half other && Equals(other);

        public override int GetHashCode() => _value.GetHashCode();

        public int CompareTo(Half other) => ((float)this).CompareTo((float)other);

        public int CompareTo(object? obj)
        {
            if (obj is null) return 1;
            if (obj is Half other) return CompareTo(other);
            throw new ArgumentException("Object must be of type Half.");
        }

        public override string ToString() => ((float)this).ToString(CultureInfo.CurrentCulture);

        public string ToString(string? format, IFormatProvider? formatProvider) =>
            ((float)this).ToString(format, formatProvider);
    }
}
#endif
