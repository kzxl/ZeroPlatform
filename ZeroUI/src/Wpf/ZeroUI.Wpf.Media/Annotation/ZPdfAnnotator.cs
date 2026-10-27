using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ZeroUI.Wpf.Theme;
using ZeroUI.Core.Media;

namespace ZeroUI.Wpf.Media
{
    public class ZPdfAnnotator : FrameworkElement
    {
        public string? DocumentPath { get; set; }
        public List<PdfAnnotation> Annotations { get; set; } = new List<PdfAnnotation>();
        public PdfAnnotationMode AnnotationMode { get; set; }

        public event EventHandler? AnnotationAdded;

        public ZPdfAnnotator()
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

    [Obsolete("PdfAnnotator is deprecated and will be removed in 5 release cycles. Please migrate to ZPdfAnnotator instead.")]
    public class PdfAnnotator : ZPdfAnnotator { }

    [Obsolete("ZeroPdfAnnotator is deprecated. Please use ZPdfAnnotator instead.")]
    public class ZeroPdfAnnotator : ZPdfAnnotator { }
}
