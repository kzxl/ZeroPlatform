using System;
using System.Text;

namespace ZeroNetwork.Common
{
    /// <summary>
    /// Ultra-fast, zero-allocation UTF-8/ASCII formatting, parsing, and validation engine.
    /// Replaces BCL <see cref="int.ToString()"/>, <see cref="int.TryParse(string, out int)"/>,
    /// <see cref="Guid.ToString()"/>, and string-heavy HTTP header / JSON formatting.
    /// </summary>
    public static class ZeroUtf8
    {
        // 200-byte table for 2-digit pairs "00" through "99"
        private static readonly byte[] TwoDigitsTable = Encoding.ASCII.GetBytes(
            "0001020304050607080910111213141516171819" +
            "2021222324252627282930313233343536373839" +
            "4041424344454647484950515253545556575859" +
            "6061626364656667686970717273747576777879" +
            "8081828384858687888990919293949596979899");

        private static readonly byte[] HexDigitsLower = Encoding.ASCII.GetBytes("0123456789abcdef");

        /// <summary>
        /// Formats an <see cref="int"/> value directly into a UTF-8 destination span without heap allocation.
        /// </summary>
        public static bool TryFormat(int value, Span<byte> destination, out int bytesWritten)
        {
            if (value == 0)
            {
                if (destination.IsEmpty) { bytesWritten = 0; return false; }
                destination[0] = (byte)'0';
                bytesWritten = 1;
                return true;
            }

            if (value == int.MinValue)
            {
                const string minIntStr = "-2147483648";
                if (destination.Length < minIntStr.Length) { bytesWritten = 0; return false; }
                for (int i = 0; i < minIntStr.Length; i++) destination[i] = (byte)minIntStr[i];
                bytesWritten = minIntStr.Length;
                return true;
            }

            bool negative = value < 0;
            uint uval = negative ? (uint)(-value) : (uint)value;

            // Compute digits count
            int digits = CountDigits(uval);
            int totalLen = digits + (negative ? 1 : 0);

            if (destination.Length < totalLen)
            {
                bytesWritten = 0;
                return false;
            }

            if (negative)
            {
                destination[0] = (byte)'-';
            }

            int pos = totalLen;
            while (uval >= 100)
            {
                uint q = uval / 100;
                uint r = uval - (q * 100);
                uval = q;

                int tblIdx = (int)(r * 2);
                destination[--pos] = TwoDigitsTable[tblIdx + 1];
                destination[--pos] = TwoDigitsTable[tblIdx];
            }

            if (uval >= 10)
            {
                int tblIdx = (int)(uval * 2);
                destination[--pos] = TwoDigitsTable[tblIdx + 1];
                destination[--pos] = TwoDigitsTable[tblIdx];
            }
            else
            {
                destination[--pos] = (byte)('0' + uval);
            }

            bytesWritten = totalLen;
            return true;
        }

        /// <summary>
        /// Formats a <see cref="long"/> value directly into a UTF-8 destination span without heap allocation.
        /// </summary>
        public static bool TryFormat(long value, Span<byte> destination, out int bytesWritten)
        {
            if (value >= int.MinValue && value <= int.MaxValue)
            {
                return TryFormat((int)value, destination, out bytesWritten);
            }

            if (value == long.MinValue)
            {
                const string minLongStr = "-9223372036854775808";
                if (destination.Length < minLongStr.Length) { bytesWritten = 0; return false; }
                for (int i = 0; i < minLongStr.Length; i++) destination[i] = (byte)minLongStr[i];
                bytesWritten = minLongStr.Length;
                return true;
            }

            bool negative = value < 0;
            ulong uval = negative ? (ulong)(-value) : (ulong)value;

            int digits = CountDigits(uval);
            int totalLen = digits + (negative ? 1 : 0);

            if (destination.Length < totalLen)
            {
                bytesWritten = 0;
                return false;
            }

            if (negative)
            {
                destination[0] = (byte)'-';
            }

            int pos = totalLen;
            while (uval >= 100)
            {
                ulong q = uval / 100;
                uint r = (uint)(uval - (q * 100));
                uval = q;

                int tblIdx = (int)(r * 2);
                destination[--pos] = TwoDigitsTable[tblIdx + 1];
                destination[--pos] = TwoDigitsTable[tblIdx];
            }

            if (uval >= 10)
            {
                int tblIdx = (int)(uval * 2);
                destination[--pos] = TwoDigitsTable[tblIdx + 1];
                destination[--pos] = TwoDigitsTable[tblIdx];
            }
            else
            {
                destination[--pos] = (byte)('0' + uval);
            }

            bytesWritten = totalLen;
            return true;
        }

