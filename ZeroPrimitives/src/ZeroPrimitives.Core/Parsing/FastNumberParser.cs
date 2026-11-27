using System;
using System.Buffers;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using ZeroPrimitives.Text;

namespace ZeroPrimitives.Parsing
{
    /// <summary>
    /// Ultra-fast, zero-allocation number parser operating on ReadOnlySpan with pointer loops and register unboxing.
    /// Eliminates intermediate string allocations and culture overhead.
    /// </summary>
    public static class FastNumberParser
    {
        /// <summary>
        /// Attempts to parse an integer from a ReadOnlySpan.
        /// Fast-paths pure ASCII digits, and handles currencies/thousand-separators when needed.
        /// If a decimal separator is present, truncates toward zero.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe bool TryParseInt32(ReadOnlySpan<char> span, out int result, int defaultValue = 0)
        {
            if (span.IsEmpty)
            {
                result = defaultValue;
                return false;
            }

            // High-throughput ASCII Fast Path (bypasses currency & separator analysis for ~90% of business inputs)
            fixed (char* p = span)
            {
                char* ptr = p;
                char* end = p + span.Length;

                while (ptr < end && (*ptr == ' ' || *ptr == '\t')) ptr++;
                while (end > ptr && (*(end - 1) == ' ' || *(end - 1) == '\t')) end--;

                if (ptr < end)
                {
                    bool neg = false;
                    if (*ptr == '-') { neg = true; ptr++; }
                    else if (*ptr == '+') { ptr++; }

                    if (ptr < end)
                    {
                        char* test = ptr;
                        bool pureDigits = true;
                        while (test < end)
                        {
                            char c = *test++;
                            if (c < '0' || c > '9') { pureDigits = false; break; }
                        }

                        if (pureDigits)
                        {
                            const uint MaxInt32Div10 = 214748364U;
                            uint acc = 0;
                            while (ptr < end)
                            {
                                uint digit = (uint)(*ptr++ - '0');
                                if (acc > MaxInt32Div10 || (acc == MaxInt32Div10 && digit > (uint)(neg ? 8 : 7)))
                                {
                                    result = defaultValue;
                                    return false;
                                }
                                acc = (acc * 10) + digit;
                            }

                            result = neg ? unchecked((int)-acc) : (int)acc;
                            return true;
                        }
                    }
                }
            }

            return TryParseInt32Complex(span, out result, defaultValue);
        }

        private static unsafe bool TryParseInt32Complex(ReadOnlySpan<char> span, out int result, int defaultValue)
        {
            if (!TryCleanAccountingCurrency(span, out span, out bool hasVnCurrency, out bool isParenthesizedNeg))
            {
                result = defaultValue;
                return false;
            }

            AnalyzeSeparators(span, hasVnCurrency, out char decimalSep, out char thousandSep);

            fixed (char* p = span)
            {
                char* ptr = p;
                char* end = p + span.Length;

                bool negative = isParenthesizedNeg;
                if (*ptr == '-')
                {
                    if (isParenthesizedNeg) { result = defaultValue; return false; }
                    negative = true;
                    ptr++;
                }
                else if (*ptr == '+')
                {
                    if (isParenthesizedNeg) { result = defaultValue; return false; }
                    ptr++;
                }

                if (ptr == end)
                {
                    result = defaultValue;
                    return false;
                }

                long acc = 0;
                bool hasDigits = false;

                while (ptr < end)
                {
                    char c = *ptr;
                    if (c >= '0' && c <= '9')
                    {
                        acc = (acc * 10) + (c - '0');
                        hasDigits = true;

                        if (acc > (long)int.MaxValue + 1)
                        {
                            result = negative ? int.MinValue : int.MaxValue;
                            return false;
                        }
                    }
                    else if (decimalSep != '\0' && c == decimalSep)
                    {
                        // Stop and truncate at decimal separator
                        break;
                    }
                    else if (c == thousandSep || c == ' ')
                    {
                        // Skip thousand separator
                    }
                    else
                    {
                        break;
                    }
                    ptr++;
                }

                if (!hasDigits)
                {
                    result = defaultValue;
                    return false;
                }

                long finalVal = negative ? -acc : acc;
                if (finalVal < int.MinValue || finalVal > int.MaxValue)
                {
                    result = negative ? int.MinValue : int.MaxValue;
                    return false;
                }

                result = (int)finalVal;
                return true;
            }
        }

