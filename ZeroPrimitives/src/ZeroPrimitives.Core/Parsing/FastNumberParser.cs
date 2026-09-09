using System;
using System.Globalization;
using ZeroPrimitives.Text;

namespace ZeroPrimitives.Parsing
{
    /// <summary>
    /// Ultra-fast, zero-allocation number parser operating on ReadOnlySpan with pointer loops.
    /// Eliminates intermediate string allocations and culture overhead.
    /// </summary>
    public static class FastNumberParser
    {
        /// <summary>
        /// Attempts to parse an integer from a ReadOnlySpan.
        /// Handles leading/trailing whitespace, signs (+/-), and thousand separators (comma, period).
        /// If a decimal separator is present, truncates toward zero.
        /// </summary>
        public static unsafe bool TryParseInt32(ReadOnlySpan<char> span, out int result, int defaultValue = 0)
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
        public static unsafe bool TryParseInt64(ReadOnlySpan<char> span, out long result, long defaultValue = 0)
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
        /// Parses a decimal from a ReadOnlySpan, handling currency marks and varied separator styles.
        /// </summary>
        public static bool TryParseDecimal(ReadOnlySpan<char> span, out decimal result, decimal defaultValue = 0)
        {
            span = SpanTextOps.CleanCurrency(span, out bool hasVnCurrency);
            if (span.IsEmpty)
            {
                result = defaultValue;
                return false;
            }

            AnalyzeSeparators(span, hasVnCurrency, out char decimalSep, out char thousandSep);

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
                    clean[cleanLen++] = '.'; // Normalize to dot for InvariantCulture
                }
                else if (c == thousandSep || c == ' ')
                {
                    // Skip thousand separator
                }
            }

            if (cleanLen == 0)
            {
                result = defaultValue;
                return false;
            }

#if NET8_0_OR_GREATER
            if (decimal.TryParse(clean.Slice(0, cleanLen), NumberStyles.Float, CultureInfo.InvariantCulture, out result))
            {
                return true;
            }
#else
            string s = clean.Slice(0, cleanLen).ToString();
            if (decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out result))
            {
                return true;
            }
#endif

            result = defaultValue;
            return false;
        }

        /// <summary>
        /// Parses a double-precision floating-point number from a ReadOnlySpan.
        /// </summary>
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
