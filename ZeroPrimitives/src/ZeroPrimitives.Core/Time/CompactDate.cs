using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ZeroPrimitives.Parsing;

namespace ZeroPrimitives.Time
{
    /// <summary>
    /// 4-byte calendar date (<c>0001-01-01</c> to <c>9999-12-31</c>) with zero time-of-day or timezone ambiguity.
    /// Provides <c>DateOnly</c> parity on <c>.NET Framework 4.6.2</c> and <c>.NET Standard 2.0</c>, and seamless conversion on <c>.NET 8.0+</c>.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 4)]
    public readonly struct CompactDate : IEquatable<CompactDate>, IComparable<CompactDate>
#if NET8_0_OR_GREATER
        , ISpanFormattable
#endif
    {
        private const int MinDayNumber = 0;       // 0001-01-01
        private const int MaxDayNumber = 3652058; // 9999-12-31

        private static readonly int[] DaysToMonth365 = { 0, 31, 59, 90, 120, 151, 181, 212, 243, 273, 304, 334, 365 };
        private static readonly int[] DaysToMonth366 = { 0, 31, 60, 91, 121, 152, 182, 213, 244, 274, 305, 335, 366 };

        private readonly int _dayNumber;

        public static readonly CompactDate MinValue = new CompactDate(MinDayNumber);
        public static readonly CompactDate MaxValue = new CompactDate(MaxDayNumber);

        private CompactDate(int dayNumber)
        {
            _dayNumber = dayNumber;
        }

        public CompactDate(int year, int month, int day)
        {
            _dayNumber = DateToDayNumber(year, month, day);
        }

        public int DayNumber
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _dayNumber;
        }

        public int Year
        {
            get
            {
                GetDateParts(out int year, out _, out _);
                return year;
            }
        }

        public int Month
        {
            get
            {
                GetDateParts(out _, out int month, out _);
                return month;
            }
        }

        public int Day
        {
            get
            {
                GetDateParts(out _, out _, out int day);
                return day;
            }
        }

