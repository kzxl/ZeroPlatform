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
        /// - ISO 8601: yyyy-MM-dd, yyyy-MM-ddTHH:mm[:ss][.fffffff][Z|±HH:mm|±HHmm|±HH]
        /// - Compact: yyyyMMdd
        /// - Day-first: d/M/yyyy, dd/MM/yyyy (and '-' separator) with optional HH:mm[:ss][.fffffff]
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

            // Case 1: ISO 8601 / yyyy-MM-dd or yyyy/MM/dd
            if (span.Length >= 10 &&
                IsAsciiDigit(span[0]) && IsAsciiDigit(span[1]) && IsAsciiDigit(span[2]) && IsAsciiDigit(span[3]) &&
                (span[4] == '-' || span[4] == '/'))
            {
                if (TryParseIsoCore(span, out DateTime baseDt, out bool hasOffset, out TimeSpan offset))
                {
                    if (hasOffset)
                    {
                        long utcTicks = baseDt.Ticks - offset.Ticks;
                        if (utcTicks < DateTime.MinValue.Ticks || utcTicks > DateTime.MaxValue.Ticks)
                        {
                            date = default;
                            return false;
                        }
                        date = new DateTime(utcTicks, DateTimeKind.Utc);
                    }
                    else
                    {
                        date = baseDt;
                    }
                    return true;
                }

                date = default;
                return false;
            }

            // Case 2: Compact 8-digit yyyyMMdd (EDI / SAP / bank statements)
            if (span.Length == 8 && IsAllAsciiDigits(span))
            {
                if (TryParse4Digits(span, 0, out int year) &&
                    TryParse2Digits(span, 4, out int month) &&
                    TryParse2Digits(span, 6, out int day) &&
                    IsValidDate(year, month, day, 0, 0, 0))
                {
                    date = new DateTime(year, month, day);
                    return true;
                }

                date = default;
                return false;
            }

            // Case 3: Day-first d/M/yyyy, dd/MM/yyyy, d-M-yyyy, dd-MM-yyyy
            if (TryParseDayFirst(span, out DateTime dayFirstDate, out bool maybeUsFormat))
            {
                date = dayFirstDate;
                return true;
            }

            if (!maybeUsFormat)
            {
                // Fallback to standard BCL parsing only for non-day-first ambiguous strings (e.g. month names)
                // If it looked like d/M/yyyy with invalid calendar values, do not misparse unless month > 12 (US MM/dd/yyyy)
            }

            // Fallback to standard BCL parsing
