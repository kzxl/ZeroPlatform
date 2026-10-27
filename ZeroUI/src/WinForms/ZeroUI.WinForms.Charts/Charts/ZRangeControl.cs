using System;

using ZeroUI.WinForms.Icons;using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;
using ZeroUI.Core.Localization;
using ZeroUI.Core.Range;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Charts
{
    /// <summary>
    /// High-performance visual timeline and numeric range selection control for WinForms.
    /// Provides interactive thumb sliding, range panning, zoom scaling, ruler scale,
    /// and background histogram/sparkline distribution visualization with rich configurable options.
    /// </summary>
    [ToolboxItem(true)]
    [ToolboxBitmap(typeof(ZeroIcons), "RangeControl.bmp")]
    public class ZRangeControl : Control
    {
        private readonly RangeControlModel _model = new RangeControlModel();
        private IRangeControlClient? _client;

        // Interaction state
        private RangeHitTestResult _dragTarget = RangeHitTestResult.None;
        private Point _dragStartPoint;
        private double _dragStartValue;
        private double _dragStartSelectedStart;
        private double _dragStartSelectedEnd;

        // Visual Layout Dimensions
        private const int ThumbWidth = 10;
        private const int RulerHeight = 22;
        private const int LabelHeight = 16;

        #region Configurable Toggle Properties

        /// <summary>
        /// Gets or sets whether the left and right sliding grip handles are visible.
        /// </summary>
        [Category("ZeroUI Behavior"), DefaultValue(true)]
        [Description("Enables or disables visual sliding thumbs.")]
        public bool ShowRangeThumbs { get; set; } = true;

        /// <summary>
        /// Gets or sets whether the background distribution graph (histogram/area/sparkline) is drawn.
        /// </summary>
        [Category("ZeroUI Appearance"), DefaultValue(true)]
        [Description("Enables or disables background distribution graph rendering.")]
        public bool ShowBackgroundGraph { get; set; } = true;

        /// <summary>
        /// Gets or sets the visual representation style of the background graph.
        /// </summary>
        [Category("ZeroUI Appearance"), DefaultValue(RangeGraphType.Area)]
        public RangeGraphType BackgroundGraphType { get; set; } = RangeGraphType.Area;

        /// <summary>
        /// Gets or sets whether the lower timeline/numeric ruler scale is displayed.
        /// </summary>
        [Category("ZeroUI Appearance"), DefaultValue(true)]
        public bool ShowRuler { get; set; } = true;

        /// <summary>
        /// Gets or sets whether the active selection span is highlighted.
        /// </summary>
        [Category("ZeroUI Appearance"), DefaultValue(true)]
        public bool ShowSelection { get; set; } = true;

        /// <summary>
        /// Gets or sets whether textual range values are displayed adjacent to thumbs.
        /// </summary>
        [Category("ZeroUI Appearance"), DefaultValue(true)]
        public bool ShowRangeLabels { get; set; } = true;

        /// <summary>
        /// Gets or sets whether mouse wheel zooming is permitted.
        /// </summary>
        [Category("ZeroUI Behavior"), DefaultValue(true)]
        public bool EnableZoom { get; set; } = true;

        /// <summary>
        /// Gets or sets whether dragging the selection body pans the entire active range.
        /// </summary>
        [Category("ZeroUI Behavior"), DefaultValue(true)]
        public bool EnablePan { get; set; } = true;

        /// <summary>
        /// Gets or sets whether thumb dragging snaps to configured intervals or steps.
        /// </summary>
        [Category("ZeroUI Behavior"), DefaultValue(false)]
        public bool SnapToInterval
        {
            get => _model.SnapToInterval;
            set => _model.SnapToInterval = value;
        }

        #endregion

        #region Model Domain Properties

        [Category("ZeroUI Data")]
        public RangeDataType DataType
        {
            get => _model.DataType;
            set { _model.DataType = value; Invalidate(); }
        }

        [Category("ZeroUI Data")]
        public RangeInterval Interval
        {
            get => _model.Interval;
            set { _model.Interval = value; Invalidate(); }
        }

        [Category("ZeroUI Data"), DefaultValue(1.0)]
        public double NumericStep
        {
            get => _model.NumericStep;
            set => _model.NumericStep = value;
        }

        [Category("ZeroUI Range")]
        public double TotalRangeStart
        {
            get => _model.TotalRangeStart;
            set { _model.TotalRangeStart = value; Invalidate(); }
        }

        [Category("ZeroUI Range")]
        public double TotalRangeEnd
        {
            get => _model.TotalRangeEnd;
            set { _model.TotalRangeEnd = value; Invalidate(); }
        }

        [Category("ZeroUI Range")]
        public double SelectedRangeStart
        {
            get => _model.SelectedRangeStart;
            set { _model.SelectedRangeStart = value; Invalidate(); }
        }

        [Category("ZeroUI Range")]
        public double SelectedRangeEnd
        {
            get => _model.SelectedRangeEnd;
            set { _model.SelectedRangeEnd = value; Invalidate(); }
        }

        [Category("ZeroUI Range")]
        public double VisibleRangeStart
        {
            get => _model.VisibleRangeStart;
            set { _model.VisibleRangeStart = value; Invalidate(); }
        }

        [Category("ZeroUI Range")]
        public double VisibleRangeEnd
        {
            get => _model.VisibleRangeEnd;
            set { _model.VisibleRangeEnd = value; Invalidate(); }
        }

        [Browsable(false)]
        public DateTime SelectedStartDate
        {
            get => _model.SelectedStartDate;
            set { _model.SelectedStartDate = value; Invalidate(); }
        }

        [Browsable(false)]
        public DateTime SelectedEndDate
        {
            get => _model.SelectedEndDate;
            set { _model.SelectedEndDate = value; Invalidate(); }
        }

        [Browsable(false)]
        public DateTime TotalStartDate
        {
            get => _model.TotalStartDate;
            set { _model.TotalStartDate = value; Invalidate(); }
        }

        [Browsable(false)]
        public DateTime TotalEndDate
        {
            get => _model.TotalEndDate;
            set { _model.TotalEndDate = value; Invalidate(); }
        }

        [Browsable(false)]
        public List<RangeDataPoint> DataPoints => _model.DataPoints;

        #endregion

        #region Custom Colors

        [Category("ZeroUI Appearance")]
        public Color? AccentColor { get; set; }

        [Category("ZeroUI Appearance")]
        public Color? GraphColor { get; set; }

        [Category("ZeroUI Appearance")]
        public Color? SelectionFillColor { get; set; }

        #endregion

        public event EventHandler<RangeChangedEventArgs>? RangeSelectionChanged;
        public event EventHandler<RangeChangedEventArgs>? VisibleRangeChanged;

        public ZRangeControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable, true);

            Size = new Size(400, 72);
            Font = new Font("Segoe UI", 9f);

            _model.RangeSelectionChanged += (s, e) =>
            {
                _client?.OnRangeSelectionChanged(e.Start, e.End);
                RangeSelectionChanged?.Invoke(this, e);
                Invalidate();
            };

            _model.VisibleRangeChanged += (s, e) =>
            {
                VisibleRangeChanged?.Invoke(this, e);
                Invalidate();
            };

            ZeroTheme.ThemeChanged += OnThemeChanged;
        }

        #region Data and Client Binding

        /// <summary>
        /// Gets or sets the bound client providing data and receiving range updates (e.g. Chart, DataGrid).
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IRangeControlClient? Client
        {
            get => _client;
            set => AttachClient(value!);
        }

        public void SetDataPoints(IEnumerable<RangeDataPoint> points)
        {
            _model.DataPoints.Clear();
            if (points != null)
            {
                _model.DataPoints.AddRange(points);
            }
            Invalidate();
        }

        public void AttachClient(IRangeControlClient client)
        {
            _client = client;
            if (client == null) return;

            DataType = client.DataType;
            var bounds = client.GetTotalRangeBounds();
            _model.SetTotalRange(bounds.Start, bounds.End);
            _model.SetVisibleRange(bounds.Start, bounds.End);
            _model.SelectAll();

            var points = client.GetDataPoints();
            if (points != null)
            {
                SetDataPoints(points);
            }
            Invalidate();
        }

        #endregion

        #region Paint Rendering

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            bool isDark = ZeroTheme.IsDark;
            var palette = ZeroTheme.Colors;

            Color bgColor = BackColor != SystemColors.Control ? BackColor : (isDark ? Color.FromArgb(24, 27, 32) : Color.FromArgb(248, 250, 252));
            Color borderColor = isDark ? Color.FromArgb(48, 54, 61) : Color.FromArgb(226, 232, 240);
            Color accent = AccentColor ?? (isDark ? Color.FromArgb(56, 139, 253) : Color.FromArgb(14, 116, 144));
            Color graphCol = GraphColor ?? (isDark ? Color.FromArgb(56, 139, 253) : Color.FromArgb(14, 116, 144));
            Color textColor = ForeColor != SystemColors.ControlText ? ForeColor : (isDark ? Color.FromArgb(230, 237, 243) : Color.FromArgb(30, 41, 59));
            Color mutedText = isDark ? Color.FromArgb(139, 148, 158) : Color.FromArgb(100, 116, 139);

            // Fill background
            using (var brush = new SolidBrush(bgColor))
            {
                g.FillRectangle(brush, ClientRectangle);
            }

            // Draw outer border
            using (var pen = new Pen(borderColor))
            {
                g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            }

            Rectangle trackRect = GetTrackRectangle();
            if (trackRect.Width <= 0 || trackRect.Height <= 0) return;

            // 1. Draw Background Graph
            if (ShowBackgroundGraph && _model.DataPoints.Count > 0)
            {
                DrawBackgroundGraph(g, trackRect, graphCol, isDark);
            }

            // 2. Draw Selection Overlay & Unselected Dimming
            float selStartX = (float)_model.ValueToPixel(_model.SelectedRangeStart, trackRect.Width) + trackRect.X;
            float selEndX = (float)_model.ValueToPixel(_model.SelectedRangeEnd, trackRect.Width) + trackRect.X;

            if (ShowSelection)
            {
                // Dim unselected left
                if (selStartX > trackRect.Left)
                {
                    using (var dimBrush = new SolidBrush(Color.FromArgb(isDark ? 110 : 80, isDark ? 10 : 220, isDark ? 12 : 225, isDark ? 16 : 230)))
                    {
                        g.FillRectangle(dimBrush, trackRect.Left, trackRect.Top, selStartX - trackRect.Left, trackRect.Height);
                    }
                }

                // Dim unselected right
                if (selEndX < trackRect.Right)
                {
                    using (var dimBrush = new SolidBrush(Color.FromArgb(isDark ? 110 : 80, isDark ? 10 : 220, isDark ? 12 : 225, isDark ? 16 : 230)))
                    {
                        g.FillRectangle(dimBrush, selEndX, trackRect.Top, trackRect.Right - selEndX, trackRect.Height);
                    }
                }

                // Highlight selected range
                Color selFill = SelectionFillColor ?? Color.FromArgb(isDark ? 45 : 35, accent.R, accent.G, accent.B);
                using (var selBrush = new SolidBrush(selFill))
                {
                    g.FillRectangle(selBrush, selStartX, trackRect.Top, Math.Max(1, selEndX - selStartX), trackRect.Height);
                }

                // Selected range top and bottom border accents
                using (var pen = new Pen(Color.FromArgb(160, accent.R, accent.G, accent.B), 1.5f))
                {
                    g.DrawLine(pen, selStartX, trackRect.Top, selEndX, trackRect.Top);
                    g.DrawLine(pen, selStartX, trackRect.Bottom, selEndX, trackRect.Bottom);
                }
            }

            // 3. Draw Ruler Scale
            if (ShowRuler)
            {
                DrawRulerScale(g, trackRect, textColor, mutedText, borderColor, isDark);
            }

            // 4. Draw Sliding Thumbs
            if (ShowRangeThumbs)
            {
                DrawThumbHandle(g, selStartX, trackRect, accent, isDark, isLeft: true);
                DrawThumbHandle(g, selEndX, trackRect, accent, isDark, isLeft: false);
            }

            // 5. Draw Range Labels
            if (ShowRangeLabels)
            {
                DrawRangeLabels(g, selStartX, selEndX, trackRect, textColor, isDark);
            }
        }

        private void DrawBackgroundGraph(Graphics g, Rectangle trackRect, Color graphColor, bool isDark)
        {
            var points = _model.DataPoints;
            if (points.Count == 0) return;

            double maxVal = 1e-6;
            for (int i = 0; i < points.Count; i++)
            {
                if (points[i].Value > maxVal) maxVal = points[i].Value;
            }

            if (BackgroundGraphType == RangeGraphType.Histogram)
            {
                int barWidth = Math.Max(2, (int)(trackRect.Width / Math.Max(1, points.Count)) - 1);
                using (var barBrush = new SolidBrush(Color.FromArgb(isDark ? 120 : 160, graphColor.R, graphColor.G, graphColor.B)))
                {
                    for (int i = 0; i < points.Count; i++)
                    {
                        float x = (float)_model.ValueToPixel(points[i].Argument, trackRect.Width) + trackRect.X;
                        if (x < trackRect.Left - barWidth || x > trackRect.Right + barWidth) continue;

                        float heightRatio = (float)(points[i].Value / maxVal);
                        float barH = trackRect.Height * heightRatio;
                        float y = trackRect.Bottom - barH;

                        g.FillRectangle(barBrush, x - barWidth / 2f, y, barWidth, barH);
                    }
                }
            }
            else
            {
                // Area or Line
                var polygonPoints = new List<PointF>();
                polygonPoints.Add(new PointF(trackRect.Left, trackRect.Bottom));

                for (int i = 0; i < points.Count; i++)
                {
                    float x = (float)_model.ValueToPixel(points[i].Argument, trackRect.Width) + trackRect.X;
                    float heightRatio = (float)(points[i].Value / maxVal);
                    float y = trackRect.Bottom - (trackRect.Height * heightRatio);
                    polygonPoints.Add(new PointF(x, y));
                }

                polygonPoints.Add(new PointF(trackRect.Right, trackRect.Bottom));

                if (polygonPoints.Count >= 3)
                {
                    if (BackgroundGraphType == RangeGraphType.Area)
                    {
                        using (var path = new GraphicsPath())
                        {
                            path.AddLines(polygonPoints.ToArray());
                            using (var fillBrush = new LinearGradientBrush(
                                       new PointF(0, trackRect.Top),
                                       new PointF(0, trackRect.Bottom),
                                       Color.FromArgb(isDark ? 140 : 100, graphColor.R, graphColor.G, graphColor.B),
                                       Color.FromArgb(isDark ? 20 : 10, graphColor.R, graphColor.G, graphColor.B)))
                            {
                                g.FillPath(fillBrush, path);
                            }
                        }
                    }

                    // Stroke top line
                    var linePoints = new PointF[points.Count];
                    for (int i = 0; i < points.Count; i++)
                    {
                        float x = (float)_model.ValueToPixel(points[i].Argument, trackRect.Width) + trackRect.X;
                        float heightRatio = (float)(points[i].Value / maxVal);
                        linePoints[i] = new PointF(x, trackRect.Bottom - (trackRect.Height * heightRatio));
                    }

                    using (var strokePen = new Pen(graphColor, 1.5f))
                    {
                        g.DrawLines(strokePen, linePoints);
                    }
                }
            }
        }

        private void DrawRulerScale(Graphics g, Rectangle trackRect, Color textColor, Color mutedText, Color borderColor, bool isDark)
        {
            int rulerTop = trackRect.Bottom + 1;
            int rulerBottom = Height - 2;

            // Ruler horizontal baseline
            using (var linePen = new Pen(borderColor))
            {
                g.DrawLine(linePen, trackRect.Left, rulerTop, trackRect.Right, rulerTop);
            }

            int numTicks = Math.Max(2, trackRect.Width / 80);
            double stepValue = _model.VisibleRangeSpan / numTicks;

            using (var tickPen = new Pen(borderColor))
            using (var font = new Font(Font.FontFamily, 7.5f))
            using (var textBrush = new SolidBrush(mutedText))
            {
                for (int i = 0; i <= numTicks; i++)
                {
                    double tickVal = _model.VisibleRangeStart + i * stepValue;
                    float x = (float)_model.ValueToPixel(tickVal, trackRect.Width) + trackRect.X;
                    if (x < trackRect.Left || x > trackRect.Right) continue;

                    g.DrawLine(tickPen, x, rulerTop, x, rulerTop + 4);

                    string label = FormatValue(tickVal, isShort: true);
                    var size = g.MeasureString(label, font);
                    float textX = x - size.Width / 2f;
                    if (textX < 2) textX = 2;
                    if (textX + size.Width > Width - 2) textX = Width - 2 - size.Width;

                    g.DrawString(label, font, textBrush, textX, rulerTop + 5);
                }
            }
        }

        private void DrawThumbHandle(Graphics g, float centerX, Rectangle trackRect, Color accent, bool isDark, bool isLeft)
        {
            float w = ThumbWidth;
            float h = trackRect.Height + 8;
            float x = centerX - w / 2f;
            float y = trackRect.Top - 4;

            var thumbRect = new RectangleF(x, y, w, h);

            // Thumb shadow/pill body
            using (var path = CreateRoundedRectanglePath(thumbRect, 3f))
            {
                using (var fillBrush = new SolidBrush(isDark ? Color.FromArgb(33, 38, 45) : Color.White))
                {
                    g.FillPath(fillBrush, path);
                }

                using (var strokePen = new Pen(accent, 1.8f))
                {
                    g.DrawPath(strokePen, path);
                }
            }

            // 2 center grip pips
            float pipX = centerX;
            float pipY = y + h / 2f;
            using (var gripPen = new Pen(accent, 1.2f))
            {
                g.DrawLine(gripPen, pipX - 1.5f, pipY - 4, pipX - 1.5f, pipY + 4);
                g.DrawLine(gripPen, pipX + 1.5f, pipY - 4, pipX + 1.5f, pipY + 4);
            }
        }

        private void DrawRangeLabels(Graphics g, float startX, float endX, Rectangle trackRect, Color textColor, bool isDark)
        {
            using (var font = new Font(Font.FontFamily, 8f, FontStyle.Bold))
            using (var brush = new SolidBrush(textColor))
            {
                string startText = FormatValue(_model.SelectedRangeStart, isShort: false);
                string endText = FormatValue(_model.SelectedRangeEnd, isShort: false);

                var startSize = g.MeasureString(startText, font);
                var endSize = g.MeasureString(endText, font);

                float y = trackRect.Top + 2;
                float labelStartX = Math.Max(trackRect.Left + 4, startX + 6);
                float labelEndX = Math.Min(trackRect.Right - endSize.Width - 4, endX - endSize.Width - 6);

                // If labels don't collide
                if (labelStartX + startSize.Width + 8 < labelEndX)
                {
                    g.DrawString(startText, font, brush, labelStartX, y);
                    g.DrawString(endText, font, brush, labelEndX, y);
                }
                else
                {
                    // Combined span label
                    string spanText = $"{startText} - {endText}";
                    var spanSize = g.MeasureString(spanText, font);
                    float midX = (startX + endX - spanSize.Width) / 2f;
                    midX = Math.Max(trackRect.Left + 4, Math.Min(trackRect.Right - spanSize.Width - 4, midX));
                    g.DrawString(spanText, font, brush, midX, y);
                }
            }
        }

        #endregion

        #region Mouse & Keyboard Interaction

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;

            Focus();
            var hit = HitTest(e.Location);
            _dragTarget = hit;
            _dragStartPoint = e.Location;
            _dragStartValue = _model.PixelToValue(e.X - GetTrackRectangle().X, GetTrackRectangle().Width);
            _dragStartSelectedStart = _model.SelectedRangeStart;
            _dragStartSelectedEnd = _model.SelectedRangeEnd;

            Capture = true;
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            var trackRect = GetTrackRectangle();
            if (_dragTarget == RangeHitTestResult.None)
            {
                // Update cursor
                var hit = HitTest(e.Location);
                switch (hit)
                {
                    case RangeHitTestResult.LeftThumb:
                    case RangeHitTestResult.RightThumb:
                        Cursor = Cursors.SizeWE;
                        break;
                    case RangeHitTestResult.SelectionRange:
                        Cursor = EnablePan ? Cursors.SizeAll : Cursors.Default;
                        break;
                    default:
                        Cursor = Cursors.Default;
                        break;
                }
                return;
            }

            double curVal = _model.PixelToValue(e.X - trackRect.X, trackRect.Width);

            if (_dragTarget == RangeHitTestResult.LeftThumb && ShowRangeThumbs)
            {
                _model.SelectedRangeStart = Math.Min(curVal, _model.SelectedRangeEnd);
            }
            else if (_dragTarget == RangeHitTestResult.RightThumb && ShowRangeThumbs)
            {
                _model.SelectedRangeEnd = Math.Max(curVal, _model.SelectedRangeStart);
            }
            else if (_dragTarget == RangeHitTestResult.SelectionRange && EnablePan)
            {
                double delta = curVal - _dragStartValue;
                double span = _dragStartSelectedEnd - _dragStartSelectedStart;
                double newStart = _dragStartSelectedStart + delta;
                double newEnd = _dragStartSelectedEnd + delta;

                if (newStart < _model.TotalRangeStart)
                {
                    newStart = _model.TotalRangeStart;
                    newEnd = newStart + span;
                }
                else if (newEnd > _model.TotalRangeEnd)
                {
                    newEnd = _model.TotalRangeEnd;
                    newStart = newEnd - span;
                }

                _model.SetSelectedRange(newStart, newEnd);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _dragTarget = RangeHitTestResult.None;
            Capture = false;
            Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (!EnableZoom) return;

            var trackRect = GetTrackRectangle();
            double ratio = (double)(e.X - trackRect.X) / Math.Max(1, trackRect.Width);
            double factor = e.Delta > 0 ? 1.25 : 0.8;
            _model.Zoom(factor, ratio);
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            // Double click resets visible range and selects all
            _model.ResetVisibleRange();
            _model.SelectAll();
        }

        #endregion

        #region Helper Methods

        private Rectangle GetTrackRectangle()
        {
            int top = 4;
            int bottom = Height - (ShowRuler ? RulerHeight : 4);
            int left = 8;
            int right = Width - 8;

            return new Rectangle(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
        }

        private RangeHitTestResult HitTest(Point pt)
        {
            var trackRect = GetTrackRectangle();
            float selStartX = (float)_model.ValueToPixel(_model.SelectedRangeStart, trackRect.Width) + trackRect.X;
            float selEndX = (float)_model.ValueToPixel(_model.SelectedRangeEnd, trackRect.Width) + trackRect.X;

            float thumbHitPadding = 7f;

            if (ShowRangeThumbs)
            {
                if (Math.Abs(pt.X - selStartX) <= thumbHitPadding && pt.Y >= trackRect.Top - 6 && pt.Y <= trackRect.Bottom + 6)
                {
                    return RangeHitTestResult.LeftThumb;
                }
                if (Math.Abs(pt.X - selEndX) <= thumbHitPadding && pt.Y >= trackRect.Top - 6 && pt.Y <= trackRect.Bottom + 6)
                {
                    return RangeHitTestResult.RightThumb;
                }
            }

            if (ShowSelection && pt.X >= selStartX && pt.X <= selEndX && pt.Y >= trackRect.Top && pt.Y <= trackRect.Bottom)
            {
                return RangeHitTestResult.SelectionRange;
            }

            return RangeHitTestResult.Background;
        }

        private string FormatValue(double val, bool isShort)
        {
            if (DataType == RangeDataType.DateTime)
            {
                try
                {
                    DateTime dt = DateTime.FromOADate(val);
                    return isShort ? dt.ToString("MM/dd") : dt.ToString("yyyy-MM-dd");
                }
                catch
                {
                    return val.ToString("F1");
                }
            }
            return val.ToString("F1");
        }

        private static GraphicsPath CreateRoundedRectanglePath(RectangleF rect, float radius)
        {
            var path = new GraphicsPath();
            float d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
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

    /// <summary>
    /// Backward-compatible alias for RangeControl.
    /// </summary>
    [Obsolete("ZeroRangeControl is deprecated and will be removed in 5 release cycles. Please migrate to ZRangeControl instead.")]
    [ToolboxItem(false)]
    public class ZeroRangeControl : ZRangeControl
    {
    }

    /// <summary>
    /// Semantic alias specialized for temporal date and time interval sliding.
    /// </summary>
    public class DateTimeRangeSlider : ZRangeControl
    {
        public DateTimeRangeSlider()
        {
            DataType = RangeDataType.DateTime;
            Interval = RangeInterval.Day;
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZRangeControl"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("RangeControl is deprecated and will be removed in 5 release cycles. Please migrate to ZRangeControl instead.")]
    [ToolboxItem(false)]
    public class RangeControl : ZRangeControl
    {
    }

    #endregion
}
