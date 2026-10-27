using System;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace ZeroUI.Core.Common
{
    /// <summary>
    /// Ultra-high-performance zero-allocation numerical and scalar formatter.
    /// Converts primitive numeric types directly into <see cref="Span{char}"/> buffers across both modern
    /// .NET 8+ and legacy .NET Framework 4.6.2 / .NET Standard 2.0 runtimes.
    /// Also provides a flyweight memoization cache for common integer strings (0..1024).
    /// </summary>
    public static class FastNumberFormatter
    {
        private const int FlyweightCacheLimit = 1024;
        private static readonly string[] _smallIntStrings;

        static FastNumberFormatter()
        {
            _smallIntStrings = new string[FlyweightCacheLimit + 1];
            for (int i = 0; i <= FlyweightCacheLimit; i++)
            {
                _smallIntStrings[i] = i.ToString(CultureInfo.InvariantCulture);
            }
        }

        /// <summary>
        /// Retrieves a flyweight cached string representation for common non-negative integers (0..1024),
        /// or calls standard string conversion for larger numbers.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GetCachedIntString(int value)
        {
            if ((uint)value <= FlyweightCacheLimit)
            {
                return _smallIntStrings[value];
            }
            return value.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Formats a 32-bit signed integer into the destination character span with zero GC allocations.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryFormatInt32(int value, Span<char> destination, out int charsWritten)
        {
#if NET8_0_OR_GREATER
            return value.TryFormat(destination, out charsWritten, provider: CultureInfo.InvariantCulture);
#else
            if (value == 0)
            {
                if (destination.Length < 1) { charsWritten = 0; return false; }
                destination[0] = '0';
                charsWritten = 1;
                return true;
            }

            if (value == int.MinValue)
            {
                const string minStr = "-2147483648";
                if (destination.Length < minStr.Length) { charsWritten = 0; return false; }
                minStr.AsSpan().CopyTo(destination);
                charsWritten = minStr.Length;
                return true;
            }

            bool isNegative = value < 0;
            uint val = (uint)(isNegative ? -value : value);

            // Maximum digits for int is 10, plus optional '-' sign
            Span<char> temp = stackalloc char[11];
            int pos = temp.Length;

            while (val > 0)
            {
                temp[--pos] = (char)('0' + (val % 10));
                val /= 10;
            }

            if (isNegative)
            {
                temp[--pos] = '-';
            }

            int length = temp.Length - pos;
            if (destination.Length < length)
            {
                charsWritten = 0;
                return false;
            }

            temp.Slice(pos, length).CopyTo(destination);
            charsWritten = length;
            return true;
#endif
        }

        /// <summary>
        /// Formats a 64-bit signed integer into the destination character span with zero GC allocations.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryFormatInt64(long value, Span<char> destination, out int charsWritten)
        {
#if NET8_0_OR_GREATER
            return value.TryFormat(destination, out charsWritten, provider: CultureInfo.InvariantCulture);
#else
            if (value == 0)
            {
                if (destination.Length < 1) { charsWritten = 0; return false; }
                destination[0] = '0';
                charsWritten = 1;
                return true;
            }

            if (value == long.MinValue)
            {
                const string minStr = "-9223372036854775808";
                if (destination.Length < minStr.Length) { charsWritten = 0; return false; }
                minStr.AsSpan().CopyTo(destination);
                charsWritten = minStr.Length;
                return true;
            }

            bool isNegative = value < 0;
            ulong val = (ulong)(isNegative ? -value : value);

            Span<char> temp = stackalloc char[21];
            int pos = temp.Length;

            while (val > 0)
            {
                temp[--pos] = (char)('0' + (val % 10));
                val /= 10;
            }

            if (isNegative)
            {
                temp[--pos] = '-';
            }

            int length = temp.Length - pos;
            if (destination.Length < length)
            {
                charsWritten = 0;
                return false;
            }

            temp.Slice(pos, length).CopyTo(destination);
            charsWritten = length;
            return true;
#endif
        }

        /// <summary>
        /// Formats a double-precision floating point number into destination character span with specified decimal precision.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryFormatDouble(double value, Span<char> destination, out int charsWritten, int precision = 2)
        {
#if NET8_0_OR_GREATER
            // Use standard 'F' format with given precision
            Span<char> format = stackalloc char[3];
            format[0] = 'F';
            int clampedPrec = Math.Min(Math.Max(precision, 0), 9);
            format[1] = (char)('0' + clampedPrec);
            return value.TryFormat(destination, out charsWritten, format.Slice(0, 2), CultureInfo.InvariantCulture);
#else
            if (double.IsNaN(value))
            {
                const string nan = "NaN";
                if (destination.Length < 3) { charsWritten = 0; return false; }
                nan.AsSpan().CopyTo(destination);
                charsWritten = 3;
                return true;
            }

            if (double.IsPositiveInfinity(value))
            {
                const string inf = "Infinity";
                if (destination.Length < inf.Length) { charsWritten = 0; return false; }
                inf.AsSpan().CopyTo(destination);
                charsWritten = inf.Length;
                return true;
            }

            if (double.IsNegativeInfinity(value))
            {
                const string ninf = "-Infinity";
                if (destination.Length < ninf.Length) { charsWritten = 0; return false; }
                ninf.AsSpan().CopyTo(destination);
                charsWritten = ninf.Length;
                return true;
            }

            // Fixed-point split
            bool isNegative = value < 0;
            double absVal = isNegative ? -value : value;

            long wholePart = (long)absVal;
            double fracPart = absVal - wholePart;

            int clampedPrecision = Math.Min(Math.Max(precision, 0), 9);
            long multiplier = 1;
            for (int i = 0; i < clampedPrecision; i++) multiplier *= 10;
            long fracDigits = (long)Math.Round(fracPart * multiplier);

            if (fracDigits >= multiplier)
            {
                wholePart++;
                fracDigits -= multiplier;
            }

            int totalWritten = 0;
            if (isNegative)
            {
                if (destination.Length < 1) { charsWritten = 0; return false; }
                destination[0] = '-';
                totalWritten = 1;
            }

            if (!TryFormatInt64(wholePart, destination.Slice(totalWritten), out int wholeLen))
            {
                charsWritten = 0;
                return false;
            }
            totalWritten += wholeLen;

            if (clampedPrecision > 0)
            {
                if (destination.Length < totalWritten + 1 + clampedPrecision)
                {
                    charsWritten = 0;
                    return false;
                }

                destination[totalWritten++] = '.';

                // Pad leading zeros for fraction if needed
                Span<char> fracBuf = stackalloc char[16];
                TryFormatInt64(fracDigits, fracBuf, out int fracLen);

                int leadingZeros = clampedPrecision - fracLen;
                for (int z = 0; z < leadingZeros; z++)
                {
                    destination[totalWritten++] = '0';
                }

                fracBuf.Slice(0, fracLen).CopyTo(destination.Slice(totalWritten));
                totalWritten += fracLen;
            }

            charsWritten = totalWritten;
            return true;
#endif
        }
    }
}
