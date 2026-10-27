using System;

namespace ZeroUI.Core.Input.Date
{
    /// <summary>
    /// Headless calendar state engine and 42-cell matrix generator.
    /// Provides zero-allocation day/month calculations, boundary clamping, and navigation across years and decades.
    /// </summary>
    public class CalendarModel
    {
        public const int TotalCells = 42; // Standard 6 rows x 7 days

        private DateTime _selectedDate;
        private DateTime _viewDate;
        private DateTime? _minDate;
        private DateTime? _maxDate;
        private DayOfWeek _firstDayOfWeek = DayOfWeek.Sunday;

        public event EventHandler? SelectedDateChanged;
        public event EventHandler? ViewDateChanged;

        public CalendarModel(DateTime? initialDate = null)
        {
            DateTime date = (initialDate ?? DateTime.Today).Date;
            _selectedDate = date;
            _viewDate = new DateTime(date.Year, date.Month, 1);
        }

        public DateTime SelectedDate
        {
            get => _selectedDate;
            set => SelectDate(value);
        }

        public DateTime ViewDate
        {
            get => _viewDate;
            set
            {
                DateTime sanitized = new DateTime(value.Year, value.Month, 1);
                if (_viewDate != sanitized)
                {
                    _viewDate = sanitized;
                    ViewDateChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public int ViewYear => _viewDate.Year;
        public int ViewMonth => _viewDate.Month;

        public DateTime? MinDate
        {
            get => _minDate;
            set
            {
                _minDate = value?.Date;
                if (_minDate.HasValue && _selectedDate < _minDate.Value)
                {
                    SelectDate(_minDate.Value);
                }
            }
        }

        public DateTime? MaxDate
        {
            get => _maxDate;
            set
            {
                _maxDate = value?.Date;
                if (_maxDate.HasValue && _selectedDate > _maxDate.Value)
                {
                    SelectDate(_maxDate.Value);
                }
            }
        }

        public DayOfWeek FirstDayOfWeek
        {
            get => _firstDayOfWeek;
            set
            {
                if (_firstDayOfWeek != value)
                {
                    _firstDayOfWeek = value;
                    ViewDateChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public bool SelectDate(DateTime date)
        {
            DateTime target = date.Date;

            // Clamp to boundaries if specified
            if (_minDate.HasValue && target < _minDate.Value)
                target = _minDate.Value;
            if (_maxDate.HasValue && target > _maxDate.Value)
                target = _maxDate.Value;

            bool changed = _selectedDate != target;
            _selectedDate = target;

            // Automatically sync view date if selection is in a different month
            if (_viewDate.Year != target.Year || _viewDate.Month != target.Month)
            {
                _viewDate = new DateTime(target.Year, target.Month, 1);
                ViewDateChanged?.Invoke(this, EventArgs.Empty);
            }

            if (changed)
            {
                SelectedDateChanged?.Invoke(this, EventArgs.Empty);
            }

            return changed;
        }

        public void NavigatePreviousMonth()
        {
            ViewDate = _viewDate.AddMonths(-1);
        }

        public void NavigateNextMonth()
        {
            ViewDate = _viewDate.AddMonths(1);
        }

        public void NavigatePreviousYear()
        {
            ViewDate = _viewDate.AddYears(-1);
        }

        public void NavigateNextYear()
        {
            ViewDate = _viewDate.AddYears(1);
        }

        public void SetView(int year, int month)
        {
            if (year < 1 || year > 9999) throw new ArgumentOutOfRangeException(nameof(year));
            if (month < 1 || month > 12) throw new ArgumentOutOfRangeException(nameof(month));

            ViewDate = new DateTime(year, month, 1);
        }

        /// <summary>
        /// Populates the caller-supplied Span buffer with exactly 42 calendar day cells for the current view.
        /// Zero heap allocation footprint.
        /// </summary>
        public void FillDaysGrid(Span<CalendarDayCell> destination)
        {
            if (destination.Length < TotalCells)
            {
                throw new ArgumentException($"Destination span must have at least {TotalCells} elements.", nameof(destination));
            }

            DateTime firstOfMonth = _viewDate;
            int offset = ((int)firstOfMonth.DayOfWeek - (int)_firstDayOfWeek + 7) % 7;
            DateTime startDate = firstOfMonth.AddDays(-offset);
            DateTime today = DateTime.Today;
            DateTime sel = _selectedDate.Date;

            for (int i = 0; i < TotalCells; i++)
            {
                DateTime cellDate = startDate.AddDays(i);
                bool isCurrentMonth = cellDate.Year == firstOfMonth.Year && cellDate.Month == firstOfMonth.Month;
                bool isToday = cellDate == today;
                bool isSelected = cellDate == sel;
                bool isDisabled = (_minDate.HasValue && cellDate < _minDate.Value) ||
                                  (_maxDate.HasValue && cellDate > _maxDate.Value);

                destination[i] = new CalendarDayCell(cellDate, isCurrentMonth, isToday, isSelected, isDisabled);
            }
        }
    }
}
