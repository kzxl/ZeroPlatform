using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ZeroPrimitives.Text;

namespace ZeroPrimitives.Numerics
{
    /// <summary>
    /// 8-byte fixed-point Q4 decimal value (<c>ScaleFactor = 10,000</c>, 4 fractional digits) designed for dense columnar ERP analytics and SIMD aggregations.
    /// Half the size of <see cref="decimal"/> with exact base-10 precision up to <c>±922,337,203,685,477.5807</c>.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 8)]
    public readonly struct ScaledInt64 : IEquatable<ScaledInt64>, IComparable<ScaledInt64>
    {
        public const long ScaleFactor = 10_000L;
        public const int DecimalPlaces = 4;

        public static readonly ScaledInt64 Zero = default;
        public static readonly ScaledInt64 One = new ScaledInt64(ScaleFactor);
        public static readonly ScaledInt64 MinValue = new ScaledInt64(long.MinValue);
        public static readonly ScaledInt64 MaxValue = new ScaledInt64(long.MaxValue);

        private readonly long _raw;

        private ScaledInt64(long raw)
        {
            _raw = raw;
        }

        public long RawValue
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _raw;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ScaledInt64 FromRaw(long rawValue) => new ScaledInt64(rawValue);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ScaledInt64 FromInt64(long integerValue) => new ScaledInt64(checked(integerValue * ScaleFactor));

        public static ScaledInt64 FromDecimal(decimal value, RoundingMode mode = RoundingMode.HalfAwayFromZero)
        {
            decimal scaled = DecimalMath.Round(value * ScaleFactor, 0, mode);
            if (scaled < long.MinValue || scaled > long.MaxValue)
                throw new OverflowException("Value exceeds ScaledInt64 range.");
            return new ScaledInt64((long)scaled);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public decimal ToDecimal() => (decimal)_raw / ScaleFactor;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ScaledInt64 operator +(ScaledInt64 left, ScaledInt64 right)
            => new ScaledInt64(checked(left._raw + right._raw));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ScaledInt64 operator -(ScaledInt64 left, ScaledInt64 right)
            => new ScaledInt64(checked(left._raw - right._raw));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ScaledInt64 operator -(ScaledInt64 value)
            => new ScaledInt64(checked(-value._raw));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ScaledInt64 operator *(ScaledInt64 left, long scalar)
            => new ScaledInt64(checked(left._raw * scalar));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ScaledInt64 operator /(ScaledInt64 left, long scalar)
            => new ScaledInt64(left._raw / scalar);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ScaledInt64 operator *(ScaledInt64 left, ScaledInt64 right)
            => Multiply(left, right, RoundingMode.HalfAwayFromZero);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ScaledInt64 operator /(ScaledInt64 left, ScaledInt64 right)
            => Divide(left, right, RoundingMode.HalfAwayFromZero);

        public static ScaledInt64 Multiply(ScaledInt64 left, ScaledInt64 right, RoundingMode mode = RoundingMode.HalfAwayFromZero)
        {
            decimal prod = ((decimal)left._raw * (decimal)right._raw) / ScaleFactor;
            decimal rounded = DecimalMath.Round(prod, 0, mode);
            if (rounded < long.MinValue || rounded > long.MaxValue)
                throw new OverflowException("ScaledInt64 multiplication overflow.");
            return new ScaledInt64((long)rounded);
        }

        public static ScaledInt64 Divide(ScaledInt64 left, ScaledInt64 right, RoundingMode mode = RoundingMode.HalfAwayFromZero)
        {
            if (right._raw == 0)
                throw new DivideByZeroException();

            decimal quot = ((decimal)left._raw * ScaleFactor) / (decimal)right._raw;
            decimal rounded = DecimalMath.Round(quot, 0, mode);
            if (rounded < long.MinValue || rounded > long.MaxValue)
                throw new OverflowException("ScaledInt64 division overflow.");
            return new ScaledInt64((long)rounded);
        }

        public bool TryFormat(Span<char> destination, out int charsWritten)
        {
            if (_raw == 0)
            {
                if (destination.IsEmpty) { charsWritten = 0; return false; }
                destination[0] = '0';
                charsWritten = 1;
                return true;
            }

            bool neg = _raw < 0;
            ulong absRaw = neg ? unchecked((ulong)(0L - _raw)) : (ulong)_raw;
            ulong intPart = absRaw / ScaleFactor;
            uint fracPart = (uint)(absRaw % ScaleFactor);

            int pos = 0;
            if (neg)
            {
                if (destination.IsEmpty) { charsWritten = 0; return false; }
                destination[pos++] = '-';
            }

            if (!FastFormat.TryFormatUInt64(intPart, destination.Slice(pos), out int intChars))
            {
                charsWritten = 0;
                return false;
            }
            pos += intChars;

            if (fracPart != 0)
            {
                Span<char> fracBuf = stackalloc char[4];
                fracBuf[0] = (char)('0' + (fracPart / 1000));
                fracBuf[1] = (char)('0' + ((fracPart / 100) % 10));
                fracBuf[2] = (char)('0' + ((fracPart / 10) % 10));
                fracBuf[3] = (char)('0' + (fracPart % 10));

                int fracLen = 4;
                while (fracLen > 0 && fracBuf[fracLen - 1] == '0') fracLen--;

                if (destination.Length < pos + 1 + fracLen)
                {
                    charsWritten = 0;
                    return false;
                }
                destination[pos++] = '.';
                fracBuf.Slice(0, fracLen).CopyTo(destination.Slice(pos));
                pos += fracLen;
            }

            charsWritten = pos;
            return true;
        }

        public bool TryFormatUtf8(Span<byte> destination, out int bytesWritten)
        {
            if (_raw == 0)
            {
                if (destination.IsEmpty) { bytesWritten = 0; return false; }
                destination[0] = (byte)'0';
                bytesWritten = 1;
                return true;
            }

            bool neg = _raw < 0;
            ulong absRaw = neg ? unchecked((ulong)(0L - _raw)) : (ulong)_raw;
            ulong intPart = absRaw / ScaleFactor;
            uint fracPart = (uint)(absRaw % ScaleFactor);

            int pos = 0;
            if (neg)
            {
                if (destination.IsEmpty) { bytesWritten = 0; return false; }
                destination[pos++] = (byte)'-';
            }

            if (!FastFormat.TryFormatUInt64Utf8(intPart, destination.Slice(pos), out int intBytes))
            {
                bytesWritten = 0;
                return false;
            }
            pos += intBytes;

            if (fracPart != 0)
            {
                Span<byte> fracBuf = stackalloc byte[4];
                fracBuf[0] = (byte)('0' + (fracPart / 1000));
                fracBuf[1] = (byte)('0' + ((fracPart / 100) % 10));
                fracBuf[2] = (byte)('0' + ((fracPart / 10) % 10));
                fracBuf[3] = (byte)('0' + (fracPart % 10));

                int fracLen = 4;
                while (fracLen > 0 && fracBuf[fracLen - 1] == (byte)'0') fracLen--;

                if (destination.Length < pos + 1 + fracLen)
                {
                    bytesWritten = 0;
                    return false;
                }
                destination[pos++] = (byte)'.';
                fracBuf.Slice(0, fracLen).CopyTo(destination.Slice(pos));
                pos += fracLen;
            }

            bytesWritten = pos;
            return true;
        }

        public override unsafe string ToString()
        {
            Span<char> chars = stackalloc char[32];
            TryFormat(chars, out int written);
#if NET8_0_OR_GREATER
            return new string(chars.Slice(0, written));
#else
            fixed (char* p = chars)
                return new string(p, 0, written);
#endif
        }

        public bool Equals(ScaledInt64 other) => _raw == other._raw;
        public override bool Equals(object? obj) => obj is ScaledInt64 other && Equals(other);
        public override int GetHashCode() => _raw.GetHashCode();
        public int CompareTo(ScaledInt64 other) => _raw.CompareTo(other._raw);

        public static bool operator ==(ScaledInt64 left, ScaledInt64 right) => left._raw == right._raw;
        public static bool operator !=(ScaledInt64 left, ScaledInt64 right) => left._raw != right._raw;
        public static bool operator <(ScaledInt64 left, ScaledInt64 right) => left._raw < right._raw;
        public static bool operator <=(ScaledInt64 left, ScaledInt64 right) => left._raw <= right._raw;
        public static bool operator >(ScaledInt64 left, ScaledInt64 right) => left._raw > right._raw;
        public static bool operator >=(ScaledInt64 left, ScaledInt64 right) => left._raw >= right._raw;
    }
}
