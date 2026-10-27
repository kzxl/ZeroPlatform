using System;
using System.Windows;
using Xunit;
using ZeroUI.Wpf.Editors;
using ZeroUI.Wpf.Media;
using ZHistogramScope = ZeroUI.Wpf.Media.ZHistogramScope;
using ZeroUI.Wpf.DataGrid;

namespace ZeroUI.Desktop.Tests
{
    public class WpfEditorsTests
    {
        [Fact]
        public void HistogramScopeControl_DefaultState_ShouldHaveValidProperties()
        {
            StaTestRunner.Run(() =>
            {
                var scope = new ZHistogramScope();
                Assert.Equal(HistogramChannelMode.Rgb, scope.ChannelMode);
                Assert.Equal(HistogramScopeType.Histogram, scope.ScopeType);
                Assert.Null(scope.RedChannel);
                Assert.Null(scope.GreenChannel);
                Assert.Null(scope.BlueChannel);
                Assert.Null(scope.LumaChannel);
                Assert.Equal(0.0, scope.ShadowClipPercent);
                Assert.Equal(0.0, scope.HighlightClipPercent);
            });
        }

        [Fact]
        public void HistogramScopeControl_EmptyAndZeroBins_ShouldNotCrash()
        {
            StaTestRunner.Run(() =>
            {
                var scope = new ZHistogramScope();
                scope.RedChannel = Array.Empty<int>();
                scope.GreenChannel = new int[256]; // All zeros
                scope.BlueChannel = new int[256];
                scope.LumaChannel = new int[256];

                // Measure & Arrange
                scope.Measure(new Size(300, 150));
                scope.Arrange(new Rect(0, 0, 300, 150));

                // Switch modes
                scope.ChannelMode = HistogramChannelMode.Luma;
                Assert.Equal(HistogramChannelMode.Luma, scope.ChannelMode);

                scope.ChannelMode = HistogramChannelMode.RedOnly;
                Assert.Equal(HistogramChannelMode.RedOnly, scope.ChannelMode);

                scope.ChannelMode = HistogramChannelMode.Rgb;
                Assert.Equal(HistogramChannelMode.Rgb, scope.ChannelMode);
            });
        }

        [Fact]
        public void HistogramScopeControl_BoundaryClippingSpikes_ShouldHandleWithoutCrashing()
        {
            StaTestRunner.Run(() =>
            {
                var scope = new ZHistogramScope();

                // 21.5% shadow clipping at bin 0 (100,000 counts) and normal midtones (500 counts)
                var red = new int[256];
                red[0] = 100_000;
                for (int i = 1; i < 255; i++)
                {
                    red[i] = 500;
                }
                red[255] = 50_000; // Highlight clipping spike

                scope.RedChannel = red;
                scope.ShadowClipPercent = 21.5;
                scope.HighlightClipPercent = 8.2;

                scope.Measure(new Size(400, 200));
                scope.Arrange(new Rect(0, 0, 400, 200));

                Assert.Equal(21.5, scope.ShadowClipPercent);
                Assert.Equal(8.2, scope.HighlightClipPercent);
            });
        }

        [Fact]
        public void SegmentedControl_DefaultStateAndSelection_ShouldWork()
        {
            StaTestRunner.Run(() =>
            {
                var seg = new SegmentedControl();
                Assert.NotNull(seg.Items);
                Assert.True(seg.Items.Length > 0);
                Assert.Equal(0, seg.SelectedIndex);
                Assert.Equal(seg.Items[0], seg.SelectedItem);

                // Change selection
                seg.SelectedIndex = 2;
                Assert.Equal(2, seg.SelectedIndex);
                Assert.Equal(seg.Items[2], seg.SelectedItem);
            });
        }

        [Fact]
        public void SegmentedControl_CustomItemsAndEmptyArray_ShouldNotThrow()
        {
            StaTestRunner.Run(() =>
            {
                var seg = new SegmentedControl();

                // Custom items
                seg.Items = new[] { "Option A", "Option B", "Option C" };
                Assert.Equal(3, seg.Items.Length);

                seg.SelectedIndex = 1;
                Assert.Equal(1, seg.SelectedIndex);
                Assert.Equal("Option B", seg.SelectedItem);

                // Setting empty items should handle gracefully without crashing
                seg.Items = Array.Empty<string>();
                Assert.Empty(seg.Items);
                Assert.Equal(-1, seg.SelectedIndex);
                Assert.Null(seg.SelectedItem);
            });
        }

        [Fact]
        public void FilterControl_InitializationAndExpressionGeneration_ShouldWork()
        {
            StaTestRunner.Run(() =>
            {
                var filter = new FilterControl();
                Assert.NotNull(filter.RootGroup);
                Assert.True(filter.RootGroup.Children.Count > 0);

                string whereClause = filter.GetSqlWhere();
                Assert.False(string.IsNullOrWhiteSpace(whereClause));
                Assert.Contains("Status", whereClause);

                string display = filter.GetDisplayString();
                Assert.False(string.IsNullOrWhiteSpace(display));
                Assert.Contains("Status", display);
            });
        }

        [Fact]
        public void CollapsibleToolCard_DefaultState_And_Toggle_ShouldWork()
        {
            StaTestRunner.Run(() =>
            {
                var card = new ZeroUI.Wpf.Layout.CollapsibleToolCard
                {
                    HeaderGlyph = "⚙️",
                    Header = "FUSION STRATEGY",
                    Content = new System.Windows.Controls.Button { Content = "Test Button" }
                };

                Assert.True(card.IsExpanded);
                Assert.Equal(1, card.Elevation);
                Assert.Equal("⚙️", card.HeaderGlyph);
                Assert.Equal("FUSION STRATEGY", card.Header);

                bool expandedFired = false;
                bool collapsedFired = false;
                card.Expanded += (s, e) => expandedFired = true;
                card.Collapsed += (s, e) => collapsedFired = true;

                card.IsExpanded = false;
                Assert.False(card.IsExpanded);
                Assert.True(collapsedFired);

                card.IsExpanded = true;
                Assert.True(card.IsExpanded);
                Assert.True(expandedFired);

                card.Measure(new Size(360, 600));
                card.Arrange(new Rect(0, 0, 360, 600));
                Assert.True(card.ActualWidth >= 0);
            });
        }

        [Fact]
        public void ReticleLoupeControl_DefaultState_And_EditValue_ShouldWork()
        {
            StaTestRunner.Run(() =>
            {
                var loupe = new ZeroUI.Wpf.Overlays.ReticleLoupeControl();

                Assert.True(loupe.IsActive);
                Assert.Equal(90.0, loupe.Radius);
                Assert.Equal(15.0, loupe.ReticleRadius);
                Assert.True(loupe.ShowCrosshair);
                Assert.Equal("100% LOUPE", loupe.ModeBadgeText);

                // EditValue IZeroEditor integration
                loupe.EditValue = new Point(120, 240);
                Assert.Equal(new Point(120, 240), loupe.Center);
                Assert.True(loupe.IsModified);

                loupe.Reset();
                Assert.Equal(new Point(0, 0), loupe.Center);
                Assert.False(loupe.IsModified);

                loupe.Measure(new Size(800, 600));
                loupe.Arrange(new Rect(0, 0, 800, 600));
            });
        }
    }
}
