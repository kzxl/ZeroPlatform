using System;
using Xunit;
using ZeroUI.Core.Range;

namespace ZeroUI.Core.Tests
{
    public class RangeControlModelTests
    {
        [Fact]
        public void InitialState_HasValidDefaultBounds()
        {
            var model = new RangeControlModel();
            Assert.Equal(0.0, model.TotalRangeStart);
            Assert.Equal(100.0, model.TotalRangeEnd);
            Assert.Equal(20.0, model.SelectedRangeStart);
            Assert.Equal(80.0, model.SelectedRangeEnd);
            Assert.Equal(60.0, model.SelectedRangeSpan);
            Assert.Equal(0.0, model.VisibleRangeStart);
            Assert.Equal(100.0, model.VisibleRangeEnd);
        }

        [Fact]
        public void ValueToRatio_And_RatioToValue_Roundtrip()
        {
            var model = new RangeControlModel();
            model.SetTotalRange(100.0, 500.0);
            model.SetVisibleRange(100.0, 500.0);

            double val = 300.0;
            double ratio = model.ValueToRatio(val);
            Assert.Equal(0.5, ratio, 4);

            double roundtrip = model.RatioToValue(ratio);
            Assert.Equal(val, roundtrip, 4);

            // Pixel mapping test with canvas width 800
            double px = model.ValueToPixel(val, 800);
            Assert.Equal(400.0, px, 4);

            double fromPx = model.PixelToValue(px, 800);
            Assert.Equal(val, fromPx, 4);
        }

        [Fact]
        public void SetSelectedRange_EnforcesClampingAndOrder()
        {
            var model = new RangeControlModel();
            model.SetTotalRange(0, 100);

            // Inverted inputs should automatically sort start <= end
            model.SetSelectedRange(70, 30);
            Assert.Equal(30.0, model.SelectedRangeStart);
            Assert.Equal(70.0, model.SelectedRangeEnd);

            // Out-of-bounds inputs should clamp to total limits
            model.SetSelectedRange(-50, 150);
            Assert.Equal(0.0, model.SelectedRangeStart);
            Assert.Equal(100.0, model.SelectedRangeEnd);
        }

        [Fact]
        public void PanSelection_ShiftsSelectionWindowPreservingSpan()
        {
            var model = new RangeControlModel();
            model.SetTotalRange(0, 100);
            model.SetSelectedRange(20, 50); // Span = 30

            // Pan forward by 10
            model.PanSelection(10);
            Assert.Equal(30.0, model.SelectedRangeStart);
            Assert.Equal(60.0, model.SelectedRangeEnd);
            Assert.Equal(30.0, model.SelectedRangeSpan);

            // Pan hitting end boundary (try +100)
            model.PanSelection(100);
            Assert.Equal(70.0, model.SelectedRangeStart);
            Assert.Equal(100.0, model.SelectedRangeEnd);
            Assert.Equal(30.0, model.SelectedRangeSpan);

            // Pan hitting start boundary (try -200)
            model.PanSelection(-200);
            Assert.Equal(0.0, model.SelectedRangeStart);
            Assert.Equal(30.0, model.SelectedRangeEnd);
            Assert.Equal(30.0, model.SelectedRangeSpan);
        }

        [Fact]
        public void Zoom_ModifiesVisibleRangeAroundCenterRatio()
        {
            var model = new RangeControlModel();
            model.SetTotalRange(0, 100);
            model.SetVisibleRange(0, 100);

            // Zoom in by factor of 2 at center (0.5)
            model.Zoom(2.0, 0.5);
            Assert.Equal(25.0, model.VisibleRangeStart, 4);
            Assert.Equal(75.0, model.VisibleRangeEnd, 4);
            Assert.Equal(50.0, model.VisibleRangeSpan, 4);

            // Zoom out by factor of 0.5 (zoom out factor 0.5 expands back to 100)
            model.Zoom(0.5, 0.5);
            Assert.Equal(0.0, model.VisibleRangeStart, 4);
            Assert.Equal(100.0, model.VisibleRangeEnd, 4);
        }

        [Fact]
        public void Snap_RoundsCorrectlyForNumericStepAndDateTime()
        {
            var model = new RangeControlModel
            {
                SnapToInterval = true,
                NumericStep = 5.0
            };
            model.SetTotalRange(0, 100);

            Assert.Equal(10.0, model.Snap(11.2));
            Assert.Equal(15.0, model.Snap(13.8));

            // Test DateTime snapping to Day
            var startDt = new DateTime(2026, 1, 1, 14, 30, 0);
            var endDt = new DateTime(2026, 1, 10, 18, 45, 0);
            model.SetTotalDateRange(startDt, endDt);
            model.Interval = RangeInterval.Day;

            double testOaDate = new DateTime(2026, 1, 5, 17, 30, 0).ToOADate();
            double snappedOaDate = model.Snap(testOaDate);
            DateTime snappedDt = DateTime.FromOADate(snappedOaDate);

            Assert.Equal(new DateTime(2026, 1, 5, 0, 0, 0), snappedDt);
        }

        [Fact]
        public void RangeSelectionChanged_FiresEventOnRangeAdjustment()
        {
            var model = new RangeControlModel();
            int firedCount = 0;
            double capturedStart = 0;
            double capturedEnd = 0;

            model.RangeSelectionChanged += (s, e) =>
            {
                firedCount++;
                capturedStart = e.Start;
                capturedEnd = e.End;
            };

            model.SetSelectedRange(30, 70);
            Assert.Equal(1, firedCount);
            Assert.Equal(30.0, capturedStart);
            Assert.Equal(70.0, capturedEnd);
        }
    }
}
