using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Editors;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Media
{
    public enum HistogramChannelMode
    {
        Rgb,
        Luma,
        RedOnly,
        GreenOnly,
        BlueOnly
    }

    public enum HistogramScopeType
    {
        Histogram,
        Waveform
    }

    public struct HistogramZoneDragEventArgs
    {
        public int ZoneIndex { get; set; }
        public string ZoneName { get; set; }
        public double DeltaX { get; set; }
        public double NormalizedDelta { get; set; }
    }

    /// <summary>
    /// Interactive real-time Histogram and Waveform Scope control for ZeroUI in WPF.
    /// Supports RGB overlay, Luma curve, 5-zone parametric tone dragging (Blacks, Shadows, Exposure, Highlights, Whites),
    /// shadow/highlight clipping indicators, and high-performance direct <see cref="DrawingContext"/> rendering.
    /// </summary>
    public class ZHistogramScope : FrameworkElement, IZeroEditor
    {
        private int _hoveredZone = -1;
        private int _draggedZone = -1;
        private Point _dragStartPoint;
        private bool _isDragging;
        private bool _isModified;

        private static readonly string[] ZoneNames = { "Blacks", "Shadows", "Exposure", "Highlights", "Whites" };
        private static readonly double[] ZoneThresholds = { 0.0, 0.10, 0.30, 0.70, 0.90, 1.0 };

        #region Dependency Properties

        public static readonly DependencyProperty RedChannelProperty =
            DependencyProperty.Register(nameof(RedChannel), typeof(int[]), typeof(ZHistogramScope),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty GreenChannelProperty =
            DependencyProperty.Register(nameof(GreenChannel), typeof(int[]), typeof(ZHistogramScope),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty BlueChannelProperty =
            DependencyProperty.Register(nameof(BlueChannel), typeof(int[]), typeof(ZHistogramScope),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty LumaChannelProperty =
            DependencyProperty.Register(nameof(LumaChannel), typeof(int[]), typeof(ZHistogramScope),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ChannelModeProperty =
            DependencyProperty.Register(nameof(ChannelMode), typeof(HistogramChannelMode), typeof(ZHistogramScope),
                new FrameworkPropertyMetadata(HistogramChannelMode.Rgb, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ScopeTypeProperty =
            DependencyProperty.Register(nameof(ScopeType), typeof(HistogramScopeType), typeof(ZHistogramScope),
                new FrameworkPropertyMetadata(HistogramScopeType.Histogram, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ShadowClipPercentProperty =
            DependencyProperty.Register(nameof(ShadowClipPercent), typeof(double), typeof(ZHistogramScope),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty HighlightClipPercentProperty =
            DependencyProperty.Register(nameof(HighlightClipPercent), typeof(double), typeof(ZHistogramScope),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ShowZoneHoverProperty =
            DependencyProperty.Register(nameof(ShowZoneHover), typeof(bool), typeof(ZHistogramScope),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ReadOnlyProperty =
            DependencyProperty.Register(nameof(ReadOnly), typeof(bool), typeof(ZHistogramScope),
                new FrameworkPropertyMetadata(false));

        #endregion

        #region Properties & Events

        public int[]? RedChannel
        {
            get => (int[]?)GetValue(RedChannelProperty);
            set => SetValue(RedChannelProperty, value);
        }

        public int[]? GreenChannel
        {
            get => (int[]?)GetValue(GreenChannelProperty);
            set => SetValue(GreenChannelProperty, value);
        }

        public int[]? BlueChannel
        {
            get => (int[]?)GetValue(BlueChannelProperty);
            set => SetValue(BlueChannelProperty, value);
        }

        public int[]? LumaChannel
        {
            get => (int[]?)GetValue(LumaChannelProperty);
            set => SetValue(LumaChannelProperty, value);
        }

        public HistogramChannelMode ChannelMode
        {
            get => (HistogramChannelMode)GetValue(ChannelModeProperty);
            set => SetValue(ChannelModeProperty, value);
        }

        public HistogramScopeType ScopeType
        {
            get => (HistogramScopeType)GetValue(ScopeTypeProperty);
            set => SetValue(ScopeTypeProperty, value);
        }

        public double ShadowClipPercent
        {
            get => (double)GetValue(ShadowClipPercentProperty);
            set => SetValue(ShadowClipPercentProperty, value);
        }

        public double HighlightClipPercent
        {
            get => (double)GetValue(HighlightClipPercentProperty);
            set => SetValue(HighlightClipPercentProperty, value);
        }

        public bool ShowZoneHover
        {
            get => (bool)GetValue(ShowZoneHoverProperty);
            set => SetValue(ShowZoneHoverProperty, value);
        }

        public bool ReadOnly
        {
            get => (bool)GetValue(ReadOnlyProperty);
            set => SetValue(ReadOnlyProperty, value);
        }

        /// <summary>
        /// Occurs when the user drags horizontally across a tonal zone.
        /// </summary>
        public event EventHandler<HistogramZoneDragEventArgs>? ZoneDragged;

        /// <summary>
        /// Occurs when the user double-clicks a zone to reset it.
        /// </summary>
        public event EventHandler<int>? ZoneReset;

        #endregion

        #region IZeroEditor Implementation

        public object? EditValue
        {
            get => ChannelMode;
            set
            {
                if (value is HistogramChannelMode m) ChannelMode = m;
            }
        }

        public event EventHandler? EditValueChanged;

        public bool IsModified
        {
            get => _isModified;
            set => _isModified = value;
        }

        public void Reset()
        {
            RedChannel = null;
            GreenChannel = null;
            BlueChannel = null;
            LumaChannel = null;
            ShadowClipPercent = 0.0;
            HighlightClipPercent = 0.0;
            _isModified = false;
            InvalidateVisual();
        }

        public void Clear() => Reset();

        #endregion

        public ZHistogramScope()
        {
            Height = 110;
            MinWidth = 160;
            Focusable = true;
            ClipToBounds = true;
            Cursor = Cursors.SizeWE;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        public void SetChannels(int[]? r, int[]? g, int[]? b, int[]? luma = null)
        {
            RedChannel = r;
            GreenChannel = g;
            BlueChannel = b;
            LumaChannel = luma;
            InvalidateVisual();
        }

        #region Rendering

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            // 1. Background & subtle border
            dc.DrawRectangle(ZeroWpfTheme.BgInput, new Pen(ZeroWpfTheme.BorderSubtle, 1.0), new Rect(0, 0, w, h));

            // 2. Grid lines
            var gridPen = new Pen(ZeroWpfTheme.BorderSubtle, 0.5);
            gridPen.Freeze();
            for (int i = 1; i <= 3; i++)
            {
                double gx = w * (i / 4.0);
                dc.DrawLine(gridPen, new Point(gx, 0), new Point(gx, h));
            }

            // 3. Highlighted Zone Overlay
            if (ShowZoneHover && (_isDragging || _hoveredZone >= 0))
            {
                int activeZone = _isDragging ? _draggedZone : _hoveredZone;
                if (activeZone >= 0 && activeZone < 5)
                {
                    double zLeft = ZoneThresholds[activeZone] * w;
                    double zRight = ZoneThresholds[activeZone + 1] * w;
                    var zoneRect = new Rect(zLeft, 0, zRight - zLeft, h);

                    var zoneFill = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
                    zoneFill.Freeze();
                    var zoneStroke = new Pen(new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)), 1.0);
                    zoneStroke.Freeze();
                    dc.DrawRectangle(zoneFill, zoneStroke, zoneRect);

                    // Draw zone name badge
                    string zName = ZoneNames[activeZone].ToUpperInvariant();
                    var ft = new FormattedText(
                        zName,
                        CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight,
                        ZeroWpfTheme.BoldTypeface,
                        9.0,
                        ZeroWpfTheme.TextPrimary,
                        VisualTreeHelper.GetDpi(this).PixelsPerDip);

                    dc.DrawText(ft, new Point(zLeft + (zRight - zLeft - ft.Width) / 2.0, 4));
                }
            }

            // 4. Histogram Channels
            RenderHistogramChannels(dc, w, h);

            // 5. Clipping Warning Badges
            RenderClippingIndicators(dc, w, h);
        }

        private void RenderHistogramChannels(DrawingContext dc, double w, double h)
        {
            var r = RedChannel;
            var g = GreenChannel;
            var b = BlueChannel;
            var l = LumaChannel;

            if (r == null && g == null && b == null && l == null) return;

            // Find peak value across interior bins [1, count - 2] to prevent boundary clipping spikes
            // (e.g. 21.5% shadow clipping at bin 0) from crushing the midtone curves flat to the floor.
            int FindInteriorPeak(int[]? ch)
            {
                if (ch == null || ch.Length == 0) return 0;
                int peak = 0;
                int start = ch.Length > 2 ? 1 : 0;
                int end = ch.Length > 2 ? ch.Length - 2 : ch.Length - 1;
                for (int i = start; i <= end; i++)
                {
                    if (ch[i] > peak) peak = ch[i];
                }
                // Fallback to all bins if interior is completely empty (e.g. pure solid color image)
                if (peak <= 0)
                {
                    for (int i = 0; i < ch.Length; i++)
                    {
                        if (ch[i] > peak) peak = ch[i];
                    }
                }
                return peak;
            }

            int maxVal = 1;
            if (ChannelMode == HistogramChannelMode.Luma)
            {
                maxVal = Math.Max(maxVal, FindInteriorPeak(l));
            }
            else
            {
                if (ChannelMode == HistogramChannelMode.Rgb || ChannelMode == HistogramChannelMode.RedOnly)
                    maxVal = Math.Max(maxVal, FindInteriorPeak(r));
                if (ChannelMode == HistogramChannelMode.Rgb || ChannelMode == HistogramChannelMode.GreenOnly)
                    maxVal = Math.Max(maxVal, FindInteriorPeak(g));
                if (ChannelMode == HistogramChannelMode.Rgb || ChannelMode == HistogramChannelMode.BlueOnly)
                    maxVal = Math.Max(maxVal, FindInteriorPeak(b));
            }

            if (ChannelMode == HistogramChannelMode.Luma)
            {
                if (l != null) DrawChannelArea(dc, l, maxVal, w, h, Color.FromArgb(160, 220, 220, 220));
            }
            else
            {
                // RGB Overlay
                if (b != null && (ChannelMode == HistogramChannelMode.Rgb || ChannelMode == HistogramChannelMode.BlueOnly))
                    DrawChannelArea(dc, b, maxVal, w, h, Color.FromArgb(100, 60, 140, 255));
                if (g != null && (ChannelMode == HistogramChannelMode.Rgb || ChannelMode == HistogramChannelMode.GreenOnly))
                    DrawChannelArea(dc, g, maxVal, w, h, Color.FromArgb(100, 50, 220, 90));
                if (r != null && (ChannelMode == HistogramChannelMode.Rgb || ChannelMode == HistogramChannelMode.RedOnly))
                    DrawChannelArea(dc, r, maxVal, w, h, Color.FromArgb(100, 240, 60, 60));
            }
        }

        private void DrawChannelArea(DrawingContext dc, int[] bins, int maxVal, double w, double h, Color col)
        {
            int count = bins.Length;
            if (count < 2) return;

            var brush = new SolidColorBrush(col);
            brush.Freeze();
            var stroke = new Pen(new SolidColorBrush(Color.FromArgb((byte)Math.Min(255, col.A * 2), col.R, col.G, col.B)), 1.0);
            stroke.Freeze();

            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(new Point(0, h), true, true);
                for (int i = 0; i < count; i++)
                {
                    double x = (i / (double)(count - 1)) * w;
                    double ratio = Math.Max(0.0, Math.Min(1.0, bins[i] / (double)maxVal));
                    // Square-root perceptual scaling so midtones, subtle shadows, and highlights are clearly visible
                    double normH = Math.Sqrt(ratio);
                    double y = h - normH * (h - 6.0);
                    ctx.LineTo(new Point(x, y), false, false);
                }
                ctx.LineTo(new Point(w, h), false, false);
            }
            geo.Freeze();
            dc.DrawGeometry(brush, stroke, geo);
        }

        private void RenderClippingIndicators(DrawingContext dc, double w, double h)
        {
            if (ShadowClipPercent > 0.05)
            {
                // Blue shadow clipping triangle (top left, matching Lightroom standard)
                var geo = new StreamGeometry();
                using (var ctx = geo.Open())
                {
                    ctx.BeginFigure(new Point(0, 0), true, true);
                    ctx.LineTo(new Point(10, 0), false, false);
                    ctx.LineTo(new Point(0, 10), false, false);
                }
                geo.Freeze();
                dc.DrawGeometry(Brushes.DeepSkyBlue, null, geo);
            }

            if (HighlightClipPercent > 0.05)
            {
                // Red highlight clipping triangle (top right, matching Lightroom standard)
                var geo = new StreamGeometry();
                using (var ctx = geo.Open())
                {
                    ctx.BeginFigure(new Point(w, 0), true, true);
                    ctx.LineTo(new Point(w - 10, 0), false, false);
                    ctx.LineTo(new Point(w, 10), false, false);
                }
                geo.Freeze();
                dc.DrawGeometry(Brushes.Crimson, null, geo);
            }
        }

        #endregion

        #region Interaction

        private int GetZoneAtX(double x)
        {
            double normX = Math.Max(0.0, Math.Min(1.0, x / Math.Max(1.0, ActualWidth)));
            for (int i = 0; i < 5; i++)
            {
                if (normX >= ZoneThresholds[i] && normX <= ZoneThresholds[i + 1])
                    return i;
            }
            return 2;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Point p = e.GetPosition(this);

            if (_isDragging && !ReadOnly)
            {
                double dx = p.X - _dragStartPoint.X;
                _dragStartPoint = p;

                ZoneDragged?.Invoke(this, new HistogramZoneDragEventArgs
                {
                    ZoneIndex = _draggedZone,
                    ZoneName = ZoneNames[_draggedZone],
                    DeltaX = dx,
                    NormalizedDelta = dx / Math.Max(1.0, ActualWidth)
                });

                _isModified = true;
                EditValueChanged?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                int z = GetZoneAtX(p.X);
                if (z != _hoveredZone)
                {
                    _hoveredZone = z;
                    InvalidateVisual();
                }
            }
        }

        protected override void OnMouseEnter(MouseEventArgs e)
        {
            base.OnMouseEnter(e);
            _hoveredZone = GetZoneAtX(e.GetPosition(this).X);
            InvalidateVisual();
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            _hoveredZone = -1;
            InvalidateVisual();
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            Focus();
            if (ReadOnly) return;

            Point p = e.GetPosition(this);
            int z = GetZoneAtX(p.X);

            if (e.ClickCount == 2)
            {
                ZoneReset?.Invoke(this, z);
                _isModified = true;
                EditValueChanged?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
                return;
            }

            _isDragging = true;
            _draggedZone = z;
            _dragStartPoint = p;
            CaptureMouse();
            InvalidateVisual();
            e.Handled = true;
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if (_isDragging)
            {
                _isDragging = false;
                ReleaseMouseCapture();
                InvalidateVisual();
                e.Handled = true;
            }
        }

        #endregion
    
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
    /// Legacy alias for <see cref="ZHistogramScope"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("HistogramScopeControl is deprecated. Use ZHistogramScope instead.")]
    public class HistogramScopeControl : ZHistogramScope
    {
    }

    /// <summary>
    /// Legacy alias for <see cref="ZHistogramScope"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroHistogramScope is deprecated. Use ZHistogramScope instead.")]
    public class ZeroHistogramScope : ZHistogramScope
    {
    }
    #endregion
}
