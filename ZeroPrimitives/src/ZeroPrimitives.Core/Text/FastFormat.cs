using System;
using System.Runtime.CompilerServices;

namespace ZeroPrimitives.Text
{
    /// <summary>
    /// High-throughput, zero-allocation number formatter for integer types.
    /// Employs a 2-digit radix-10 reverse lookup table for register-speed conversion into Span&lt;char&gt; and Span&lt;byte&gt;.
    /// Provides .NET 8 TryFormat equivalence for legacy runtimes (.NET Framework 4.6.2 and .NET Standard 2.0).
    /// </summary>
    public static class FastFormat
    {
        // 2-digit lookup table for Radix-10 fast formatting (00 .. 99)
        private const string TwoDigitDigits =
            "00010203040506070809" +
            "10111213141516171819" +
            "20212223242526272829" +
            "30313233343536373839" +
            "40414243444546474849" +
            "50515253545556575859" +
            "60616263646566676869" +
            "70717273747576777879" +
            "80818283848586878889" +
            "90919293949596979899";

        private static readonly byte[] TwoDigitBytes = System.Text.Encoding.ASCII.GetBytes(TwoDigitDigits);

        #region UTF-16 (Span<char>) Formatting

        /// <summary>
        /// Formats an Int32 into a Span&lt;char&gt; with 0 heap allocation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryFormatInt32(int value, Span<char> destination, out int charsWritten)
        {
            if (value >= 0 && value < 10)
            {
                if (destination.Length < 1)
                {
                    charsWritten = 0;
                    return false;
                }
                destination[0] = (char)('0' + value);
                charsWritten = 1;
                return true;
            }

            Span<char> buffer = stackalloc char[12]; // Max "-2147483648" = 11 chars
            int pos = 12;
            bool negative = value < 0;
            uint uval = negative ? unchecked((uint)-value) : (uint)value;

            while (uval >= 100)
            {
                uint rem = uval % 100;
                uval /= 100;
                int idx = (int)(rem * 2);
                buffer[--pos] = TwoDigitDigits[idx + 1];
                buffer[--pos] = TwoDigitDigits[idx];
            }

            if (uval < 10)
            {
                buffer[--pos] = (char)('0' + uval);
            }
            else
            {
                int idx = (int)(uval * 2);
                buffer[--pos] = TwoDigitDigits[idx + 1];
                buffer[--pos] = TwoDigitDigits[idx];
            }

            if (negative)
            {
                buffer[--pos] = '-';
            }

            int count = 12 - pos;
            if (destination.Length < count)
            {
                charsWritten = 0;
                return false;
            }

            buffer.Slice(pos, count).CopyTo(destination);
            charsWritten = count;
            return true;
        }

        /// <summary>
        /// Formats an Int64 into a Span&lt;char&gt; with 0 heap allocation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryFormatInt64(long value, Span<char> destination, out int charsWritten)
        {
            if (value >= 0 && value <= int.MaxValue)
            {
                return TryFormatInt32((int)value, destination, out charsWritten);
            }

            Span<char> buffer = stackalloc char[24]; // Max "-9223372036854775808" = 20 chars
            int pos = 24;
            bool negative = value < 0;
            ulong uval = negative ? unchecked((ulong)-value) : (ulong)value;

            while (uval >= 100)
            {
                ulong rem = uval % 100;
                uval /= 100;
                int idx = (int)(rem * 2);
                buffer[--pos] = TwoDigitDigits[idx + 1];
                buffer[--pos] = TwoDigitDigits[idx];
            }

            if (uval < 10)
            {
                buffer[--pos] = (char)('0' + uval);
            }
            else
            {
                int idx = (int)(uval * 2);
                buffer[--pos] = TwoDigitDigits[idx + 1];
                buffer[--pos] = TwoDigitDigits[idx];
            }

            if (negative)
            {
                buffer[--pos] = '-';
            }

            int count = 24 - pos;
            if (destination.Length < count)
            {
                charsWritten = 0;
                return false;
            }

            buffer.Slice(pos, count).CopyTo(destination);
            charsWritten = count;
            return true;
        }

        #endregion

        #region UTF-8 (Span<byte>) Formatting

        /// <summary>
        /// Formats an Int64 into a Span&lt;byte&gt; (ASCII/UTF-8) with 0 heap allocation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryFormatInt64Utf8(long value, Span<byte> destination, out int bytesWritten)
        {
            Span<byte> buffer = stackalloc byte[24];
            int pos = 24;
            bool negative = value < 0;
            ulong uval = negative ? unchecked((ulong)-value) : (ulong)value;

            while (uval >= 100)
            {
                ulong rem = uval % 100;
                uval /= 100;
                int idx = (int)(rem * 2);
                buffer[--pos] = TwoDigitBytes[idx + 1];
                buffer[--pos] = TwoDigitBytes[idx];
            }

            if (uval < 10)
            {
                buffer[--pos] = (byte)('0' + uval);
            }
            else
            {
                int idx = (int)(uval * 2);
                buffer[--pos] = TwoDigitBytes[idx + 1];
                buffer[--pos] = TwoDigitBytes[idx];
            }

            if (negative)
            {
                buffer[--pos] = (byte)'-';
            }

            int count = 24 - pos;
            if (destination.Length < count)
            {
                bytesWritten = 0;
                return false;
            }

            buffer.Slice(pos, count).CopyTo(destination);
            bytesWritten = count;
            return true;
        }

        #endregion
    }
}
