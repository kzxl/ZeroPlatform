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

        public static DateTime FirstDayOfMonth(this DateTime date)
            => new DateTime(date.Year, date.Month, 1, 0, 0, 0, date.Kind);

        public static DateTime? FirstDayOfMonth(this DateTime? date)
            => date.HasValue ? date.Value.FirstDayOfMonth() : (DateTime?)null;

        public static DateTime LastDayOfMonth(this DateTime date)
            => new DateTime(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month), 23, 59, 59, 999, date.Kind);

        public static DateTime? LastDayOfMonth(this DateTime? date)
            => date.HasValue ? date.Value.LastDayOfMonth() : (DateTime?)null;

        public static DateTime FirstDayOfYear(this DateTime date)
            => new DateTime(date.Year, 1, 1, 0, 0, 0, date.Kind);

        public static DateTime? FirstDayOfYear(this DateTime? date)
            => date.HasValue ? date.Value.FirstDayOfYear() : (DateTime?)null;

        public static DateTime LastDayOfYear(this DateTime date)
            => new DateTime(date.Year, 12, 31, 23, 59, 59, 999, date.Kind);

        public static DateTime? LastDayOfYear(this DateTime? date)
            => date.HasValue ? date.Value.LastDayOfYear() : (DateTime?)null;

        public static DateTime FirstDayOfQuarter(this DateTime date)
        {
            int quarterFirstMonth = ((date.Month - 1) / 3) * 3 + 1;
            return new DateTime(date.Year, quarterFirstMonth, 1, 0, 0, 0, date.Kind);
        }

        public static DateTime LastDayOfQuarter(this DateTime date)
        {
            int quarterLastMonth = (((date.Month - 1) / 3) * 3) + 3;
            return new DateTime(date.Year, quarterLastMonth, DateTime.DaysInMonth(date.Year, quarterLastMonth), 23, 59, 59, 999, date.Kind);
        }

        public static DateTime StartOfDay(this DateTime date)
            => new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, date.Kind);

        public static DateTime EndOfDay(this DateTime date)
            => new DateTime(date.Year, date.Month, date.Day, 23, 59, 59, 999, date.Kind);

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
        public static string AsDateString_ddMMyyyy(this DateTime dt)
            => dt.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

        public static string AsDateString_ddMMyyyy(this DateTime? dt)
            => dt.HasValue ? dt.Value.AsDateString_ddMMyyyy() : string.Empty;

        /// <summary>
        /// Formats date to Vietnamese format with time: dd/MM/yyyy HH:mm:ss.
        /// </summary>
        public static string AsDateString_ddMMyyyyHHmmss(this DateTime dt)
            => dt.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture);

        public static string AsDateString_ddMMyyyyHHmmss(this DateTime? dt)
            => dt.HasValue ? dt.Value.AsDateString_ddMMyyyyHHmmss() : string.Empty;

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
