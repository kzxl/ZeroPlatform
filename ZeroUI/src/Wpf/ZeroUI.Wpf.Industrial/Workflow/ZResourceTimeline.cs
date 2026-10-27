using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ZeroUI.Wpf.Theme;
using ZeroUI.Core.Workflow;

namespace ZeroUI.Wpf.Workflow
{
    public class ZResourceTimeline : FrameworkElement
    {
        public List<Resource> Resources { get; set; } = new List<Resource>();
        public TimeSpan TimeRange { get; set; }

        public event EventHandler? AllocationClicked;

        public ZResourceTimeline()
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

    [Obsolete("ResourceTimeline is deprecated and will be removed in 5 release cycles. Please migrate to ZResourceTimeline instead.")]
    public class ResourceTimeline : ZResourceTimeline { }

    [Obsolete("ZeroResourceTimeline is deprecated. Please use ZResourceTimeline instead.")]
    public class ZeroResourceTimeline : ZResourceTimeline { }
}
