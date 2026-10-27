using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ZeroUI.Wpf.Theme;


namespace ZeroUI.Wpf.Media
{
    public class ZLiveCamera : FrameworkElement
    {
        public string? StreamUrl { get; set; }
        public bool IsPlaying { get; set; }
        public bool ShowControls { get; set; }

        

        public ZLiveCamera()
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

        public void Play() { }
        public void Pause() { }
        public void TakeSnapshot() { }
    }

    [Obsolete("LiveCamera is deprecated and will be removed in 5 release cycles. Please migrate to ZLiveCamera instead.")]
    public class LiveCamera : ZLiveCamera { }

    [Obsolete("ZeroLiveCamera is deprecated. Please use ZLiveCamera instead.")]
    public class ZeroLiveCamera : ZLiveCamera { }
}
