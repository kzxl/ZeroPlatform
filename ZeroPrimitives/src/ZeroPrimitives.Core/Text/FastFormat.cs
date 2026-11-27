using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ZeroPrimitives.Time;

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

        /// <summary>
        /// Formats an Int32 into a UTF-8 <see cref="Span{Byte}"/> with 0 heap allocation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryFormatInt32Utf8(int value, Span<byte> destination, out int bytesWritten)
        {
            if (value >= 0 && value < 10)
            {
                if (destination.Length < 1)
                {
                    bytesWritten = 0;
                    return false;
                }
                destination[0] = (byte)('0' + value);
                bytesWritten = 1;
                return true;
            }

            Span<byte> buffer = stackalloc byte[12];
            int pos = 12;
            bool negative = value < 0;
            uint uval = negative ? unchecked((uint)-value) : (uint)value;

            while (uval >= 100)
            {
                uint rem = uval % 100;
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

            int count = 12 - pos;
            if (destination.Length < count)
            {
                bytesWritten = 0;
                return false;
            }

            buffer.Slice(pos, count).CopyTo(destination);
            bytesWritten = count;
            return true;
        }

        /// <summary>
        /// Formats a UInt64 into a <see cref="Span{Char}"/> with 0 heap allocation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryFormatUInt64(ulong value, Span<char> destination, out int charsWritten)
        {
            if (value < 10)
            {
                if (destination.Length < 1) { charsWritten = 0; return false; }
                destination[0] = (char)('0' + value);
                charsWritten = 1;
                return true;
            }

            Span<char> buffer = stackalloc char[24];
            int pos = 24;
            while (value >= 100)
            {
                ulong rem = value % 100;
                value /= 100;
                int idx = (int)(rem * 2);
                buffer[--pos] = TwoDigitDigits[idx + 1];
                buffer[--pos] = TwoDigitDigits[idx];
            }

            if (value < 10)
            {
                buffer[--pos] = (char)('0' + value);
            }
            else
            {
                int idx = (int)(value * 2);
                buffer[--pos] = TwoDigitDigits[idx + 1];
                buffer[--pos] = TwoDigitDigits[idx];
            }

            int count = 24 - pos;
            if (destination.Length < count) { charsWritten = 0; return false; }
            buffer.Slice(pos, count).CopyTo(destination);
            charsWritten = count;
            return true;
        }

        /// <summary>
        /// Formats a UInt64 into a UTF-8 <see cref="Span{Byte}"/> with 0 heap allocation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryFormatUInt64Utf8(ulong value, Span<byte> destination, out int bytesWritten)
        {
            if (value < 10)
            {
                if (destination.Length < 1) { bytesWritten = 0; return false; }
                destination[0] = (byte)('0' + value);
                bytesWritten = 1;
                return true;
            }

            Span<byte> buffer = stackalloc byte[24];
            int pos = 24;
            while (value >= 100)
            {
                ulong rem = value % 100;
                value /= 100;
                int idx = (int)(rem * 2);
                buffer[--pos] = TwoDigitBytes[idx + 1];
                buffer[--pos] = TwoDigitBytes[idx];
            }

            if (value < 10)
            {
                buffer[--pos] = (byte)('0' + value);
            }
            else
            {
                int idx = (int)(value * 2);
                buffer[--pos] = TwoDigitBytes[idx + 1];
                buffer[--pos] = TwoDigitBytes[idx];
            }

            int count = 24 - pos;
            if (destination.Length < count) { bytesWritten = 0; return false; }
            buffer.Slice(pos, count).CopyTo(destination);
            bytesWritten = count;
            return true;
        }

        #endregion

        #region Decimal Formatting

#if !NET8_0_OR_GREATER
        [StructLayout(LayoutKind.Explicit)]
        private struct DecimalLayout
        {
            [FieldOffset(0)] public int Flags;
            [FieldOffset(4)] public int Hi;
            [FieldOffset(8)] public int Lo;
            [FieldOffset(12)] public int Mid;
        }
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ExtractDecimalDigits(decimal value, Span<byte> digitsLeastFirst, out bool negative, out int scale)
        {
            int flags;
            uint uHi, uMid, uLo;
#if NET8_0_OR_GREATER
            Span<int> bits = stackalloc int[4];
            decimal.GetBits(value, bits);
            uLo = (uint)bits[0];
            uMid = (uint)bits[1];
            uHi = (uint)bits[2];
            flags = bits[3];
#else
            ref DecimalLayout layout = ref Unsafe.As<decimal, DecimalLayout>(ref value);
            flags = layout.Flags;
            uHi = (uint)layout.Hi;
            uMid = (uint)layout.Mid;
            uLo = (uint)layout.Lo;
#endif
            negative = (flags & int.MinValue) != 0;
            scale = (flags >> 16) & 0xFF;

            if (uHi == 0 && uMid == 0 && uLo == 0)
            {
                negative = false;
                return 0;
            }

            int digitCount = 0;
            while (uHi != 0 || uMid != 0 || uLo != 0)
            {
                ulong rem = uHi % 10;
                uHi /= 10;
                ulong cur = (rem << 32) | uMid;
                uMid = (uint)(cur / 10);
                rem = cur % 10;
                cur = (rem << 32) | uLo;
                uLo = (uint)(cur / 10);
                digitsLeastFirst[digitCount++] = (byte)(cur % 10);
            }

            return digitCount;
        }

        /// <summary>
        /// Formats a <see cref="decimal"/> in invariant culture format with zero heap allocation across all target frameworks.
        /// </summary>
        public static bool TryFormatDecimal(decimal value, Span<char> destination, out int charsWritten)
        {
            Span<byte> digits = stackalloc byte[32];
            int digitCount = ExtractDecimalDigits(value, digits, out bool negative, out int scale);

            int required = negative ? 1 : 0;
            if (digitCount == 0)
            {
                required += scale == 0 ? 1 : 2 + scale;
            }
            else if (scale == 0)
            {
                required += digitCount;
            }
            else if (digitCount <= scale)
            {
                required += 2 + scale;
            }
            else
            {
                required += digitCount + 1;
            }

            if (destination.Length < required)
            {
                charsWritten = 0;
                return false;
            }

            int pos = 0;
            if (negative) destination[pos++] = '-';

            if (digitCount == 0)
            {
                destination[pos++] = '0';
                if (scale > 0)
                {
                    destination[pos++] = '.';
                    for (int i = 0; i < scale; i++) destination[pos++] = '0';
                }
            }
            else if (scale == 0)
            {
                for (int i = digitCount - 1; i >= 0; i--)
                    destination[pos++] = (char)('0' + digits[i]);
            }
            else if (digitCount <= scale)
            {
                destination[pos++] = '0';
                destination[pos++] = '.';
                for (int i = 0; i < scale - digitCount; i++) destination[pos++] = '0';
                for (int i = digitCount - 1; i >= 0; i--)
                    destination[pos++] = (char)('0' + digits[i]);
            }
            else
            {
                for (int i = digitCount - 1; i >= scale; i--)
                    destination[pos++] = (char)('0' + digits[i]);
                destination[pos++] = '.';
                for (int i = scale - 1; i >= 0; i--)
                    destination[pos++] = (char)('0' + digits[i]);
            }

            charsWritten = pos;
            return true;
        }

        /// <summary>
        /// Formats a <see cref="decimal"/> into UTF-8 bytes in invariant culture format with zero heap allocation across all target frameworks.
        /// </summary>
        public static bool TryFormatDecimalUtf8(decimal value, Span<byte> destination, out int bytesWritten)
        {
            Span<byte> digits = stackalloc byte[32];
            int digitCount = ExtractDecimalDigits(value, digits, out bool negative, out int scale);

            int required = negative ? 1 : 0;
            if (digitCount == 0)
            {
                required += scale == 0 ? 1 : 2 + scale;
            }
            else if (scale == 0)
            {
                required += digitCount;
            }
            else if (digitCount <= scale)
            {
                required += 2 + scale;
            }
            else
            {
                required += digitCount + 1;
            }

            if (destination.Length < required)
            {
                bytesWritten = 0;
                return false;
            }

            int pos = 0;
            if (negative) destination[pos++] = (byte)'-';

            if (digitCount == 0)
            {
                destination[pos++] = (byte)'0';
                if (scale > 0)
                {
                    destination[pos++] = (byte)'.';
                    for (int i = 0; i < scale; i++) destination[pos++] = (byte)'0';
                }
            }
            else if (scale == 0)
            {
                for (int i = digitCount - 1; i >= 0; i--)
                    destination[pos++] = (byte)('0' + digits[i]);
            }
            else if (digitCount <= scale)
            {
                destination[pos++] = (byte)'0';
                destination[pos++] = (byte)'.';
                for (int i = 0; i < scale - digitCount; i++) destination[pos++] = (byte)'0';
                for (int i = digitCount - 1; i >= 0; i--)
                    destination[pos++] = (byte)('0' + digits[i]);
            }
            else
            {
                for (int i = digitCount - 1; i >= scale; i--)
                    destination[pos++] = (byte)('0' + digits[i]);
                destination[pos++] = (byte)'.';
                for (int i = scale - 1; i >= 0; i--)
                    destination[pos++] = (byte)('0' + digits[i]);
            }

            bytesWritten = pos;
            return true;
        }

        /// <summary>
        /// Formats a <see cref="decimal"/> with thousand grouping separators and configurable decimal precision without heap allocation.
        /// </summary>
        public static bool TryFormatDecimalGrouped(
            decimal value,
            Span<char> destination,
            out int charsWritten,
            int decimals = -1,
            char thousandSeparator = ',',
            char decimalSeparator = '.',
            bool trimTrailingZeros = false)
        {
            if (decimals >= 0)
            {
                if ((uint)decimals > 28U)
                    throw new ArgumentOutOfRangeException(nameof(decimals), "Decimals must be between 0 and 28.");
                value = Math.Round(value, decimals, MidpointRounding.AwayFromZero);
            }

            Span<byte> digits = stackalloc byte[32];
            int digitCount = ExtractDecimalDigits(value, digits, out bool negative, out int scale);

            int fracStart = 0;
            int effScale = scale;
            if (trimTrailingZeros && effScale > 0)
            {
                if (digitCount == 0)
                {
                    effScale = 0;
                }
                else
                {
                    while (effScale > 0 && fracStart < digitCount && digits[fracStart] == 0)
                    {
                        fracStart++;
                        effScale--;
                    }
                }
            }

            int effDigitCount = digitCount - fracStart;
            int intDigitCount = (digitCount == 0 || effDigitCount <= effScale) ? 1 : (effDigitCount - effScale);
            int groupSepCount = thousandSeparator != '\0' ? (intDigitCount - 1) / 3 : 0;

            int required = (negative ? 1 : 0) + intDigitCount + groupSepCount + (effScale > 0 ? 1 + effScale : 0);
            if (destination.Length < required)
            {
                charsWritten = 0;
                return false;
            }

            int pos = 0;
            if (negative) destination[pos++] = '-';

            if (digitCount == 0 || effDigitCount <= effScale)
            {
                destination[pos++] = '0';
            }
            else
            {
                int intTop = digitCount - 1;
                int intBottom = fracStart + effScale;
                for (int i = intTop; i >= intBottom; i--)
                {
                    destination[pos++] = (char)('0' + digits[i]);
                    int remaining = i - intBottom;
                    if (thousandSeparator != '\0' && remaining > 0 && (remaining % 3) == 0)
                    {
                        destination[pos++] = thousandSeparator;
                    }
                }
            }

            if (effScale > 0)
            {
                destination[pos++] = decimalSeparator;
                if (digitCount == 0)
                {
                    for (int i = 0; i < effScale; i++) destination[pos++] = '0';
                }
                else
                {
                    int leadingZeros = effScale - effDigitCount;
                    for (int i = 0; i < leadingZeros; i++) destination[pos++] = '0';
                    int fracTop = Math.Min(digitCount - 1, fracStart + effScale - 1);
                    for (int i = fracTop; i >= fracStart; i--)
                    {
                        destination[pos++] = (char)('0' + digits[i]);
                    }
                }
            }

            charsWritten = pos;
            return true;
        }

        #endregion

        #region Date & Guid Formatting

        public static bool TryFormatIsoDate(DateTime value, Span<char> destination, out int charsWritten)
        {
            if (destination.Length < 10) { charsWritten = 0; return false; }
            CompactDate.Write4Digits(destination, 0, value.Year);
            destination[4] = '-';
            CompactDate.Write2Digits(destination, 5, value.Month);
            destination[7] = '-';
            CompactDate.Write2Digits(destination, 8, value.Day);
            charsWritten = 10;
            return true;
        }

        public static bool TryFormatIsoDateUtf8(DateTime value, Span<byte> destination, out int bytesWritten)
        {
            if (destination.Length < 10) { bytesWritten = 0; return false; }
            CompactDate.Write4DigitsUtf8(destination, 0, value.Year);
            destination[4] = (byte)'-';
            CompactDate.Write2DigitsUtf8(destination, 5, value.Month);
            destination[7] = (byte)'-';
            CompactDate.Write2DigitsUtf8(destination, 8, value.Day);
            bytesWritten = 10;
            return true;
        }

        public static bool TryFormatIsoDateTime(
            DateTime value,
            Span<char> destination,
            out int charsWritten,
            bool includeMilliseconds = false,
            bool useT = true)
        {
            bool isUtc = value.Kind == DateTimeKind.Utc;
            int required = 19 + (includeMilliseconds ? 4 : 0) + (isUtc ? 1 : 0);
            if (destination.Length < required) { charsWritten = 0; return false; }

            CompactDate.Write4Digits(destination, 0, value.Year);
            destination[4] = '-';
            CompactDate.Write2Digits(destination, 5, value.Month);
            destination[7] = '-';
            CompactDate.Write2Digits(destination, 8, value.Day);
            destination[10] = useT ? 'T' : ' ';
            CompactDate.Write2Digits(destination, 11, value.Hour);
            destination[13] = ':';
            CompactDate.Write2Digits(destination, 14, value.Minute);
            destination[16] = ':';
            CompactDate.Write2Digits(destination, 17, value.Second);

            int pos = 19;
            if (includeMilliseconds)
            {
                int ms = value.Millisecond;
                destination[pos++] = '.';
                destination[pos++] = (char)('0' + (ms / 100));
                destination[pos++] = (char)('0' + ((ms / 10) % 10));
                destination[pos++] = (char)('0' + (ms % 10));
            }
            if (isUtc) destination[pos++] = 'Z';

            charsWritten = pos;
            return true;
        }

        public static bool TryFormatIsoDateTimeUtf8(
            DateTime value,
            Span<byte> destination,
            out int bytesWritten,
            bool includeMilliseconds = false,
            bool useT = true)
        {
            bool isUtc = value.Kind == DateTimeKind.Utc;
            int required = 19 + (includeMilliseconds ? 4 : 0) + (isUtc ? 1 : 0);
            if (destination.Length < required) { bytesWritten = 0; return false; }

            CompactDate.Write4DigitsUtf8(destination, 0, value.Year);
            destination[4] = (byte)'-';
            CompactDate.Write2DigitsUtf8(destination, 5, value.Month);
            destination[7] = (byte)'-';
            CompactDate.Write2DigitsUtf8(destination, 8, value.Day);
            destination[10] = (byte)(useT ? 'T' : ' ');
            CompactDate.Write2DigitsUtf8(destination, 11, value.Hour);
            destination[13] = (byte)':';
            CompactDate.Write2DigitsUtf8(destination, 14, value.Minute);
            destination[16] = (byte)':';
            CompactDate.Write2DigitsUtf8(destination, 17, value.Second);

            int pos = 19;
            if (includeMilliseconds)
            {
                int ms = value.Millisecond;
                destination[pos++] = (byte)'.';
                destination[pos++] = (byte)('0' + (ms / 100));
                destination[pos++] = (byte)('0' + ((ms / 10) % 10));
                destination[pos++] = (byte)('0' + (ms % 10));
            }
            if (isUtc) destination[pos++] = (byte)'Z';

            bytesWritten = pos;
            return true;
        }

        /// <summary>
        /// Formats a <see cref="DateTime"/> in day-first format (<c>dd/MM/yyyy</c> by default) with zero heap allocation.
        /// </summary>
        public static bool TryFormatDate(DateTime value, Span<char> destination, out int charsWritten, char separator = '/')
        {
            if (destination.Length < 10) { charsWritten = 0; return false; }
            CompactDate.Write2Digits(destination, 0, value.Day);
            destination[2] = separator;
            CompactDate.Write2Digits(destination, 3, value.Month);
            destination[5] = separator;
            CompactDate.Write4Digits(destination, 6, value.Year);
            charsWritten = 10;
            return true;
        }

        public static bool TryFormatGuid(Guid value, Span<char> destination, out int charsWritten, char format = 'D')
        {
            char fmt = char.ToUpperInvariant(format);
            int required = (fmt == 'N') ? 32 : ((fmt == 'B' || fmt == 'P') ? 38 : 36);
            if (destination.Length < required) { charsWritten = 0; return false; }

            Span<byte> bytes = stackalloc byte[16];
#if NET8_0_OR_GREATER
            value.TryWriteBytes(bytes);
#else
            value.ToByteArray().AsSpan().CopyTo(bytes);
#endif
            if (BitConverter.IsLittleEndian)
            {
                byte t0 = bytes[0]; byte t1 = bytes[1]; byte t2 = bytes[2]; byte t3 = bytes[3];
                bytes[0] = t3; bytes[1] = t2; bytes[2] = t1; bytes[3] = t0;
                byte t4 = bytes[4]; byte t5 = bytes[5]; bytes[4] = t5; bytes[5] = t4;
                byte t6 = bytes[6]; byte t7 = bytes[7]; bytes[6] = t7; bytes[7] = t6;
            }

            const string hexDigits = "0123456789abcdef";
            int dstIdx = 0;
            if (fmt == 'B') destination[dstIdx++] = '{';
            else if (fmt == 'P') destination[dstIdx++] = '(';

            for (int i = 0; i < 16; i++)
            {
                if (fmt != 'N' && (i == 4 || i == 6 || i == 8 || i == 10))
                    destination[dstIdx++] = '-';
                byte b = bytes[i];
                destination[dstIdx++] = hexDigits[b >> 4];
                destination[dstIdx++] = hexDigits[b & 0x0F];
            }

            if (fmt == 'B') destination[dstIdx++] = '}';
            else if (fmt == 'P') destination[dstIdx++] = ')';

            charsWritten = dstIdx;
            return true;
        }

        public static bool TryFormatGuidUtf8(Guid value, Span<byte> destination, out int bytesWritten, char format = 'D')
        {
            char fmt = char.ToUpperInvariant(format);
            int required = (fmt == 'N') ? 32 : ((fmt == 'B' || fmt == 'P') ? 38 : 36);
            if (destination.Length < required) { bytesWritten = 0; return false; }

            Span<byte> bytes = stackalloc byte[16];
#if NET8_0_OR_GREATER
            value.TryWriteBytes(bytes);
#else
            value.ToByteArray().AsSpan().CopyTo(bytes);
#endif
            if (BitConverter.IsLittleEndian)
            {
                byte t0 = bytes[0]; byte t1 = bytes[1]; byte t2 = bytes[2]; byte t3 = bytes[3];
                bytes[0] = t3; bytes[1] = t2; bytes[2] = t1; bytes[3] = t0;
                byte t4 = bytes[4]; byte t5 = bytes[5]; bytes[4] = t5; bytes[5] = t4;
                byte t6 = bytes[6]; byte t7 = bytes[7]; bytes[6] = t7; bytes[7] = t6;
            }

            const string hexDigits = "0123456789abcdef";
            int dstIdx = 0;
            if (fmt == 'B') destination[dstIdx++] = (byte)'{';
            else if (fmt == 'P') destination[dstIdx++] = (byte)'(';

            for (int i = 0; i < 16; i++)
            {
                if (fmt != 'N' && (i == 4 || i == 6 || i == 8 || i == 10))
                    destination[dstIdx++] = (byte)'-';
                byte b = bytes[i];
                destination[dstIdx++] = (byte)hexDigits[b >> 4];
                destination[dstIdx++] = (byte)hexDigits[b & 0x0F];
            }

            if (fmt == 'B') destination[dstIdx++] = (byte)'}';
            else if (fmt == 'P') destination[dstIdx++] = (byte)')';

            bytesWritten = dstIdx;
            return true;
        }

        #endregion
    }
}
