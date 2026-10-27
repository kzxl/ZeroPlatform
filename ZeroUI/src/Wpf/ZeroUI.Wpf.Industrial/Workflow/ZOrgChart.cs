using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ZeroUI.Wpf.Theme;
using ZeroUI.Core.Workflow;

namespace ZeroUI.Wpf.Workflow
{
    public class ZOrgChart : FrameworkElement
    {
        public OrgNode? RootNode { get; set; }
        public OrgChartOrientation Orientation { get; set; }
        public int NodeSpacing { get; set; }

        public event EventHandler? NodeClicked;

        public ZOrgChart()
        {
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ZeroWpfTheme.ThemeChanged += OnThemeChanged;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            ZeroWpfTheme.ThemeChanged -= OnThemeChanged;
        }

        private void OnThemeChanged() => InvalidateVisual();

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            // drawingContext.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight));
        }

        
    }

    [Obsolete("OrgChart is deprecated and will be removed in 5 release cycles. Please migrate to ZOrgChart instead.")]
    public class OrgChart : ZOrgChart { }

    [Obsolete("ZeroOrgChart is deprecated. Please use ZOrgChart instead.")]
    public class ZeroOrgChart : ZOrgChart { }
}
