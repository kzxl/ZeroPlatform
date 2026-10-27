using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using ZeroPrimitives.Text;

namespace ZeroPrimitives.Parsing
{
    /// <summary>
    /// Fast zero-allocation DateTime parser specialized for ISO 8601, SQL Server, and Vietnamese dates.
    /// </summary>
    public static class FastDateParser
    {
        public static readonly DateTime SqlMinDate = new DateTime(1753, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
        public static readonly DateTime SqlMaxDate = new DateTime(9999, 12, 31, 23, 59, 59, DateTimeKind.Unspecified);
        private static readonly byte[] DaysInMonthTable = { 0, 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };

        /// <summary>
        /// Ensures a DateTime falls within the valid range for Microsoft SQL Server DATETIME (1753-01-01 to 9999-12-31).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DateTime EnsureSqlDateTime(this DateTime dt)
        {
            if (dt < SqlMinDate) return SqlMinDate;
            if (dt > SqlMaxDate) return SqlMaxDate;
            return dt;
        }

        /// <summary>
        /// Fast parser for common business date formats without allocations:
        /// - yyyy-MM-dd or yyyy-MM-dd HH:mm:ss (or 'T')
        /// - dd/MM/yyyy or dd/MM/yyyy HH:mm:ss
        /// - yyyy/MM/dd
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryParse(ReadOnlySpan<char> span, out DateTime date)
        {
            span = SpanTextOps.TrimAsciiWhitespace(span);
            if (span.Length < 8)
            {
                date = default;
                return false;
            }

            // Case 1: yyyy-MM-dd or yyyy/MM/dd (starts with 4-digit year)
            if (char.IsDigit(span[0]) && char.IsDigit(span[1]) && char.IsDigit(span[2]) && char.IsDigit(span[3]) &&
                (span[4] == '-' || span[4] == '/'))
            {
                int year = Parse4Digits(span, 0);
                char sep = span[4];
                int month = Parse2Digits(span, 5);
                int day = 0;
                int hour = 0, minute = 0, second = 0;

                if (span.Length >= 10 && span[7] == sep)
                {
                    day = Parse2Digits(span, 8);

                    // Optional time: yyyy-MM-dd HH:mm:ss or 'T'
                    if (span.Length >= 19 && (span[10] == ' ' || span[10] == 'T') && span[13] == ':' && span[16] == ':')
                    {
                        hour = Parse2Digits(span, 11);
                        minute = Parse2Digits(span, 14);
                        second = Parse2Digits(span, 17);
                    }

                    if (IsValidDate(year, month, day, hour, minute, second))
                    {
                        date = new DateTime(year, month, day, hour, minute, second);
                        return true;
                    }
                }
            }

            // Case 2: dd/MM/yyyy or dd-MM-yyyy (Vietnamese format: 2-digit day first)
            if (char.IsDigit(span[0]) && char.IsDigit(span[1]) && (span[2] == '/' || span[2] == '-'))
            {
                char sep = span[2];
                int day = Parse2Digits(span, 0);
                int month = Parse2Digits(span, 3);

                if (span.Length >= 10 && span[5] == sep)
                {
                    int year = Parse4Digits(span, 6);
                    int hour = 0, minute = 0, second = 0;

                    if (span.Length >= 19 && (span[10] == ' ' || span[10] == 'T') && span[13] == ':' && span[16] == ':')
                    {
                        hour = Parse2Digits(span, 11);
                        minute = Parse2Digits(span, 14);
                        second = Parse2Digits(span, 17);
                    }

                    if (IsValidDate(year, month, day, hour, minute, second))
                    {
                        date = new DateTime(year, month, day, hour, minute, second);
                        return true;
                    }
                }
            }

            // Fallback to standard BCL parsing
#if NET8_0_OR_GREATER
            return DateTime.TryParse(span, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
#else
            return DateTime.TryParse(span.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Parse4Digits(ReadOnlySpan<char> span, int offset)
        {
            return ((span[offset] - '0') * 1000) +
                   ((span[offset + 1] - '0') * 100) +
                   ((span[offset + 2] - '0') * 10) +
                   (span[offset + 3] - '0');
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Parse2Digits(ReadOnlySpan<char> span, int offset)
        {
            return ((span[offset] - '0') * 10) + (span[offset + 1] - '0');
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsValidDate(int year, int month, int day, int hour, int minute, int second)
        {
            if ((uint)(year - 1) >= 9999) return false;
            if ((uint)(month - 1) >= 12) return false;

            int maxDays = DaysInMonthTable[month];
            if (month == 2 && ((year & 3) == 0 && (year % 100 != 0 || year % 400 == 0)))
            {
                maxDays = 29;
            }

            if (day < 1 || day > maxDays) return false;
            if ((uint)hour >= 24 || (uint)minute >= 60 || (uint)second >= 60) return false;
            return true;
        }
    }
}