        /// <summary>
        /// Formats a <see cref="Guid"/> into UTF-8 bytes directly without heap allocation.
        /// Supports format 'D' (36 chars: 8-4-4-4-12) and 'N' (32 chars).
        /// </summary>
        public static bool TryFormat(Guid value, Span<byte> destination, out int bytesWritten, char format = 'D')
        {
            Span<byte> guidBytes = stackalloc byte[16];
#if NET8_0_OR_GREATER
            value.TryWriteBytes(guidBytes);
#else
            byte[] raw = value.ToByteArray();
            raw.CopyTo(guidBytes);
#endif

            if (format == 'N' || format == 'n')
            {
                if (destination.Length < 32) { bytesWritten = 0; return false; }
                int pos = 0;
                for (int i = 0; i < 16; i++)
                {
                    byte b = guidBytes[i];
                    destination[pos++] = HexDigitsLower[b >> 4];
                    destination[pos++] = HexDigitsLower[b & 0x0F];
                }
                bytesWritten = 32;
                return true;
            }
            else // 'D' default: 8-4-4-4-12
            {
                if (destination.Length < 36) { bytesWritten = 0; return false; }
                int pos = 0;

                // First 4 bytes (time_low)
                for (int i = 3; i >= 0; i--)
                {
                    byte b = guidBytes[i];
                    destination[pos++] = HexDigitsLower[b >> 4];
                    destination[pos++] = HexDigitsLower[b & 0x0F];
                }
                destination[pos++] = (byte)'-';

                // Next 2 bytes (time_mid)
                for (int i = 5; i >= 4; i--)
                {
                    byte b = guidBytes[i];
                    destination[pos++] = HexDigitsLower[b >> 4];
                    destination[pos++] = HexDigitsLower[b & 0x0F];
                }
                destination[pos++] = (byte)'-';

                // Next 2 bytes (time_hi)
                for (int i = 7; i >= 6; i--)
                {
                    byte b = guidBytes[i];
                    destination[pos++] = HexDigitsLower[b >> 4];
                    destination[pos++] = HexDigitsLower[b & 0x0F];
                }
                destination[pos++] = (byte)'-';

                // Next 2 bytes (clock_seq)
                for (int i = 8; i <= 9; i++)
                {
                    byte b = guidBytes[i];
                    destination[pos++] = HexDigitsLower[b >> 4];
                    destination[pos++] = HexDigitsLower[b & 0x0F];
                }
                destination[pos++] = (byte)'-';

                // Remaining 6 bytes (node)
                for (int i = 10; i <= 15; i++)
                {
                    byte b = guidBytes[i];
                    destination[pos++] = HexDigitsLower[b >> 4];
                    destination[pos++] = HexDigitsLower[b & 0x0F];
                }

                bytesWritten = 36;
                return true;
            }
        }

        /// <summary>
        /// Parses an integer from a UTF-8 byte span branchlessly without string allocation or culture overhead.
        /// </summary>
        public static bool FastTryParseInt32(ReadOnlySpan<byte> source, out int result)
        {
            result = 0;
            if (source.IsEmpty) return false;

            int idx = 0;
            // Trim leading whitespaces
            while (idx < source.Length && (source[idx] == ' ' || source[idx] == '\t' || source[idx] == '\r' || source[idx] == '\n'))
                idx++;

            if (idx >= source.Length) return false;

            bool negative = false;
            if (source[idx] == '-')
            {
                negative = true;
                idx++;
            }
            else if (source[idx] == '+')
            {
                idx++;
            }

            if (idx >= source.Length) return false;

            long accumulator = 0;
            while (idx < source.Length)
            {
                byte b = source[idx];
                if (b < '0' || b > '9')
                {
                    // Allow trailing whitespaces
                    while (idx < source.Length && (source[idx] == ' ' || source[idx] == '\t' || source[idx] == '\r' || source[idx] == '\n'))
                        idx++;
                    if (idx == source.Length) break;
                    return false;
                }

                accumulator = (accumulator * 10) + (b - '0');
                if (negative)
                {
                    if (-accumulator < int.MinValue) return false;
                }
                else
                {
                    if (accumulator > int.MaxValue) return false;
                }
                idx++;
            }

            result = negative ? (int)(-accumulator) : (int)accumulator;
            return true;
        }

