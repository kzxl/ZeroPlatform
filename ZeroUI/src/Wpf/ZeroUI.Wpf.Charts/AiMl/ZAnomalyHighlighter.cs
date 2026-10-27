using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Charts.AiMl
{
    /// <summary>
    /// ZAnomalyHighlighter control for AiMl.
    /// </summary>
    public class ZAnomalyHighlighter : FrameworkElement
    {
        public System.Collections.Generic.List<ZeroUI.Core.AiMl.TimeValue> DataPoints { get; set; } public System.Collections.Generic.List<ZeroUI.Core.AiMl.AnomalyRange> Anomalies { get; set; } public double ThresholdLine { get; set; } public bool ShowScores { get; set; }

        public ZAnomalyHighlighter()
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
            
            var ft = new FormattedText("ZAnomalyHighlighter rendering", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 12, Brushes.Black, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            drawingContext.DrawText(ft, new Point(10, 10));
        }
    }

    [Obsolete("Use ZAnomalyHighlighter instead.")]
    public class ZeroAnomalyHighlighter : ZAnomalyHighlighter { }
}
