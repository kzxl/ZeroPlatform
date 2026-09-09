using System;
using System.Globalization;

namespace ZeroPrimitives.Extensions
{
    /// <summary>
    /// Comprehensive date, time, calendar, and timestamp extension methods.
    /// </summary>
    public static class DateTimeExtensions
    {
        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        #region Month & Calendar Boundaries

        public static DateTime StartOfMonth(this DateTime date)
            => new DateTime(date.Year, date.Month, 1, 0, 0, 0, date.Kind);

        public static DateTime? StartOfMonth(this DateTime? date)
            => date.HasValue ? date.Value.StartOfMonth() : (DateTime?)null;

        public static DateTime EndOfMonth(this DateTime date)
            => new DateTime(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month), 23, 59, 59, 999, date.Kind);

        public static DateTime? EndOfMonth(this DateTime? date)
            => date.HasValue ? date.Value.EndOfMonth() : (DateTime?)null;

        public static DateTime StartOfYear(this DateTime date)
            => new DateTime(date.Year, 1, 1, 0, 0, 0, date.Kind);

        public static DateTime? StartOfYear(this DateTime? date)
            => date.HasValue ? date.Value.StartOfYear() : (DateTime?)null;

        public static DateTime EndOfYear(this DateTime date)
            => new DateTime(date.Year, 12, 31, 23, 59, 59, 999, date.Kind);

        public static DateTime? EndOfYear(this DateTime? date)
            => date.HasValue ? date.Value.EndOfYear() : (DateTime?)null;

        public static DateTime StartOfQuarter(this DateTime date)
        {
            int quarterFirstMonth = ((date.Month - 1) / 3) * 3 + 1;
            return new DateTime(date.Year, quarterFirstMonth, 1, 0, 0, 0, date.Kind);
        }

        public static DateTime? StartOfQuarter(this DateTime? date)
            => date.HasValue ? date.Value.StartOfQuarter() : (DateTime?)null;

        public static DateTime EndOfQuarter(this DateTime date)
        {
            int quarterLastMonth = (((date.Month - 1) / 3) * 3) + 3;
            return new DateTime(date.Year, quarterLastMonth, DateTime.DaysInMonth(date.Year, quarterLastMonth), 23, 59, 59, 999, date.Kind);
        }

        public static DateTime? EndOfQuarter(this DateTime? date)
            => date.HasValue ? date.Value.EndOfQuarter() : (DateTime?)null;

