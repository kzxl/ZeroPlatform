using System;

namespace ZeroUI.Core.Input.Date
{
    /// <summary>
    /// Lightweight value-type representing a single day cell in a 42-cell (6x7) calendar grid.
    /// Zero heap allocation footprint when filling or iterating calendar matrices.
    /// </summary>
    public readonly struct CalendarDayCell : IEquatable<CalendarDayCell>
    {
        public DateTime Date { get; }
        public int DayNumber => Date.Day;
        public bool IsCurrentMonth { get; }
        public bool IsToday { get; }
        public bool IsSelected { get; }
        public bool IsDisabled { get; }

        public CalendarDayCell(
            DateTime date,
            bool isCurrentMonth,
            bool isToday,
            bool isSelected,
            bool isDisabled = false)
        {
            Date = date;
            IsCurrentMonth = isCurrentMonth;
            IsToday = isToday;
            IsSelected = isSelected;
            IsDisabled = isDisabled;
        }

        public bool Equals(CalendarDayCell other) =>
            Date == other.Date &&
            IsCurrentMonth == other.IsCurrentMonth &&
            IsToday == other.IsToday &&
            IsSelected == other.IsSelected &&
            IsDisabled == other.IsDisabled;

        public override bool Equals(object? obj) =>
            obj is CalendarDayCell other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Date.GetHashCode();
                hash = (hash * 397) ^ IsCurrentMonth.GetHashCode();
                hash = (hash * 397) ^ IsToday.GetHashCode();
                hash = (hash * 397) ^ IsSelected.GetHashCode();
                hash = (hash * 397) ^ IsDisabled.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(CalendarDayCell left, CalendarDayCell right) => left.Equals(right);
        public static bool operator !=(CalendarDayCell left, CalendarDayCell right) => !left.Equals(right);

        public override string ToString() =>
            $"{Date:yyyy-MM-dd} (Day {DayNumber}, {(IsCurrentMonth ? "Current" : "Adjacent")}{(IsSelected ? ", Selected" : "")}{(IsToday ? ", Today" : "")})";
    }
}
