using System;
using System.Runtime.CompilerServices;

namespace ZeroPrimitives.Text
{
    /// <summary>
    /// Zero-allocation text and span operations for currency cleaning, whitespace trimming, and hex encoding.
    /// </summary>
    public static class SpanTextOps
    {
        private static readonly string HexCharsUpper = "0123456789ABCDEF";
        private static readonly string HexCharsLower = "0123456789abcdef";

        /// <summary>
        /// Trims whitespace and removes common currency symbols without allocating intermediate strings.
        /// Handles: VNĐ, VND, vnđ, vnd, đ, ₫, $, €, ¥, £.
        /// </summary>
        public static ReadOnlySpan<char> CleanCurrency(ReadOnlySpan<char> span)
            => CleanCurrency(span, out _);

        /// <summary>
        /// Trims whitespace and removes common currency symbols without allocating intermediate strings,
        /// and reports whether Vietnamese currency markings (VNĐ, VND, đ, ₫) were detected.
        /// </summary>
        public static ReadOnlySpan<char> CleanCurrency(ReadOnlySpan<char> span, out bool hasVnCurrency)
        {
            hasVnCurrency = false;
            span = TrimAsciiWhitespace(span);
            if (span.IsEmpty) return span;

            // Check prefix currency symbols ($, €, ¥, £, ₫, đ)
            while (!span.IsEmpty)
            {
                char c = span[0];
                if (c == '₫' || c == 'đ')
                {
                    hasVnCurrency = true;
                    span = span.Slice(1);
                    span = TrimAsciiWhitespace(span);
                }
                else if (c == '$' || c == '€' || c == '¥' || c == '£')
                {
                    span = span.Slice(1);
                    span = TrimAsciiWhitespace(span);
                }
                else
                {
                    break;
                }
            }

            // Check suffix currency symbols
            while (!span.IsEmpty)
            {
                if (EndsWithOrdinalIgnoreCase(span, "VNĐ") || EndsWithOrdinalIgnoreCase(span, "VND"))
                {
                    hasVnCurrency = true;
                    span = span.Slice(0, span.Length - 3);
                    span = TrimAsciiWhitespace(span);
                }
                else if (EndsWithOrdinalIgnoreCase(span, "USD"))
                {
                    span = span.Slice(0, span.Length - 3);
                    span = TrimAsciiWhitespace(span);
                }
                else
                {
                    char c = span[span.Length - 1];
                    if (c == '₫' || c == 'đ')
                    {
                        hasVnCurrency = true;
                        span = span.Slice(0, span.Length - 1);
                        span = TrimAsciiWhitespace(span);
                    }
                    else if (c == '$' || c == '€' || c == '¥' || c == '£')
                    {
                        span = span.Slice(0, span.Length - 1);
                        span = TrimAsciiWhitespace(span);
                    }
                    else
                    {
                        break;
                    }
                }
            }

            return span;
        }
        /// <summary>
        /// Fast whitespace trim for ReadOnlySpan without allocating.
        /// </summary>
        public static ReadOnlySpan<char> TrimAsciiWhitespace(ReadOnlySpan<char> span)
        {
            int start = 0;
            int end = span.Length - 1;

            while (start <= end && char.IsWhiteSpace(span[start]))
            {
                start++;
            }

            while (end >= start && char.IsWhiteSpace(span[end]))
            {
                end--;
            }

            return start <= end ? span.Slice(start, end - start + 1) : ReadOnlySpan<char>.Empty;
        }

        private static bool EndsWithOrdinalIgnoreCase(ReadOnlySpan<char> span, string suffix)
        {
            if (span.Length < suffix.Length) return false;
            int offset = span.Length - suffix.Length;
            for (int i = 0; i < suffix.Length; i++)
            {
                char c1 = span[offset + i];
                char c2 = suffix[i];
                if (char.ToUpperInvariant(c1) != char.ToUpperInvariant(c2))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Encodes binary data to hex characters in a pre-allocated span.
        /// </summary>
        public static int BytesToHex(ReadOnlySpan<byte> bytes, Span<char> output, bool lowerCase = false)
        {
            if (output.Length < bytes.Length * 2)
                throw new ArgumentException("Output span is too small.", nameof(output));

            string hexMap = lowerCase ? HexCharsLower : HexCharsUpper;
            int outIdx = 0;

            for (int i = 0; i < bytes.Length; i++)
            {
                byte b = bytes[i];
                output[outIdx++] = hexMap[b >> 4];
                output[outIdx++] = hexMap[b & 0x0F];
            }

            return outIdx;
        }

        /// <summary>
        /// Parses hex characters to bytes in a pre-allocated span.
        /// </summary>
        public static bool TryHexToBytes(ReadOnlySpan<char> hex, Span<byte> output, out int bytesWritten)
        {
            bytesWritten = 0;
            hex = TrimAsciiWhitespace(hex);
            if (hex.Length % 2 != 0) return false;
            if (output.Length < hex.Length / 2) return false;

            int outIdx = 0;
            for (int i = 0; i < hex.Length; i += 2)
            {
                int h1 = HexVal(hex[i]);
                int h2 = HexVal(hex[i + 1]);
                if (h1 < 0 || h2 < 0) return false;
                output[outIdx++] = (byte)((h1 << 4) | h2);
            }

            bytesWritten = outIdx;
            return true;
        }

        private static int HexVal(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            return -1;
        }

        /// <summary>
        /// Returns true if the span contains only ASCII digit characters ('0'-'9').
        /// Returns false if empty or contains non-digit characters.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsDigitsOnly(ReadOnlySpan<char> span)
        {
            if (span.IsEmpty) return false;
            for (int i = 0; i < span.Length; i++)
            {
                char c = span[i];
                if (c < '0' || c > '9') return false;
            }
            return true;
        }

        /// <summary>
        /// Returns true if the string contains only ASCII digit characters ('0'-'9').
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsDigitsOnly(string? value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            return IsDigitsOnly(value.AsSpan());
        }

        /// <summary>
        /// Returns true if the span represents a valid 32-bit integer (optionally allowing negative sign).
        /// Rejects decimal numbers, exponents, and non-digit characters.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsInteger(ReadOnlySpan<char> span, bool allowNegative = true)
        {
            span = TrimAsciiWhitespace(span);
            if (span.IsEmpty) return false;

            int start = 0;
            if (span[0] == '-')
            {
                if (!allowNegative || span.Length == 1) return false;
                start = 1;
            }
            else if (span[0] == '+')
            {
                if (span.Length == 1) return false;
                start = 1;
            }

            for (int i = start; i < span.Length; i++)
            {
                char c = span[i];
                if (c < '0' || c > '9') return false;
            }

#if NET8_0_OR_GREATER
            return int.TryParse(span, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out _);
#else
            return int.TryParse(span.ToString(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out _);
#endif
        }

        /// <summary>
        /// Returns true if the string represents a valid 32-bit integer (optionally allowing negative sign).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsInteger(string? value, bool allowNegative = true)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            return IsInteger(value.AsSpan(), allowNegative);
        }
    }
}