        public DayOfWeek DayOfWeek
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (DayOfWeek)((_dayNumber + 1) % 7);
        }

        public int DayOfYear
        {
            get
            {
                GetDateParts(out int year, out int month, out int day);
                int[] days = IsLeapYear(year) ? DaysToMonth366 : DaysToMonth365;
                return days[month - 1] + day;
            }
        }

        public int Quarter => ((Month - 1) / 3) + 1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CompactDate FromDayNumber(int dayNumber)
        {
            if ((uint)dayNumber > MaxDayNumber)
                throw new ArgumentOutOfRangeException(nameof(dayNumber), "DayNumber must be between 0 and 3652058.");
            return new CompactDate(dayNumber);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CompactDate FromDateTime(DateTime dateTime)
        {
            return new CompactDate((int)(dateTime.Ticks / TimeSpan.TicksPerDay));
        }

        public static CompactDate FromYyyyMmDd(int yyyyMmDd)
        {
            if (!TryFromYyyyMmDd(yyyyMmDd, out CompactDate date))
                throw new ArgumentOutOfRangeException(nameof(yyyyMmDd), "Invalid yyyyMMdd date integer.");
            return date;
        }

        public static bool TryFromYyyyMmDd(int yyyyMmDd, out CompactDate date)
        {
            if (yyyyMmDd < 10101 || yyyyMmDd > 99991231)
            {
                date = default;
                return false;
            }

            int year = yyyyMmDd / 10000;
            int rem = yyyyMmDd - (year * 10000);
            int month = rem / 100;
            int day = rem - (month * 100);

            if (!IsValidDate(year, month, day))
            {
                date = default;
                return false;
            }

            date = new CompactDate(year, month, day);
            return true;
        }

        public static bool TryParse(ReadOnlySpan<char> span, out CompactDate date)
        {
            if (FastDateParser.TryParse(span, out DateTime dt))
            {
                date = FromDateTime(dt);
                return true;
            }
            date = default;
            return false;
        }

        public static bool TryParse(ReadOnlySpan<byte> utf8Span, out CompactDate date)
        {
            if (FastDateParser.TryParse(utf8Span, out DateTime dt))
            {
                date = FromDateTime(dt);
                return true;
            }
            date = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ToYyyyMmDd()
        {
            GetDateParts(out int y, out int m, out int d);
            return (y * 10000) + (m * 100) + d;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DateTime ToDateTime(DateTimeKind kind = DateTimeKind.Unspecified)
        {
            return new DateTime(_dayNumber * TimeSpan.TicksPerDay, kind);
        }

        public CompactDate AddDays(int days)
        {
            long target = (long)_dayNumber + days;
            if (target < MinDayNumber || target > MaxDayNumber)
                throw new ArgumentOutOfRangeException(nameof(days), "Resulting CompactDate is out of range.");
            return new CompactDate((int)target);
        }

        public CompactDate AddMonths(int months)
        {
            GetDateParts(out int year, out int month, out int day);
            int totalMonths = ((year - 1) * 12) + (month - 1) + months;
            if (totalMonths < 0 || totalMonths >= 9999 * 12)
                throw new ArgumentOutOfRangeException(nameof(months), "Resulting CompactDate is out of range.");

            int newYear = (totalMonths / 12) + 1;
            int newMonth = (totalMonths % 12) + 1;
            int maxDay = DaysInMonth(newYear, newMonth);
            int newDay = day > maxDay ? maxDay : day;
            return new CompactDate(newYear, newMonth, newDay);
        }

        public CompactDate AddYears(int years) => AddMonths(years * 12);

        public CompactDate StartOfMonth()
        {
            GetDateParts(out int y, out int m, out _);
            return new CompactDate(y, m, 1);
        }

        public CompactDate EndOfMonth()
        {
            GetDateParts(out int y, out int m, out _);
            return new CompactDate(y, m, DaysInMonth(y, m));
        }

        public CompactDate StartOfQuarter()
        {
            GetDateParts(out int y, out int m, out _);
            int qStart = (((m - 1) / 3) * 3) + 1;
            return new CompactDate(y, qStart, 1);
        }

        public CompactDate EndOfQuarter()
        {
            GetDateParts(out int y, out int m, out _);
            int qEnd = (((m - 1) / 3) * 3) + 3;
            return new CompactDate(y, qEnd, DaysInMonth(y, qEnd));
        }

        public CompactDate StartOfYear() => new CompactDate(Year, 1, 1);
        public CompactDate EndOfYear() => new CompactDate(Year, 12, 31);

        public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
        {
            GetDateParts(out int y, out int m, out int d);

            if (format.IsEmpty || format.Equals("O".AsSpan(), StringComparison.Ordinal) || format.Equals("yyyy-MM-dd".AsSpan(), StringComparison.Ordinal))
            {
                if (destination.Length < 10) { charsWritten = 0; return false; }
                Write4Digits(destination, 0, y);
                destination[4] = '-';
                Write2Digits(destination, 5, m);
                destination[7] = '-';
                Write2Digits(destination, 8, d);
                charsWritten = 10;
                return true;
            }

            if (format.Equals("yyyyMMdd".AsSpan(), StringComparison.Ordinal))
            {
                if (destination.Length < 8) { charsWritten = 0; return false; }
                Write4Digits(destination, 0, y);
                Write2Digits(destination, 4, m);
                Write2Digits(destination, 6, d);
                charsWritten = 8;
                return true;
            }

            if (format.Equals("dd/MM/yyyy".AsSpan(), StringComparison.Ordinal))
            {
                if (destination.Length < 10) { charsWritten = 0; return false; }
                Write2Digits(destination, 0, d);
                destination[2] = '/';
                Write2Digits(destination, 3, m);
                destination[5] = '/';
                Write4Digits(destination, 6, y);
                charsWritten = 10;
                return true;
            }

            charsWritten = 0;
            return false;
        }

        public bool TryFormatUtf8(Span<byte> destination, out int bytesWritten, ReadOnlySpan<char> format = default)
        {
            GetDateParts(out int y, out int m, out int d);

            if (format.IsEmpty || format.Equals("O".AsSpan(), StringComparison.Ordinal) || format.Equals("yyyy-MM-dd".AsSpan(), StringComparison.Ordinal))
            {
                if (destination.Length < 10) { bytesWritten = 0; return false; }
                Write4DigitsUtf8(destination, 0, y);
                destination[4] = (byte)'-';
                Write2DigitsUtf8(destination, 5, m);
                destination[7] = (byte)'-';
                Write2DigitsUtf8(destination, 8, d);
                bytesWritten = 10;
                return true;
            }

            if (format.Equals("yyyyMMdd".AsSpan(), StringComparison.Ordinal))
            {
                if (destination.Length < 8) { bytesWritten = 0; return false; }
                Write4DigitsUtf8(destination, 0, y);
                Write2DigitsUtf8(destination, 4, m);
                Write2DigitsUtf8(destination, 6, d);
                bytesWritten = 8;
                return true;
            }

            if (format.Equals("dd/MM/yyyy".AsSpan(), StringComparison.Ordinal))
            {
                if (destination.Length < 10) { bytesWritten = 0; return false; }
                Write2DigitsUtf8(destination, 0, d);
                destination[2] = (byte)'/';
                Write2DigitsUtf8(destination, 3, m);
                destination[5] = (byte)'/';
                Write4DigitsUtf8(destination, 6, y);
                bytesWritten = 10;
                return true;
            }

            bytesWritten = 0;
            return false;
        }

        public override unsafe string ToString()
        {
            Span<char> chars = stackalloc char[10];
            TryFormat(chars, out int written);
#if NET8_0_OR_GREATER
            return new string(chars.Slice(0, written));
#else
            fixed (char* p = chars)
                return new string(p, 0, written);
#endif
        }

        public unsafe string ToString(string? format, IFormatProvider? formatProvider = null)
        {
            Span<char> chars = stackalloc char[10];
            if (!TryFormat(chars, out int written, format.AsSpan(), formatProvider))
                return ToDateTime().ToString(format, formatProvider);
#if NET8_0_OR_GREATER
            return new string(chars.Slice(0, written));
#else
            fixed (char* p = chars)
                return new string(p, 0, written);
#endif
        }

        public void GetDateParts(out int year, out int month, out int day)
        {
            int n = _dayNumber;
            int y400 = n / 146097;
            n -= y400 * 146097;
            int y100 = n / 36524;
            if (y100 == 4) y100 = 3;
            n -= y100 * 36524;
            int y4 = n / 1461;
            n -= y4 * 1461;
            int y1 = n / 365;
            if (y1 == 4) y1 = 3;
            year = (y400 * 400) + (y100 * 100) + (y4 * 4) + y1 + 1;
            n -= y1 * 365;
            int[] days = IsLeapYear(year) ? DaysToMonth366 : DaysToMonth365;
            int m = (n >> 5) + 1;
            while (n >= days[m]) m++;
            month = m;
            day = n - days[m - 1] + 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsLeapYear(int year)
            => (year & 3) == 0 && (year % 100 != 0 || year % 400 == 0);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int DaysInMonth(int year, int month)
        {
            if ((uint)(month - 1) >= 12)
                throw new ArgumentOutOfRangeException(nameof(month));
            int[] days = IsLeapYear(year) ? DaysToMonth366 : DaysToMonth365;
            return days[month] - days[month - 1];
        }

        private static bool IsValidDate(int year, int month, int day)
        {
            if (year < 1 || year > 9999 || month < 1 || month > 12) return false;
            return day >= 1 && day <= DaysInMonth(year, month);
        }

        private static int DateToDayNumber(int year, int month, int day)
        {
            if (!IsValidDate(year, month, day))
                throw new ArgumentOutOfRangeException(nameof(day), $"Invalid date {year:D4}-{month:D2}-{day:D2}.");
            int[] days = IsLeapYear(year) ? DaysToMonth366 : DaysToMonth365;
            int y = year - 1;
            return (y * 365) + (y / 4) - (y / 100) + (y / 400) + days[month - 1] + day - 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Write4Digits(Span<char> dst, int offset, int val)
        {
            dst[offset] = (char)('0' + (val / 1000));
            dst[offset + 1] = (char)('0' + ((val / 100) % 10));
            dst[offset + 2] = (char)('0' + ((val / 10) % 10));
            dst[offset + 3] = (char)('0' + (val % 10));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Write2Digits(Span<char> dst, int offset, int val)
        {
            dst[offset] = (char)('0' + (val / 10));
            dst[offset + 1] = (char)('0' + (val % 10));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Write4DigitsUtf8(Span<byte> dst, int offset, int val)
        {
            dst[offset] = (byte)('0' + (val / 1000));
            dst[offset + 1] = (byte)('0' + ((val / 100) % 10));
            dst[offset + 2] = (byte)('0' + ((val / 10) % 10));
            dst[offset + 3] = (byte)('0' + (val % 10));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Write2DigitsUtf8(Span<byte> dst, int offset, int val)
        {
            dst[offset] = (byte)('0' + (val / 10));
            dst[offset + 1] = (byte)('0' + (val % 10));
        }

        public bool Equals(CompactDate other) => _dayNumber == other._dayNumber;
        public override bool Equals(object? obj) => obj is CompactDate other && Equals(other);
        public override int GetHashCode() => _dayNumber;
        public int CompareTo(CompactDate other) => _dayNumber.CompareTo(other._dayNumber);

        public static bool operator ==(CompactDate left, CompactDate right) => left._dayNumber == right._dayNumber;
        public static bool operator !=(CompactDate left, CompactDate right) => left._dayNumber != right._dayNumber;
        public static bool operator <(CompactDate left, CompactDate right) => left._dayNumber < right._dayNumber;
        public static bool operator <=(CompactDate left, CompactDate right) => left._dayNumber <= right._dayNumber;
        public static bool operator >(CompactDate left, CompactDate right) => left._dayNumber > right._dayNumber;
        public static bool operator >=(CompactDate left, CompactDate right) => left._dayNumber >= right._dayNumber;
        public static int operator -(CompactDate left, CompactDate right) => left._dayNumber - right._dayNumber;

#if NET8_0_OR_GREATER
        public DateOnly ToDateOnly() => DateOnly.FromDayNumber(_dayNumber);
        public static CompactDate FromDateOnly(DateOnly dateOnly) => new CompactDate(dateOnly.DayNumber);
        public static explicit operator DateOnly(CompactDate d) => d.ToDateOnly();
        public static explicit operator CompactDate(DateOnly d) => FromDateOnly(d);
#endif
    }
}