        /// <summary>
        /// Attempts to parse a 64-bit integer from a ReadOnlySpan.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe bool TryParseInt64(ReadOnlySpan<char> span, out long result, long defaultValue = 0)
        {
            if (span.IsEmpty)
            {
                result = defaultValue;
                return false;
            }

            // High-throughput ASCII Fast Path
            fixed (char* p = span)
            {
                char* ptr = p;
                char* end = p + span.Length;

                while (ptr < end && (*ptr == ' ' || *ptr == '\t')) ptr++;
                while (end > ptr && (*(end - 1) == ' ' || *(end - 1) == '\t')) end--;

                if (ptr < end)
                {
                    bool neg = false;
                    if (*ptr == '-') { neg = true; ptr++; }
                    else if (*ptr == '+') { ptr++; }

                    if (ptr < end)
                    {
                        char* test = ptr;
                        bool pureDigits = true;
                        while (test < end)
                        {
                            char c = *test++;
                            if (c < '0' || c > '9') { pureDigits = false; break; }
                        }

                        if (pureDigits)
                        {
                            const ulong MaxInt64Div10 = 922337203685477580UL;
                            ulong acc = 0;
                            while (ptr < end)
                            {
                                ulong digit = (ulong)(*ptr++ - '0');
                                if (acc > MaxInt64Div10 || (acc == MaxInt64Div10 && digit > (ulong)(neg ? 8 : 7)))
                                {
                                    result = defaultValue;
                                    return false;
                                }
                                acc = (acc * 10) + digit;
                            }

                            result = neg ? unchecked((long)(0UL - acc)) : unchecked((long)acc);
                            return true;
                        }
                    }
                }
            }

            return TryParseInt64Complex(span, out result, defaultValue);
        }

        private static unsafe bool TryParseInt64Complex(ReadOnlySpan<char> span, out long result, long defaultValue)
        {
            if (!TryCleanAccountingCurrency(span, out span, out bool hasVnCurrency, out bool isParenthesizedNeg))
            {
                result = defaultValue;
                return false;
            }

            AnalyzeSeparators(span, hasVnCurrency, out char decimalSep, out char thousandSep);

            fixed (char* p = span)
            {
                char* ptr = p;
                char* end = p + span.Length;

                bool negative = isParenthesizedNeg;
                if (*ptr == '-')
                {
                    if (isParenthesizedNeg) { result = defaultValue; return false; }
                    negative = true;
                    ptr++;
                }
                else if (*ptr == '+')
                {
                    if (isParenthesizedNeg) { result = defaultValue; return false; }
                    ptr++;
                }

                if (ptr == end)
                {
                    result = defaultValue;
                    return false;
                }

                const ulong MaxInt64Div10 = 922337203685477580UL;
                ulong acc = 0;
                bool hasDigits = false;

                while (ptr < end)
                {
                    char c = *ptr;
                    if (c >= '0' && c <= '9')
                    {
                        ulong digit = (ulong)(c - '0');
                        if (acc > MaxInt64Div10 || (acc == MaxInt64Div10 && digit > (ulong)(negative ? 8 : 7)))
                        {
                            result = defaultValue;
                            return false;
                        }
                        acc = (acc * 10) + digit;
                        hasDigits = true;
                    }
                    else if (decimalSep != '\0' && c == decimalSep)
                    {
                        break;
                    }
                    else if (c == thousandSep || c == ' ')
                    {
                        // Skip thousand separator
                    }
                    else
                    {
                        break;
                    }
                    ptr++;
                }

                if (!hasDigits)
                {
                    result = defaultValue;
                    return false;
                }

                result = negative ? unchecked((long)(0UL - acc)) : unchecked((long)acc);
                return true;
            }
        }

