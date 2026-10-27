using System;

namespace ZeroUI.Core.Input.Date
{
    /// <summary>
    /// Quick selection date presets for industrial calendar and date range selectors.
    /// </summary>
    public enum DatePresetType
    {
        Today,
        Yesterday,
        Tomorrow,
        NextWeek,
        StartOfWeek,
        EndOfWeek,
        StartOfMonth,
        EndOfMonth
    }

    /// <summary>
    /// Helper utilities for evaluating date presets with zero heap allocations.
    /// </summary>
    public static class DatePresetHelper
    {
        /// <summary>
        /// Evaluates a date preset relative to a base date (defaults to DateTime.Today).
        /// </summary>
        public static DateTime Calculate(DatePresetType preset, DateTime? relativeTo = null)
        {
            DateTime baseDate = (relativeTo ?? DateTime.Today).Date;

            switch (preset)
            {
                case DatePresetType.Today:
                    return baseDate;

                case DatePresetType.Yesterday:
                    return baseDate.AddDays(-1);

                case DatePresetType.Tomorrow:
                    return baseDate.AddDays(1);

                case DatePresetType.NextWeek:
                    return baseDate.AddDays(7);

                case DatePresetType.StartOfWeek:
                    int diffToMonday = ((int)baseDate.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
                    return baseDate.AddDays(-diffToMonday);

                case DatePresetType.EndOfWeek:
                    int diffToSunday = ((int)DayOfWeek.Sunday - (int)baseDate.DayOfWeek + 7) % 7;
                    return baseDate.AddDays(diffToSunday);

                case DatePresetType.StartOfMonth:
                    return new DateTime(baseDate.Year, baseDate.Month, 1);

                case DatePresetType.EndOfMonth:
                    int daysInMonth = DateTime.DaysInMonth(baseDate.Year, baseDate.Month);
                    return new DateTime(baseDate.Year, baseDate.Month, daysInMonth);

                default:
                    return baseDate;
            }
        }
    }

    /// <summary>
    /// Quick range presets for industrial date range selectors (<c>DateRangePicker</c>).
    /// </summary>
    public enum DateRangePreset
    {
        Custom,
        Today,
        Yesterday,
        Last7Days,
        Last30Days,
        ThisMonth,
        LastMonth,
        YearToDate
    }

    /// <summary>
    /// Helper utilities for calculating date range presets.
    /// </summary>
    public static class DateRangePresetHelper
    {
        /// <summary>
        /// Calculates the start and end dates for the given preset relative to a base date (defaults to DateTime.Today).
        /// </summary>
        public static (DateTime Start, DateTime End) CalculateRange(DateRangePreset preset, DateTime? relativeTo = null, bool fullMonthForThisMonth = false)
        {
            DateTime baseDate = (relativeTo ?? DateTime.Today).Date;

            switch (preset)
            {
                case DateRangePreset.Today:
                    return (baseDate, baseDate);

                case DateRangePreset.Yesterday:
                    var y = baseDate.AddDays(-1);
                    return (y, y);

                case DateRangePreset.Last7Days:
                    return (baseDate.AddDays(-6), baseDate);

                case DateRangePreset.Last30Days:
                    return (baseDate.AddDays(-29), baseDate);

                case DateRangePreset.ThisMonth:
                    var startThisMonth = new DateTime(baseDate.Year, baseDate.Month, 1);
                    var endThisMonth = fullMonthForThisMonth
                        ? new DateTime(baseDate.Year, baseDate.Month, DateTime.DaysInMonth(baseDate.Year, baseDate.Month))
                        : baseDate;
                    return (startThisMonth, endThisMonth);

                case DateRangePreset.LastMonth:
                    var lastMonth = baseDate.AddMonths(-1);
                    int daysInLastMonth = DateTime.DaysInMonth(lastMonth.Year, lastMonth.Month);
                    return (new DateTime(lastMonth.Year, lastMonth.Month, 1), new DateTime(lastMonth.Year, lastMonth.Month, daysInLastMonth));

                case DateRangePreset.YearToDate:
                    return (new DateTime(baseDate.Year, 1, 1), baseDate);

                case DateRangePreset.Custom:
                default:
                    return (baseDate, baseDate);
            }
        }
    }
}
