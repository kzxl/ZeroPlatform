using System;
using System.Drawing;
using System.Windows.Forms;
using Xunit;
using ZeroUI.Core.Analytics;
using ZeroUI.WinForms.Charts;

namespace ZeroUI.Desktop.Tests
{
    public class ExpandedChartsWinFormsTests
    {
        [Fact]
        public void HistogramChart_WinForms_RendersIntoBitmapWithoutCrashing()
        {
            using (var chart = new HistogramChart())
            {
                chart.Size = new Size(500, 300);
                chart.SetData(new double[] { 12, 15, 18, 22, 25, 27, 30, 31, 35, 40, 42, 45, 50, 52, 60 });
                chart.BinCount = 5;
                chart.ShowNormalCurve = true;

                using (var bmp = new Bitmap(chart.Width, chart.Height))
                using (var g = Graphics.FromImage(bmp))
                {
                    chart.InvokePaint(g, new Rectangle(0, 0, chart.Width, chart.Height));
                }

                Assert.Equal(15, chart.Data.Count);
            }
        }

        [Fact]
        public void ScatterChart_WinForms_RendersPointsAndRegressionWithoutCrashing()
        {
            using (var chart = new ScatterChart())
            {
                chart.Size = new Size(550, 350);
                var s = new ScatterSeries("Alpha");
                s.Points.Add(new ScatterDataPoint(10, 20, 15));
                s.Points.Add(new ScatterDataPoint(25, 45, 20));
                s.Points.Add(new ScatterDataPoint(40, 60, 25));
                s.Points.Add(new ScatterDataPoint(60, 95, 30));
                chart.Series.Add(s);
                chart.ShowRegressionLine = true;

                using (var bmp = new Bitmap(chart.Width, chart.Height))
                using (var g = Graphics.FromImage(bmp))
                {
                    chart.InvokePaint(g, new Rectangle(0, 0, chart.Width, chart.Height));
                }

                Assert.Single(chart.Series);
                Assert.Equal(4, chart.Series[0].Points.Count);
            }
        }

        [Fact]
        public void SunburstChart_WinForms_RendersHierarchyWithoutCrashing()
        {
            using (var chart = new SunburstChart())
            {
                chart.Size = new Size(450, 450);
                var root = new SunburstNode("Global", 0);
                var na = new SunburstNode("North America", 300);
                var eu = new SunburstNode("Europe", 200);
                na.Children.Add(new SunburstNode("USA", 250));
                na.Children.Add(new SunburstNode("Canada", 50));
                root.Children.Add(na);
                root.Children.Add(eu);

                chart.RootNode = root;

                using (var bmp = new Bitmap(chart.Width, chart.Height))
                using (var g = Graphics.FromImage(bmp))
                {
                    chart.InvokePaint(g, new Rectangle(0, 0, chart.Width, chart.Height));
                }

                Assert.NotNull(chart.RootNode);
                Assert.Equal(500, chart.RootNode!.GetEffectiveValue());
            }
        }

        [Fact]
        public void LollipopChart_WinForms_RendersHorizontalAndVerticalWithoutCrashing()
        {
            using (var chart = new LollipopChart())
            {
                chart.Size = new Size(500, 320);
                chart.Items.Add(new LollipopItem("Product A", 45));
                chart.Items.Add(new LollipopItem("Product B", 78));
                chart.Items.Add(new LollipopItem("Product C", 92));

                // 1. Horizontal
                chart.Orientation = LollipopOrientation.Horizontal;
                using (var bmp = new Bitmap(chart.Width, chart.Height))
                using (var g = Graphics.FromImage(bmp))
                {
                    chart.InvokePaint(g, new Rectangle(0, 0, chart.Width, chart.Height));
                }

                // 2. Vertical
                chart.Orientation = LollipopOrientation.Vertical;
                using (var bmp = new Bitmap(chart.Width, chart.Height))
                using (var g = Graphics.FromImage(bmp))
                {
                    chart.InvokePaint(g, new Rectangle(0, 0, chart.Width, chart.Height));
                }

                Assert.Equal(3, chart.Items.Count);
            }
        }
    }
}