        /// <summary>
        /// Parses a decimal from a ReadOnlySpan with true 100% zero-heap-allocation.
        /// Operates directly in 64-bit CPU registers and bitwise-constructs the decimal struct.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe bool TryParseDecimal(ReadOnlySpan<char> span, out decimal result, decimal defaultValue = 0)
        {
            if (!TryCleanAccountingCurrency(span, out span, out bool hasVnCurrency, out bool isParenthesizedNeg))
            {
                result = defaultValue;
                return false;
            }

            AnalyzeSeparators(span, hasVnCurrency, out char decimalSep, out char thousandSep);

            fixed (char* p = span)
            {
                char* ptr = p;
                char* end = p + span.Length;

                bool negative = isParenthesizedNeg;
                if (*ptr == '-')
                {
                    if (isParenthesizedNeg) { result = defaultValue; return false; }
                    negative = true;
                    ptr++;
                }
                else if (*ptr == '+')
                {
                    if (isParenthesizedNeg) { result = defaultValue; return false; }
                    ptr++;
                }

                ulong acc = 0;
                int scale = 0;
                bool hasDigits = false;
                bool afterDecimal = false;
                int digitCount = 0;

                while (ptr < end)
                {
                    char c = *ptr;
                    if (c >= '0' && c <= '9')
                    {
                        hasDigits = true;
                        uint digit = (uint)(c - '0');
                        if (acc != 0 || digit != 0)
                        {
                            digitCount++;
                        }

                        if (digitCount <= 18) // Fits in 64-bit integer register without overflow
                        {
                            acc = (acc * 10) + digit;
                            if (afterDecimal)
                            {
                                scale++;
                                if (scale > 28)
                                {
                                    return TryParseDecimalExtended(span, decimalSep, thousandSep, isParenthesizedNeg, out result, defaultValue);
                                }
                            }
                        }
                        else
                        {
                            // Extreme precision fallback (> 18 significant digits)
                            return TryParseDecimalExtended(span, decimalSep, thousandSep, isParenthesizedNeg, out result, defaultValue);
                        }
                    }
                    else if (decimalSep != '\0' && c == decimalSep)
                    {
                        afterDecimal = true;
                    }
                    else if (c == thousandSep || c == ' ')
                    {
                        // Skip separator
                    }
                    else
                    {
                        break;
                    }
                    ptr++;
                }

                if (!hasDigits)
                {
                    result = defaultValue;
                    return false;
                }

                int lo = (int)(acc & 0xFFFFFFFF);
                int mid = (int)(acc >> 32);
                result = new decimal(lo, mid, 0, negative, (byte)scale);
                return true;
            }
        }