#if NET8_0_OR_GREATER
            return DateTime.TryParse(span, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
#else
            return DateTime.TryParse(span.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
#endif
        }

        /// <summary>
        /// Parses a UTF-8 byte span into a <see cref="DateTime"/> without intermediate string allocations.
        /// </summary>
        public static bool TryParse(ReadOnlySpan<byte> utf8Span, out DateTime date)
        {
            utf8Span = TrimAsciiBytes(utf8Span);
            if (utf8Span.Length < 8 || utf8Span.Length > 64)
            {
                date = default;
                return false;
            }

            Span<char> chars = stackalloc char[utf8Span.Length];
            for (int i = 0; i < utf8Span.Length; i++)
            {
                byte b = utf8Span[i];
                if (b >= 0x80)
                {
                    date = default;
                    return false;
                }
                chars[i] = (char)b;
            }

            return TryParse(chars, out date);
        }

        /// <summary>
        /// Parses an ISO 8601 or common business date string into a <see cref="DateTimeOffset"/>, preserving explicit UTC offsets.
        /// </summary>
        public static bool TryParseDateTimeOffset(ReadOnlySpan<char> span, out DateTimeOffset result)
        {
            span = SpanTextOps.TrimAsciiWhitespace(span);
            if (span.Length < 8)
            {
                result = default;
                return false;
            }

            if (span.Length >= 10 &&
                IsAsciiDigit(span[0]) && IsAsciiDigit(span[1]) && IsAsciiDigit(span[2]) && IsAsciiDigit(span[3]) &&
                (span[4] == '-' || span[4] == '/'))
            {
                if (TryParseIsoCore(span, out DateTime baseDt, out bool hasOffset, out TimeSpan offset))
                {
                    if (hasOffset)
                    {
                        long utcTicks = baseDt.Ticks - offset.Ticks;
                        if (utcTicks < DateTimeOffset.MinValue.Ticks || utcTicks > DateTimeOffset.MaxValue.Ticks)
                        {
                            result = default;
                            return false;
                        }
                        result = new DateTimeOffset(baseDt.Ticks, offset);
                        return true;
                    }

                    result = new DateTimeOffset(baseDt);
                    return true;
                }

                result = default;
                return false;
            }

            if (span.Length == 8 && IsAllAsciiDigits(span))
            {
                if (TryParse4Digits(span, 0, out int year) &&
                    TryParse2Digits(span, 4, out int month) &&
                    TryParse2Digits(span, 6, out int day) &&
                    IsValidDate(year, month, day, 0, 0, 0))
                {
                    result = new DateTimeOffset(new DateTime(year, month, day));
                    return true;
                }

                result = default;
                return false;
            }

            if (TryParseDayFirst(span, out DateTime dayFirstDate, out bool maybeUsFormat))
            {
                result = new DateTimeOffset(dayFirstDate);
                return true;
            }

            if (!maybeUsFormat && span.IndexOf('/') >= 0)
            {
                result = default;
                return false;
            }

#if NET8_0_OR_GREATER
            return DateTimeOffset.TryParse(span, CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
#else
            return DateTimeOffset.TryParse(span.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
#endif
        }

        /// <summary>
        /// Parses a UTF-8 byte span into a <see cref="DateTimeOffset"/> without intermediate string allocations.
        /// </summary>
        public static bool TryParseDateTimeOffset(ReadOnlySpan<byte> utf8Span, out DateTimeOffset result)
        {
            utf8Span = TrimAsciiBytes(utf8Span);
            if (utf8Span.Length < 8 || utf8Span.Length > 64)
            {
                result = default;
                return false;
            }

            Span<char> chars = stackalloc char[utf8Span.Length];
            for (int i = 0; i < utf8Span.Length; i++)
            {
                byte b = utf8Span[i];
                if (b >= 0x80)
                {
                    result = default;
                    return false;
                }
                chars[i] = (char)b;
            }

            return TryParseDateTimeOffset(chars, out result);
        }

        private static bool TryParseIsoCore(
            ReadOnlySpan<char> span,
            out DateTime baseDt,
            out bool hasOffset,
            out TimeSpan offset)
        {
            baseDt = default;
            hasOffset = false;
            offset = default;

            char sep = span[4];
            if (span.Length < 10 || span[7] != sep)
                return false;

            if (!TryParse4Digits(span, 0, out int year) ||
                !TryParse2Digits(span, 5, out int month) ||
                !TryParse2Digits(span, 8, out int day))
            {
                return false;
            }

            if (span.Length == 10)
            {
                if (!IsValidDate(year, month, day, 0, 0, 0))
                    return false;
                baseDt = new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Unspecified);
                return true;
            }

            char timeSep = span[10];
            if (timeSep != ' ' && timeSep != 'T' && timeSep != 't')
                return false;

            int pos = 11;
            if (!TryParseTimeComponent(span, ref pos, requireTwoDigitHour: true, out int hour, out int minute, out int second, out int fracTicks))
                return false;

            if (!IsValidDate(year, month, day, hour, minute, second))
                return false;

            if (pos < span.Length)
            {
                char tz = span[pos];
                if (tz == 'Z' || tz == 'z')
                {
                    pos++;
                    if (pos != span.Length) return false;
                    hasOffset = true;
                    offset = TimeSpan.Zero;
                }
                else if (tz == '+' || tz == '-')
                {
                    bool neg = tz == '-';
                    pos++;
                    if (!TryParse2Digits(span, pos, out int offH)) return false;
                    pos += 2;

                    int offM = 0;
                    if (pos < span.Length)
                    {
                        if (span[pos] == ':')
                        {
                            pos++;
                        }
                        if (!TryParse2Digits(span, pos, out offM)) return false;
                        pos += 2;
                    }

                    if (pos != span.Length) return false;
                    if (offH > 14 || offM >= 60 || (offH == 14 && offM != 0))
                        return false;

                    offset = new TimeSpan(offH, offM, 0);
                    if (neg) offset = -offset;
                    hasOffset = true;
                }
                else
                {
                    return false;
                }
            }

            baseDt = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Unspecified).AddTicks(fracTicks);
            return true;
        }

        private static bool TryParseDayFirst(ReadOnlySpan<char> span, out DateTime date, out bool maybeUsFormat)
        {
            date = default;
            maybeUsFormat = false;

            int sep1 = -1;
            for (int i = 0; i < span.Length && i < 3; i++)
            {
                char c = span[i];
                if (c == '/' || c == '-')
                {
                    sep1 = i;
                    break;
                }
                if (!IsAsciiDigit(c)) return false;
            }

            if (sep1 < 1 || sep1 > 2) return false;
            char sep = span[sep1];

            int day = sep1 == 1 ? (span[0] - '0') : ((span[0] - '0') * 10 + (span[1] - '0'));

            int monthStart = sep1 + 1;
            int sep2 = -1;
            for (int i = monthStart; i < span.Length && i < monthStart + 3; i++)
            {
                char c = span[i];
                if (c == sep)
                {
                    sep2 = i;
                    break;
                }
                if (!IsAsciiDigit(c)) return false;
            }

            if (sep2 < monthStart + 1 || sep2 > monthStart + 2) return false;
            int monthLen = sep2 - monthStart;
            int month = monthLen == 1
                ? (span[monthStart] - '0')
                : ((span[monthStart] - '0') * 10 + (span[monthStart + 1] - '0'));

            int yearStart = sep2 + 1;
            if (!TryParse4Digits(span, yearStart, out int year))
                return false;

            int pos = yearStart + 4;
            int hour = 0, minute = 0, second = 0, fracTicks = 0;

            if (pos < span.Length)
            {
                char t = span[pos];
                if (t != ' ' && t != 'T' && t != 't')
                    return false;
                pos++;
                if (!TryParseTimeComponent(span, ref pos, requireTwoDigitHour: false, out hour, out minute, out second, out fracTicks))
                    return false;
                if (pos != span.Length)
                    return false;
            }

            if (!IsValidDate(year, month, day, hour, minute, second))
            {
                if (month > 12 && day >= 1 && day <= 12)
                    maybeUsFormat = true;
                return false;
            }

            date = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Unspecified).AddTicks(fracTicks);
            return true;
        }

        private static bool TryParseTimeComponent(
            ReadOnlySpan<char> span,
            ref int pos,
            bool requireTwoDigitHour,
            out int hour,
            out int minute,
            out int second,
            out int fracTicks)
        {
            hour = 0;
            minute = 0;
            second = 0;
            fracTicks = 0;

            if (pos >= span.Length) return false;

            if (requireTwoDigitHour)
            {
                if (!TryParse2Digits(span, pos, out hour)) return false;
                pos += 2;
            }
            else
            {
                if (!IsAsciiDigit(span[pos])) return false;
                if (pos + 1 < span.Length && IsAsciiDigit(span[pos + 1]))
                {
                    hour = (span[pos] - '0') * 10 + (span[pos + 1] - '0');
                    pos += 2;
                }
                else
                {
                    hour = span[pos] - '0';
                    pos += 1;
                }
            }

            if (pos >= span.Length || span[pos] != ':') return false;
            pos++;

            if (!TryParse2Digits(span, pos, out minute)) return false;
            pos += 2;

            if (pos < span.Length && span[pos] == ':')
            {
                pos++;
                if (!TryParse2Digits(span, pos, out second)) return false;
                pos += 2;

                if (pos < span.Length && span[pos] == '.')
                {
                    pos++;
                    if (pos >= span.Length || !IsAsciiDigit(span[pos])) return false;
                    int digits = 0;
                    int ticks = 0;
                    while (pos < span.Length && IsAsciiDigit(span[pos]))
                    {
                        if (digits < 7)
                        {
                            ticks = (ticks * 10) + (span[pos] - '0');
                            digits++;
                        }
                        pos++;
                    }
                    while (digits < 7)
                    {
                        ticks *= 10;
                        digits++;
                    }
                    fracTicks = ticks;
                }
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsAsciiDigit(char c) => (uint)(c - '0') <= 9U;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsAllAsciiDigits(ReadOnlySpan<char> span)
        {
            for (int i = 0; i < span.Length; i++)
            {
                if (!IsAsciiDigit(span[i])) return false;
            }
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ReadOnlySpan<byte> TrimAsciiBytes(ReadOnlySpan<byte> span)
        {
            int start = 0;
            int end = span.Length - 1;
            while (start <= end && (span[start] == ' ' || span[start] == '\t' || span[start] == '\r' || span[start] == '\n'))
                start++;
            while (end >= start && (span[end] == ' ' || span[end] == '\t' || span[end] == '\r' || span[end] == '\n'))
                end--;
            return start <= end ? span.Slice(start, end - start + 1) : ReadOnlySpan<byte>.Empty;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryParse4Digits(ReadOnlySpan<char> span, int offset, out int value)
        {
            if (offset + 4 > span.Length)
            {
                value = 0;
                return false;
            }
            uint d0 = (uint)(span[offset] - '0');
            uint d1 = (uint)(span[offset + 1] - '0');
            uint d2 = (uint)(span[offset + 2] - '0');
            uint d3 = (uint)(span[offset + 3] - '0');
            if (d0 > 9U || d1 > 9U || d2 > 9U || d3 > 9U)
            {
                value = 0;
                return false;
            }
            value = (int)((d0 * 1000U) + (d1 * 100U) + (d2 * 10U) + d3);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryParse2Digits(ReadOnlySpan<char> span, int offset, out int value)
        {
            if (offset + 2 > span.Length)
            {
                value = 0;
                return false;
            }
            uint d0 = (uint)(span[offset] - '0');
            uint d1 = (uint)(span[offset + 1] - '0');
            if (d0 > 9U || d1 > 9U)
            {
                value = 0;
                return false;
            }
            value = (int)((d0 * 10U) + d1);
            return true;
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
