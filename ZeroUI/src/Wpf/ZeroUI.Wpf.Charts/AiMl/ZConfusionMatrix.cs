using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Charts.AiMl
{
    /// <summary>
    /// ZConfusionMatrix control for AiMl.
    /// </summary>
    public class ZConfusionMatrix : FrameworkElement
    {
        public string[] Labels { get; set; } public int[,] Matrix { get; set; } public bool ShowPercentages { get; set; } public string ColorScale { get; set; } public string Title { get; set; }

        public ZConfusionMatrix()
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
            
            var ft = new FormattedText("ZConfusionMatrix rendering", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 12, Brushes.Black, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            drawingContext.DrawText(ft, new Point(10, 10));
        }
    }

    [Obsolete("Use ZConfusionMatrix instead.")]
    public class ZeroConfusionMatrix : ZConfusionMatrix { }
}