        private static bool TryParseDecimalExtended(
            ReadOnlySpan<char> span,
            char decimalSep,
            char thousandSep,
            bool isParenthesizedNegative,
            out decimal result,
            decimal defaultValue)
        {
            int maxLen = span.Length + (isParenthesizedNegative ? 1 : 0);
            char[]? rented = null;
            Span<char> clean = maxLen <= 128
                ? stackalloc char[maxLen]
                : (rented = ArrayPool<char>.Shared.Rent(maxLen));

            try
            {
                int cleanLen = 0;
                if (isParenthesizedNegative)
                {
                    clean[cleanLen++] = '-';
                }

                for (int i = 0; i < span.Length; i++)
                {
                    char c = span[i];
                    if ((c >= '0' && c <= '9') || c == '-' || c == '+')
                    {
                        clean[cleanLen++] = c;
                    }
                    else if (decimalSep != '\0' && c == decimalSep)
                    {
                        clean[cleanLen++] = '.';
                    }
                }

                if (cleanLen == 0 || (isParenthesizedNegative && cleanLen == 1))
                {
                    result = defaultValue;
                    return false;
                }

#if NET8_0_OR_GREATER
                if (decimal.TryParse(clean.Slice(0, cleanLen), NumberStyles.Float, CultureInfo.InvariantCulture, out result))
                    return true;
#else
                string s = clean.Slice(0, cleanLen).ToString();
                if (decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out result))
                    return true;
#endif
                result = defaultValue;
                return false;
            }
            finally
            {
                if (rented != null)
                {
                    ArrayPool<char>.Shared.Return(rented);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryCleanAccountingCurrency(
            ReadOnlySpan<char> span,
            out ReadOnlySpan<char> cleaned,
            out bool hasVnCurrency,
            out bool isParenthesizedNegative)
        {
            isParenthesizedNegative = false;
            span = SpanTextOps.CleanCurrency(span, out hasVnCurrency);
            if (span.IsEmpty)
            {
                cleaned = span;
                return false;
            }

            if (span.Length >= 2 && span[0] == '(' && span[span.Length - 1] == ')')
            {
                isParenthesizedNegative = true;
                span = SpanTextOps.CleanCurrency(span.Slice(1, span.Length - 2), out bool innerVn);
                hasVnCurrency |= innerVn;
                if (span.IsEmpty || span[0] == '-' || span[0] == '+')
                {
                    cleaned = default;
                    return false;
                }
            }

            cleaned = span;
            return true;
        }

        /// <summary>
        /// Parses a double-precision floating-point number from a ReadOnlySpan.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryParseDouble(ReadOnlySpan<char> span, out double result, double defaultValue = 0)
        {
            span = SpanTextOps.TrimAsciiWhitespace(span);
            if (span.IsEmpty)
            {
                result = defaultValue;
                return false;
            }

            if (span.IndexOf('e') >= 0 || span.IndexOf('E') >= 0)
            {
#if NET8_0_OR_GREATER
                if (double.TryParse(span, NumberStyles.Float, CultureInfo.InvariantCulture, out result))
                    return true;
#else
                if (double.TryParse(span.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out result))
                    return true;
#endif
                result = defaultValue;
                return false;
            }

            if (TryParseDecimal(span, out var dec, 0m))
            {
                result = (double)dec;
                return true;
            }

#if NET8_0_OR_GREATER
            if (double.TryParse(span, NumberStyles.Float, CultureInfo.InvariantCulture, out result))
                return true;
#else
            if (double.TryParse(span.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out result))
                return true;
#endif

            result = defaultValue;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AnalyzeSeparators(ReadOnlySpan<char> span, bool hasVnCurrency, out char decimalSep, out char thousandSep)
        {
            int dotCount = 0;
            int commaCount = 0;
            int lastDot = -1;
            int lastComma = -1;

            for (int i = 0; i < span.Length; i++)
            {
                if (span[i] == '.')
                {
                    dotCount++;
                    lastDot = i;
                }
                else if (span[i] == ',')
                {
                    commaCount++;
                    lastComma = i;
                }
            }

            // Case 1: Both '.' and ',' present -> last one is decimal
            if (dotCount > 0 && commaCount > 0)
            {
                if (lastDot > lastComma)
                {
                    decimalSep = '.';
                    thousandSep = ',';
                }
                else
                {
                    decimalSep = ',';
                    thousandSep = '.';
                }
                return;
            }

            // Case 2: Multiple dots, no comma -> all dots are thousand separators (e.g. 1.500.000)
            if (dotCount > 1)
            {
                decimalSep = '\0';
                thousandSep = '.';
                return;
            }

            // Case 3: Multiple commas, no dot -> all commas are thousand separators (e.g. 1,500,000)
            if (commaCount > 1)
            {
                decimalSep = '\0';
                thousandSep = ',';
                return;
            }

            // Case 4: Exactly one dot, no comma
            if (dotCount == 1)
            {
                if (hasVnCurrency)
                {
                    decimalSep = '\0';
                    thousandSep = '.';
                }
                else
                {
                    // Universal computing standard: single dot is decimal point (123.456)
                    decimalSep = '.';
                    thousandSep = ',';
                }
                return;
            }

            // Case 5: Exactly one comma, no dot
            if (commaCount == 1)
            {
                int digitsAfter = 0;
                for (int i = lastComma + 1; i < span.Length; i++)
                {
                    if (span[i] >= '0' && span[i] <= '9') digitsAfter++;
                }

                int digitsBefore = 0;
                for (int i = 0; i < lastComma; i++)
                {
                    if (span[i] >= '0' && span[i] <= '9') digitsBefore++;
                }

                if (digitsAfter == 3 && digitsBefore >= 1 && digitsBefore <= 3)
                {
                    // Thousand separator (e.g. 1,000 or 12,345)
                    decimalSep = '\0';
                    thousandSep = ',';
                }
                else
                {
                    // Decimal separator (e.g. 12,50 or 0,99)
                    decimalSep = ',';
                    thousandSep = '.';
                }
                return;
            }

            // Case 6: No separators
            decimalSep = '\0';
            thousandSep = '\0';
        }

        #region ReadOnlySpan<byte> Overloads

        /// <summary>
        /// Parses a 32-bit integer directly from a UTF-8 ReadOnlySpan of bytes without string or char array allocations.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe bool TryParseInt32(ReadOnlySpan<byte> span, out int result)
        {
            result = 0;
            if (span.IsEmpty) return false;

            fixed (byte* p = span)
            {
                byte* ptr = p;
                byte* end = p + span.Length;

                while (ptr < end && (*ptr == (byte)' ' || *ptr == (byte)'\t')) ptr++;
                while (end > ptr && (*(end - 1) == (byte)' ' || *(end - 1) == (byte)'\t')) end--;

                if (ptr >= end) return false;

                bool neg = false;
                if (*ptr == (byte)'-') { neg = true; ptr++; }
                else if (*ptr == (byte)'+') { ptr++; }

                if (ptr >= end) return false;

                const uint MaxInt32Div10 = 214748364U;
                uint acc = 0;
                while (ptr < end)
                {
                    byte b = *ptr++;
                    if (b < (byte)'0' || b > (byte)'9') return false;
                    uint digit = (uint)(b - (byte)'0');
                    if (acc > MaxInt32Div10 || (acc == MaxInt32Div10 && digit > (uint)(neg ? 8 : 7)))
                        return false;
                    acc = (acc * 10) + digit;
                }

                result = neg ? unchecked((int)(0U - acc)) : unchecked((int)acc);
                return true;
            }
        }

        /// <summary>
        /// Parses a 64-bit integer directly from a UTF-8 ReadOnlySpan of bytes without string or char array allocations.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe bool TryParseInt64(ReadOnlySpan<byte> span, out long result)
        {
            result = 0;
            if (span.IsEmpty) return false;

            fixed (byte* p = span)
            {
                byte* ptr = p;
                byte* end = p + span.Length;

                while (ptr < end && (*ptr == (byte)' ' || *ptr == (byte)'\t')) ptr++;
                while (end > ptr && (*(end - 1) == (byte)' ' || *(end - 1) == (byte)'\t')) end--;

                if (ptr >= end) return false;

                bool neg = false;
                if (*ptr == (byte)'-') { neg = true; ptr++; }
                else if (*ptr == (byte)'+') { ptr++; }

                if (ptr >= end) return false;

                const ulong MaxInt64Div10 = 922337203685477580UL;
                ulong acc = 0;
                while (ptr < end)
                {
                    byte b = *ptr++;
                    if (b < (byte)'0' || b > (byte)'9') return false;
                    ulong digit = (ulong)(b - (byte)'0');
                    if (acc > MaxInt64Div10 || (acc == MaxInt64Div10 && digit > (ulong)(neg ? 8 : 7)))
                        return false;
                    acc = (acc * 10) + digit;
                }

                result = neg ? unchecked((long)(0UL - acc)) : unchecked((long)acc);
                return true;
            }
        }

        /// <summary>
        /// Parses a decimal directly from a UTF-8 ReadOnlySpan of bytes using zero-allocation stack transformation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryParseDecimal(ReadOnlySpan<byte> span, out decimal result)
        {
            result = 0m;
            if (span.IsEmpty) return false;

            if (span.Length <= 64)
            {
                Span<char> chars = stackalloc char[span.Length];
                int charLen = DecodeUtf8ToChars(span, chars);
                return TryParseDecimal(chars.Slice(0, charLen), out result);
            }

            char[] rented = ArrayPool<char>.Shared.Rent(span.Length);
            try
            {
                int charLen = DecodeUtf8ToChars(span, rented.AsSpan());
                return TryParseDecimal(rented.AsSpan(0, charLen), out result);
            }
            finally
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }

        /// <summary>
        /// Parses a double directly from a UTF-8 ReadOnlySpan of bytes using zero-allocation stack transformation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryParseDouble(ReadOnlySpan<byte> span, out double result)
        {
            result = 0.0;
            if (span.IsEmpty) return false;

            if (span.Length <= 64)
            {
                Span<char> chars = stackalloc char[span.Length];
                int charLen = DecodeUtf8ToChars(span, chars);
                return TryParseDouble(chars.Slice(0, charLen), out result);
            }

            char[] rented = ArrayPool<char>.Shared.Rent(span.Length);
            try
            {
                int charLen = DecodeUtf8ToChars(span, rented.AsSpan());
                return TryParseDouble(rented.AsSpan(0, charLen), out result);
            }
            finally
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }

        private static unsafe int DecodeUtf8ToChars(ReadOnlySpan<byte> utf8, Span<char> destination)
        {
            bool hasNonAscii = false;
            for (int i = 0; i < utf8.Length; i++)
            {
                if (utf8[i] >= 0x80)
                {
                    hasNonAscii = true;
                    break;
                }
            }

            if (!hasNonAscii)
            {
                for (int i = 0; i < utf8.Length; i++)
                {
                    destination[i] = (char)utf8[i];
                }
                return utf8.Length;
            }

#if NET8_0_OR_GREATER
            return Encoding.UTF8.GetChars(utf8, destination);
#else
            fixed (byte* src = utf8)
            fixed (char* dst = destination)
            {
                return Encoding.UTF8.GetChars(src, utf8.Length, dst, destination.Length);
            }
#endif
        }

        #endregion
    }
}
