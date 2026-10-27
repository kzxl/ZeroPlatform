using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.Windows.Forms;
using ZeroUI.Core.Analytics;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Charts
{
    /// <summary>
    /// High-performance multi-pen time-series canvas with independent multi-Y axes,
    /// LTTB vector decimation, dual-cursor delta measurements, and interactive zooming/panning.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Charts & Analytics")]
    public class ZTrendPlot : Control
    {
        private readonly List<TrendPen> _pens = new List<TrendPen>();
        private readonly List<TrendAnnotation> _annotations = new List<TrendAnnotation>();
        private readonly TrendDualCursor _dualCursor = new TrendDualCursor();

        private DateTime _startTime;
        private DateTime _endTime;
        private TimeSpan _timeWindow = TimeSpan.FromMinutes(5);
        private bool _isLiveFollow = true;

        // Interaction state
        private bool _isDraggingCursorA = false;
        private bool _isDraggingCursorB = false;
        private bool _isPanning = false;
        private Point _lastPanPoint;
        private bool _isBoxZooming = false;
        private Rectangle _boxZoomRect;
        private Point _boxZoomStart;

        // Hover inspection
        private Point? _hoverPoint;

        public List<TrendPen> Pens => _pens;
        public List<TrendAnnotation> Annotations => _annotations;
        public TrendDualCursor DualCursor => _dualCursor;

        [Category("ZeroUI - Behavior")]
        public bool IsLiveFollow
        {
            get => _isLiveFollow;
            set { _isLiveFollow = value; Invalidate(); }
        }

        [Category("ZeroUI - Behavior")]
        public TimeSpan TimeWindow
        {
            get => _timeWindow;
            set
            {
                if (value.TotalSeconds >= 1)
                {
                    _timeWindow = value;
                    if (_isLiveFollow)
                    {
                        _endTime = DateTime.Now;
                        _startTime = _endTime - _timeWindow;
                    }
                    Invalidate();
                }
            }
        }

        public DateTime StartTime
        {
            get => _startTime;
            set { _startTime = value; _isLiveFollow = false; Invalidate(); }
        }

        public DateTime EndTime
        {
            get => _endTime;
            set { _endTime = value; _isLiveFollow = false; Invalidate(); }
        }

        public event EventHandler? TimeRangeChanged;
        public event EventHandler? CursorsChanged;

        public ZTrendPlot()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.Selectable |
                ControlStyles.SupportsTransparentBackColor, true);

            Size = new Size(600, 360);
            Font = new Font("Segoe UI", 9f);
            BackColor = Color.FromArgb(15, 23, 42); // Sleek Slate 900 dark theme

            _endTime = DateTime.Now;
            _startTime = _endTime - _timeWindow;

            ZeroTheme.ThemeChanged += OnThemeChanged;
        }

        public void SetTimeRange(DateTime start, DateTime end)
        {
            if (end > start)
            {
                _startTime = start;
                _endTime = end;
                _timeWindow = end - start;
                _isLiveFollow = false;
                TimeRangeChanged?.Invoke(this, EventArgs.Empty);
                Invalidate();
            }
        }

        public void ResetZoom()
        {
            _endTime = DateTime.Now;
            _startTime = _endTime - _timeWindow;
            _isLiveFollow = true;
            TimeRangeChanged?.Invoke(this, EventArgs.Empty);
            Invalidate();
        }

        public void UpdateLiveStream()
        {
            if (_isLiveFollow)
            {
                _endTime = DateTime.Now;
                _startTime = _endTime - _timeWindow;
                Invalidate();
            }
        }

        private Rectangle GetPlotArea()
        {
            int leftMargin = 16;
            int rightMargin = 16;

            foreach (var pen in _pens)
            {
                if (!pen.IsVisible) continue;
                if (pen.AxisPosition == TrendYAxisPosition.Left1 || pen.AxisPosition == TrendYAxisPosition.Left2)
                    leftMargin += 52;
                else
                    rightMargin += 52;
            }

            int topMargin = 28;
            int bottomMargin = 32;

            int w = Math.Max(50, Width - leftMargin - rightMargin);
            int h = Math.Max(50, Height - topMargin - bottomMargin);

            return new Rectangle(leftMargin, topMargin, w, h);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            bool isDark = ZeroTheme.IsDark;
            Color bgColor = isDark ? Color.FromArgb(15, 23, 42) : Color.FromArgb(248, 250, 252);
            Color plotBg = isDark ? Color.FromArgb(10, 15, 28) : Color.White;
            Color gridColor = isDark ? Color.FromArgb(30, 41, 59) : Color.FromArgb(226, 232, 240);
            Color textColor = isDark ? Color.FromArgb(148, 163, 184) : Color.FromArgb(100, 116, 139);

            g.Clear(bgColor);

            var plotRect = GetPlotArea();
            using (var b = new SolidBrush(plotBg))
            {
                g.FillRectangle(b, plotRect);
            }

            // 1. Draw Grid Lines
            DrawGridLines(g, plotRect, gridColor, textColor);

            // 2. Draw Annotations / Event Markers
            DrawAnnotations(g, plotRect);

            // 3. Draw Pens (Waveform curves)
            var clip = g.Clip;
            g.SetClip(plotRect);
            foreach (var pen in _pens)
            {
                if (pen.IsVisible)
                {
                    DrawPenSeries(g, pen, plotRect);
                }
            }
            g.Clip = clip;

            // 4. Draw Multi-Y Axes
            DrawMultiYAxes(g, plotRect, isDark);

            // 5. Draw Dual Cursors
            if (_dualCursor.Enabled)
            {
                DrawDualCursors(g, plotRect, isDark);
            }

            // 6. Draw Box Zoom overlay
            if (_isBoxZooming && _boxZoomRect.Width > 2 && _boxZoomRect.Height > 2)
            {
                using var fill = new SolidBrush(Color.FromArgb(40, 59, 130, 246));
                using var border = new Pen(Color.FromArgb(59, 130, 246), 1.5f) { DashStyle = DashStyle.Dash };
                g.FillRectangle(fill, _boxZoomRect);
                g.DrawRectangle(border, _boxZoomRect);
            }

            // Border around plot area
            using (var borderPen = new Pen(gridColor, 1.5f))
            {
                g.DrawRectangle(borderPen, plotRect);
            }
        }

        private void DrawGridLines(Graphics g, Rectangle r, Color gridColor, Color textColor)
        {
            using var pen = new Pen(gridColor, 1f) { DashStyle = DashStyle.Dot };
            using var textBrush = new SolidBrush(textColor);
            using var timeFont = new Font(Font.FontFamily, 8f);
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near };

            // Horizontal grid lines (4 divisions)
            for (int i = 1; i < 5; i++)
            {
                float y = r.Bottom - (r.Height * i / 5f);
                g.DrawLine(pen, r.Left, y, r.Right, y);
            }

            // Vertical time grid lines (6 divisions)
            int divCount = 6;
            double totalSeconds = (_endTime - _startTime).TotalSeconds;
            if (totalSeconds <= 0) totalSeconds = 1;

            for (int i = 0; i <= divCount; i++)
            {
                float x = r.Left + (r.Width * i / (float)divCount);
                if (i > 0 && i < divCount)
                {
                    g.DrawLine(pen, x, r.Top, x, r.Bottom);
                }

                // X-Axis Time Label
                DateTime t = _startTime.AddSeconds(totalSeconds * i / divCount);
                string label = totalSeconds < 120
                    ? t.ToString("HH:mm:ss.f")
                    : totalSeconds < 86400
                        ? t.ToString("HH:mm:ss")
                        : t.ToString("MM/dd HH:mm");

                g.DrawString(label, timeFont, textBrush, x, r.Bottom + 6, sf);
            }
        }

        private void DrawMultiYAxes(Graphics g, Rectangle plotRect, bool isDark)
        {
            int leftOffs = plotRect.Left;
            int rightOffs = plotRect.Right;

            using var font = new Font(Font.FontFamily, 8f, FontStyle.Regular);
            using var fontBold = new Font(Font.FontFamily, 8f, FontStyle.Bold);

            foreach (var pen in _pens)
            {
                if (!pen.IsVisible) continue;

                var (min, max) = pen.GetEffectiveRange(_startTime, _endTime);
                bool isLeft = pen.AxisPosition == TrendYAxisPosition.Left1 || pen.AxisPosition == TrendYAxisPosition.Left2;

                int axisX;
                StringFormat sf;

                if (isLeft)
                {
                    leftOffs -= 52;
                    axisX = leftOffs;
                    sf = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
                }
                else
                {
                    axisX = rightOffs;
                    rightOffs += 52;
                    sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
                }

                using var axisPen = new Pen(pen.Color, 1.5f);
                using var axisBrush = new SolidBrush(pen.Color);

                // Draw vertical axis line
                g.DrawLine(axisPen, axisX, plotRect.Top, axisX, plotRect.Bottom);

                // Draw top unit/name label
                string title = string.IsNullOrEmpty(pen.Unit) ? pen.Name : $"{pen.Name} ({pen.Unit})";
                if (title.Length > 10) title = title.Substring(0, 9) + "…";
                var titleSf = new StringFormat { Alignment = isLeft ? StringAlignment.Far : StringAlignment.Near };
                g.DrawString(title, fontBold, axisBrush, axisX + (isLeft ? -4 : 4), plotRect.Top - 18, titleSf);

                // Draw 5 value ticks
                for (int i = 0; i <= 4; i++)
                {
                    float y = plotRect.Bottom - (plotRect.Height * i / 4f);
                    double val = min + (max - min) * i / 4.0;

                    // Tick mark
                    g.DrawLine(axisPen, axisX - 3, y, axisX + 3, y);

                    // Value label
                    string valStr = Math.Abs(val) >= 1000 ? val.ToString("N0") : val.ToString("F1");
                    g.DrawString(valStr, font, axisBrush, axisX + (isLeft ? -6 : 6), y, sf);
                }
            }
        }

        private void DrawPenSeries(Graphics g, TrendPen pen, Rectangle r)
        {
            var rawPoints = pen.GetPointsInRange(_startTime, _endTime);
            if (rawPoints.Count < 2) return;

            // Apply LTTB decimation when dense
            IReadOnlyList<TrendDataPoint> drawList = rawPoints;
            int targetThreshold = Math.Max(100, r.Width * 2);
            if (rawPoints.Count > targetThreshold)
            {
                drawList = LttbDecimator.Downsample(rawPoints, targetThreshold);
            }

            var (min, max) = pen.GetEffectiveRange(_startTime, _endTime);
            double range = max - min;
            if (range <= 0.00001) range = 1.0;

            double totalTicks = (_endTime - _startTime).Ticks;
            if (totalTicks <= 0) totalTicks = 1;

            var screenPts = new List<PointF>(drawList.Count);
            for (int i = 0; i < drawList.Count; i++)
            {
                var pt = drawList[i];
                double xRatio = (double)(pt.Timestamp.Ticks - _startTime.Ticks) / totalTicks;
                double yRatio = (pt.Value - min) / range;

                float sx = (float)(r.Left + xRatio * r.Width);
                float sy = (float)(r.Bottom - yRatio * r.Height);

                screenPts.Add(new PointF(sx, sy));
            }

            if (screenPts.Count >= 2)
            {
                // Semi-transparent area fill under curve
                using (var fillPath = new GraphicsPath())
                {
                    fillPath.AddLines(screenPts.ToArray());
                    fillPath.AddLine(screenPts[screenPts.Count - 1], new PointF(screenPts[screenPts.Count - 1].X, r.Bottom));
                    fillPath.AddLine(new PointF(screenPts[screenPts.Count - 1].X, r.Bottom), new PointF(screenPts[0].X, r.Bottom));
                    fillPath.AddLine(new PointF(screenPts[0].X, r.Bottom), screenPts[0]);
                    fillPath.CloseFigure();

                    using var areaBrush = new LinearGradientBrush(
                        new Point(r.Left, r.Top), new Point(r.Left, r.Bottom),
                        Color.FromArgb(40, pen.Color), Color.FromArgb(5, pen.Color));
                    g.FillPath(areaBrush, fillPath);
                }

                // Main waveform line
                using var linePen = new Pen(pen.Color, pen.LineWidth) { LineJoin = LineJoin.Round };
                g.DrawLines(linePen, screenPts.ToArray());
            }

            // Draw Alarm Limit lines if defined
            if (pen.HighAlarmLimit.HasValue && pen.HighAlarmLimit.Value >= min && pen.HighAlarmLimit.Value <= max)
            {
                float alarmY = (float)(r.Bottom - ((pen.HighAlarmLimit.Value - min) / range) * r.Height);
                using var alarmPen = new Pen(Color.FromArgb(239, 68, 68), 1.2f) { DashStyle = DashStyle.Dash };
                g.DrawLine(alarmPen, r.Left, alarmY, r.Right, alarmY);

                using var badgeBrush = new SolidBrush(Color.FromArgb(239, 68, 68));
                using var textBrush = new SolidBrush(Color.White);
                using var alarmFont = new Font(Font.FontFamily, 7.5f, FontStyle.Bold);
                g.FillRectangle(badgeBrush, r.Right - 65, alarmY - 8, 60, 16);
                g.DrawString($"HIGH {pen.HighAlarmLimit.Value:F0}", alarmFont, textBrush, r.Right - 63, alarmY - 7);
            }
        }

        private void DrawAnnotations(Graphics g, Rectangle r)
        {
            if (_annotations.Count == 0) return;
            double totalTicks = (_endTime - _startTime).Ticks;
            if (totalTicks <= 0) return;

            using var font = new Font(Font.FontFamily, 8f, FontStyle.Bold);

            foreach (var ann in _annotations)
            {
                if (ann.Timestamp >= _startTime && ann.Timestamp <= _endTime)
                {
                    double xRatio = (double)(ann.Timestamp.Ticks - _startTime.Ticks) / totalTicks;
                    float x = (float)(r.Left + xRatio * r.Width);

                    using var pen = new Pen(ann.Color, 1.2f) { DashStyle = DashStyle.DashDot };
                    g.DrawLine(pen, x, r.Top, x, r.Bottom);

                    // Badge at top
                    string txt = $"{ann.Icon} {ann.Label}";
                    var size = g.MeasureString(txt, font);
                    var badgeRect = new RectangleF(x - size.Width / 2f, r.Top + 4, size.Width + 8, size.Height + 4);

                    using var fill = new SolidBrush(Color.FromArgb(220, ann.Color));
                    using var textB = new SolidBrush(Color.White);
                    g.FillRectangle(fill, badgeRect);
                    g.DrawString(txt, font, textB, badgeRect.X + 4, badgeRect.Y + 2);
                }
            }
        }

        private void DrawDualCursors(Graphics g, Rectangle r, bool isDark)
        {
            double totalTicks = (_endTime - _startTime).Ticks;
            if (totalTicks <= 0) return;

            // Auto-position cursors if not set
            if (!_dualCursor.TimeA.HasValue)
            {
                _dualCursor.TimeA = _startTime.AddSeconds((_endTime - _startTime).TotalSeconds * 0.3);
            }
            if (!_dualCursor.TimeB.HasValue)
            {
                _dualCursor.TimeB = _startTime.AddSeconds((_endTime - _startTime).TotalSeconds * 0.7);
            }

            float xA = (float)(r.Left + ((double)(_dualCursor.TimeA!.Value.Ticks - _startTime.Ticks) / totalTicks) * r.Width);
            float xB = (float)(r.Left + ((double)(_dualCursor.TimeB!.Value.Ticks - _startTime.Ticks) / totalTicks) * r.Width);

            using var font = new Font(Font.FontFamily, 8f, FontStyle.Bold);

            // Draw Cursor A (Gold)
            if (xA >= r.Left && xA <= r.Right)
            {
                using var penA = new Pen(_dualCursor.ColorA, 1.5f) { DashStyle = DashStyle.Dash };
                g.DrawLine(penA, xA, r.Top, xA, r.Bottom);

                // Cursor A Handle
                using var brushA = new SolidBrush(_dualCursor.ColorA);
                using var textB = new SolidBrush(Color.Black);
                g.FillPolygon(brushA, new[] { new PointF(xA - 6, r.Top), new PointF(xA + 6, r.Top), new PointF(xA, r.Top + 9) });
                g.FillRectangle(brushA, xA - 10, r.Bottom - 18, 20, 16);
                g.DrawString("A", font, textB, xA - 5, r.Bottom - 17);
            }

            // Draw Cursor B (Cyan)
            if (xB >= r.Left && xB <= r.Right)
            {
                using var penB = new Pen(_dualCursor.ColorB, 1.5f) { DashStyle = DashStyle.Dash };
                g.DrawLine(penB, xB, r.Top, xB, r.Bottom);

                // Cursor B Handle
                using var brushB = new SolidBrush(_dualCursor.ColorB);
                using var textB = new SolidBrush(Color.Black);
                g.FillPolygon(brushB, new[] { new PointF(xB - 6, r.Top), new PointF(xB + 6, r.Top), new PointF(xB, r.Top + 9) });
                g.FillRectangle(brushB, xB - 10, r.Bottom - 18, 20, 16);
                g.DrawString("B", font, textB, xB - 5, r.Bottom - 17);
            }

            // Draw Delta HUD in the top-right corner
            DrawDeltaHud(g, r, isDark);
        }

        private void DrawDeltaHud(Graphics g, Rectangle r, bool isDark)
        {
            var hudLines = new List<(string text, Color color)>();

            double deltaSec = _dualCursor.DeltaSeconds;
            string timeStr = deltaSec < 60
                ? $"{deltaSec:F3}s"
                : deltaSec < 3600
                    ? $"{deltaSec / 60.0:F2}m"
                    : $"{deltaSec / 3600.0:F2}h";

            double freq = _dualCursor.FrequencyHz;
            string freqStr = freq > 0.01 && freq < 10000 ? $" ({freq:F2} Hz)" : "";

            hudLines.Add(($"ΔT: {timeStr}{freqStr}", Color.White));

            foreach (var pen in _pens)
            {
                if (!pen.IsVisible) continue;
                double? valA = TrendDualCursor.GetValueAt(pen, _dualCursor.TimeA!.Value);
                double? valB = TrendDualCursor.GetValueAt(pen, _dualCursor.TimeB!.Value);
                if (valA.HasValue && valB.HasValue)
                {
                    double diff = valB.Value - valA.Value;
                    string sign = diff >= 0 ? "+" : "";
                    hudLines.Add(($"{pen.Name}: {valA.Value:F1} → {valB.Value:F1} (Δ {sign}{diff:F1} {pen.Unit})", pen.Color));
                }
            }

            int hudW = 240;
            int hudH = 14 + hudLines.Count * 17;
            var hudRect = new Rectangle(r.Right - hudW - 10, r.Top + 10, hudW, hudH);

            using (var bgBrush = new SolidBrush(Color.FromArgb(220, 15, 23, 42)))
            using (var borderPen = new Pen(Color.FromArgb(51, 65, 85), 1.2f))
            {
                g.FillRectangle(bgBrush, hudRect);
                g.DrawRectangle(borderPen, hudRect);
            }

            using var hudFont = new Font(Font.FontFamily, 8f, FontStyle.Regular);
            for (int i = 0; i < hudLines.Count; i++)
            {
                using var b = new SolidBrush(hudLines[i].color);
                g.DrawString(hudLines[i].text, hudFont, b, hudRect.X + 8, hudRect.Y + 6 + i * 17);
            }
        }

        #region Mouse & Zoom Interactions

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();

            var r = GetPlotArea();
            if (!r.Contains(e.Location) && e.X < r.Left && e.X > r.Right) return;

            double totalTicks = (_endTime - _startTime).Ticks;
            if (totalTicks <= 0) return;

            if (_dualCursor.Enabled)
            {
                float xA = (float)(r.Left + ((double)(_dualCursor.TimeA!.Value.Ticks - _startTime.Ticks) / totalTicks) * r.Width);
                float xB = (float)(r.Left + ((double)(_dualCursor.TimeB!.Value.Ticks - _startTime.Ticks) / totalTicks) * r.Width);

                if (Math.Abs(e.X - xA) <= 8)
                {
                    _isDraggingCursorA = true;
                    Capture = true;
                    return;
                }
                if (Math.Abs(e.X - xB) <= 8)
                {
                    _isDraggingCursorB = true;
                    Capture = true;
                    return;
                }
            }

            if (e.Button == MouseButtons.Right)
            {
                _isPanning = true;
                _lastPanPoint = e.Location;
                _isLiveFollow = false;
                Capture = true;
            }
            else if (e.Button == MouseButtons.Left && r.Contains(e.Location))
            {
                _isBoxZooming = true;
                _boxZoomStart = e.Location;
                _boxZoomRect = new Rectangle(e.Location, new Size(0, 0));
                Capture = true;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var r = GetPlotArea();
            _hoverPoint = e.Location;

            double totalTicks = (_endTime - _startTime).Ticks;

            if (_isDraggingCursorA)
            {
                double ratio = Math.Max(0.0, Math.Min(1.0, (double)(e.X - r.Left) / r.Width));
                _dualCursor.TimeA = _startTime.AddTicks((long)(ratio * totalTicks));
                CursorsChanged?.Invoke(this, EventArgs.Empty);
                Invalidate();
                return;
            }

            if (_isDraggingCursorB)
            {
                double ratio = Math.Max(0.0, Math.Min(1.0, (double)(e.X - r.Left) / r.Width));
                _dualCursor.TimeB = _startTime.AddTicks((long)(ratio * totalTicks));
                CursorsChanged?.Invoke(this, EventArgs.Empty);
                Invalidate();
                return;
            }

            if (_isPanning)
            {
                int dx = e.X - _lastPanPoint.X;
                _lastPanPoint = e.Location;

                double shiftSec = -(dx / (double)r.Width) * (_endTime - _startTime).TotalSeconds;
                _startTime = _startTime.AddSeconds(shiftSec);
                _endTime = _endTime.AddSeconds(shiftSec);
                TimeRangeChanged?.Invoke(this, EventArgs.Empty);
                Invalidate();
                return;
            }

            if (_isBoxZooming)
            {
                int x = Math.Min(_boxZoomStart.X, e.X);
                int y = Math.Min(_boxZoomStart.Y, e.Y);
                int w = Math.Abs(e.X - _boxZoomStart.X);
                int h = Math.Abs(e.Y - _boxZoomStart.Y);
                _boxZoomRect = new Rectangle(x, y, w, h);
                Invalidate();
                return;
            }

            // Update mouse cursor
            if (_dualCursor.Enabled)
            {
                float xA = (float)(r.Left + ((double)(_dualCursor.TimeA!.Value.Ticks - _startTime.Ticks) / totalTicks) * r.Width);
                float xB = (float)(r.Left + ((double)(_dualCursor.TimeB!.Value.Ticks - _startTime.Ticks) / totalTicks) * r.Width);

                if (Math.Abs(e.X - xA) <= 8 || Math.Abs(e.X - xB) <= 8)
                {
                    Cursor = Cursors.VSplit;
                    return;
                }
            }

            Cursor = Cursors.Cross;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            Capture = false;

            if (_isDraggingCursorA || _isDraggingCursorB)
            {
                _isDraggingCursorA = false;
                _isDraggingCursorB = false;
                return;
            }

            if (_isPanning)
            {
                _isPanning = false;
                return;
            }

            if (_isBoxZooming)
            {
                _isBoxZooming = false;
                var r = GetPlotArea();

                if (_boxZoomRect.Width >= 15)
                {
                    double totalSec = (_endTime - _startTime).TotalSeconds;
                    double startSec = Math.Max(0.0, (_boxZoomRect.Left - r.Left) / (double)r.Width) * totalSec;
                    double endSec = Math.Min(1.0, (_boxZoomRect.Right - r.Left) / (double)r.Width) * totalSec;

                    var newStart = _startTime.AddSeconds(startSec);
                    var newEnd = _startTime.AddSeconds(endSec);

                    if (newEnd > newStart.AddSeconds(1))
                    {
                        SetTimeRange(newStart, newEnd);
                    }
                }

                _boxZoomRect = Rectangle.Empty;
                Invalidate();
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            var r = GetPlotArea();
            if (!r.Contains(e.Location)) return;

            _isLiveFollow = false;
            double factor = e.Delta > 0 ? 0.75 : 1.33; // Zoom in or out

            double mouseRatio = (e.X - r.Left) / (double)r.Width;
            double currentSpanSec = (_endTime - _startTime).TotalSeconds;
            double newSpanSec = Math.Max(2.0, currentSpanSec * factor);

            DateTime centerTime = _startTime.AddSeconds(currentSpanSec * mouseRatio);
            _startTime = centerTime.AddSeconds(-newSpanSec * mouseRatio);
            _endTime = centerTime.AddSeconds(newSpanSec * (1.0 - mouseRatio));

            TimeRangeChanged?.Invoke(this, EventArgs.Empty);
            Invalidate();
        }

        #endregion
    
    private void OnThemeChanged(object sender, EventArgs e) => Invalidate();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ZeroTheme.ThemeChanged -= OnThemeChanged;
        }
        base.Dispose(disposing);
    }

}

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZTrendPlot"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("TrendPlot is deprecated and will be removed in 5 release cycles. Please migrate to ZTrendPlot instead.")]
    [ToolboxItem(false)]
    public class TrendPlot : ZTrendPlot
    {
    }

    #endregion
}
