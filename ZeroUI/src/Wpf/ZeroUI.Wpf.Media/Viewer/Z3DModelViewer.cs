using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ZeroUI.Wpf.Theme;


namespace ZeroUI.Wpf.Media
{
    public class Z3DModelViewer : FrameworkElement
    {
        public byte[]? ModelSource { get; set; }
        public double RotationX { get; set; }
        public double RotationY { get; set; }
        public double RotationZ { get; set; }
        public double Zoom { get; set; }
        public bool ShowWireframe { get; set; }
        public Color BackgroundColor { get; set; }

        

        public Z3DModelViewer()
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

    [Obsolete("ThreeDModelViewer is deprecated and will be removed in 5 release cycles. Please migrate to Z3DModelViewer instead.")]
    public class ThreeDModelViewer : Z3DModelViewer { }

    [Obsolete("Zero3DModelViewer is deprecated. Please use Z3DModelViewer instead.")]
    public class Zero3DModelViewer : Z3DModelViewer { }
}
