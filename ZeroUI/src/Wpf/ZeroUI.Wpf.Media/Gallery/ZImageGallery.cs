using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ZeroUI.Wpf.Theme;
using ZeroUI.Core.Media;

namespace ZeroUI.Wpf.Media
{
    public class ZImageGallery : FrameworkElement
    {
        public List<GalleryImage> Images { get; set; } = new List<GalleryImage>();
        public int SelectedIndex { get; set; }
        public bool ShowThumbnailStrip { get; set; }
        public int SlideShowInterval { get; set; }

        public event EventHandler? ImageChanged;

        public ZImageGallery()
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

    [Obsolete("ImageGallery is deprecated and will be removed in 5 release cycles. Please migrate to ZImageGallery instead.")]
    public class ImageGallery : ZImageGallery { }

    [Obsolete("ZeroImageGallery is deprecated. Please use ZImageGallery instead.")]
    public class ZeroImageGallery : ZImageGallery { }
}
