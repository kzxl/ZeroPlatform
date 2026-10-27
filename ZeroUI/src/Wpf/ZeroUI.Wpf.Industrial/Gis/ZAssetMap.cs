using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Industrial.Gis
{
    /// <summary>
    /// ZAssetMap control for Gis.
    /// </summary>
    public class ZAssetMap : FrameworkElement
    {
        public System.Windows.Media.ImageSource BackgroundImage { get; set; } public System.Collections.Generic.List<ZeroUI.Core.Gis.AssetPin> Assets { get; set; } public bool ShowLabels { get; set; } public bool ShowStatusColor { get; set; } public event EventHandler AssetClicked;

        public ZAssetMap()
        {
            Loaded += (s, e) => ZeroWpfTheme.ThemeChanged += OnThemeChanged;
            Unloaded += (s, e) => ZeroWpfTheme.ThemeChanged -= OnThemeChanged;
        }

        private void OnThemeChanged()
        {
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            var rect = new Rect(0, 0, ActualWidth > 0 ? ActualWidth : 100, ActualHeight > 0 ? ActualHeight : 100);
            drawingContext.DrawRectangle(Brushes.Transparent, null, rect);
            
            var ft = new FormattedText("ZAssetMap rendering", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 12, Brushes.Black, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            drawingContext.DrawText(ft, new Point(10, 10));
        }
    }

    [Obsolete("Use ZAssetMap instead.")]
    public class ZeroAssetMap : ZAssetMap { }
}
