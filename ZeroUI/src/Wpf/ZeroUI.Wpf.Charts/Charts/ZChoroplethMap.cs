using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Charts
{
    public class ZChoroplethMap : FrameworkElement
    {
        public object Regions { get; set; } public object ColorScale { get; set; } public bool ShowLegend { get; set; }
        
        public ZChoroplethMap()
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

    [Obsolete("ZeroChoroplethMap is deprecated and will be removed in 5 release cycles. Please migrate to ZChoroplethMap instead.")]
    public class ZeroChoroplethMap : ZChoroplethMap
    {
    }
}
