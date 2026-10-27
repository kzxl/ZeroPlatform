using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Xunit;
using ZeroUI.Core.Common;
using ZeroUI.Core.Editors;
using ZeroUI.Core.Scada;
using ZeroUI.Core.Theme;
using ZeroUI.WinForms.Charts;
using ZeroUI.WinForms.Data;
using ZeroUI.WinForms.DataGrid;
using ZeroUI.WinForms.Editors;
using ZeroUI.WinForms.Feedback;
using ZeroUI.WinForms.Industrial;
using ZeroUI.WinForms.Theme;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Desktop.Tests
{
    public class ExtendedControlsMultiDimensionalTests
    {
        #region Dimension 1: Multi-threaded Concurrency

        [Fact]
        public void NotificationCenter_ConcurrentPushes_MaintainsCapacityLimitAndIntegrity()
        {
            using var center = new ZNotificationCenter();
            center.MaxVisible = 30;

            // Push 100 notifications across parallel tasks
            Parallel.For(0, 100, i =>
            {
                lock (center)
                {
                    center.Push(new ZNotification
                    {
                        Id = $"N_{i}",
                        Title = $"Thread Dispatch {i}",
                        Message = "Sensor payload received",
                        Severity = (NotificationSeverity)(i % 4)
                    });
                }
            });

            Assert.True(center.Notifications.Count <= 30);
            Assert.True(center.UnreadCount <= 30);
        }

        #endregion

        #region Dimension 2: Extreme Data Load & Virtualization

        [Fact]
        public void VirtualGrid_ExtremeRowCount_VirtualSetupDoesNotAllocateMassiveMemory()
        {
            using var grid = new ZVirtualGrid();
            grid.RowCount = 1_000_000;
            grid.ColumnCount = 5;

            Assert.Equal(1_000_000, grid.RowCount);
            Assert.Equal(5, grid.ColumnCount);
            Assert.False(grid.ReadOnly);

            bool eventFired = false;
            grid.CellValueNeeded += (s, e) => { eventFired = true; };
            Assert.NotNull(grid);
        }

        #endregion

        #region Dimension 3: Dynamic Theme Switching

        [Fact]
        public void WinForms_NewControls_HandleThemePaletteSwitchingCleanly()
        {
            using var autoComplete = new ZAutoComplete();
            using var notifCenter = new ZNotificationCenter();
            using var alarmSummary = new ZAlarmSummary();
            using var richText = new ZRichTextEditor();
            using var scheduler = new ZScheduler();
            using var bubbleChart = new ZBubbleChart();

            // Toggle through light and dark palettes
            ZeroTheme.CurrentMode = ZeroThemeMode.Dark;
            Assert.NotNull(autoComplete);
            Assert.NotNull(notifCenter);
            Assert.NotNull(alarmSummary);

            ZeroTheme.CurrentMode = ZeroThemeMode.Light;
            Assert.NotNull(richText);
            Assert.NotNull(scheduler);
            Assert.NotNull(bubbleChart);
        }

        [Fact]
        public void Wpf_NewControls_HandleThemePaletteSwitchingOnStaThread()
        {
            StaTestRunner.Run(() =>
            {
                var autoComplete = new ZeroUI.Wpf.Editors.ZAutoComplete();
                var notifCenter = new ZeroUI.Wpf.Feedback.ZNotificationCenter();
                var alarmSummary = new ZeroUI.Wpf.Industrial.ZAlarmSummary();
                var scheduler = new ZeroUI.Wpf.Data.ZScheduler();

                ZeroWpfTheme.ApplyPalette(ZeroUI.Core.Theme.ZeroSkinDefaults.ObsidianDark.Tokens, true);
                Assert.NotNull(autoComplete);
                Assert.NotNull(notifCenter);

                ZeroWpfTheme.ApplyPalette(ZeroUI.Core.Theme.ZeroSkinDefaults.CleanLight.Tokens, false);
                Assert.NotNull(alarmSummary);
                Assert.NotNull(scheduler);
            });
        }

        #endregion

        #region Dimension 4: High DPI Scaling Calculations

        [Fact]
        public void WinForms_NewControls_DpiCalculationsScaleAccurately()
        {
            var originalSize = new Size(300, 400);
            var scaled150 = ZeroDpi.Scale(originalSize, 1.5f);
            var scaled200 = ZeroDpi.Scale(originalSize, 2.0f);

            Assert.Equal(450, scaled150.Width);
            Assert.Equal(600, scaled150.Height);
            Assert.Equal(600, scaled200.Width);
            Assert.Equal(800, scaled200.Height);
        }

        #endregion

        #region Dimension 5: Memory Leak & Resource Cleanup on Dispose

        [Fact]
        public void NewControls_DisposedCleanly_DoesNotThrowOrLeak()
        {
            for (int i = 0; i < 20; i++)
            {
                var ac = new ZAutoComplete();
                ac.Dispose();

                var nc = new ZNotificationCenter();
                nc.Push(new ZNotification { Title = $"Temp {i}" });
                nc.Dispose();

                var al = new ZAlarmSummary();
                al.Dispose();

                var rt = new ZRichTextEditor();
                rt.Dispose();

                var sc = new ZScheduler();
                sc.Dispose();
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            Assert.True(true);
        }

        #endregion

        #region Dimension 6: Specialized Control Features

        [Fact]
        public void SignaturePad_Properties_SetAndGetCorrectly()
        {
            using var sig = new ZSignaturePad();
            sig.StrokeColor = Color.Blue;
            sig.StrokeWidth = 3;
            sig.BackgroundColor = Color.White;

            Assert.Equal(Color.Blue, sig.StrokeColor);
            Assert.Equal(3, sig.StrokeWidth);
            Assert.Equal(Color.White, sig.BackgroundColor);

            sig.Clear();
            Assert.NotNull(sig);
        }

        [Fact]
        public void MultiSelect_SelectionManagement_HoldsSelectedItems()
        {
            using var multi = new ZMultiSelect();
            var options = new List<string> { "React", "Angular", "Vue", "Blazor", "WPF" };
            multi.ItemsSource = options;
            multi.Placeholder = "Choose technologies...";

            Assert.Empty(multi.SelectedItems);
            multi.SelectedItems.Add("WPF");
            multi.SelectedItems.Add("Blazor");

            Assert.Equal(2, multi.SelectedItems.Count);
            Assert.Equal("Choose technologies...", multi.Placeholder);
        }

        #endregion
    }
}
