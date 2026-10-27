using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Xunit;
using ZeroCharts.Axis;
using ZeroCharts.Controls;
using ZeroCharts.DataModels;
using ZeroCharts.Interaction;

namespace ZeroCharts.Tests
{
    public class ExtendedChartTests
    {
        [Fact]
        public void BarSeries_ClusteredAndCategories_CalculatesMetricsAndMinMax()
        {
            var barSeries = new BarSeries("Production_Output", Color.LimeGreen);
            barSeries.Add("Station_A", 150.0);
            barSeries.Add("Station_B", -20.0);
            barSeries.Add("Station_C", 340.0);

            Assert.Equal(3, barSeries.Count);
            Assert.Equal(-20.0, barSeries.MinValue);
            Assert.Equal(340.0, barSeries.MaxValue);

            var itemB = barSeries.FindByCategory("Station_B");
            Assert.NotNull(itemB);
            Assert.Equal(-20.0, itemB!.Value);

            // Test Category Axis Mapping
            var catAxis = new CategoryAxis();
            catAxis.SetCategories(new[] { "Station_A", "Station_B", "Station_C" });
            Assert.Equal(3, catAxis.Count);

            float screenStart = 100f;
            float screenLength = 600f;
            float slotW = catAxis.GetSlotWidth(screenLength);
            Assert.Equal(200f, slotW);

            float center0 = catAxis.GetSlotCenter(0, screenStart, screenLength);
            Assert.Equal(200f, center0); // 100 + 0.5 * 200 = 200

            int catAtScreen = catAxis.GetCategoryAtScreen(center0, screenStart, screenLength);
            Assert.Equal(0, catAtScreen);
        }

        [Fact]
        public void PieSeries_SlicePercentagesAndHitTest_ResolvesCorrectly()
        {
            var pie = new PieSeries("Defect_Distribution");
            pie.HoleRadiusRatio = 0.5f; // Doughnut
            pie.AddSlice("Scratch", 50.0, Color.Red);
            pie.AddSlice("Dent", 30.0, Color.Orange);
            pie.AddSlice("Burr", 20.0, Color.Yellow);

            Assert.Equal(3, pie.Count);
            Assert.Equal(100.0, pie.TotalValue);

            Assert.Equal(50.0, pie.Slices[0].Percentage);
            Assert.Equal(180.0f, pie.Slices[0].SweepAngle);

            Assert.Equal(30.0, pie.Slices[1].Percentage);
            Assert.Equal(108.0f, pie.Slices[1].SweepAngle);

            Assert.Equal(20.0, pie.Slices[2].Percentage);
            Assert.Equal(72.0f, pie.Slices[2].SweepAngle);

            // Hit-testing: center at (200, 200), radius = 100
            // Doughnut hole is r < 50
            // Point at (200, 200) inside hole -> returns null
            var hitHole = pie.HitTest(200f, 200f, 200f, 200f, 100f);
            Assert.Null(hitHole);

            // Test point inside first slice (starts at -90 / 270 deg, sweeps 180 deg)
            // Point at (200 + 75, 200) -> angle 0 deg, which falls inside [-90..+90]
            var hitSlice = pie.HitTest(275f, 200f, 200f, 200f, 100f);
            Assert.NotNull(hitSlice);
            Assert.Equal("Scratch", hitSlice!.Label);
        }

        [Fact]
        public void AreaSeries_LttbAndBounds_MaintainsEnvelope()
        {
            var area = new AreaSeries("Sensor_Envelope", Color.Cyan, Color.DarkBlue);
            area.Baseline = 0.0;

            for (int i = 0; i < 500; i++)
            {
                area.Add(i, 20.0 + Math.Sin(i * 0.1) * 15.0);
            }

            Assert.Equal(500, area.Count);
            Assert.Equal(0.0, area.MinX);
            Assert.Equal(499.0, area.MaxX);
            Assert.True(area.MinY >= 5.0);
            Assert.True(area.MaxY <= 35.0);

            var downsampled = area.DownsampleLttb(50);
            Assert.Equal(50, downsampled.Count);
            Assert.Equal(area.Points[0].X, downsampled[0].X);
            Assert.Equal(area.Points[area.Count - 1].X, downsampled[49].X);
        }

        [Fact]
        public void ScatterSeries_MarkerShapesAndNearestSearch_FindsTargetPoint()
        {
            var scatter = new ScatterSeries("QC_Tolerance", Color.Magenta, MarkerShape.Diamond);
            scatter.Add(10.0, 20.0, radius: 6f, label: "P1");
            scatter.Add(50.0, 60.0, radius: 8f, label: "P2");
            scatter.Add(100.0, 120.0, radius: 5f, label: "P3");

            Assert.Equal(3, scatter.Count);
            Assert.Equal(10.0, scatter.MinX);
            Assert.Equal(100.0, scatter.MaxX);

            // Nearest point search
            var nearest = scatter.FindNearest(12.0, 18.0, maxDistanceSq: 25.0);
            Assert.NotNull(nearest);
            Assert.Equal("P1", nearest!.Label);

            // Outside search radius
            var notFound = scatter.FindNearest(80.0, 80.0, maxDistanceSq: 5.0);
            Assert.Null(notFound);
        }

