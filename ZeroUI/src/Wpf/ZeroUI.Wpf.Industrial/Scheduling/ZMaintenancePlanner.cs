using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Industrial.Scheduling
{
    /// <summary>
    /// ZMaintenancePlanner control for Scheduling.
    /// </summary>
    public class ZMaintenancePlanner : FrameworkElement
    {
        public System.Collections.Generic.List<ZeroUI.Core.Scheduling.MaintenanceTask> Tasks { get; set; } public DateTime CurrentDate { get; set; }

        public ZMaintenancePlanner()
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
            
            var ft = new FormattedText("ZMaintenancePlanner rendering", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 12, Brushes.Black, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            drawingContext.DrawText(ft, new Point(10, 10));
        }
    }

    [Obsolete("Use ZMaintenancePlanner instead.")]
    public class ZeroMaintenancePlanner : ZMaintenancePlanner { }
}
