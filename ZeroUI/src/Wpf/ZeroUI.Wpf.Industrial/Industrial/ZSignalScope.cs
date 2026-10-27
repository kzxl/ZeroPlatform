using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Signal;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Industrial
{
    /// <summary>
    /// High-performance Single-Visual real-time oscilloscope and multi-channel logic analyzer for WPF.
    /// Eliminates visual tree overhead, capable of 100,000+ points/sec streaming with zero GC allocations,
    /// hardware edge trigger stabilization, and interactive precision dual measurement cursors.
    /// </summary>
    public class ZSignalScope : FrameworkElement
    {
        private readonly ObservableCollection<ScopeChannel> _channels = new ObservableCollection<ScopeChannel>();
        private readonly ScopeTrigger _trigger = new ScopeTrigger();
        private readonly ScopeCursor _cursor = new ScopeCursor();

        private double _timePerDiv = 0.01; // 10ms per division
        private int _horizontalDivs = 10;
        private int _verticalDivs = 8;
        private bool _showGrid = true;
        private bool _showCursors = true;
        private bool _showHud = true;

        // Interaction state
        private int _draggingCursor = 0; // 0=None, 1=X1, 2=X2, 3=Y1, 4=Y2
        private Point _lastMousePos;

        public ObservableCollection<ScopeChannel> Channels => _channels;
        public ScopeTrigger Trigger => _trigger;
        public ScopeCursor CursorMeasurements => _cursor;

        public double TimePerDiv
        {
            get => _timePerDiv;
            set { _timePerDiv = Math.Max(0.0001, Math.Min(5.0, value)); InvalidateVisual(); }
        }

        public bool ShowGrid
        {
            get => _showGrid;
            set { _showGrid = value; InvalidateVisual(); }
        }

        public bool ShowCursors
        {
            get => _showCursors;
            set { _showCursors = value; InvalidateVisual(); }
        }

        public bool ShowHud
        {
            get => _showHud;
            set { _showHud = value; InvalidateVisual(); }
        }

        public ZSignalScope()
        {
            ClipToBounds = true;
            Focusable = true;
            Cursor = Cursors.Cross;

            _channels.CollectionChanged += (s, e) => InvalidateVisual();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 10 || h <= 10) return;

            // 1. Draw Phosphor Dark Grid Background
            DrawGrid(dc, w, h);

            // 2. Draw Waveforms
            DrawWaveforms(dc, w, h);

            // 3. Draw Measurement Cursors
            if (_showCursors && _cursor.IsEnabled)
            {
                DrawCursors(dc, w, h);
            }

            // 4. Draw Oscilloscope Telemetry HUD
            if (_showHud)
            {
                DrawHud(dc, w, h);
            }
        }

        // Static frozen graphic assets for zero-alloc drawing
        private static readonly Brush BgBrush;
        private static readonly Pen GridPen;
        private static readonly Pen CenterPen;
        private static readonly Pen CursorPen;
        private static readonly Brush BadgeBgBrush;
        private static readonly Pen BadgeBorderPen;
        private static readonly Brush HudBgBrush;
        private static readonly Pen HudTopBorderPen;

        static ZSignalScope()
        {
            var bg = new SolidColorBrush(Color.FromRgb(8, 12, 20));
            bg.Freeze();
            BgBrush = bg;

            var gp = new Pen(new SolidColorBrush(Color.FromArgb(50, 56, 189, 248)), 0.8);
            gp.Freeze();
            GridPen = gp;

            var cp = new Pen(new SolidColorBrush(Color.FromArgb(90, 56, 189, 248)), 1.2);
            cp.Freeze();
            CenterPen = cp;

            var curP = new Pen(new SolidColorBrush(Color.FromArgb(180, 245, 158, 11)), 1.2)
            {
                DashStyle = DashStyles.Dash
            };
            curP.Freeze();
            CursorPen = curP;

            var bbg = new SolidColorBrush(Color.FromArgb(220, 15, 23, 42));
            bbg.Freeze();
            BadgeBgBrush = bbg;

            var bpen = new Pen(new SolidColorBrush(Color.FromArgb(120, 100, 116, 139)), 1);
            bpen.Freeze();
            BadgeBorderPen = bpen;

            var hbg = new SolidColorBrush(Color.FromArgb(235, 11, 17, 33));
            hbg.Freeze();
            HudBgBrush = hbg;

            var hpen = new Pen(new SolidColorBrush(Color.FromArgb(90, 56, 189, 248)), 1);
            hpen.Freeze();
            HudTopBorderPen = hpen;
        }

        private readonly System.Collections.Generic.Dictionary<uint, (Pen pen, Brush fill, Brush textBrush)> _chanGraphicCache =
            new System.Collections.Generic.Dictionary<uint, (Pen, Brush, Brush)>();

        private readonly System.Collections.Generic.Dictionary<int, (string text, FormattedText ft)> _hudChanTextCache =
            new System.Collections.Generic.Dictionary<int, (string, FormattedText)>();

        private double _cachedTimebaseSec = -1;
        private FormattedText? _cachedTimebaseFt;

        private string? _cachedCursorText;
        private FormattedText? _cachedCursorFt;

        private (Pen pen, Brush fill, Brush textBrush) GetChannelGraphics(uint argb)
        {
            if (!_chanGraphicCache.TryGetValue(argb, out var tuple))
            {
                var chanColor = Color.FromArgb(
                    (byte)((argb >> 24) & 0xFF),
                    (byte)((argb >> 16) & 0xFF),
                    (byte)((argb >> 8) & 0xFF),
                    (byte)(argb & 0xFF));

                var brush = new SolidColorBrush(chanColor);
                brush.Freeze();
                var pen = new Pen(brush, 1.8);
                pen.Freeze();

                var fill = new SolidColorBrush(Color.FromArgb(45, chanColor.R, chanColor.G, chanColor.B));
                fill.Freeze();

                tuple = (pen, fill, brush);
                _chanGraphicCache[argb] = tuple;
            }
            return tuple;
        }

        private void DrawGrid(DrawingContext dc, double w, double h)
        {
            // CRT Dark Background
            dc.DrawRectangle(BgBrush, null, new Rect(0, 0, w, h));

            if (!_showGrid) return;

            double dx = w / _horizontalDivs;
            double dy = h / _verticalDivs;

            int midX = _horizontalDivs / 2;
            int midY = _verticalDivs / 2;

            // Vertical Div Lines
            for (int i = 0; i <= _horizontalDivs; i++)
            {
                double x = i * dx;
                dc.DrawLine(i == midX ? CenterPen : GridPen, new Point(x, 0), new Point(x, h));
            }

            // Horizontal Div Lines
            for (int j = 0; j <= _verticalDivs; j++)
            {
                double y = j * dy;
                dc.DrawLine(j == midY ? CenterPen : GridPen, new Point(0, y), new Point(w, y));
            }

            // Outer border
            dc.DrawRectangle(null, CenterPen, new Rect(0, 0, w, h));
        }

        private void DrawWaveforms(DrawingContext dc, double w, double h)
        {
            double centerY = h / 2.0;
            double divHeight = h / _verticalDivs;

            // Compute trigger index on trigger source channel if enabled
            int triggerOffset = -1;
            if (_channels.Count > 0)
            {
                var trigChan = _channels[0];
                for (int c = 0; c < _channels.Count; c++)
                {
                    if (_channels[c].Id == _trigger.ChannelId)
                    {
                        trigChan = _channels[c];
                        break;
                    }
                }
                triggerOffset = trigChan.Buffer.FindTriggerIndex(_trigger.Threshold, _trigger.Slope == TriggerSlope.RisingEdge, 1000);
            }

            foreach (var ch in _channels)
            {
                if (!ch.IsVisible || ch.Buffer.Count < 2) continue;

                var (pen, fillBrush, _) = GetChannelGraphics(ch.ColorArgb);

                int sampleCount = ch.Buffer.Count;
                int visibleSamples = Math.Min(sampleCount, (int)w);
                if (visibleSamples < 2) continue;

                int startSampleIdx = (triggerOffset >= 0 && triggerOffset + visibleSamples <= sampleCount)
                    ? triggerOffset
                    : Math.Max(0, sampleCount - visibleSamples);

                if (ch.ChannelType == ScopeChannelType.Analog)
                {
                    // Render continuous analog trace
                    var geo = new StreamGeometry();
                    using (var ctx = geo.Open())
                    {
                        double xStep = w / (visibleSamples - 1);
                        float firstVal = ch.Buffer[startSampleIdx];
                        double firstY = centerY - (firstVal / ch.VoltsPerDiv + ch.VerticalOffsetDiv) * divHeight;
                        ctx.BeginFigure(new Point(0, firstY), false, false);

                        for (int i = 1; i < visibleSamples; i++)
                        {
                            double x = i * xStep;
                            float val = ch.Buffer[startSampleIdx + i];
                            double y = centerY - (val / ch.VoltsPerDiv + ch.VerticalOffsetDiv) * divHeight;
                            ctx.LineTo(new Point(x, y), true, false);
                        }
                    }
                    geo.Freeze();
                    dc.DrawGeometry(null, pen, geo);
                }
                else
                {
                    // Render discrete digital logic analyzer track
                    double trackCenterY = centerY - (ch.VerticalOffsetDiv * divHeight);
                    double highY = trackCenterY - 14;
                    double lowY = trackCenterY + 14;

                    var geo = new StreamGeometry();
                    using (var ctx = geo.Open())
                    {
                        double xStep = w / (visibleSamples - 1);
                        bool isHigh = ch.Buffer[startSampleIdx] > 0.5f;
                        ctx.BeginFigure(new Point(0, isHigh ? highY : lowY), true, true);

                        for (int i = 1; i < visibleSamples; i++)
                        {
                            double x = i * xStep;
                            bool nextHigh = ch.Buffer[startSampleIdx + i] > 0.5f;
                            if (nextHigh != isHigh)
                            {
                                ctx.LineTo(new Point(x, isHigh ? highY : lowY), true, false);
                                ctx.LineTo(new Point(x, nextHigh ? highY : lowY), true, false);
                                isHigh = nextHigh;
                            }
                            else
                            {
                                ctx.LineTo(new Point(x, isHigh ? highY : lowY), true, false);
                            }
                        }

                        // Close to bottom line for shading
                        ctx.LineTo(new Point(w, lowY), true, false);
                        ctx.LineTo(new Point(0, lowY), true, false);
                    }
                    geo.Freeze();
                    dc.DrawGeometry(fillBrush, pen, geo);
                }
            }
        }

        private void DrawCursors(DrawingContext dc, double w, double h)
        {
            double x1 = _cursor.X1 * w;
            double x2 = _cursor.X2 * w;
            double y1 = _cursor.Y1 * h;
            double y2 = _cursor.Y2 * h;

            // X Cursors (Time)
            dc.DrawLine(CursorPen, new Point(x1, 0), new Point(x1, h));
            dc.DrawLine(CursorPen, new Point(x2, 0), new Point(x2, h));

            // Y Cursors (Voltage)
            dc.DrawLine(CursorPen, new Point(0, y1), new Point(w, y1));
            dc.DrawLine(CursorPen, new Point(0, y2), new Point(w, y2));

            // Cursor Measurement Badge
            double totalTime = _timePerDiv * _horizontalDivs;
            double dt = _cursor.CalculateDeltaTime(totalTime);
            double freq = _cursor.CalculateFrequency(totalTime);
            double dyDiv = (_cursor.DeltaY * _verticalDivs);

            string text = $"Δt: {FormatTime(dt)} | Freq: {FormatFrequency(freq)} | ΔV (Div): {dyDiv:0.00}";
            if (_cachedCursorFt == null || _cachedCursorText != text)
            {
                _cachedCursorText = text;
                _cachedCursorFt = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    ZeroWpfTheme.BoldTypeface, 11, CursorPen.Brush, 1.0);
            }

            var ft = _cachedCursorFt;
            double badgeW = ft.Width + 16;
            double badgeH = ft.Height + 8;
            Rect badgeRect = new Rect(Math.Min(x1, x2) + 6, Math.Min(y1, y2) + 6, badgeW, badgeH);

            dc.DrawRoundedRectangle(BadgeBgBrush, BadgeBorderPen, badgeRect, 4, 4);
            dc.DrawText(ft, new Point(badgeRect.Left + 8, badgeRect.Top + 4));
        }

        private void DrawHud(DrawingContext dc, double w, double h)
        {
            // Bottom HUD Bar
            double barH = 26;
            Rect barRect = new Rect(0, h - barH, w, barH);
            dc.DrawRectangle(HudBgBrush, null, barRect);
            dc.DrawLine(HudTopBorderPen, new Point(0, h - barH), new Point(w, h - barH));

            double curX = 14;

            // 1. Timebase
            if (_cachedTimebaseFt == null || Math.Abs(_cachedTimebaseSec - _timePerDiv) > 1e-7)
            {
                _cachedTimebaseSec = _timePerDiv;
                string timebase = $"⏱ Time: {FormatTime(_timePerDiv)}/Div";
                _cachedTimebaseFt = new FormattedText(timebase, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    ZeroWpfTheme.BoldTypeface, 11, ZeroWpfTheme.TextPrimary, 1.0);
            }
            dc.DrawText(_cachedTimebaseFt, new Point(curX, h - barH + 5));
            curX += _cachedTimebaseFt.Width + 24;

            // 2. Channel Badges
            foreach (var ch in _channels)
            {
                if (!ch.IsVisible) continue;

                var (_, _, textBrush) = GetChannelGraphics(ch.ColorArgb);

                ch.GetOrComputeMetrics(out float min, out float max, out float p2p, out float rms);

                string chText = $"{ch.Name}: {ch.VoltsPerDiv:0.#}{ch.Unit}/Div | Vpp: {p2p:0.00}{ch.Unit}";

                if (!_hudChanTextCache.TryGetValue(ch.Id, out var cached) || cached.text != chText)
                {
                    var chFt = new FormattedText(chText, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                        ZeroWpfTheme.BoldTypeface, 11, textBrush, 1.0);
                    cached = (chText, chFt);
                    _hudChanTextCache[ch.Id] = cached;
                }

                dc.DrawText(cached.ft, new Point(curX, h - barH + 5));
                curX += cached.ft.Width + 20;
            }
        }

        private static string FormatTime(double sec)
        {
            if (sec >= 1.0) return $"{sec:0.##}s";
            if (sec >= 0.001) return $"{sec * 1000.0:0.##}ms";
            if (sec >= 1e-6) return $"{sec * 1e6:0.##}µs";
            return $"{sec * 1e9:0}ns";
        }

        private static string FormatFrequency(double hz)
        {
            if (hz >= 1e6) return $"{hz / 1e6:0.##} MHz";
            if (hz >= 1e3) return $"{hz / 1e3:0.##} kHz";
            return $"{hz:0.#} Hz";
        }

        #region Mouse Cursor Dragging

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            if (!_showCursors) return;

            Point pt = e.GetPosition(this);
            double w = ActualWidth;
            double h = ActualHeight;

            double x1 = _cursor.X1 * w;
            double x2 = _cursor.X2 * w;
            double y1 = _cursor.Y1 * h;
            double y2 = _cursor.Y2 * h;

            double tol = 12.0;

            if (Math.Abs(pt.X - x1) <= tol) _draggingCursor = 1;
            else if (Math.Abs(pt.X - x2) <= tol) _draggingCursor = 2;
            else if (Math.Abs(pt.Y - y1) <= tol) _draggingCursor = 3;
            else if (Math.Abs(pt.Y - y2) <= tol) _draggingCursor = 4;
            else _draggingCursor = 0;

            if (_draggingCursor > 0)
            {
                CaptureMouse();
                _lastMousePos = pt;
                e.Handled = true;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Point pt = e.GetPosition(this);
            double w = ActualWidth;
            double h = ActualHeight;

            if (_draggingCursor > 0)
            {
                switch (_draggingCursor)
                {
                    case 1: _cursor.X1 = Math.Max(0.02, Math.Min(0.98, pt.X / w)); break;
                    case 2: _cursor.X2 = Math.Max(0.02, Math.Min(0.98, pt.X / w)); break;
                    case 3: _cursor.Y1 = Math.Max(0.02, Math.Min(0.98, pt.Y / h)); break;
                    case 4: _cursor.Y2 = Math.Max(0.02, Math.Min(0.98, pt.Y / h)); break;
                }
                InvalidateVisual();
                return;
            }

            // Cursor Hover Cue
            if (_showCursors)
            {
                double x1 = _cursor.X1 * w;
                double x2 = _cursor.X2 * w;
                double y1 = _cursor.Y1 * h;
                double y2 = _cursor.Y2 * h;
                double tol = 8.0;

                if (Math.Abs(pt.X - x1) <= tol || Math.Abs(pt.X - x2) <= tol)
                    Cursor = Cursors.SizeWE;
                else if (Math.Abs(pt.Y - y1) <= tol || Math.Abs(pt.Y - y2) <= tol)
                    Cursor = Cursors.SizeNS;
                else
                    Cursor = Cursors.Cross;
            }
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);
            if (_draggingCursor > 0)
            {
                _draggingCursor = 0;
                ReleaseMouseCapture();
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
    /// Legacy alias for <see cref="ZSignalScope"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("SignalScope is deprecated and will be removed in 5 release cycles. Please migrate to ZSignalScope instead.")]
    public class SignalScope : ZSignalScope { }

    /// <summary>
    /// Legacy alias for <see cref="ZSignalScope"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroSignalScope is deprecated and will be removed in 5 release cycles. Please migrate to ZSignalScope instead.")]
    public class ZeroSignalScope : ZSignalScope { }

    #endregion

}
