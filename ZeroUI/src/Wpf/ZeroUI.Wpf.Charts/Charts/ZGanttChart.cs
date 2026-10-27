using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Charts.Charts
{
    /// <summary>
    /// ZGanttChart control for Charts.
    /// </summary>
    public class ZGanttChart : FrameworkElement
    {
        public System.Collections.Generic.List<ZeroUI.Core.Charts.GanttTask> Tasks { get; set; } public bool ShowProgress { get; set; } public string TimeScale { get; set; } public bool ShowDependencies { get; set; }

        public ZGanttChart()
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
            
            var ft = new FormattedText("ZGanttChart rendering", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 12, Brushes.Black, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            drawingContext.DrawText(ft, new Point(10, 10));
        }
    }

    [Obsolete("Use ZGanttChart instead.")]
    public class ZeroGanttChart : ZGanttChart { }
}
