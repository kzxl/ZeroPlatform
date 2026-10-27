using System;
using Xunit;
using ZeroUI.Core.Input.Date;

namespace ZeroUI.Core.Tests
{
    public class DateRangeViewModeTests
    {
        [Fact]
        public void NormalizeRange_DayMode_PreservesDatePortions()
        {
            var start = new DateTime(2026, 9, 14, 15, 30, 0);
            var end = new DateTime(2026, 9, 20, 18, 45, 0);

            var (normalizedStart, normalizedEnd) = DateRangeViewModeHelper.NormalizeRange(start, end, DateRangeViewMode.Day);

            Assert.Equal(new DateTime(2026, 9, 14), normalizedStart);
            Assert.Equal(new DateTime(2026, 9, 20), normalizedEnd);
        }

        [Fact]
        public void NormalizeRange_DayMode_SwapsIfEndBeforeStart()
        {
            var start = new DateTime(2026, 9, 20);
            var end = new DateTime(2026, 9, 14);

            var (normalizedStart, normalizedEnd) = DateRangeViewModeHelper.NormalizeRange(start, end, DateRangeViewMode.Day);

            Assert.Equal(new DateTime(2026, 9, 14), normalizedStart);
            Assert.Equal(new DateTime(2026, 9, 20), normalizedEnd);
        }

        [Fact]
        public void NormalizeRange_MonthMode_ExpandsToStartAndEndOfMonths()
        {
            // Start: 2026-02-15 -> Should become 2026-02-01
            // End: 2026-04-10 -> Should become 2026-04-30
            var start = new DateTime(2026, 2, 15);
            var end = new DateTime(2026, 4, 10);

            var (normalizedStart, normalizedEnd) = DateRangeViewModeHelper.NormalizeRange(start, end, DateRangeViewMode.Month);

            Assert.Equal(new DateTime(2026, 2, 1), normalizedStart);
            Assert.Equal(new DateTime(2026, 4, 30), normalizedEnd);
        }

        [Fact]
        public void NormalizeRange_MonthMode_LeapYearFebruary_Handles29Days()
        {
            // 2024 is a leap year (Feb has 29 days)
            var start = new DateTime(2024, 2, 5);
            var end = new DateTime(2024, 2, 18);

            var (normalizedStart, normalizedEnd) = DateRangeViewModeHelper.NormalizeRange(start, end, DateRangeViewMode.Month);

            Assert.Equal(new DateTime(2024, 2, 1), normalizedStart);
            Assert.Equal(new DateTime(2024, 2, 29), normalizedEnd);
        }

        [Fact]
        public void NormalizeRange_YearMode_ExpandsToFullDecadeOrYears()
        {
            var start = new DateTime(2023, 5, 20);
            var end = new DateTime(2026, 9, 14);

            var (normalizedStart, normalizedEnd) = DateRangeViewModeHelper.NormalizeRange(start, end, DateRangeViewMode.Year);

            Assert.Equal(new DateTime(2023, 1, 1), normalizedStart);
            Assert.Equal(new DateTime(2026, 12, 31), normalizedEnd);
        }

        [Fact]
        public void GetDefaultFormat_ReturnsExpectedFormatForEachMode()
        {
            Assert.Equal("yyyy-MM-dd", DateRangeViewModeHelper.GetDefaultFormat(DateRangeViewMode.Day));
            Assert.Equal("yyyy-MM", DateRangeViewModeHelper.GetDefaultFormat(DateRangeViewMode.Month));
            Assert.Equal("yyyy", DateRangeViewModeHelper.GetDefaultFormat(DateRangeViewMode.Year));
        }

        [Fact]
        public void FormatRange_FormatsCorrectlyForEachMode()
        {
            var start = new DateTime(2026, 3, 1);
            var end = new DateTime(2026, 10, 31);

            var dayFormatted = DateRangeViewModeHelper.FormatRange(start, end, DateRangeViewMode.Day);
            var monthFormatted = DateRangeViewModeHelper.FormatRange(start, end, DateRangeViewMode.Month);
            var yearFormatted = DateRangeViewModeHelper.FormatRange(start, end, DateRangeViewMode.Year);

            Assert.Equal("2026-03-01 - 2026-10-31", dayFormatted);
            Assert.Equal("2026-03 - 2026-10", monthFormatted);
            Assert.Equal("2026 - 2026", yearFormatted);
        }
    }
}
