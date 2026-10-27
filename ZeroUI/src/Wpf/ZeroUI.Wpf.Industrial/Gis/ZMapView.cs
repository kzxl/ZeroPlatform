using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Industrial.Gis
{
    /// <summary>
    /// ZMapView control for Gis.
    /// </summary>
    public class ZMapView : FrameworkElement
    {
        public double CenterLat { get; set; } public double CenterLon { get; set; } public double ZoomLevel { get; set; } public System.Collections.Generic.List<ZeroUI.Core.Gis.MapMarker> Markers { get; set; } public bool ShowGrid { get; set; } public event EventHandler MarkerClicked; public event EventHandler MapClicked;

        public ZMapView()
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
            
            var ft = new FormattedText("ZMapView rendering", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 12, Brushes.Black, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            drawingContext.DrawText(ft, new Point(10, 10));
        }
    }

    [Obsolete("Use ZMapView instead.")]
    public class ZeroMapView : ZMapView { }
}
