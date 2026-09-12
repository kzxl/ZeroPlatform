using System;
using System.Drawing;
using Xunit;
using ZeroPlatform.Charts.Axis;
using ZeroPlatform.Charts.DataModels;

namespace ZeroPlatform.Charts.Tests
{
    public class ChartTests
    {
        [Fact]
        public void LineSeries_AddPoints_CalculatesCorrectBounds()
        {
            var series = new LineSeries("Telemetry_Sensor_1");
            series.Add(0, 10);
            series.Add(1, 25);
            series.Add(2, -5);
            series.Add(3, 40);

            Assert.Equal(4, series.Count);
            Assert.Equal(0.0, series.MinX);
            Assert.Equal(3.0, series.MaxX);
            Assert.Equal(-5.0, series.MinY);
            Assert.Equal(40.0, series.MaxY);
        }

        [Fact]
        public void LineSeries_DownsampleLttb_PreservesEndpointsAndReducesPoints()
        {
            var series = new LineSeries("SineWave");
            int totalPoints = 5000;
            for (int i = 0; i < totalPoints; i++)
            {
                series.Add(i, Math.Sin(i * 0.05) * 50.0);
            }

            int targetThreshold = 200;
            var downsampled = series.DownsampleLttb(targetThreshold);

            Assert.Equal(targetThreshold, downsampled.Count);
            // Endpoints preserved
            Assert.Equal(series.Points[0].X, downsampled[0].X);
            Assert.Equal(series.Points[0].Y, downsampled[0].Y);
            Assert.Equal(series.Points[totalPoints - 1].X, downsampled[targetThreshold - 1].X);
            Assert.Equal(series.Points[totalPoints - 1].Y, downsampled[targetThreshold - 1].Y);
        }

        [Fact]
        public void Candlestick_OHLC_CalculatesBullishAndBodyCorrectly()
        {
            DateTime dt = DateTime.UtcNow;
            var bullish = new CandlestickItem(dt, open: 100, high: 115, low: 95, close: 110);
            Assert.True(bullish.IsBullish);
            Assert.Equal(110.0, bullish.BodyTop);
            Assert.Equal(100.0, bullish.BodyBottom);
            Assert.Equal(10.0, bullish.BodyHeight);

            var bearish = new CandlestickItem(dt, open: 120, high: 125, low: 105, close: 110);
            Assert.False(bearish.IsBullish);
            Assert.Equal(120.0, bearish.BodyTop);
            Assert.Equal(110.0, bearish.BodyBottom);
            Assert.Equal(10.0, bearish.BodyHeight);

            var series = new CandlestickSeries();
            series.Add(bullish);
            series.Add(bearish);

            Assert.Equal(2, series.Count);
            Assert.Equal(95.0, series.MinPrice);
            Assert.Equal(125.0, series.MaxPrice);
        }

        [Fact]
        public void Heatmap_ScalarValues_MapsToGradientColors()
        {
            var heatmap = new HeatmapSeries(10, 10);
            heatmap.SetValue(0, 0, 0.0);   // Coldest
            heatmap.SetValue(9, 9, 100.0); // Hottest
            heatmap.SetValue(5, 5, 50.0);  // Middle

            Assert.Equal(0.0, heatmap.MinValue);
            Assert.Equal(100.0, heatmap.MaxValue);

            Color coldColor = heatmap.GetThermalColor(0.0);
            Assert.Equal(0, coldColor.R);
            Assert.Equal(255, coldColor.B); // Pure blue

            Color hotColor = heatmap.GetThermalColor(100.0);
            Assert.Equal(255, hotColor.R); // Pure red
            Assert.Equal(0, hotColor.B);
        }

        [Fact]
        public void GanttSeries_Tasks_OrganizesByTrackGroup()
        {
            var gantt = new GanttSeries();
            DateTime now = DateTime.UtcNow;

            gantt.AddTask(new GanttTask("T1", "Milling", "CNC-01", now, now.AddHours(2), "Running"));
            gantt.AddTask(new GanttTask("T2", "Tool Change", "CNC-01", now.AddHours(2), now.AddHours(2.5), "Setup"));
            gantt.AddTask(new GanttTask("T3", "Inspection", "QC-Station", now.AddHours(1), now.AddHours(3), "Running"));

            Assert.Equal(3, gantt.Count);
            var groups = gantt.GetDistinctTrackGroups();
            Assert.Equal(2, groups.Count);
            Assert.Contains("CNC-01", groups);
            Assert.Contains("QC-Station", groups);

            Assert.Equal(TimeSpan.FromHours(2), gantt.Tasks[0].Duration);
        }

        [Fact]
        public void ChartAxis_CoordinateTransformation_IsReversible()
        {
            var axis = new ChartAxis();
            axis.SetRange(10.0, 90.0);

            float screenStart = 50f;
            float screenLength = 500f;

            double testValue = 50.0;
            float screenPos = axis.ToScreen(testValue, screenStart, screenLength, invert: false);
            double backValue = axis.FromScreen(screenPos, screenStart, screenLength, invert: false);

            Assert.Equal(testValue, backValue, 4);

            var ticks = axis.GenerateMajorTicks(5);
            Assert.True(ticks.Count >= 2);
            Assert.True(ticks[0] >= 10.0);
        }
    }
}
