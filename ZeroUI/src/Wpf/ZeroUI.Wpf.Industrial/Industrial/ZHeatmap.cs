using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Industrial
{
    /// <summary>
    /// Industrial 2D Matrix Heatmap for machine throughput, thermal maps, and load distribution.
    /// Rendered directly via DrawingContext with vector gradient interpolation and cell inspection.
    /// </summary>
    public class ZHeatmap : FrameworkElement
    {
        private string[] _xLabels = new[] { "08:00", "10:00", "12:00", "14:00", "16:00", "18:00", "20:00" };
        private string[] _yLabels = new[] { "Line 1", "Line 2", "Line 3", "Line 4", "Line 5" };
        private float[,] _data = new float[5, 7];

        public string Title { get; set; } = "SMT Production Throughput Heatmap (Units / Hour)";
        public float MinValue { get; set; } = 0f;
        public float MaxValue { get; set; } = 120f;

        private Point? _mousePos;

        public string[] XLabels { get => _xLabels; set { _xLabels = value; InvalidateVisual(); } }
        public string[] YLabels { get => _yLabels; set { _yLabels = value; InvalidateVisual(); } }
        public float[,] Data { get => _data; set { _data = value; InvalidateVisual(); } }

        public ZHeatmap()
        {
            ClipToBounds = true;
            InitSampleData();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        public void InitSampleData()
        {
            var rand = new Random(101);
            for (int r = 0; r < 5; r++)
            {
                for (int c = 0; c < 7; c++)
                {
                    _data[r, c] = 30f + rand.Next(0, 85);
                }
            }
        }

        #if NETFRAMEWORK
        private static FormattedText CreateFormattedText(string text, Typeface typeface, double fontSize, Brush brush, double pixelsPerDip = 1.0)
        {
            return new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, fontSize, brush);
        }
        #else
        private static FormattedText CreateFormattedText(string text, Typeface typeface, double fontSize, Brush brush, double pixelsPerDip = 1.0)
        {
            return new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, fontSize, brush, pixelsPerDip);
        }
        #endif

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            _mousePos = e.GetPosition(this);
            InvalidateVisual();
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            _mousePos = null;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            #if NETFRAMEWORK
            double dpi = 1.0;
            #else
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            #endif

            // Card background & border
            dc.DrawRectangle(ZeroWpfTheme.BgCard, null, new Rect(0, 0, w, h));
            dc.DrawRectangle(null, ZeroWpfTheme.BorderPen, new Rect(0.5, 0.5, w - 1, h - 1));

            // Title
            var titleFt = CreateFormattedText(Title, ZeroWpfTheme.BoldTypeface, 12.5, ZeroWpfTheme.TextPrimary, dpi);
            dc.DrawText(titleFt, new Point(14, 12));

            int rows = _yLabels.Length;
            int cols = _xLabels.Length;
            if (rows == 0 || cols == 0 || _data == null) return;

            double padLeft = 65;
            double padTop = 40;
            double padRight = 20;
            double padBottom = 32;

            double gridW = Math.Max(20, w - padLeft - padRight);
            double gridH = Math.Max(20, h - padTop - padBottom);

            double cellW = gridW / cols;
            double cellH = gridH / rows;

            // Draw Y-Labels
            for (int r = 0; r < rows; r++)
            {
                var yFt = CreateFormattedText(_yLabels[r], ZeroWpfTheme.BoldTypeface, 11.0, ZeroWpfTheme.TextSecondary, dpi);
                double yPos = padTop + r * cellH + (cellH - yFt.Height) / 2.0;
                dc.DrawText(yFt, new Point(padLeft - yFt.Width - 8, yPos));
            }

            // Draw Cells & Values
            (int HoverR, int HoverC)? hoveredCell = null;

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    float val = _data[r, c];
                    float ratio = Math.Max(0f, Math.Min(1f, (val - MinValue) / Math.Max(1f, MaxValue - MinValue)));

                    Color cellColor = GetHeatmapColor(ratio);
                    var cellBrush = new SolidColorBrush(cellColor);
                    cellBrush.Freeze();

                    double cx = padLeft + c * cellW;
                    double cy = padTop + r * cellH;
                    Rect cellRect = new Rect(cx + 1.5, cy + 1.5, Math.Max(2, cellW - 3), Math.Max(2, cellH - 3));

                    dc.DrawRoundedRectangle(cellBrush, null, cellRect, 3, 3);

                    // Hover check
                    if (_mousePos.HasValue && cellRect.Contains(_mousePos.Value))
                    {
                        hoveredCell = (r, c);
                        dc.DrawRoundedRectangle(null, ZeroWpfTheme.AccentPen, cellRect, 3, 3);
                    }

                    // Value Text
                    if (cellW > 28 && cellH > 18)
                    {
                        var valFt = CreateFormattedText($"{val:0}", ZeroWpfTheme.BoldTypeface, 10.5, (ratio > 0.6f) ? Brushes.Black : Brushes.White, dpi);
                        dc.DrawText(valFt, new Point(cx + (cellW - valFt.Width) / 2.0, cy + (cellH - valFt.Height) / 2.0));
                    }
                }
            }

            // Draw X-Labels
            for (int c = 0; c < cols; c++)
            {
                var xFt = CreateFormattedText(_xLabels[c], ZeroWpfTheme.RegularTypeface, 10.0, ZeroWpfTheme.TextMuted, dpi);
                double xPos = padLeft + c * cellW + (cellW - xFt.Width) / 2.0;
                dc.DrawText(xFt, new Point(xPos, padTop + gridH + 6));
            }

            // Draw Tooltip if hovered
            if (hoveredCell.HasValue && _mousePos.HasValue)
            {
                var (hr, hc) = hoveredCell.Value;
                string tip = $"{_yLabels[hr]} @ {_xLabels[hc]}: {_data[hr, hc]:0.#} Units/hr";
                var tipFt = CreateFormattedText(tip, ZeroWpfTheme.BoldTypeface, 11.0, ZeroWpfTheme.TextPrimary, dpi);

                double tipW = tipFt.Width + 16;
                double tipH = tipFt.Height + 8;
                double tx = Math.Min(w - tipW - 8, Math.Max(8, _mousePos.Value.X - tipW / 2.0));
                double ty = Math.Max(8, _mousePos.Value.Y - tipH - 12);

                dc.DrawRoundedRectangle(ZeroWpfTheme.BgInput, ZeroWpfTheme.BorderPen, new Rect(tx, ty, tipW, tipH), 4, 4);
                dc.DrawText(tipFt, new Point(tx + 8, ty + 4));
            }
        }

        private static Color GetHeatmapColor(float ratio)
        {
            // Cool-to-warm gradient: Navy/Slate -> Teal -> Green -> Yellow -> Coral Red
            if (ratio < 0.25f)
            {
                float t = ratio / 0.25f;
                return InterpolateColor(Color.FromRgb(30, 41, 59), Color.FromRgb(56, 189, 248), t);
            }
            if (ratio < 0.50f)
            {
                float t = (ratio - 0.25f) / 0.25f;
                return InterpolateColor(Color.FromRgb(56, 189, 248), Color.FromRgb(74, 222, 128), t);
            }
            if (ratio < 0.75f)
            {
                float t = (ratio - 0.50f) / 0.25f;
                return InterpolateColor(Color.FromRgb(74, 222, 128), Color.FromRgb(250, 204, 21), t);
            }
            else
            {
                float t = (ratio - 0.75f) / 0.25f;
                return InterpolateColor(Color.FromRgb(250, 204, 21), Color.FromRgb(244, 63, 94), t);
            }
        }

        private static Color InterpolateColor(Color c1, Color c2, float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            byte r = (byte)(c1.R + (c2.R - c1.R) * t);
            byte g = (byte)(c1.G + (c2.G - c1.G) * t);
            byte b = (byte)(c1.B + (c2.B - c1.B) * t);
            return Color.FromRgb(r, g, b);
        }
    
    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        ZeroWpfTheme.ThemeChanged += OnThemeChanged;
    }

    private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        ZeroWpfTheme.ThemeChanged -= OnThemeChanged;
    }
    private void OnThemeChanged() => InvalidateVisual();
}
    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZHeatmap"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("Heatmap is deprecated and will be removed in 5 release cycles. Please migrate to ZHeatmap instead.")]
    public class Heatmap : ZHeatmap { }

    /// <summary>
    /// Legacy alias for <see cref="ZHeatmap"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroHeatmap is deprecated and will be removed in 5 release cycles. Please migrate to ZHeatmap instead.")]
    public class ZeroHeatmap : ZHeatmap { }

    #endregion

}
