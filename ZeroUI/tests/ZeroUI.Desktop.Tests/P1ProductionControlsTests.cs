using System;
using System.Drawing;
using System.Linq;
using Xunit;
using WinGauge = ZeroUI.WinForms.Charts.ZGaugeChart;
using WinGaugeZone = ZeroUI.WinForms.Charts.GaugeZone;
using WpfGauge = ZeroUI.Wpf.Charts.ZGaugeChart;
using WinBubble = ZeroUI.WinForms.Charts.ZBubbleChart;
using WinBubblePoint = ZeroUI.WinForms.Charts.BubblePoint;
using WpfBubble = ZeroUI.Wpf.Charts.ZBubbleChart;
using WpfBubblePoint = ZeroUI.Wpf.Charts.BubblePoint;
using WinPassword = ZeroUI.WinForms.Editors.ZPasswordBox;
using WinPasswordStrength = ZeroUI.WinForms.Editors.PasswordStrength;
using WpfPassword = ZeroUI.Wpf.Editors.ZPasswordBox;
using WpfPasswordStrength = ZeroUI.Wpf.Editors.PasswordStrength;
using WinVirtualGrid = ZeroUI.WinForms.Data.ZVirtualGrid;
using WinCellValueNeededEventArgs = ZeroUI.WinForms.Data.CellValueNeededEventArgs;
using WpfVirtualGrid = ZeroUI.Wpf.Data.ZVirtualGrid;
using WpfCellValueNeededEventArgs = ZeroUI.Wpf.Data.CellValueNeededEventArgs;
using WinCron = ZeroUI.WinForms.Editors.ZCronEditor;
using WpfCron = ZeroUI.Wpf.Editors.ZCronEditor;

namespace ZeroUI.Desktop.Tests
{
    public class P1ProductionControlsTests
    {
        #region ZGaugeChart Tests

        [Fact]
        public void ZGaugeChart_WinForms_ValueClampingAndZones()
        {
            using var gauge = new WinGauge();
            gauge.Min = 0;
            gauge.Max = 200;

            bool eventFired = false;
            gauge.ValueChanged += (s, e) => eventFired = true;

            gauge.Value = 150;
            Assert.Equal(150, gauge.Value);
            Assert.True(eventFired);

            // Test clamping
            gauge.Value = 300;
            Assert.Equal(200, gauge.Value);

            gauge.Value = -50;
            Assert.Equal(0, gauge.Value);

            // Custom zones
            gauge.Zones.Clear();
            gauge.Zones.Add(new WinGaugeZone(0, 100, Color.Green, "Optimal"));
            gauge.Zones.Add(new WinGaugeZone(100, 200, Color.Red, "Warning"));
            Assert.Equal(2, gauge.Zones.Count);
        }

        [Fact]
        public void ZGaugeChart_Wpf_DependencyPropertiesOnStaThread()
        {
            StaTestRunner.Run(() =>
            {
                var gauge = new WpfGauge();
                gauge.Min = 10;
                gauge.Max = 500;
                gauge.Value = 250;
                gauge.Unit = "PSI";
                gauge.Title = "Boiler Pressure";

                Assert.Equal(10, gauge.Min);
                Assert.Equal(500, gauge.Max);
                Assert.Equal(250, gauge.Value);
                Assert.Equal("PSI", gauge.Unit);
                Assert.Equal("Boiler Pressure", gauge.Title);

                gauge.Measure(new System.Windows.Size(200, 200));
                gauge.Arrange(new System.Windows.Rect(0, 0, 200, 200));
            });
        }

        #endregion

        #region ZBubbleChart Tests

        [Fact]
        public void ZBubbleChart_WinForms_DataPointManagement()
        {
            using var chart = new WinBubble();
            chart.DataSource.Clear();

            chart.DataSource.Add(new WinBubblePoint(10, 20, 5, "Alpha"));
            chart.DataSource.Add(new WinBubblePoint(30, 40, 15, "Beta"));
            chart.DataSource.Add(new WinBubblePoint(50, 80, 25, "Gamma"));

            Assert.Equal(3, chart.DataSource.Count);
            Assert.Equal("Alpha", chart.DataSource[0].Label);
            Assert.Equal(10, chart.DataSource[0].X);
            Assert.Equal(20, chart.DataSource[0].Y);
            Assert.Equal(5, chart.DataSource[0].Size);
        }

        [Fact]
        public void ZBubbleChart_Wpf_MeasureAndArrangeOnStaThread()
        {
            StaTestRunner.Run(() =>
            {
                var chart = new WpfBubble();
                chart.DataSource.Clear();
                chart.DataSource.Add(new WpfBubblePoint(100, 200, 30, "Station 1"));
                chart.DataSource.Add(new WpfBubblePoint(300, 400, 50, "Station 2"));

                Assert.Equal(2, chart.DataSource.Count);
                chart.Measure(new System.Windows.Size(400, 300));
                chart.Arrange(new System.Windows.Rect(0, 0, 400, 300));
            });
        }

