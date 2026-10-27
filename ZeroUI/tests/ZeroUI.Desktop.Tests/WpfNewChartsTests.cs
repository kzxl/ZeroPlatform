using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xunit;
using ZeroUI.Core.Analytics;
using ZeroUI.Wpf.Charts;

namespace ZeroUI.Desktop.Tests
{
    public class WpfNewChartsTests
    {
        [Fact]
        public void Wpf_TreemapChart_InitializesAndRendersWithoutCrashing()
        {
            StaTestRunner.Run(() =>
            {
                var chart = new TreemapChart
                {
                    Width = 600,
                    Height = 400
                };

                chart.AddItem("Equities", 500, Colors.Blue, "Assets");
                chart.AddItem("Bonds", 300, Colors.Green, "Assets");
                chart.AddItem("Real Estate", 150, Colors.Orange, "Assets");
                chart.AddItem("Cash", 50, Colors.Gray, "Liquidity");

                Assert.Equal(4, chart.Items.Count);

                chart.Measure(new Size(600, 400));
                chart.Arrange(new Rect(0, 0, 600, 400));

                var rtb = new RenderTargetBitmap(600, 400, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(chart);
                Assert.True(rtb.PixelWidth == 600);
            });
        }

        [Fact]
        public void Wpf_SankeyChart_CalculatesFlowsAndRendersWithoutCrashing()
        {
            StaTestRunner.Run(() =>
            {
                var chart = new SankeyChart
                {
                    Width = 700,
                    Height = 450
                };

                chart.AddNode("Solar", Colors.Gold, 0);
                chart.AddNode("Wind", Colors.SkyBlue, 0);
                chart.AddNode("Grid Storage", Colors.DarkCyan, 1);
                chart.AddNode("Direct Use", Colors.LimeGreen, 1);
                chart.AddNode("Industry", Colors.SteelBlue, 2);

                chart.AddLink("Solar", "Grid Storage", 40);
                chart.AddLink("Solar", "Direct Use", 60);
                chart.AddLink("Wind", "Grid Storage", 50);
                chart.AddLink("Wind", "Direct Use", 30);
                chart.AddLink("Grid Storage", "Industry", 90);
                chart.AddLink("Direct Use", "Industry", 90);

                Assert.Equal(5, chart.Nodes.Count);
                Assert.Equal(6, chart.Links.Count);

                chart.Measure(new Size(700, 450));
                chart.Arrange(new Rect(0, 0, 700, 450));

                var rtb = new RenderTargetBitmap(700, 450, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(chart);
                Assert.True(rtb.PixelWidth == 700);
            });
        }

        [Fact]
        public void Wpf_BulletChart_RendersKPIScalesWithoutCrashing()
        {
            StaTestRunner.Run(() =>
            {
                var chart = new BulletChart
                {
                    Width = 600,
                    Height = 300
                };

                chart.LoadSampleData();
                Assert.NotEmpty(chart.Items);

                chart.Measure(new Size(600, 300));
                chart.Arrange(new Rect(0, 0, 600, 300));

                var rtb = new RenderTargetBitmap(600, 300, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(chart);
                Assert.True(rtb.PixelWidth == 600);
            });
        }

        [Fact]
        public void Wpf_ParetoChart_Renders8020CurvesWithoutCrashing()
        {
            StaTestRunner.Run(() =>
            {
                var chart = new ParetoChart
                {
                    Width = 650,
                    Height = 350,
                    CutoffPercentage = 80.0
                };

                chart.Items.Add(new ParetoItem("Defect A", 100));
                chart.Items.Add(new ParetoItem("Defect B", 60));
                chart.Items.Add(new ParetoItem("Defect C", 20));
                chart.Items.Add(new ParetoItem("Defect D", 20));

                Assert.Equal(4, chart.Items.Count);

                chart.Measure(new Size(650, 350));
                chart.Arrange(new Rect(0, 0, 650, 350));

                var rtb = new RenderTargetBitmap(650, 350, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(chart);
                Assert.True(rtb.PixelWidth == 650);
            });
        }
    }
}
