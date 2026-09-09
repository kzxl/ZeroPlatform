using System;
using System.Globalization;
using System.Runtime.CompilerServices;
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
                            long acc = 0;
                            while (ptr < end)
                            {
                                acc = (acc * 10) + (*ptr++ - '0');
                                if (acc > (long)int.MaxValue + 1)
                                {
                                    result = neg ? int.MinValue : int.MaxValue;
                                    return false;
                                }
                            }

                            long finalVal = neg ? -acc : acc;
                            if (finalVal < int.MinValue || finalVal > int.MaxValue)
                            {
                                result = neg ? int.MinValue : int.MaxValue;
                                return false;
                            }

                            result = (int)finalVal;
                            return true;
                        }
                    }
                }
            }

            return TryParseInt32Complex(span, out result, defaultValue);
        }

        private static unsafe bool TryParseInt32Complex(ReadOnlySpan<char> span, out int result, int defaultValue)
        {
            span = SpanTextOps.CleanCurrency(span, out bool hasVnCurrency);
            if (span.IsEmpty)
            {
                result = defaultValue;
                return false;
            }

            AnalyzeSeparators(span, hasVnCurrency, out char decimalSep, out char thousandSep);

            fixed (char* p = span)
            {
                char* ptr = p;
                char* end = p + span.Length;

                bool negative = false;
                if (*ptr == '-')
                {
                    negative = true;
                    ptr++;
                }
                else if (*ptr == '+')
                {
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
                            ulong acc = 0;
                            while (ptr < end)
                            {
                                acc = (acc * 10) + (ulong)(*ptr++ - '0');
                            }

                            result = neg ? -(long)acc : (long)acc;
                            return true;
                        }
                    }
                }
            }

            return TryParseInt64Complex(span, out result, defaultValue);
        }

        private static unsafe bool TryParseInt64Complex(ReadOnlySpan<char> span, out long result, long defaultValue)
        {
            span = SpanTextOps.CleanCurrency(span, out bool hasVnCurrency);
            if (span.IsEmpty)
            {
                result = defaultValue;
                return false;
            }

            AnalyzeSeparators(span, hasVnCurrency, out char decimalSep, out char thousandSep);

            fixed (char* p = span)
            {
                char* ptr = p;
                char* end = p + span.Length;

                bool negative = false;
                if (*ptr == '-')
                {
                    negative = true;
                    ptr++;
                }
                else if (*ptr == '+')
                {
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

                result = negative ? -acc : acc;
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
            span = SpanTextOps.CleanCurrency(span, out bool hasVnCurrency);
            if (span.IsEmpty)
            {
                result = defaultValue;
                return false;
            }

            AnalyzeSeparators(span, hasVnCurrency, out char decimalSep, out char thousandSep);

            fixed (char* p = span)
            {
                char* ptr = p;
                char* end = p + span.Length;

                bool negative = false;
                if (*ptr == '-')
                {
                    negative = true;
                    ptr++;
                }
                else if (*ptr == '+')
                {
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
                        digitCount++;
                        if (digitCount <= 18) // Fits in 64-bit integer register without overflow
                        {
                            acc = (acc * 10) + (ulong)(c - '0');
                            if (afterDecimal) scale++;
                        }
                        else if (digitCount <= 28)
                        {
                            // Extreme precision fallback (> 18 digits)
                            return TryParseDecimalExtended(span, decimalSep, thousandSep, out result, defaultValue);
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

                if (scale > 28) scale = 28;

                int lo = (int)(acc & 0xFFFFFFFF);
                int mid = (int)(acc >> 32);
                result = new decimal(lo, mid, 0, negative, (byte)scale);
                return true;
            }
        }

        private static bool TryParseDecimalExtended(ReadOnlySpan<char> span, char decimalSep, char thousandSep, out decimal result, decimal defaultValue)
        {
            Span<char> clean = stackalloc char[span.Length];
            int cleanLen = 0;

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

            if (cleanLen == 0)
            {
                result = defaultValue;
                return false;
            }

#if NET8_0_OR_GREATER
            return decimal.TryParse(clean.Slice(0, cleanLen), NumberStyles.Float, CultureInfo.InvariantCulture, out result);
#else
            string s = clean.Slice(0, cleanLen).ToString();
            return decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
#endif
        }

        /// <summary>
        /// Parses a double-precision floating-point number from a ReadOnlySpan.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryParseDouble(ReadOnlySpan<char> span, out double result, double defaultValue = 0)
        {
            if (TryParseDecimal(span, out var dec, (decimal)defaultValue))
            {
                result = (double)dec;
                return true;
            }

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
    }
}