        #endregion

        #region ZPasswordBox Tests

        [Theory]
        [InlineData("", WinPasswordStrength.None)]
        [InlineData("123", WinPasswordStrength.Weak)]
        [InlineData("Secret9", WinPasswordStrength.Fair)]
        [InlineData("Secret9!", WinPasswordStrength.Good)]
        [InlineData("VeryStrongP@ssw0rd2026!", WinPasswordStrength.Strong)]
        public void ZPasswordBox_WinForms_StrengthScoring(string input, WinPasswordStrength expected)
        {
            using var box = new WinPassword();
            box.Password = input;
            Assert.Equal(expected, box.Strength);
        }

        [Fact]
        public void ZPasswordBox_WinForms_EventAndVisibilityToggle()
        {
            using var box = new WinPassword();
            bool eventFired = false;
            box.PasswordChanged += (s, e) => eventFired = true;

            box.Password = "TestPassword";
            Assert.True(eventFired);
            Assert.Equal("TestPassword", box.Password);

            Assert.True(box.ShowStrengthMeter);
            Assert.True(box.ShowToggleVisibility);
        }

        [Fact]
        public void ZPasswordBox_Wpf_StrengthScoringOnStaThread()
        {
            StaTestRunner.Run(() =>
            {
                var box = new WpfPassword();
                box.Password = "P@ssw0rd123Excellence!";
                Assert.Equal(WpfPasswordStrength.Strong, box.Strength);

                box.Password = "weak";
                Assert.Equal(WpfPasswordStrength.Weak, box.Strength);
            });
        }

        #endregion

        #region ZVirtualGrid Tests

        [Fact]
        public void ZVirtualGrid_WinForms_MultiMillionRowsDataStreaming()
        {
            using var grid = new WinVirtualGrid();
            grid.RowCount = 5_000_000;
            grid.ColumnCount = 4;

            Assert.Equal(5_000_000, grid.RowCount);
            Assert.Equal(4, grid.ColumnCount);
            Assert.Equal(4, grid.Columns.Count);

            grid.CellValueNeeded += (s, e) =>
            {
                e.Value = $"Row{e.RowIndex}_Col{e.ColumnIndex}";
            };

            var testArgs = new WinCellValueNeededEventArgs(1_234_567, 2);
            grid.RaiseCellValueNeeded(testArgs);
            Assert.Equal("Row1234567_Col2", testArgs.Value);

            grid.SelectedIndex = 100;
            Assert.Equal(100, grid.SelectedIndex);
        }

        [Fact]
        public void ZVirtualGrid_Wpf_MeasureAndVirtualEventOnStaThread()
        {
            StaTestRunner.Run(() =>
            {
                var grid = new WpfVirtualGrid();
                grid.RowCount = 10_000_000;
                grid.ColumnCount = 6;

                grid.CellValueNeeded += (s, e) =>
                {
                    e.Value = $"Cell[{e.RowIndex},{e.ColumnIndex}]";
                };

                var args = new WpfCellValueNeededEventArgs(9_999_999, 5);
                grid.RaiseCellValueNeeded(args);
                Assert.Equal("Cell[9999999,5]", args.Value);

                grid.Measure(new System.Windows.Size(600, 400));
                grid.Arrange(new System.Windows.Rect(0, 0, 600, 400));
            });
        }

        #endregion

        #region ZCronEditor Tests

        [Fact]
        public void ZCronEditor_WinForms_PresetsAndNaturalLanguagePreview()
        {
            using var editor = new WinCron();
            Assert.Equal("0 8 * * 1-5", editor.CronExpression);

            bool eventFired = false;
            editor.ExpressionChanged += (s, e) => eventFired = true;

            editor.CronExpression = "*/15 * * * *";
            Assert.True(eventFired);
            Assert.Equal("*/15 * * * *", editor.CronExpression);

            string desc = WinCron.DescribeCron("*/15 * * * *");
            Assert.Contains("15 minutes", desc);

            string dailyDesc = WinCron.DescribeCron("0 8 * * *");
            Assert.Contains("08:00 AM", dailyDesc);
        }

        [Fact]
        public void ZCronEditor_Wpf_ExpressionChangedOnStaThread()
        {
            StaTestRunner.Run(() =>
            {
                var editor = new WpfCron();
                Assert.Equal("0 8 * * 1-5", editor.CronExpression);

                bool eventFired = false;
                editor.ExpressionChanged += (s, e) => eventFired = true;

                editor.CronExpression = "0 0 * * 0";
                Assert.True(eventFired);
                Assert.Equal("0 0 * * 0", editor.CronExpression);

                editor.Measure(new System.Windows.Size(460, 160));
                editor.Arrange(new System.Windows.Rect(0, 0, 460, 160));
            });
        }

        #endregion
    }
}
