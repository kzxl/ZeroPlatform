using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using ZeroUI.Core.Analytics;

namespace ZeroUI.Core.Tests.Analytics
{
    public class CoreChartEnginesTests
    {
        [Fact]
        public void ParetoEngine_ComputesAccurateRanksAndCumulativePercent()
        {
            var items = new List<ParetoItem>
            {
                new ParetoItem("Minor Scratch", 50),
                new ParetoItem("Major Dent", 150),
                new ParetoItem("Missing Screw", 25),
                new ParetoItem("Solder Bridge", 75)
            };

            var result = ParetoEngine.Compute(items, cutoffPercentage: 80.0);

            Assert.Equal(300, result.TotalSum);
            Assert.Equal(4, result.Rows.Count);

            // Assert descending order
            Assert.Equal("Major Dent", result.Rows[0].Item.Category);
            Assert.Equal(150, result.Rows[0].Item.Value);
            Assert.Equal(50.0, result.Rows[0].Percentage);
            Assert.Equal(50.0, result.Rows[0].CumulativePercentage);

            Assert.Equal("Solder Bridge", result.Rows[1].Item.Category);
            Assert.Equal(75, result.Rows[1].Item.Value);
            Assert.Equal(25.0, result.Rows[1].Percentage);
            Assert.Equal(75.0, result.Rows[1].CumulativePercentage);

            Assert.Equal("Minor Scratch", result.Rows[2].Item.Category);
            Assert.Equal(50, result.Rows[2].Item.Value);
            Assert.Equal(16.67, Math.Round(result.Rows[2].Percentage, 2));
            Assert.Equal(91.67, Math.Round(result.Rows[2].CumulativePercentage, 2));

            // Vital few verification: Top 3 reach 91.67% >= 80%
            Assert.Equal(3, result.VitalFewCount);
            Assert.True(result.Rows[0].IsVitalFew);
            Assert.True(result.Rows[1].IsVitalFew);
            Assert.True(result.Rows[2].IsVitalFew);
            Assert.False(result.Rows[3].IsVitalFew);
        }

        [Fact]
        public void ParetoEngine_HandlesEmptyOrZeroValuesGracefully()
        {
            var emptyResult = ParetoEngine.Compute(null!);
            Assert.Equal(0, emptyResult.TotalSum);
            Assert.Empty(emptyResult.Rows);

            var zeroItems = new List<ParetoItem>
            {
                new ParetoItem("None", 0),
                new ParetoItem("Negative", -10)
            };
            var zeroResult = ParetoEngine.Compute(zeroItems);
            Assert.Equal(0, zeroResult.TotalSum);
            Assert.Empty(zeroResult.Rows);
        }

        [Fact]
        public void SankeyLayoutEngine_ComputesNormalizedTopologyAndRibbons()
        {
            var nodes = new List<SankeyNode>
            {
                new SankeyNode("Inlet 1", 0),
                new SankeyNode("Inlet 2", 0),
                new SankeyNode("Process Reactor", 1),
                new SankeyNode("Outlet Gas", 2),
                new SankeyNode("Outlet Liquid", 2)
            };

            var links = new List<SankeyLink>
            {
                new SankeyLink("Inlet 1", "Process Reactor", 60),
                new SankeyLink("Inlet 2", "Process Reactor", 40),
                new SankeyLink("Process Reactor", "Outlet Gas", 30),
                new SankeyLink("Process Reactor", "Outlet Liquid", 70)
            };

            var layout = SankeyLayoutEngine.Compute(nodes, links);

            Assert.Equal(3, layout.ColumnCount);
            Assert.Equal(5, layout.Nodes.Count);
            Assert.Equal(4, layout.Ribbons.Count);

            // Column X-coordinates are linearly stratified [0, 0.5, 1.0]
            var col0Nodes = layout.Nodes.Where(n => n.Node.Column == 0).ToList();
            var col1Nodes = layout.Nodes.Where(n => n.Node.Column == 1).ToList();
            var col2Nodes = layout.Nodes.Where(n => n.Node.Column == 2).ToList();

            foreach (var n in col0Nodes) Assert.Equal(0.0, n.NormalizedX);
            foreach (var n in col1Nodes) Assert.Equal(0.5, n.NormalizedX);
            foreach (var n in col2Nodes) Assert.Equal(1.0, n.NormalizedX);

            // Ribbon vertical segments must be well-formed (SourceY0 < SourceY1, TargetY0 < TargetY1)
            foreach (var r in layout.Ribbons)
            {
                Assert.True(r.SourceY1 > r.SourceY0);
                Assert.True(r.TargetY1 > r.TargetY0);
                Assert.True(r.SourceY0 >= 0.0 && r.SourceY1 <= 1.05);
                Assert.True(r.TargetY0 >= 0.0 && r.TargetY1 <= 1.05);
            }
        }

