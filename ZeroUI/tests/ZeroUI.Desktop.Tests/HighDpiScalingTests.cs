using System;
using System.Drawing;
using System.Windows.Forms;
using Xunit;
using ZeroUI.Core.Common;
using ZeroUI.Core.Data;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Containers;
using ZeroUI.WinForms.DataGrid;
using ZeroUI.WinForms.Industrial;
using ZeroUI.WinForms.Navigation;
using ZeroUI.WinForms.Theme;
using Card = ZeroUI.WinForms.Containers.Card;

namespace ZeroUI.Desktop.Tests
{
    public class HighDpiScalingTests
    {
        [Fact]
        public void ZeroDpi_ScalingCalculations_ShouldBeAccurate()
        {
            Assert.Equal(26, ZeroDpi.Scale(26, 1.0f));
            Assert.Equal(39, ZeroDpi.Scale(26, 1.5f));
            Assert.Equal(52, ZeroDpi.Scale(26, 2.0f));
            Assert.Equal(56, ZeroDpi.Scale(28, 2.0f));

            var size = ZeroDpi.Scale(new Size(100, 50), 2.0f);
            Assert.Equal(200, size.Width);
            Assert.Equal(100, size.Height);

            var rect = ZeroDpi.Scale(new Rectangle(10, 20, 100, 50), 2.0f);
            Assert.Equal(20, rect.X);
            Assert.Equal(40, rect.Y);
            Assert.Equal(200, rect.Width);
            Assert.Equal(100, rect.Height);

            var pad = ZeroDpi.Scale(new Padding(5, 10, 15, 20), 2.0f);
            Assert.Equal(10, pad.Left);
            Assert.Equal(20, pad.Top);
            Assert.Equal(30, pad.Right);
            Assert.Equal(40, pad.Bottom);
        }

        [Fact]
        public void GridControl_ApplyDpiScaling_ScalesRowAndHeaderMetrics()
        {
            StaTestRunner.Run(() =>
            {
                using var grid = new GridControl();
                Assert.Equal(26, grid.RowHeight);
                Assert.Equal(28, grid.HeaderHeight);

                grid.Columns.Add(new ZeroColumn("Code", "Code", 100));

                // Scale to 200% (e.g. 192 DPI 4K monitor)
                grid.ApplyDpiScaling(2.0f);

                Assert.Equal(52, grid.RowHeight);
                Assert.Equal(56, grid.HeaderHeight);
                Assert.Equal(200, grid.Columns[0].Width);
                Assert.Equal(2.0f, grid.DpiScale);
            });
        }

        [Fact]
        public void GridControl_DensityAdjustment_RespectsActiveDpiScale()
        {
            StaTestRunner.Run(() =>
            {
                using var grid = new GridControl();
                grid.ApplyDpiScaling(2.0f);

                // Compact density: 24 base * 2.0 = 48
                grid.Density = GridDensity.Compact;
                Assert.Equal(48, grid.RowHeight);

                // Loose density: 36 base * 2.0 = 72
                grid.Density = GridDensity.Loose;
                Assert.Equal(72, grid.RowHeight);
            });
        }

        [Fact]
        public void TabControlEx_ApplyDpiScaling_ScalesTabDimensions()
        {
            StaTestRunner.Run(() =>
            {
                using var tab = new TabControlEx
                {
                    Orientation = TabOrientation.Vertical,
                    TabWidth = 140,
                    TabHeight = 42
                };

                tab.ApplyDpiScaling(2.0f);

                Assert.Equal(280, tab.TabWidth);
                Assert.Equal(84, tab.TabHeight);
                Assert.Equal(2.0f, tab.DpiScale);
            });
        }

        [Fact]
        public void ToolbarControl_ApplyDpiScaling_ScalesHeightAndItemMetrics()
        {
            StaTestRunner.Run(() =>
            {
                using var toolbar = new ToolbarControl
                {
                    Height = 44,
                    ItemHeight = 32
                };

                toolbar.ApplyDpiScaling(2.0f);

                Assert.Equal(88, toolbar.Height);
                Assert.Equal(64, toolbar.ItemHeight);
                Assert.Equal(2.0f, toolbar.DpiScale);
            });
        }

