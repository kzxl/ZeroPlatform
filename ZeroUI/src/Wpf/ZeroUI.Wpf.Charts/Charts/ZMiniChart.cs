using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Charts
{
    public class ZMiniChart : FrameworkElement
    {
        public double[] Values { get; set; } public string ChartType { get; set; } public bool HighlightMin { get; set; } public bool HighlightMax { get; set; }
        
        public ZMiniChart()
        {
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }
        private void OnLoaded(object sender, RoutedEventArgs e) => ZeroWpfTheme.ThemeChanged += OnThemeChanged;
        private void OnUnloaded(object sender, RoutedEventArgs e) => ZeroWpfTheme.ThemeChanged -= OnThemeChanged;
        private void OnThemeChanged() => InvalidateVisual();
        
        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            // Rendering logic
        }
    }

    [Obsolete("ZeroMiniChart is deprecated and will be removed in 5 release cycles. Please migrate to ZMiniChart instead.")]
    public class ZeroMiniChart : ZMiniChart
    {
    }
}