        [Fact]
        public void BulletBenchmarkEngine_ComputesNormalizedScalesAndDefaultRanges()
        {
            var item = new BulletItem("Profit Margin", "%", 22.5, 25.0, 30.0, 0.0)
            {
                ComparativeValue = 18.0
            };

            var metrics = BulletBenchmarkEngine.Compute(item);

            // 22.5 / 30.0 = 0.75
            Assert.Equal(0.75, Math.Round(metrics.NormalizedActual, 4));
            // 25.0 / 30.0 = 0.8333
            Assert.Equal(0.8333, Math.Round(metrics.NormalizedTarget, 4));
            // 18.0 / 30.0 = 0.6000
            Assert.NotNull(metrics.NormalizedComparative);
            Assert.Equal(0.60, Math.Round(metrics.NormalizedComparative.Value, 2));

            // Default ranges: Poor (0 - 60%), Satisfactory (60% - 85%), Good (85% - 100%)
            Assert.Equal(3, metrics.Ranges.Count);
            Assert.Equal("Poor", metrics.Ranges[0].Name);
            Assert.Equal(0.0, metrics.Ranges[0].NormalizedStart);
            Assert.Equal(0.60, Math.Round(metrics.Ranges[0].NormalizedEnd, 2));

            Assert.Equal("Satisfactory", metrics.Ranges[1].Name);
            Assert.Equal(0.60, Math.Round(metrics.Ranges[1].NormalizedStart, 2));
            Assert.Equal(0.85, Math.Round(metrics.Ranges[1].NormalizedEnd, 2));

            Assert.Equal("Good", metrics.Ranges[2].Name);
            Assert.Equal(0.85, Math.Round(metrics.Ranges[2].NormalizedStart, 2));
            Assert.Equal(1.00, Math.Round(metrics.Ranges[2].NormalizedEnd, 2));

            // % of target: (22.5 / 25.0) * 100 = 90.0%
            Assert.Equal(90.0, Math.Round(metrics.PercentOfTarget, 1));
        }

        [Fact]
        public void TreemapEngine_ComputesSquarifiedPartitionWithOffset()
        {
            var items = new List<TreemapItem>
            {
                new TreemapItem("Equities", 600, "Assets"),
                new TreemapItem("Bonds", 300, "Assets"),
                new TreemapItem("Cash", 100, "Liquidity")
            };

            double ox = 15;
            double oy = 25;
            double ow = 400;
            double oh = 300;

            var tiles = TreemapEngine.ComputeLayout(items, ox, oy, ow, oh, padding: 2.0);

            Assert.Equal(3, tiles.Count);
            foreach (var t in tiles)
            {
                Assert.True(t.X >= ox);
                Assert.True(t.Y >= oy);
                Assert.True(t.X + t.Width <= ox + ow + 1.0);
                Assert.True(t.Y + t.Height <= oy + oh + 1.0);
                Assert.True(t.Width > 0);
                Assert.True(t.Height > 0);
            }
        }
    }
}
