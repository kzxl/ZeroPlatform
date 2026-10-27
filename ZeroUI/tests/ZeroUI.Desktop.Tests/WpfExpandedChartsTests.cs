using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xunit;
using ZeroUI.Core.Analytics;
using ZeroUI.Wpf.Charts;

namespace ZeroUI.Desktop.Tests
{
    public class WpfExpandedChartsTests
    {
        [Fact]
        public void Wpf_HistogramChart_InitializesAndRendersWithoutCrashing()
        {
            StaTestRunner.Run(() =>
            {
                var chart = new HistogramChart
                {
                    Width = 550,
                    Height = 350,
                    BinCount = 5,
                    ShowNormalCurve = true
                };

                chart.SetData(new double[] { 10, 15, 20, 25, 30, 35, 40, 45, 50, 55, 60 });
                Assert.Equal(11, chart.Data.Count);

                chart.Measure(new Size(550, 350));
                chart.Arrange(new Rect(0, 0, 550, 350));

                var rtb = new RenderTargetBitmap(550, 350, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(chart);
                Assert.Equal(550, rtb.PixelWidth);
            });
        }

        [Fact]
        public void Wpf_ScatterChart_InitializesAndRendersWithoutCrashing()
        {
            StaTestRunner.Run(() =>
            {
                var chart = new ScatterChart
                {
                    Width = 600,
                    Height = 400,
                    ShowRegressionLine = true
                };

                var series = new ScatterSeries("Alpha");
                series.Points.Add(new ScatterDataPoint(5, 10, 15));
                series.Points.Add(new ScatterDataPoint(15, 30, 20));
                series.Points.Add(new ScatterDataPoint(25, 50, 25));
                chart.Series.Add(series);

                chart.Measure(new Size(600, 400));
                chart.Arrange(new Rect(0, 0, 600, 400));

                var rtb = new RenderTargetBitmap(600, 400, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(chart);
                Assert.Equal(600, rtb.PixelWidth);
            });
        }

        [Fact]
        public void Wpf_SunburstChart_InitializesAndRendersWithoutCrashing()
        {
            StaTestRunner.Run(() =>
            {
                var chart = new SunburstChart
                {
                    Width = 450,
                    Height = 450
                };

                var root = new SunburstNode("Global", 0);
                var a = new SunburstNode("Segment A", 120);
                var b = new SunburstNode("Segment B", 80);
                root.Children.Add(a);
                root.Children.Add(b);
                chart.RootNode = root;

                chart.Measure(new Size(450, 450));
                chart.Arrange(new Rect(0, 0, 450, 450));

                var rtb = new RenderTargetBitmap(450, 450, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(chart);
                Assert.Equal(450, rtb.PixelWidth);
            });
        }

        [Fact]
        public void Wpf_LollipopChart_InitializesAndRendersWithoutCrashing()
        {
            StaTestRunner.Run(() =>
            {
                var chart = new LollipopChart
                {
                    Width = 520,
                    Height = 340,
                    Orientation = LollipopOrientation.Horizontal
                };

                chart.Items.Add(new LollipopItem("Metric A", 45));
                chart.Items.Add(new LollipopItem("Metric B", 85));
                chart.Items.Add(new LollipopItem("Metric C", 65));

                chart.Measure(new Size(520, 340));
                chart.Arrange(new Rect(0, 0, 520, 340));

                var rtb = new RenderTargetBitmap(520, 340, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(chart);
                Assert.Equal(520, rtb.PixelWidth);
            });
        }
    }
}
