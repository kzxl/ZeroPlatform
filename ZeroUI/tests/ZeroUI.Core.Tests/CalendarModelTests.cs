using System;
using Xunit;
using ZeroUI.Core.Input.Date;

namespace ZeroUI.Core.Tests
{
    public class CalendarModelTests
    {
        [Fact]
        public void CalendarModel_InitializesAndSetsViewToMonth()
        {
            var initial = new DateTime(2026, 9, 4);
            var model = new CalendarModel(initial);

            Assert.Equal(initial, model.SelectedDate);
            Assert.Equal(2026, model.ViewYear);
            Assert.Equal(9, model.ViewMonth);
        }

        [Fact]
        public void CalendarModel_FillDaysGrid_Populates42CellsAccurately()
        {
            // September 2026 starts on Tuesday (DayOfWeek.Tuesday = 2)
            // If FirstDayOfWeek = Sunday (0), offset is 2 days (Aug 30, Aug 31)
            var model = new CalendarModel(new DateTime(2026, 9, 15))
            {
                FirstDayOfWeek = DayOfWeek.Sunday
            };

            Span<CalendarDayCell> cells = stackalloc CalendarDayCell[CalendarModel.TotalCells];
            model.FillDaysGrid(cells);

            Assert.Equal(42, cells.Length);

            // First cell should be Sunday, Aug 30, 2026
            Assert.Equal(new DateTime(2026, 8, 30), cells[0].Date);
            Assert.False(cells[0].IsCurrentMonth);

            // Tuesday Sep 1 should be at index 2
            Assert.Equal(new DateTime(2026, 9, 1), cells[2].Date);
            Assert.True(cells[2].IsCurrentMonth);

            // Wednesday Sep 30 should be at index 31
            Assert.Equal(new DateTime(2026, 9, 30), cells[31].Date);
            Assert.True(cells[31].IsCurrentMonth);

            // Thursday Oct 1 should be at index 32
            Assert.Equal(new DateTime(2026, 10, 1), cells[32].Date);
            Assert.False(cells[32].IsCurrentMonth);

            // Cell 16 (Sep 15) should be selected
            Assert.Equal(new DateTime(2026, 9, 15), cells[16].Date);
            Assert.True(cells[16].IsSelected);
        }

        [Fact]
        public void CalendarModel_Navigation_MonthAndYearBoundaries()
        {
            var model = new CalendarModel(new DateTime(2026, 12, 15));

            model.NavigateNextMonth();
            Assert.Equal(2027, model.ViewYear);
            Assert.Equal(1, model.ViewMonth);

            model.NavigatePreviousMonth();
            Assert.Equal(2026, model.ViewYear);
            Assert.Equal(12, model.ViewMonth);

            model.NavigateNextYear();
            Assert.Equal(2027, model.ViewYear);
            Assert.Equal(12, model.ViewMonth);

            model.NavigatePreviousYear();
            Assert.Equal(2026, model.ViewYear);
            Assert.Equal(12, model.ViewMonth);
        }

        [Fact]
        public void CalendarModel_MinMaxClamping_DisablesCellsAndClampsSelection()
        {
            var model = new CalendarModel(new DateTime(2026, 9, 15))
            {
                MinDate = new DateTime(2026, 9, 10),
                MaxDate = new DateTime(2026, 9, 20)
            };

            // Attempting to select before min clamps to min
            model.SelectDate(new DateTime(2026, 9, 5));
            Assert.Equal(new DateTime(2026, 9, 10), model.SelectedDate);

            // Attempting to select after max clamps to max
            model.SelectDate(new DateTime(2026, 9, 25));
            Assert.Equal(new DateTime(2026, 9, 20), model.SelectedDate);

            Span<CalendarDayCell> cells = stackalloc CalendarDayCell[CalendarModel.TotalCells];
            model.FillDaysGrid(cells);

            foreach (var cell in cells)
            {
                if (cell.Date < new DateTime(2026, 9, 10) || cell.Date > new DateTime(2026, 9, 20))
                {
                    Assert.True(cell.IsDisabled, $"Cell {cell.Date:yyyy-MM-dd} should be disabled.");
                }
                else
                {
                    Assert.False(cell.IsDisabled, $"Cell {cell.Date:yyyy-MM-dd} should be enabled.");
                }
            }
        }

        [Fact]
        public void DatePresetHelper_CalculatesKeyPresetsAccurately()
        {
            var baseDate = new DateTime(2026, 9, 4); // A Friday

            Assert.Equal(new DateTime(2026, 9, 4), DatePresetHelper.Calculate(DatePresetType.Today, baseDate));
            Assert.Equal(new DateTime(2026, 9, 3), DatePresetHelper.Calculate(DatePresetType.Yesterday, baseDate));
            Assert.Equal(new DateTime(2026, 9, 5), DatePresetHelper.Calculate(DatePresetType.Tomorrow, baseDate));
            Assert.Equal(new DateTime(2026, 9, 11), DatePresetHelper.Calculate(DatePresetType.NextWeek, baseDate));
            Assert.Equal(new DateTime(2026, 9, 1), DatePresetHelper.Calculate(DatePresetType.StartOfMonth, baseDate));
            Assert.Equal(new DateTime(2026, 9, 30), DatePresetHelper.Calculate(DatePresetType.EndOfMonth, baseDate));
        }
    }
}