        [Fact]
        public void RadarSeries_CartesianConversion_ComputesExpectedPolarRadius()
        {
            var radar = new RadarSeries("OEE_Metrics");
            radar.Add("Availability", 100.0, 100.0); // Index 0: Top (-PI/2)
            radar.Add("Performance", 50.0, 100.0);   // Index 1: Right (0)
            radar.Add("Quality", 100.0, 100.0);       // Index 2: Bottom (+PI/2)
            radar.Add("MTBF", 0.0, 100.0);           // Index 3: Left (PI)

            Assert.Equal(4, radar.Count);

            float cx = 200f;
            float cy = 200f;
            float radius = 100f;

            // Index 0: Top -> X = cx, Y = cy - radius
            var pt0 = radar.GetCartesianPoint(0, cx, cy, radius);
            Assert.Equal(cx, pt0.X, 2);
            Assert.Equal(cy - radius, pt0.Y, 2);

            // Index 1: Right with 50% ratio -> X = cx + radius * 0.5, Y = cy
            var pt1 = radar.GetCartesianPoint(1, cx, cy, radius);
            Assert.Equal(cx + 50f, pt1.X, 2);
            Assert.Equal(cy, pt1.Y, 2);

            // Index 3: 0% ratio -> Center (cx, cy)
            var pt3 = radar.GetCartesianPoint(3, cx, cy, radius);
            Assert.Equal(cx, pt3.X, 2);
            Assert.Equal(cy, pt3.Y, 2);
        }

        [Fact]
        public void ChartLegend_ItemClick_TogglesVisibility()
        {
            var legend = new ChartLegend { Placement = LegendPlacement.Top };
            bool seriesVisible = true;

            legend.AddEntry("Line_1", Color.Red, isVisible: true, v => seriesVisible = v);
            Assert.Single(legend.Entries);

            using var bmp = new Bitmap(500, 300);
            using var g = Graphics.FromImage(bmp);
            using var font = new Font("Segoe UI", 9f);

            var bounds = legend.MeasureAndLayout(g, font, new RectangleF(0, 0, 500, 300));
            Assert.True(bounds.Width > 0 && bounds.Height > 0);

            var entryBounds = legend.Entries[0].ClickBounds;
            Assert.True(entryBounds.Width > 0);

            // Click inside entry
            Point clickPt = new Point((int)(entryBounds.X + 2), (int)(entryBounds.Y + 2));
            bool handled = legend.HandleClick(clickPt);

            Assert.True(handled);
            Assert.False(legend.Entries[0].IsVisible);
            Assert.False(seriesVisible);

            // Click again to toggle back
            legend.HandleClick(clickPt);
            Assert.True(legend.Entries[0].IsVisible);
            Assert.True(seriesVisible);
        }

        [Fact]
        public void ZeroChartControl_ToBitmap_RendersAllSeriesHeadlessWithoutException()
        {
            using var chart = new ZeroChartControl();
            chart.Size = new Size(800, 600);

            // 1. Add Bar Series
            var bar = new BarSeries("Units_Sold", Color.Teal);
            bar.Add("Jan", 120);
            bar.Add("Feb", 180);
            bar.Add("Mar", 220);
            chart.AddSeries(bar);

            // 2. Add Area Series
            var area = new AreaSeries("Energy_Usage", Color.Orange);
            area.Add(0, 50);
            area.Add(1, 80);
            area.Add(2, 65);
            chart.AddSeries(area);

            // 3. Add Scatter Series
            var scatter = new ScatterSeries("Defects", Color.Red, MarkerShape.Square);
            scatter.Add(0.5, 45);
            scatter.Add(1.5, 95);
            chart.AddSeries(scatter);

            // Capture Headless Bitmap
            using var bmp = chart.ToBitmap(800, 600);
            Assert.NotNull(bmp);
            Assert.Equal(800, bmp.Width);
            Assert.Equal(600, bmp.Height);

            // Export image file verification
            string tempFile = Path.Combine(Path.GetTempPath(), $"zerochart_test_{Guid.NewGuid():N}.png");
            try
            {
                chart.SaveImage(tempFile, ImageFormat.Png, 800, 600);
                Assert.True(File.Exists(tempFile));
                var fileInfo = new FileInfo(tempFile);
                Assert.True(fileInfo.Length > 1000); // Image contains non-empty binary data
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }
    }
}
