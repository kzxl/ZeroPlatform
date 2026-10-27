using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using ZeroUI.Core.Analytics;

namespace ZeroUI.Core.Tests.Analytics
{
    public class ExpandedChartEnginesTests
    {
        [Fact]
        public void HistogramEngine_AutoBinningAndMoments_CalculatedAccurately()
        {
            var data = new List<double> { 10, 20, 20, 30, 30, 30, 40, 40, 50 };

            var result = HistogramEngine.Compute(data, binCount: 4);

            Assert.Equal(9, result.TotalCount);
            Assert.Equal(10, result.Min);
            Assert.Equal(50, result.Max);
            Assert.Equal(4, result.Bins.Count);
            Assert.Equal(9, result.Bins.Sum(b => b.Count));
            Assert.Equal(30.0, Math.Round(result.Mean, 2));
            Assert.True(result.StandardDeviation > 0);
            Assert.True(result.MaxBinCount >= 3);
        }

        [Fact]
        public void HistogramEngine_GaussianCurve_GeneratedCorrectly()
        {
            var data = new List<double> { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 };

            var result = HistogramEngine.Compute(data, binCount: 5, curveSamplePoints: 50);

            Assert.Equal(50, result.GaussianCurve.Count);
            Assert.All(result.GaussianCurve, pt => Assert.True(pt.Density >= 0));
            Assert.All(result.GaussianCurve, pt => Assert.True(pt.ScaledCount >= 0));
        }

        [Fact]
        public void ScatterPlotEngine_LinearRegression_CalculatesSlopeAndRSquared()
        {
            // Perfect line: y = 2x + 5
            var points = new List<ScatterDataPoint>
            {
                new ScatterDataPoint(1, 7),
                new ScatterDataPoint(2, 9),
                new ScatterDataPoint(3, 11),
                new ScatterDataPoint(4, 13),
                new ScatterDataPoint(5, 15)
            };

            var reg = ScatterPlotEngine.ComputeLinearRegression(points);

            Assert.NotNull(reg);
            Assert.Equal(2.0, Math.Round(reg!.Slope, 4));
            Assert.Equal(5.0, Math.Round(reg.Intercept, 4));
            Assert.Equal(1.0, Math.Round(reg.RSquared, 4));
            Assert.Equal(7.0, reg.StartY);
            Assert.Equal(15.0, reg.EndY);
        }

        [Fact]
        public void ScatterPlotEngine_ComputeBounds_AddsPaddingAppropriately()
        {
            var s = new ScatterSeries("Test");
            s.Points.Add(new ScatterDataPoint(10, 20, 5));
            s.Points.Add(new ScatterDataPoint(90, 80, 25));

            var bounds = ScatterPlotEngine.ComputeBounds(new[] { s }, paddingRatio: 0.1);

            Assert.True(bounds.MinX < 10);
            Assert.True(bounds.MaxX > 90);
            Assert.True(bounds.MinY < 20);
            Assert.True(bounds.MaxY > 80);
            Assert.Equal(5, bounds.MinSize);
            Assert.Equal(25, bounds.MaxSize);
        }

        [Fact]
        public void SunburstLayoutEngine_HierarchicalPartitions_SumTo360Degrees()
        {
            var root = new SunburstNode("Root");
            var childA = new SunburstNode("A", 100);
            var childB = new SunburstNode("B", 300);
            root.Children.Add(childA);
            root.Children.Add(childB);

            // Child B has sub-children
            childB.Children.Add(new SunburstNode("B1", 150));
            childB.Children.Add(new SunburstNode("B2", 150));

            var sectors = SunburstLayoutEngine.ComputeLayout(root, maxRadius: 200, innerHoleRatio: 0.25, startAngleOffset: 0.0);

            Assert.NotEmpty(sectors);

            // Level 1 sectors should sum to 360 degrees
            var level1 = sectors.Where(s => s.Depth == 1).ToList();
            Assert.Equal(2, level1.Count);
            double totalLevel1Sweep = level1.Sum(s => s.SweepAngle);
            Assert.Equal(360.0, Math.Round(totalLevel1Sweep, 2));

            // Child A gets 25% = 90 deg, Child B gets 75% = 270 deg
            var secA = level1.First(s => s.Node.Name == "A");
            var secB = level1.First(s => s.Node.Name == "B");
            Assert.Equal(90.0, Math.Round(secA.SweepAngle, 2));
            Assert.Equal(270.0, Math.Round(secB.SweepAngle, 2));

            // Level 2 sectors under B should sum to 270 degrees
            var level2 = sectors.Where(s => s.Depth == 2).ToList();
            Assert.Equal(2, level2.Count);
            Assert.Equal(270.0, Math.Round(level2.Sum(s => s.SweepAngle), 2));

            // Polar hit test
            // Radius of Level 1 is between 50 and 125
            Assert.True(secA.Contains(0, 80, 0, 0));
        }

        [Fact]
        public void LollipopEngine_HorizontalAndVerticalLayout_CorrectCoordinates()
        {
            var items = new List<LollipopItem>
            {
                new LollipopItem("CatA", 20),
                new LollipopItem("CatB", 80)
            };

            // 1. Horizontal layout
            var horiz = LollipopEngine.ComputeLayout(items, plotX: 0, plotY: 0, plotWidth: 200, plotHeight: 100, orientation: LollipopOrientation.Horizontal);

            Assert.Equal(2, horiz.RenderItems.Count);
            Assert.Equal(0, horiz.BaselineValue);
            var item1 = horiz.RenderItems[0];
            var item2 = horiz.RenderItems[1];

            Assert.Equal(0.0, item1.StemStartX);
            Assert.True(item1.StemEndX > 0);
            Assert.True(item2.StemEndX > item1.StemEndX);
            Assert.True(item1.Contains(item1.DotCenterX, item1.DotCenterY));

            // 2. Vertical layout
            var vert = LollipopEngine.ComputeLayout(items, plotX: 0, plotY: 0, plotWidth: 100, plotHeight: 200, orientation: LollipopOrientation.Vertical);

            Assert.Equal(2, vert.RenderItems.Count);
            var vItem1 = vert.RenderItems[0];
            var vItem2 = vert.RenderItems[1];

            // In vertical layout, CatB (value 80) is higher up (smaller Y) than CatA (value 20)
            Assert.True(vItem2.DotCenterY < vItem1.DotCenterY);
        }
    }
}
