using System;
using System.Globalization;

namespace ZeroDocuments.Excel.Internal
{
    /// <summary>
    /// Converts between .NET <see cref="DateTime"/> values, Excel OLE Automation serials, and the ISO text
    /// representation exposed by <see cref="ExcelReader"/> (yyyy-MM-dd [HH:mm:ss[.fff]]).
    /// </summary>
    internal static class OADateFormatter
    {
        private const double MinOADate = -657435.0;
        private const double MaxOADate = 2958466.0;
        private const double Date1904Offset = 1462.0;

        /// <summary>
        /// Formats a DateTime as ISO text: date-only when the time component is zero, milliseconds only when non-zero.
        /// </summary>
        public static string FormatIso(DateTime dt)
        {
            if (dt.TimeOfDay == TimeSpan.Zero)
                return dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            return dt.Millisecond != 0
                ? dt.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)
                : dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Converts a raw numeric cell value of a date-formatted cell into ISO text.
        /// Serials in [0, 1) are treated as time-of-day values and formatted as HH:mm:ss.
        /// </summary>
        public static bool TryFormatSerial(string raw, bool date1904, out string formatted)
        {
            formatted = raw;
            if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double serial))
                return false;

            if (serial >= 0 && serial < 1)
            {
                var time = FromDayFraction(serial);
                formatted = time.Milliseconds != 0
                    ? time.ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture)
                    : time.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
                return true;
            }

            if (!TryFromSerial(serial, date1904, out var dt))
                return false;

            formatted = FormatIso(dt);
            return true;
        }

        /// <summary>
        /// Converts an OLE Automation serial (1900 or 1904 date system) into a DateTime.
        /// </summary>
        public static bool TryFromSerial(double serial, bool date1904, out DateTime value)
        {
            value = default;
            if (double.IsNaN(serial) || double.IsInfinity(serial)) return false;
            if (date1904) serial += Date1904Offset;
            if (serial <= MinOADate || serial >= MaxOADate) return false;

            value = DateTime.FromOADate(serial);
            return true;
        }

        /// <summary>
        /// Converts a fraction of a day into a TimeSpan rounded to the nearest millisecond
        /// (consistent across .NET Framework and .NET Core rounding semantics).
        /// </summary>
        public static TimeSpan FromDayFraction(double days)
        {
            double ms = Math.Round(days * TimeSpan.TicksPerDay / TimeSpan.TicksPerMillisecond);
            return TimeSpan.FromTicks((long)ms * TimeSpan.TicksPerMillisecond);
        }
    }
}