        [Fact]
        public void GridSearchBar_ApplyDpiScaling_ScalesHeightAndControls()
        {
            StaTestRunner.Run(() =>
            {
                using var searchBar = new GridSearchBar();
                Assert.Equal(48, searchBar.Height);

                searchBar.ApplyDpiScaling(2.0f);

                Assert.Equal(96, searchBar.Height);
                Assert.Equal(2.0f, searchBar.DpiScale);
            });
        }

        [Fact]
        public void Card_ApplyDpiScaling_ScalesHeaderHeightAndBorderRadiusAndPadding()
        {
            StaTestRunner.Run(() =>
            {
                using var card = new Card();
                card.ApplyDpiScaling(2.0f);

                Assert.Equal(24, card.Padding.Left);
                Assert.Equal(24, card.Padding.Top);
                Assert.Equal(2.0f, card.DpiScale);
            });
        }

        [Fact]
        public void CommandButton_ApplyDpiScaling_ScalesSizeAndFont()
        {
            StaTestRunner.Run(() =>
            {
                using var btn = new CommandButton();
                Assert.Equal(160, btn.Width);
                Assert.Equal(48, btn.Height);

                btn.ApplyDpiScaling(2.0f);

                Assert.Equal(320, btn.Width);
                Assert.Equal(96, btn.Height);
                Assert.Equal(19f, btn.Font.Size); // 9.5 * 2.0 = 19
                Assert.Equal(2.0f, btn.DpiScale);
            });
        }

        [Fact]
        public void SetpointInput_ApplyDpiScaling_ScalesDimensions()
        {
            StaTestRunner.Run(() =>
            {
                using var sp = new SetpointInput();
                Assert.Equal(150, sp.Width);
                Assert.Equal(60, sp.Height);

                sp.ApplyDpiScaling(2.0f);

                Assert.Equal(300, sp.Width);
                Assert.Equal(120, sp.Height);
                Assert.Equal(2.0f, sp.DpiScale);
            });
        }

        [Fact]
        public void ScaleControlHierarchy_ScalesNestedControlsRecursively()
        {
            StaTestRunner.Run(() =>
            {
                using var form = new BaseForm();
                var card = new Card();
                var tab = new TabControlEx { Orientation = TabOrientation.Vertical, TabWidth = 140, TabHeight = 42 };
                var toolbar = new ToolbarControl { Height = 44, ItemHeight = 32 };
                var cmdBtn = new CommandButton();
                var sp = new SetpointInput();

                card.Controls.Add(cmdBtn);
                card.Controls.Add(sp);
                form.Controls.Add(card);
                form.Controls.Add(tab);
                form.Controls.Add(toolbar);

                ZeroDpi.ScaleControlHierarchy(form, 2.0f);

                Assert.Equal(280, tab.TabWidth);
                Assert.Equal(84, tab.TabHeight);
                Assert.Equal(88, toolbar.Height);
                Assert.Equal(64, toolbar.ItemHeight);
                Assert.Equal(320, cmdBtn.Width);
                Assert.Equal(96, cmdBtn.Height);
                Assert.Equal(300, sp.Width);
                Assert.Equal(120, sp.Height);
            });
        }

        [Fact]
        public void BaseForm_And_BaseUserControl_HaveDpiAutoScaleMode()
        {
            StaTestRunner.Run(() =>
            {
                using var form = new BaseForm();
                Assert.Equal(AutoScaleMode.Dpi, form.AutoScaleMode);
                Assert.Equal(new SizeF(96F, 96F), form.AutoScaleDimensions);

                using var uc = new BaseUserControl();
                Assert.Equal(AutoScaleMode.Dpi, uc.AutoScaleMode);
                Assert.Equal(new SizeF(96F, 96F), uc.AutoScaleDimensions);
            });
        }
    }
}