        public static DateTime StartOfDay(this DateTime date)
            => new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, date.Kind);

        public static DateTime? StartOfDay(this DateTime? date)
            => date.HasValue ? date.Value.StartOfDay() : (DateTime?)null;

        public static DateTime EndOfDay(this DateTime date)
            => new DateTime(date.Year, date.Month, date.Day, 23, 59, 59, 999, date.Kind);

        public static DateTime? EndOfDay(this DateTime? date)
            => date.HasValue ? date.Value.EndOfDay() : (DateTime?)null;

        // Legacy aliases
        public static DateTime FirstDayOfMonth(this DateTime date) => StartOfMonth(date);
        public static DateTime? FirstDayOfMonth(this DateTime? date) => StartOfMonth(date);
        public static DateTime LastDayOfMonth(this DateTime date) => EndOfMonth(date);
        public static DateTime? LastDayOfMonth(this DateTime? date) => EndOfMonth(date);
        public static DateTime FirstDayOfYear(this DateTime date) => StartOfYear(date);
        public static DateTime? FirstDayOfYear(this DateTime? date) => StartOfYear(date);
        public static DateTime LastDayOfYear(this DateTime date) => EndOfYear(date);
        public static DateTime? LastDayOfYear(this DateTime? date) => EndOfYear(date);
        public static DateTime FirstDayOfQuarter(this DateTime date) => StartOfQuarter(date);
        public static DateTime? FirstDayOfQuarter(this DateTime? date) => StartOfQuarter(date);
        public static DateTime LastDayOfQuarter(this DateTime date) => EndOfQuarter(date);
        public static DateTime? LastDayOfQuarter(this DateTime? date) => EndOfQuarter(date);

        #endregion

        #region Unix Timestamp Conversions

        public static long ToUnixTimestampSeconds(this DateTime date)
        {
            var utc = date.Kind == DateTimeKind.Utc ? date : date.ToUniversalTime();
            return (long)(utc - UnixEpoch).TotalSeconds;
        }

        public static long ToUnixTimestampMilliseconds(this DateTime date)
        {
            var utc = date.Kind == DateTimeKind.Utc ? date : date.ToUniversalTime();
            return (long)(utc - UnixEpoch).TotalMilliseconds;
        }

        public static DateTime FromUnixSeconds(long seconds)
            => UnixEpoch.AddSeconds(seconds);

        public static DateTime FromUnixMilliseconds(long milliseconds)
            => UnixEpoch.AddMilliseconds(milliseconds);

        #endregion

        #region Formatting

        /// <summary>
        /// Formats date to Vietnamese format: dd/MM/yyyy.
        /// </summary>
        public static string ToVnDateString(this DateTime dt)
            => dt.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

        public static string ToVnDateString(this DateTime? dt)
            => dt.HasValue ? dt.Value.ToVnDateString() : string.Empty;

        /// <summary>
        /// Formats date to Vietnamese format with time: dd/MM/yyyy HH:mm:ss.
        /// </summary>
        public static string ToVnDateTimeString(this DateTime dt)
            => dt.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture);

        public static string ToVnDateTimeString(this DateTime? dt)
            => dt.HasValue ? dt.Value.ToVnDateTimeString() : string.Empty;

        /// <summary>
        /// Formats date to ISO 8601 date format: yyyy-MM-dd.
        /// </summary>
        public static string ToIsoDateString(this DateTime dt)
            => dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        public static string ToIsoDateString(this DateTime? dt)
            => dt.HasValue ? dt.Value.ToIsoDateString() : string.Empty;

        /// <summary>
        /// Formats date to ISO 8601 datetime format: yyyy-MM-dd HH:mm:ss.
        /// </summary>
        public static string ToIsoDateTimeString(this DateTime dt)
            => dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        public static string ToIsoDateTimeString(this DateTime? dt)
            => dt.HasValue ? dt.Value.ToIsoDateTimeString() : string.Empty;

        /// <summary>
        /// Formats date with custom or default pattern.
        /// </summary>
        public static string ToDateString(this DateTime dt, string format = "dd/MM/yyyy")
            => dt.ToString(format, CultureInfo.InvariantCulture);

        public static string ToDateString(this DateTime? dt, string format = "dd/MM/yyyy")
            => dt.HasValue ? dt.Value.ToDateString(format) : string.Empty;

        // Backward compatibility aliases (deprecated underscore names)
        public static string AsDateString_ddMMyyyy(this DateTime dt) => ToVnDateString(dt);
        public static string AsDateString_ddMMyyyy(this DateTime? dt) => ToVnDateString(dt);
        public static string AsDateString_ddMMyyyyHHmmss(this DateTime dt) => ToVnDateTimeString(dt);
        public static string AsDateString_ddMMyyyyHHmmss(this DateTime? dt) => ToVnDateTimeString(dt);

        /// <summary>
        /// Converts to English ordinal date format string (e.g. "6th Jan, 2026" or "6&lt;sup&gt;th&lt;/sup&gt; Jan, 2026").
        /// </summary>
        public static string ToOrdinalDateString(this DateTime dt, bool useHtmlSuperscript = false)
        {
            int day = dt.Day;
            string suffix = GetOrdinalSuffix(day);
            string month = dt.ToString("MMM", CultureInfo.InvariantCulture);
            string formattedSuffix = useHtmlSuperscript ? $"<sup>{suffix}</sup>" : suffix;
            return $"{day}{formattedSuffix} {month}, {dt.Year}";
        }

        public static string ToOrdinalDateString(this DateTime? dt, bool useHtmlSuperscript = false)
            => dt.HasValue ? dt.Value.ToOrdinalDateString(useHtmlSuperscript) : string.Empty;

        public static string GetOrdinalSuffix(int day)
        {
            if (day >= 11 && day <= 13) return "th";
            switch (day % 10)
            {
                case 1: return "st";
                case 2: return "nd";
                case 3: return "rd";
                default: return "th";
            }
        }

        #endregion
    }
}
