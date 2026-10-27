using System;
using Xunit;
using ZeroUI.Core.Input.Time;

namespace ZeroUI.Core.Tests
{
    public class TimeSegmentModelTests
    {
        [Fact]
        public void TimeSegmentModel_InitializesAndSanitizesTime()
        {
            var model = new TimeSegmentModel(new TimeSpan(14, 30, 45));
            Assert.Equal(14, model.Time.Hours);
            Assert.Equal(30, model.Time.Minutes);
            Assert.Equal(45, model.Time.Seconds);

            // Modulo normalization test (e.g. >= 24h)
            model.Time = new TimeSpan(25, 10, 5);
            Assert.Equal(1, model.Time.Hours);
            Assert.Equal(10, model.Time.Minutes);
            Assert.Equal(5, model.Time.Seconds);
        }

        [Fact]
        public void TimeSegmentModel_AdjustHour_WrapsModulo24()
        {
            var model = new TimeSegmentModel(new TimeSpan(23, 0, 0));
            model.FocusedSegment = TimeSegment.Hour;

            model.AdjustCurrentSegment(1);
            Assert.Equal(0, model.Time.Hours);

            model.AdjustCurrentSegment(-1);
            Assert.Equal(23, model.Time.Hours);
        }

        [Fact]
        public void TimeSegmentModel_AdjustMinute_RespectsStepAndWraps()
        {
            var model = new TimeSegmentModel(new TimeSpan(10, 55, 0));
            model.FocusedSegment = TimeSegment.Minute;
            model.StepMinutes = 10;

            model.AdjustCurrentSegment(1); // 55 + 10 = 65 -> 5
            Assert.Equal(5, model.Time.Minutes);

            model.AdjustCurrentSegment(-1); // 5 - 10 = -5 -> 55
            Assert.Equal(55, model.Time.Minutes);
        }

        [Fact]
        public void TimeSegmentModel_12HourConversion_WorksCorrectly()
        {
            var model = new TimeSegmentModel(new TimeSpan(0, 15, 0)) { Is24Hour = false };
            Assert.Equal(12, model.DisplayHour);
            Assert.True(model.IsAm);

            model.Time = new TimeSpan(12, 0, 0);
            Assert.Equal(12, model.DisplayHour);
            Assert.False(model.IsAm);

            model.Time = new TimeSpan(15, 45, 0);
            Assert.Equal(3, model.DisplayHour);
            Assert.False(model.IsAm);

            model.ToggleAmPm();
            Assert.Equal(3, model.Time.Hours);
            Assert.True(model.IsAm);
        }

        [Fact]
        public void TimeSegmentModel_Navigation_TraversesSegmentsCorrectly()
        {
            var model = new TimeSegmentModel(new TimeSpan(8, 0, 0))
            {
                ShowSeconds = true,
                Is24Hour = false
            };

            Assert.Equal(TimeSegment.Hour, model.FocusedSegment);

            Assert.True(model.MoveNextSegment());
            Assert.Equal(TimeSegment.Minute, model.FocusedSegment);

            Assert.True(model.MoveNextSegment());
            Assert.Equal(TimeSegment.Second, model.FocusedSegment);

            Assert.True(model.MoveNextSegment());
            Assert.Equal(TimeSegment.AmPm, model.FocusedSegment);

            Assert.False(model.MoveNextSegment()); // End of segments

            Assert.True(model.MovePreviousSegment());
            Assert.Equal(TimeSegment.Second, model.FocusedSegment);
        }

        [Fact]
        public void TimeSegmentModel_DigitEntry_AccumulatesAndAdvances()
        {
            var model = new TimeSegmentModel(new TimeSpan(0, 0, 0))
            {
                Is24Hour = true
            };

            model.FocusedSegment = TimeSegment.Hour;
            // Type '1' then '4' -> 14
            Assert.True(model.TryApplyDigit(1));
            Assert.Equal(1, model.Time.Hours);
            Assert.Equal(TimeSegment.Hour, model.FocusedSegment);

            Assert.True(model.TryApplyDigit(4));
            Assert.Equal(14, model.Time.Hours);
            Assert.Equal(TimeSegment.Minute, model.FocusedSegment); // Auto advanced!

            // Minute: Type '5' then '8' -> 58
            Assert.True(model.TryApplyDigit(5));
            Assert.Equal(5, model.Time.Minutes);

            Assert.True(model.TryApplyDigit(8));
            Assert.Equal(58, model.Time.Minutes);
        }

        [Fact]
        public void TimeSegmentModel_SpanFormatting_ZeroAllocationMatch()
        {
            var model = new TimeSegmentModel(new TimeSpan(9, 5, 2))
            {
                ShowSeconds = true,
                Is24Hour = true
            };

            Span<char> buf = stackalloc char[16];
            Assert.True(model.TryFormat(buf, out int written));
            Assert.Equal("09:05:02", new string(buf.Slice(0, written).ToArray()));

            model.Is24Hour = false;
            Assert.True(model.TryFormat(buf, out written));
            Assert.Equal("09:05:02 AM", new string(buf.Slice(0, written).ToArray()));
        }
    }
}
