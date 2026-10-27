using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Charts.AiMl
{
    /// <summary>
    /// ZModelMetrics control for AiMl.
    /// </summary>
    public class ZModelMetrics : FrameworkElement
    {
        public double Accuracy { get; set; } public double Precision { get; set; } public double Recall { get; set; } public double F1Score { get; set; } public double AucRoc { get; set; } public double LossValue { get; set; } public string ModelName { get; set; } public DateTime TrainDate { get; set; }

        public ZModelMetrics()
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
            
            var ft = new FormattedText("ZModelMetrics rendering", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 12, Brushes.Black, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            drawingContext.DrawText(ft, new Point(10, 10));
        }
    }

    [Obsolete("Use ZModelMetrics instead.")]
    public class ZeroModelMetrics : ZModelMetrics { }
}
