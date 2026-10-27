using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ZeroUI.Core.Containers;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Common.Containers
{
    /// <summary>
    /// ZDiagramCanvas control.
    /// </summary>
    public class ZDiagramCanvas : FrameworkElement
    {
        public List<DiagramNode> Nodes { get; set; }
        public List<DiagramLink> Links { get; set; }
        public bool AllowDrag { get; set; }
        public bool ShowGrid { get; set; }

        public ZDiagramCanvas()
        {
            Loaded += (s, e) => ZeroWpfTheme.ThemeChanged += OnZeroThemeChanged;
            Unloaded += (s, e) => ZeroWpfTheme.ThemeChanged -= OnZeroThemeChanged;
        }

        private void OnZeroThemeChanged()
        {
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            var rect = new Rect(0, 0, ActualWidth > 0 ? ActualWidth : 100, ActualHeight > 0 ? ActualHeight : 100);
            drawingContext.DrawRectangle(Brushes.Transparent, null, rect);
            
            var ft = new FormattedText("ZDiagramCanvas rendering", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 12, Brushes.Black, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            drawingContext.DrawText(ft, new Point(10, 10));
        }
    }

    [Obsolete("Use ZDiagramCanvas instead.")]
    public class ZeroDiagramCanvas : ZDiagramCanvas { }
}
