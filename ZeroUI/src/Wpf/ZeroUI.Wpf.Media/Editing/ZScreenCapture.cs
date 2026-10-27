using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ZeroUI.Wpf.Theme;
using ZeroUI.Core.Media;

namespace ZeroUI.Wpf.Media
{
    public class ZScreenCapture : FrameworkElement
    {
        public ScreenCaptureMode CaptureMode { get; set; }
        public object? LastCapture { get; set; }

        public event EventHandler? CaptureCompleted;

        public ZScreenCapture()
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

        public void StartCapture() { }
    }

    [Obsolete("ScreenCapture is deprecated and will be removed in 5 release cycles. Please migrate to ZScreenCapture instead.")]
    public class ScreenCapture : ZScreenCapture { }

    [Obsolete("ZeroScreenCapture is deprecated. Please use ZScreenCapture instead.")]
    public class ZeroScreenCapture : ZScreenCapture { }
}
