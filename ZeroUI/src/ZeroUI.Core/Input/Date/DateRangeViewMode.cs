using System;

namespace ZeroUI.Core.Input.Date
{
    /// <summary>
    /// Specifies the calendar resolution and selection granularity for a <c>DateRangePicker</c>.
    /// </summary>
    public enum DateRangeViewMode
    {
        /// <summary>
        /// Standard day-level range picker (select specific days from/to).
        /// </summary>
        Day,

        /// <summary>
        /// Month-level range picker (select month from/to; restricts day input and clamps to full months).
        /// </summary>
        Month,

        /// <summary>
        /// Year-level range picker (select year from/to; clamps to Jan 1 - Dec 31).
        /// </summary>
        Year
    }

    /// <summary>
    /// Helper utilities for calculating and clamping date boundaries based on <see cref="DateRangeViewMode"/>.
    /// </summary>
    public static class DateRangeViewModeHelper
    {
        /// <summary>
        /// Clamps the given start and end dates according to the specified <see cref="DateRangeViewMode"/>.
        /// For Month mode, start becomes 1st of month (00:00:00) and end becomes last day of month.
        /// For Year mode, start becomes Jan 1 and end becomes Dec 31.
        /// </summary>
        public static (DateTime Start, DateTime End) NormalizeRange(DateRangeViewMode mode, DateTime start, DateTime end)
        {
            DateTime s = start < end ? start.Date : end.Date;
            DateTime e = end >= start ? end.Date : start.Date;

            switch (mode)
            {
                case DateRangeViewMode.Month:
                    var firstDay = new DateTime(s.Year, s.Month, 1);
                    int daysInEndMonth = DateTime.DaysInMonth(e.Year, e.Month);
                    var lastDay = new DateTime(e.Year, e.Month, daysInEndMonth);
                    return (firstDay, lastDay);

                case DateRangeViewMode.Year:
                    var firstDayOfYear = new DateTime(s.Year, 1, 1);
                    var lastDayOfYear = new DateTime(e.Year, 12, 31);
                    return (firstDayOfYear, lastDayOfYear);

                case DateRangeViewMode.Day:
                default:
                    return (s, e);
            }
        }

        /// <summary>
        /// Clamps the given start and end dates according to the specified <see cref="DateRangeViewMode"/>.
        /// </summary>
        public static (DateTime Start, DateTime End) NormalizeRange(DateTime start, DateTime end, DateRangeViewMode mode)
            => NormalizeRange(mode, start, end);

        /// <summary>
        /// Gets the recommended standard date format pattern for the given <see cref="DateRangeViewMode"/>.
        /// </summary>
        public static string GetDefaultFormat(DateRangeViewMode mode)
        {
            return mode switch
            {
                DateRangeViewMode.Month => "yyyy-MM",
                DateRangeViewMode.Year => "yyyy",
                _ => "yyyy-MM-dd"
            };
        }

        /// <summary>
        /// Formats the date range into a user-friendly string based on the active <see cref="DateRangeViewMode"/>.
        /// </summary>
        public static string FormatRange(DateTime start, DateTime end, DateRangeViewMode mode, string separator = " - ")
        {
            string fmt = GetDefaultFormat(mode);
            return $"{start.ToString(fmt)}{separator}{end.ToString(fmt)}";
        }

        /// <summary>
        /// Formats the date range into a user-friendly string based on the active <see cref="DateRangeViewMode"/>.
        /// </summary>
        public static string FormatRange(DateRangeViewMode mode, DateTime start, DateTime end, string separator = " - ")
            => FormatRange(start, end, mode, separator);
    }
}
