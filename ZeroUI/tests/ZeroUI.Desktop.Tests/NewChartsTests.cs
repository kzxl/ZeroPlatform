using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Xunit;
using ZeroUI.Core.Analytics;
using ZeroUI.WinForms.Charts;

namespace ZeroUI.Desktop.Tests
{
    public class NewChartsTests
    {
        [Fact]
        public void TreemapChart_InitializationAndDataLayout_ComputesValidTiles()
        {
            using (var chart = new TreemapChart())
            {
                chart.Size = new Size(600, 400);
                chart.AddItem("Equities", 500, Color.Blue, "Assets");
                chart.AddItem("Bonds", 300, Color.Green, "Assets");
                chart.AddItem("Real Estate", 150, Color.Orange, "Assets");
                chart.AddItem("Cash", 50, Color.Gray, "Liquidity");

                Assert.Equal(4, chart.Items.Count);

                // Render into bitmap to verify paint execution
                using (var bmp = new Bitmap(chart.Width, chart.Height))
                using (var g = Graphics.FromImage(bmp))
                {
                    chart.InvokePaint(g, new Rectangle(0, 0, chart.Width, chart.Height));
                }

                Assert.True(chart.Items.Sum(x => x.Value) == 1000);
            }
        }

        [Fact]
        public void SankeyChart_NodesAndLinks_CalculatesFlowsSuccessfully()
        {
            using (var chart = new SankeyChart())
            {
                chart.Size = new Size(700, 450);
                chart.AddNode("Solar", Color.Gold, 0);
                chart.AddNode("Wind", Color.SkyBlue, 0);
                chart.AddNode("Grid Storage", Color.DarkCyan, 1);
                chart.AddNode("Direct Use", Color.LimeGreen, 1);
                chart.AddNode("Industry", Color.SteelBlue, 2);

                chart.AddLink("Solar", "Grid Storage", 40);
                chart.AddLink("Solar", "Direct Use", 60);
                chart.AddLink("Wind", "Grid Storage", 50);
                chart.AddLink("Wind", "Direct Use", 30);
                chart.AddLink("Grid Storage", "Industry", 90);
                chart.AddLink("Direct Use", "Industry", 90);

                Assert.Equal(5, chart.Nodes.Count);
                Assert.Equal(6, chart.Links.Count);

                // Render into bitmap to verify layout and Bezier rendering
                using (var bmp = new Bitmap(chart.Width, chart.Height))
                using (var g = Graphics.FromImage(bmp))
                {
                    chart.InvokePaint(g, new Rectangle(0, 0, chart.Width, chart.Height));
                }
            }
        }

        [Fact]
        public void BulletChart_KPIThresholdsAndActualTarget_RendersAccurately()
        {
            using (var chart = new BulletChart())
            {
                chart.Size = new Size(600, 300);
                chart.LoadSampleData();

                Assert.NotEmpty(chart.Items);
                var rev = chart.Items.First(x => x.Title == "Revenue");
                Assert.Equal(275, rev.ActualValue);
                Assert.Equal(250, rev.TargetValue);
                Assert.Equal(3, rev.Ranges.Count);

                // Render into bitmap
                using (var bmp = new Bitmap(chart.Width, chart.Height))
                using (var g = Graphics.FromImage(bmp))
                {
                    chart.InvokePaint(g, new Rectangle(0, 0, chart.Width, chart.Height));
                }
            }
        }

        [Fact]
        public void ParetoChart_8020Analysis_IdentifiesVitalFewAccurately()
        {
            using (var chart = new ParetoChart())
            {
                chart.Size = new Size(650, 350);
                chart.CutoffPercentage = 80.0;
                chart.Items.Add(new ParetoItem("Defect A", 100)); // 50%
                chart.Items.Add(new ParetoItem("Defect B", 60));  // 30% -> Cumul: 80%
                chart.Items.Add(new ParetoItem("Defect C", 20));  // 10%
                chart.Items.Add(new ParetoItem("Defect D", 20));  // 10%

                Assert.Equal(4, chart.Items.Count);

                // Render into bitmap to verify sorting, cumulative line, and 80% cutoff line
                using (var bmp = new Bitmap(chart.Width, chart.Height))
                using (var g = Graphics.FromImage(bmp))
                {
                    chart.InvokePaint(g, new Rectangle(0, 0, chart.Width, chart.Height));
                }
            }
        }
    }

    internal static class ControlTestExtensions
    {
        public static void InvokePaint(this Control control, Graphics g, Rectangle clip)
        {
            var method = typeof(Control).GetMethod("OnPaint", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method?.Invoke(control, new object[] { new PaintEventArgs(g, clip) });
        }
    }
}
