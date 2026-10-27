using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Industrial.Gis
{
    /// <summary>
    /// ZGeoFence control for Gis.
    /// </summary>
    public class ZGeoFence : FrameworkElement
    {
        public System.Collections.Generic.List<ZeroUI.Core.Gis.GeoPoint> Polygon { get; set; } public bool EditMode { get; set; } public System.Windows.Media.Color FillColor { get; set; } public System.Windows.Media.Color BorderColor { get; set; } public event EventHandler PolygonChanged;

        public ZGeoFence()
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
            
            var ft = new FormattedText("ZGeoFence rendering", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 12, Brushes.Black, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            drawingContext.DrawText(ft, new Point(10, 10));
        }
    }

    [Obsolete("Use ZGeoFence instead.")]
    public class ZeroGeoFence : ZGeoFence { }
}
