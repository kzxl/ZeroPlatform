using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Industrial.Gis
{
    /// <summary>
    /// ZRouteTracker control for Gis.
    /// </summary>
    public class ZRouteTracker : FrameworkElement
    {
        public System.Collections.Generic.List<ZeroUI.Core.Gis.GeoPoint> RoutePoints { get; set; } public ZeroUI.Core.Gis.GeoPoint CurrentPosition { get; set; } public bool ShowTrail { get; set; } public System.Windows.Media.Color TrailColor { get; set; } public string VehicleGlyph { get; set; } public event EventHandler PositionUpdated;

        public ZRouteTracker()
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
            
            var ft = new FormattedText("ZRouteTracker rendering", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 12, Brushes.Black, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            drawingContext.DrawText(ft, new Point(10, 10));
        }
    }

    [Obsolete("Use ZRouteTracker instead.")]
    public class ZeroRouteTracker : ZRouteTracker { }
}