        /// <summary>
        /// Parses a 64-bit integer from a UTF-8 byte span without string allocation.
        /// </summary>
        public static bool FastTryParseInt64(ReadOnlySpan<byte> source, out long result)
        {
            result = 0;
            if (source.IsEmpty) return false;

            int idx = 0;
            while (idx < source.Length && (source[idx] == ' ' || source[idx] == '\t' || source[idx] == '\r' || source[idx] == '\n'))
                idx++;

            if (idx >= source.Length) return false;

            bool negative = false;
            if (source[idx] == '-')
            {
                negative = true;
                idx++;
            }
            else if (source[idx] == '+')
            {
                idx++;
            }

            if (idx >= source.Length) return false;

            ulong accumulator = 0;
            while (idx < source.Length)
            {
                byte b = source[idx];
                if (b < '0' || b > '9')
                {
                    while (idx < source.Length && (source[idx] == ' ' || source[idx] == '\t' || source[idx] == '\r' || source[idx] == '\n'))
                        idx++;
                    if (idx == source.Length) break;
                    return false;
                }

                accumulator = (accumulator * 10) + (ulong)(b - '0');
                if (negative)
                {
                    if (accumulator > ((ulong)long.MaxValue + 1)) return false;
                }
                else
                {
                    if (accumulator > (ulong)long.MaxValue) return false;
                }
                idx++;
            }

            if (negative)
            {
                result = accumulator == ((ulong)long.MaxValue + 1) ? long.MinValue : -(long)accumulator;
            }
            else
            {
                result = (long)accumulator;
            }
            return true;
        }

        /// <summary>
        /// Validates whether a byte sequence contains strictly well-formed UTF-8 characters without decoding into strings.
        /// </summary>
        public static bool IsValidUtf8(ReadOnlySpan<byte> bytes)
        {
            int i = 0;
            int len = bytes.Length;

            while (i < len)
            {
                byte b1 = bytes[i++];
                if (b1 <= 0x7F) continue; // 1-byte ASCII

                if ((b1 & 0xE0) == 0xC0) // 2-byte sequence
                {
                    if (i >= len || (bytes[i++] & 0xC0) != 0x80) return false;
                    if (b1 < 0xC2) return false; // Overlong encoding
                }
                else if ((b1 & 0xF0) == 0xE0) // 3-byte sequence
                {
                    if (i + 1 >= len) return false;
                    byte b2 = bytes[i++];
                    byte b3 = bytes[i++];
                    if ((b2 & 0xC0) != 0x80 || (b3 & 0xC0) != 0x80) return false;
                    if (b1 == 0xE0 && b2 < 0xA0) return false; // Overlong
                    if (b1 == 0xED && b2 >= 0xA0) return false; // UTF-16 surrogates
                }
                else if ((b1 & 0xF8) == 0xF0) // 4-byte sequence
                {
                    if (i + 2 >= len) return false;
                    byte b2 = bytes[i++];
                    byte b3 = bytes[i++];
                    byte b4 = bytes[i++];
                    if ((b2 & 0xC0) != 0x80 || (b3 & 0xC0) != 0x80 || (b4 & 0xC0) != 0x80) return false;
                    if (b1 == 0xF0 && b2 < 0x90) return false; // Overlong
                    if (b1 == 0xF4 && b2 > 0x8F) return false; // Above U+10FFFF
                }
                else
                {
                    return false;
                }
            }

            return true;
        }

        private static int CountDigits(uint val)
        {
            if (val < 10) return 1;
            if (val < 100) return 2;
            if (val < 1000) return 3;
            if (val < 10000) return 4;
            if (val < 100000) return 5;
            if (val < 1000000) return 6;
            if (val < 10000000) return 7;
            if (val < 100000000) return 8;
            if (val < 1000000000) return 9;
            return 10;
        }

        private static int CountDigits(ulong val)
        {
            if (val <= uint.MaxValue) return CountDigits((uint)val);

            ulong p = 10000000000UL;
            for (int i = 10; i < 20; i++)
            {
                if (val < p) return i;
                p *= 10;
            }
            return 20;
        }
    }
}
