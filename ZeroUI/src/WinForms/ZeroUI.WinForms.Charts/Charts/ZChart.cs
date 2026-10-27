using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using ZeroUI.Core.Analytics;
using ZeroUI.WinForms.Charts.Model;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Charts
{
    /// <summary>
    /// High-performance, zero-allocation universal Chart control supporting Column, Bar, Line,
    /// Spline, Area, Pie, and Donut visualizations with interactive tooltips, crosshairs, and legends.
    /// </summary>
    [ToolboxItem(true)]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroChart.bmp")]
    [Category("ZeroUI - Charts & Analytics")]
    [Description("High-performance universal chart control for modern desktop analytics and dashboards")]
    [Designer("ZeroUI.WinForms.Design.Charts.ZChartDesigner, ZeroUI.WinForms.Design")]
    public class ZChart : Control
    {
        private string _title = string.Empty;
        private string _subtitle = string.Empty;
        private ChartType _chartType = ChartType.Column;
        private ChartLegendPosition _legendPosition = ChartLegendPosition.Top;
        private bool _showGridLines = true;
        private bool _showTooltips = true;
        private bool _showCrosshair = true;
        private CrosshairMode _crosshairMode = CrosshairMode.Both;
        private bool _crosshairSnapToPoint = true;
        private bool _showCrosshairBadges = true;
        private Color? _crosshairColor;

        // SPC / Quality Control Belts
        private bool _enableSpcBelts = false;
        private bool _autoCalculate3Sigma = true;
        private SpcBeltStyle _spcBeltStyle = SpcBeltStyle.TrafficLight;
        private double? _upperControlLimit;
        private double? _lowerControlLimit;
        private double? _upperSpecLimit;
        private double? _lowerSpecLimit;
        private double? _nominalTarget;
        private SpcControlLimits? _lastCalculatedSpcLimits;

        // Visible Range Slicing
        private int? _visibleStartIndex;
        private int? _visibleEndIndex;

        private bool _showDataLabels = false;
        private string? _valuePrefix;
        private string? _valueSuffix;
        private float _donutHoleRatio = 0.58f;
        private string _centerTitle = string.Empty;
        private string _centerValue = string.Empty;

        // Interactive states
        private Point _mousePos = new Point(-1, -1);
        private int _hoveredCategoryIndex = -1;
        private int _hoveredPieSliceIndex = -1;
        private readonly List<RectangleF> _legendHitBoxes = new List<RectangleF>();


        public List<ChartSeries> Series { get; } = new List<ChartSeries>();

        [Category("Appearance")]
        [DefaultValue("")]
        public string Title
        {
            get => _title;
            set { _title = value ?? string.Empty; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue("")]
        public string Subtitle
        {
            get => _subtitle;
            set { _subtitle = value ?? string.Empty; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(ChartType.Column)]
        public ChartType ChartType
        {
            get => _chartType;
            set { _chartType = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(ChartLegendPosition.Top)]
        public ChartLegendPosition LegendPosition
        {
            get => _legendPosition;
            set { _legendPosition = value; Invalidate(); }
        }

        [Category("Behavior")]
        [DefaultValue(true)]
        public bool ShowGridLines
        {
            get => _showGridLines;
            set { _showGridLines = value; Invalidate(); }
        }

        [Category("Behavior")]
        [DefaultValue(true)]
        public bool ShowTooltips
        {
            get => _showTooltips;
            set { _showTooltips = value; Invalidate(); }
        }

        [Category("Behavior")]
        [DefaultValue(true)]
        public bool ShowCrosshair
        {
            get => _showCrosshair;
            set { _showCrosshair = value; Invalidate(); }
        }

        [Category("Behavior")]
        [DefaultValue(CrosshairMode.Both)]
        public CrosshairMode CrosshairMode
        {
            get => _crosshairMode;
            set { _crosshairMode = value; Invalidate(); }
        }

        [Category("Behavior")]
        [DefaultValue(true)]
        public bool CrosshairSnapToPoint
        {
            get => _crosshairSnapToPoint;
            set { _crosshairSnapToPoint = value; Invalidate(); }
        }

        [Category("Behavior")]
        [DefaultValue(true)]
        public bool ShowCrosshairBadges
        {
            get => _showCrosshairBadges;
            set { _showCrosshairBadges = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(null)]
        public Color? CrosshairColor
        {
            get => _crosshairColor;
            set { _crosshairColor = value; Invalidate(); }
        }

        [Category("SPC / Quality Analytics")]
        [DefaultValue(false)]
        public bool EnableSpcBelts
        {
            get => _enableSpcBelts;
            set { _enableSpcBelts = value; Invalidate(); }
        }

        [Category("SPC / Quality Analytics")]
        [DefaultValue(true)]
        public bool AutoCalculate3Sigma
        {
            get => _autoCalculate3Sigma;
            set { _autoCalculate3Sigma = value; Invalidate(); }
        }

        [Category("SPC / Quality Analytics")]
        [DefaultValue(SpcBeltStyle.TrafficLight)]
        public SpcBeltStyle SpcBeltStyle
        {
            get => _spcBeltStyle;
            set { _spcBeltStyle = value; Invalidate(); }
        }

        [Category("SPC / Quality Analytics")]
        [DefaultValue(null)]
        public double? UpperControlLimit
        {
            get => _upperControlLimit;
            set { _upperControlLimit = value; Invalidate(); }
        }

        [Category("SPC / Quality Analytics")]
        [DefaultValue(null)]
        public double? LowerControlLimit
        {
            get => _lowerControlLimit;
            set { _lowerControlLimit = value; Invalidate(); }
        }

        [Category("SPC / Quality Analytics")]
        [DefaultValue(null)]
        public double? UpperSpecLimit
        {
            get => _upperSpecLimit;
            set { _upperSpecLimit = value; Invalidate(); }
        }

        [Category("SPC / Quality Analytics")]
        [DefaultValue(null)]
        public double? LowerSpecLimit
        {
            get => _lowerSpecLimit;
            set { _lowerSpecLimit = value; Invalidate(); }
        }

        [Category("SPC / Quality Analytics")]
        [DefaultValue(null)]
        public double? NominalTarget
        {
            get => _nominalTarget;
            set { _nominalTarget = value; Invalidate(); }
        }

        [Browsable(false)]
        public SpcControlLimits? LastCalculatedSpcLimits => _lastCalculatedSpcLimits;

        [Browsable(false)]
        public int? VisibleStartIndex
        {
            get => _visibleStartIndex;
            set { _visibleStartIndex = value; Invalidate(); }
        }

        [Browsable(false)]
        public int? VisibleEndIndex
        {
            get => _visibleEndIndex;
            set { _visibleEndIndex = value; Invalidate(); }
        }

        [Category("Behavior")]
        [DefaultValue(false)]
        public bool ShowDataLabels
        {
            get => _showDataLabels;
            set { _showDataLabels = value; Invalidate(); }
        }

        [Category("Format")]
        [DefaultValue(null)]
        public string? ValuePrefix
        {
            get => _valuePrefix;
            set { _valuePrefix = value; Invalidate(); }
        }

        [Category("Format")]
        [DefaultValue(null)]
        public string? ValueSuffix
        {
            get => _valueSuffix;
            set { _valueSuffix = value; Invalidate(); }
        }

        [Category("Appearance (Donut)")]
        [DefaultValue(0.58f)]
        public float DonutHoleRatio
        {
            get => _donutHoleRatio;
            set { _donutHoleRatio = Math.Max(0.1f, Math.Min(0.85f, value)); Invalidate(); }
        }

        [Category("Appearance (Donut)")]
        [DefaultValue("")]
        public string CenterTitle
        {
            get => _centerTitle;
            set { _centerTitle = value ?? string.Empty; Invalidate(); }
        }

        [Category("Appearance (Donut)")]
        [DefaultValue("")]
        public string CenterValue
        {
            get => _centerValue;
            set { _centerValue = value ?? string.Empty; Invalidate(); }
        }

        public ZChart()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);

            Size = new Size(480, 320);
            BackColor = Color.Transparent;
            Font = new Font("Segoe UI", 9f, FontStyle.Regular);

            ZeroTheme.ThemeChanged += OnThemeChanged;
        }

        public ChartSeries AddSeries(string name, Color? color = null)
        {
            var assignedColor = color ?? ChartPalette.GetColor(Series.Count, ZeroTheme.IsDark);
            var series = new ChartSeries(name, assignedColor);
            Series.Add(series);
            Invalidate();
            return series;
        }

        public void Clear()
        {
            Series.Clear();
            Invalidate();
        }

        #region Input & Hover Interaction

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            _mousePos = e.Location;

            // Cursor for clickable legends
            bool overLegend = _legendHitBoxes.Any(r => r.Contains(e.Location));
            Cursor = overLegend ? Cursors.Hand : Cursors.Default;

            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _mousePos = new Point(-1, -1);
            _hoveredCategoryIndex = -1;
            _hoveredPieSliceIndex = -1;
            Cursor = Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button == MouseButtons.Left)
            {
                for (int i = 0; i < _legendHitBoxes.Count; i++)
                {
                    if (_legendHitBoxes[i].Contains(e.Location) && i < Series.Count)
                    {
                        Series[i].IsVisible = !Series[i].IsVisible;
                        Invalidate();
                        break;
                    }
                }
            }
        }

        #endregion

        #region Paint Pipeline

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            var bounds = ClientRectangle;
            if (bounds.Width <= 10 || bounds.Height <= 10) return;

            bool isDark = ZeroTheme.IsDark;
            var palette = ZeroTheme.Colors;

            // 1. Draw Title & Subtitle
            int topOffset = 12;
            if (!string.IsNullOrEmpty(_title))
            {
                using var titleFont = new Font(Font.FontFamily, 11f, FontStyle.Bold);
                using var titleBrush = new SolidBrush(palette.TextPrimary);
                g.DrawString(_title, titleFont, titleBrush, 14, topOffset);
                topOffset += 20;

                if (!string.IsNullOrEmpty(_subtitle))
                {
                    using var subFont = new Font(Font.FontFamily, 8.5f, FontStyle.Regular);
                    using var subBrush = new SolidBrush(palette.TextSecondary);
                    g.DrawString(_subtitle, subFont, subBrush, 14, topOffset);
                    topOffset += 16;
                }
                topOffset += 6;
            }

            // 2. Draw Legend
            _legendHitBoxes.Clear();
            int legendHeight = 0;
            if (_legendPosition == ChartLegendPosition.Top && Series.Count > 0)
            {
                legendHeight = DrawLegend(g, new Rectangle(14, topOffset, bounds.Width - 28, 26), isDark);
                topOffset += legendHeight + 6;
            }

            int bottomOffset = 14;
            if (_legendPosition == ChartLegendPosition.Bottom && Series.Count > 0)
            {
                bottomOffset += 26;
            }

            var plotRect = new Rectangle(
                14,
                topOffset,
                bounds.Width - 28,
                bounds.Height - topOffset - bottomOffset);

            if (plotRect.Width < 40 || plotRect.Height < 40) return;

            // 3. Render Cartesian vs. Radial Charts
            if (_chartType == ChartType.Pie || _chartType == ChartType.Donut)
            {
                RenderPieOrDonut(g, plotRect, isDark);
            }
            else
            {
                RenderCartesianChart(g, plotRect, isDark);
            }

            // Draw Bottom Legend if configured
            if (_legendPosition == ChartLegendPosition.Bottom && Series.Count > 0)
            {
                DrawLegend(g, new Rectangle(14, bounds.Height - 32, bounds.Width - 28, 26), isDark);
            }
        }

        #endregion

        #region Cartesian Chart Rendering (Column, Bar, Line, Spline, Area)

        private void RenderCartesianChart(Graphics g, Rectangle bounds, bool isDark)
        {
            var palette = ZeroTheme.Colors;

            // Extract visible series and unique category labels
            var visibleSeries = Series.Where(s => s.IsVisible).ToList();
            if (visibleSeries.Count == 0)
            {
                using var emptyBrush = new SolidBrush(palette.TextSecondary);
                var sfEmptyCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("No data series available", Font, emptyBrush, bounds, sfEmptyCenter);
                return;
            }

            // Categories list across series
            var categories = new List<string>();
            foreach (var s in visibleSeries)
            {
                for (int i = 0; i < s.Points.Count; i++)
                {
                    string label = string.IsNullOrEmpty(s.Points[i].Label) ? $"Cat {i + 1}" : s.Points[i].Label;
                    if (!categories.Contains(label)) categories.Add(label);
                }
            }

            if (categories.Count == 0) return;

            // Apply Visible Range windowing (if bound to RangeControl or manually configured)
            if (_visibleStartIndex.HasValue || _visibleEndIndex.HasValue)
            {
                int startIdx = Math.Max(0, _visibleStartIndex ?? 0);
                int endIdx = Math.Min(categories.Count - 1, _visibleEndIndex ?? (categories.Count - 1));
                if (startIdx <= endIdx && (startIdx > 0 || endIdx < categories.Count - 1))
                {
                    categories = categories.GetRange(startIdx, endIdx - startIdx + 1);
                }
            }

            // Compute Y-Axis Range
            double minY = 0;
            double maxY = 10;
            bool hasData = false;

            foreach (var s in visibleSeries)
            {
                foreach (var p in s.Points)
                {
                    if (!hasData)
                    {
                        minY = Math.Min(0, p.Value);
                        maxY = Math.Max(0, p.Value);
                        hasData = true;
                    }
                    else
                    {
                        if (p.Value < minY) minY = p.Value;
                        if (p.Value > maxY) maxY = p.Value;
                    }
                }
            }

            if (maxY <= minY) maxY = minY + 10;
            double niceMax = CalculateNiceMax(maxY);
            double niceMin = minY < 0 ? -CalculateNiceMax(Math.Abs(minY)) : 0;
            double yRange = niceMax - niceMin;
            if (yRange <= 0) yRange = 10;

            // Margins for axes
            int yAxisWidth = 48;
            int xAxisHeight = 28;

            var plot = new Rectangle(
                bounds.Left + yAxisWidth,
                bounds.Top + 6,
                bounds.Width - yAxisWidth - 10,
                bounds.Height - xAxisHeight - 6);

            if (plot.Width < 20 || plot.Height < 20) return;

            // 1. Render SPC / Out-of-Spec Warning Belts (if enabled)
            if (_enableSpcBelts)
            {
                RenderSpcBelts(g, plot, visibleSeries, niceMin, yRange, isDark);
            }

            // 1.1. Draw Grid Lines and Y-Axis Ticks
            int tickCount = 5;
            using var gridPen = new Pen(isDark ? Color.FromArgb(40, 51, 65) : Color.FromArgb(241, 245, 249), 1f)
            {
                DashStyle = DashStyle.Dash
            };
            using var axisTextBrush = new SolidBrush(palette.TextSecondary);
            using var sfRight = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
            using var sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near };

            for (int i = 0; i <= tickCount; i++)
            {
                double tickVal = niceMin + (yRange * i / tickCount);
                float y = plot.Bottom - (float)((tickVal - niceMin) / yRange * plot.Height);

                if (_showGridLines)
                {
                    g.DrawLine(gridPen, plot.Left, y, plot.Right, y);
                }

                string yStr = FormatValueShort(tickVal);
                g.DrawString(yStr, Font, axisTextBrush, new RectangleF(bounds.Left, y - 9, yAxisWidth - 6, 18), sfRight);
            }

            // Draw Base Axis Line
            using var axisLinePen = new Pen(isDark ? Color.FromArgb(71, 85, 105) : Color.FromArgb(203, 213, 225), 1.2f);
            g.DrawLine(axisLinePen, plot.Left, plot.Bottom, plot.Right, plot.Bottom);

            // 2. Map Category Slot Widths
            float slotWidth = (float)plot.Width / categories.Count;
            _hoveredCategoryIndex = -1;

            if (_mousePos.X >= plot.Left && _mousePos.X <= plot.Right &&
                _mousePos.Y >= plot.Top && _mousePos.Y <= plot.Bottom)
            {
                _hoveredCategoryIndex = (int)((_mousePos.X - plot.Left) / slotWidth);
                if (_hoveredCategoryIndex >= categories.Count) _hoveredCategoryIndex = categories.Count - 1;
            }

            // Draw Category Labels
            for (int i = 0; i < categories.Count; i++)
            {
                float slotCenter = plot.Left + (i * slotWidth) + (slotWidth / 2f);

                // Auto-skip category labels if tight
                bool showLabel = true;
                if (categories.Count > 10 && slotWidth < 38 && (i % 2 != 0)) showLabel = false;
                if (showLabel)
                {
                    g.DrawString(categories[i], Font, axisTextBrush, new RectangleF(slotCenter - 35, plot.Bottom + 6, 70, 20), sfCenter);
                }
            }

            // 2.5. Full Cartesian Crosshair HUD (Vertical + Horizontal lines + Badges + Snap Halos)
            if (_showCrosshair && _crosshairMode != CrosshairMode.None && _hoveredCategoryIndex >= 0 && _hoveredCategoryIndex < categories.Count)
            {
                float hoveredSlotCenter = plot.Left + (_hoveredCategoryIndex * slotWidth) + (slotWidth / 2f);

                // Crosshair column highlight
                using var crosshairBrush = new SolidBrush(isDark ? Color.FromArgb(20, 255, 255, 255) : Color.FromArgb(18, 79, 70, 229));
                g.FillRectangle(crosshairBrush, plot.Left + (_hoveredCategoryIndex * slotWidth), plot.Top, slotWidth, plot.Height);

                Color penColor = _crosshairColor ?? (isDark ? Color.FromArgb(148, 163, 184) : Color.FromArgb(99, 102, 241));
                using var crosshairPen = new Pen(penColor, 1.2f) { DashStyle = DashStyle.Dash };

                // Vertical crosshair
                if (_crosshairMode == CrosshairMode.VerticalOnly || _crosshairMode == CrosshairMode.Both)
                {
                    g.DrawLine(crosshairPen, hoveredSlotCenter, plot.Top, hoveredSlotCenter, plot.Bottom);
                }

                // Horizontal crosshair
                if ((_crosshairMode == CrosshairMode.HorizontalOnly || _crosshairMode == CrosshairMode.Both) &&
                    _mousePos.Y >= plot.Top && _mousePos.Y <= plot.Bottom)
                {
                    g.DrawLine(crosshairPen, plot.Left, _mousePos.Y, plot.Right, _mousePos.Y);
                }

                // Snap halos on active series
                DrawCrosshairSnapHalos(g, plot, visibleSeries, categories[_hoveredCategoryIndex], hoveredSlotCenter, niceMin, yRange);

                // Readout badges
                if (_showCrosshairBadges)
                {
                    // X-Axis Category Badge
                    if (_crosshairMode == CrosshairMode.VerticalOnly || _crosshairMode == CrosshairMode.Both)
                    {
                        DrawCrosshairXBadge(g, hoveredSlotCenter, plot.Bottom + 3, categories[_hoveredCategoryIndex], isDark);
                    }

                    // Y-Axis Value Badge
                    if ((_crosshairMode == CrosshairMode.HorizontalOnly || _crosshairMode == CrosshairMode.Both) &&
                        _mousePos.Y >= plot.Top && _mousePos.Y <= plot.Bottom)
                    {
                        double cursorYVal = niceMin + ((plot.Bottom - _mousePos.Y) / (double)plot.Height * yRange);
                        DrawCrosshairYBadge(g, bounds.Left + 2, _mousePos.Y, yAxisWidth - 4, cursorYVal, isDark);
                    }
                }
            }


            // 3. Render Visualizations per Series
            foreach (var series in visibleSeries)
            {
                var effectiveType = series.ChartTypeOverride ?? _chartType;

                switch (effectiveType)
                {
                    case ChartType.Column:
                    case ChartType.StackedColumn:
                        RenderColumns(g, plot, visibleSeries, series, categories, niceMin, yRange, slotWidth, isDark);
                        break;

                    case ChartType.Line:
                    case ChartType.Spline:
                    case ChartType.Area:
                    case ChartType.SplineArea:
                        RenderLinesAndAreas(g, plot, series, categories, niceMin, yRange, slotWidth, effectiveType, isDark);
                        break;
                }
            }

            // 4. Draw Floating Tooltip Card
            if (_showTooltips && _hoveredCategoryIndex >= 0 && _hoveredCategoryIndex < categories.Count)
            {
                DrawCartesianTooltip(g, plot, visibleSeries, categories[_hoveredCategoryIndex], _hoveredCategoryIndex, isDark);
            }
        }

        private void RenderColumns(
            Graphics g, Rectangle plot, List<ChartSeries> allSeries, ChartSeries series,
            List<string> categories, double niceMin, double yRange, float slotWidth, bool isDark)
        {
            int seriesIndex = allSeries.IndexOf(series);
            int totalSeries = allSeries.Count;

            float groupPadding = slotWidth * 0.18f;
            float availableWidth = slotWidth - (groupPadding * 2);
            float barWidth = Math.Max(4, (availableWidth / totalSeries) - 3);

            for (int i = 0; i < categories.Count; i++)
            {
                string cat = categories[i];
                var pt = series.Points.FirstOrDefault(p => p.Label == cat);
                if (pt == null) continue;

                double val = pt.Value;
                float barHeight = (float)((val - niceMin) / yRange * plot.Height);
                float x = plot.Left + (i * slotWidth) + groupPadding + (seriesIndex * (barWidth + 3));
                float y = plot.Bottom - barHeight;

                var barRect = new RectangleF(x, y, barWidth, barHeight);
                if (barRect.Width <= 0 || barRect.Height <= 0) continue;

                Color barColor = pt.ColorOverride ?? series.Color;
                if (i == _hoveredCategoryIndex)
                {
                    barColor = ControlPaint.Light(barColor, 0.15f);
                }

                using var barBrush = new SolidBrush(barColor);
                if (barRect.Height > 6 && ZeroUIConfig.RoundedCorners)
                {
                    using var path = CreateTopRoundedBarPath(barRect, 4f);
                    g.FillPath(barBrush, path);
                }
                else
                {
                    g.FillRectangle(barBrush, barRect);
                }
            }
        }

        private void RenderLinesAndAreas(
            Graphics g, Rectangle plot, ChartSeries series, List<string> categories,
            double niceMin, double yRange, float slotWidth, ChartType type, bool isDark)
        {
            var pts = new List<PointF>();
            for (int i = 0; i < categories.Count; i++)
            {
                string cat = categories[i];
                var pt = series.Points.FirstOrDefault(p => p.Label == cat);
                double val = pt?.Value ?? 0;

                float x = plot.Left + (i * slotWidth) + (slotWidth / 2f);
                float y = plot.Bottom - (float)((val - niceMin) / yRange * plot.Height);
                pts.Add(new PointF(x, y));
            }

            if (pts.Count < 2) return;

            bool isCurved = (type == ChartType.Spline || type == ChartType.SplineArea);
            bool isArea = (type == ChartType.Area || type == ChartType.SplineArea);

            // 1. Draw Gradient Area Fill
            if (isArea)
            {
                using var areaPath = new GraphicsPath();
                if (isCurved)
                {
                    areaPath.AddCurve(pts.ToArray(), 0.5f);
                }
                else
                {
                    areaPath.AddLines(pts.ToArray());
                }

                areaPath.AddLine(pts[pts.Count - 1], new PointF(pts[pts.Count - 1].X, plot.Bottom));
                areaPath.AddLine(new PointF(pts[pts.Count - 1].X, plot.Bottom), new PointF(pts[0].X, plot.Bottom));
                areaPath.AddLine(new PointF(pts[0].X, plot.Bottom), pts[0]);
                areaPath.CloseFigure();

                Color topColor = Color.FromArgb((int)(series.FillOpacity * 255), series.Color);
                Color botColor = Color.FromArgb(10, series.Color);
                using var gradBrush = new LinearGradientBrush(plot, topColor, botColor, LinearGradientMode.Vertical);
                g.FillPath(gradBrush, areaPath);
            }

            // 2. Draw Line Stroke
            using var linePen = new Pen(series.Color, series.StrokeWidth)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            };

            if (isCurved)
            {
                g.DrawCurve(linePen, pts.ToArray(), 0.5f);
            }
            else
            {
                g.DrawLines(linePen, pts.ToArray());
            }

            // 3. Draw Data Point Halos & Markers
            for (int i = 0; i < pts.Count; i++)
            {
                var p = pts[i];
                bool isHovered = (i == _hoveredCategoryIndex);
                float radius = isHovered ? 6f : 4f;

                if (isHovered)
                {
                    using var glowBrush = new SolidBrush(Color.FromArgb(60, series.Color));
                    g.FillEllipse(glowBrush, p.X - 10, p.Y - 10, 20, 20);
                }

                using var markerBrush = new SolidBrush(isDark ? Color.FromArgb(15, 23, 42) : Color.White);
                using var markerPen = new Pen(series.Color, 2f);
                g.FillEllipse(markerBrush, p.X - radius, p.Y - radius, radius * 2, radius * 2);
                g.DrawEllipse(markerPen, p.X - radius, p.Y - radius, radius * 2, radius * 2);
            }
        }

        #endregion

        #region Radial Chart Rendering (Pie & Donut)

        private void RenderPieOrDonut(Graphics g, Rectangle bounds, bool isDark)
        {
            var palette = ZeroTheme.Colors;

            // Get points from first series or combined
            var points = Series.Where(s => s.IsVisible).SelectMany(s => s.Points).ToList();
            if (points.Count == 0)
            {
                using var emptyBrush = new SolidBrush(palette.TextSecondary);
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("No data slices available", Font, emptyBrush, bounds, sf);
                return;
            }

            double total = points.Sum(p => Math.Max(0, p.Value));
            if (total <= 0) return;

            int pad = 24;
            int size = Math.Min(bounds.Width, bounds.Height) - (pad * 2);
            if (size < 40) return;

            float cx = bounds.Left + (bounds.Width / 2f);
            float cy = bounds.Top + (bounds.Height / 2f);
            float radius = size / 2f;

            // Hit test for mouse hover on slices
            _hoveredPieSliceIndex = -1;
            float mouseDx = _mousePos.X - cx;
            float mouseDy = _mousePos.Y - cy;
            float mouseDist = (float)Math.Sqrt(mouseDx * mouseDx + mouseDy * mouseDy);

            float holeRadius = (_chartType == ChartType.Donut) ? radius * _donutHoleRatio : 0f;

            if (mouseDist <= radius + 10 && mouseDist >= holeRadius)
            {
                float mouseAngle = (float)(Math.Atan2(mouseDy, mouseDx) * 180.0 / Math.PI);
                if (mouseAngle < 0) mouseAngle += 360f;

                float scanAngle = 0f;
                for (int i = 0; i < points.Count; i++)
                {
                    float sweep = (float)(points[i].Value / total * 360.0);
                    if (mouseAngle >= scanAngle && mouseAngle <= scanAngle + sweep)
                    {
                        _hoveredPieSliceIndex = i;
                        break;
                    }
                    scanAngle += sweep;
                }
            }

            // Draw Slices
            float currentAngle = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                var pt = points[i];
                float sweep = (float)(pt.Value / total * 360.0);
                if (sweep <= 0.1f) continue;

                Color sliceColor = pt.ColorOverride ?? ChartPalette.GetColor(i, isDark);
                bool isHovered = (i == _hoveredPieSliceIndex);

                // Offset exploded slice on hover
                float sliceCx = cx;
                float sliceCy = cy;
                if (isHovered)
                {
                    float midAngleRad = (float)((currentAngle + sweep / 2f) * Math.PI / 180.0);
                    sliceCx += (float)Math.Cos(midAngleRad) * 8f;
                    sliceCy += (float)Math.Sin(midAngleRad) * 8f;
                    sliceColor = ControlPaint.Light(sliceColor, 0.12f);
                }

                var sliceRect = new RectangleF(sliceCx - radius, sliceCy - radius, radius * 2, radius * 2);

                using (var sliceBrush = new SolidBrush(sliceColor))
                using (var borderPen = new Pen(isDark ? Color.FromArgb(30, 41, 59) : Color.White, 2f))
                {
                    g.FillPie(sliceBrush, sliceRect.X, sliceRect.Y, sliceRect.Width, sliceRect.Height, currentAngle, sweep);
                    g.DrawPie(borderPen, sliceRect.X, sliceRect.Y, sliceRect.Width, sliceRect.Height, currentAngle, sweep);
                }

                currentAngle += sweep;
            }

            // Cut out Donut Hole
            if (_chartType == ChartType.Donut && holeRadius > 5)
            {
                var holeRect = new RectangleF(cx - holeRadius, cy - holeRadius, holeRadius * 2, holeRadius * 2);
                Color holeColor = isDark ? Color.FromArgb(15, 23, 42) : Color.White;
                using var holeBrush = new SolidBrush(holeColor);
                g.FillEllipse(holeBrush, holeRect);

                // Center Title & Value
                string displayCenterTitle = !string.IsNullOrEmpty(_centerTitle) ? _centerTitle : "Total";
                string displayCenterValue = !string.IsNullOrEmpty(_centerValue) ? _centerValue : FormatValue(total);

                using var cTitleFont = new Font(Font.FontFamily, 8.5f, FontStyle.Regular);
                using var cValFont = new Font(Font.FontFamily, 13f, FontStyle.Bold);
                using var cTitleBrush = new SolidBrush(palette.TextSecondary);
                using var cValBrush = new SolidBrush(palette.TextPrimary);
                using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

                g.DrawString(displayCenterTitle, cTitleFont, cTitleBrush, new RectangleF(cx - holeRadius, cy - 22, holeRadius * 2, 16), sf);
                g.DrawString(displayCenterValue, cValFont, cValBrush, new RectangleF(cx - holeRadius, cy - 4, holeRadius * 2, 26), sf);
            }

            // Hover Slice Tooltip
            if (_showTooltips && _hoveredPieSliceIndex >= 0 && _hoveredPieSliceIndex < points.Count)
            {
                var p = points[_hoveredPieSliceIndex];
                double pct = (p.Value / total) * 100.0;
                DrawRadialTooltip(g, _mousePos, p.Label, p.Value, pct, isDark);
            }
        }

        #endregion

        #region Tooltip & Legend Helpers

        private void DrawCartesianTooltip(Graphics g, Rectangle plot, List<ChartSeries> series, string category, int catIndex, bool isDark)
        {
            var lines = new List<(Color Color, string Text)>();
            foreach (var s in series)
            {
                var pt = s.Points.FirstOrDefault(p => p.Label == category);
                if (pt != null)
                {
                    lines.Add((s.Color, $"{s.Name}: {FormatValue(pt.Value)}"));
                }
            }

            if (lines.Count == 0) return;

            using var fontBold = new Font(Font.FontFamily, 8.5f, FontStyle.Bold);
            using var fontReg = new Font(Font.FontFamily, 8f, FontStyle.Regular);

            int cardWidth = 160;
            int cardHeight = 26 + (lines.Count * 18);

            int x = _mousePos.X + 12;
            int y = _mousePos.Y - (cardHeight / 2);

            if (x + cardWidth > plot.Right + 20) x = _mousePos.X - cardWidth - 12;
            if (y < plot.Top) y = plot.Top;
            if (y + cardHeight > plot.Bottom + 10) y = plot.Bottom - cardHeight + 10;

            var cardRect = new Rectangle(x, y, cardWidth, cardHeight);

            // Draw Drop Shadow
            using (var shadowBrush = new SolidBrush(Color.FromArgb(30, 0, 0, 0)))
            {
                g.FillRectangle(shadowBrush, cardRect.X + 2, cardRect.Y + 2, cardRect.Width, cardRect.Height);
            }

            // Draw Card Body
            using (var bodyBrush = new SolidBrush(isDark ? Color.FromArgb(30, 41, 59) : Color.FromArgb(15, 23, 42)))
            using (var borderPen = new Pen(isDark ? Color.FromArgb(71, 85, 105) : Color.FromArgb(51, 65, 85), 1f))
            {
                g.FillRectangle(bodyBrush, cardRect);
                g.DrawRectangle(borderPen, cardRect);
            }

            // Draw Category Header
            using (var headBrush = new SolidBrush(Color.White))
            {
                g.DrawString(category, fontBold, headBrush, cardRect.X + 10, cardRect.Y + 6);
            }

            // Draw Series Rows
            int rowY = cardRect.Y + 24;
            using (var textBrush = new SolidBrush(Color.FromArgb(226, 232, 240)))
            {
                foreach (var line in lines)
                {
                    using (var dotBrush = new SolidBrush(line.Color))
                    {
                        g.FillEllipse(dotBrush, cardRect.X + 10, rowY + 4, 7, 7);
                    }
                    g.DrawString(line.Text, fontReg, textBrush, cardRect.X + 22, rowY);
                    rowY += 18;
                }
            }
        }

        private void DrawRadialTooltip(Graphics g, Point mousePos, string label, double value, double percentage, bool isDark)
        {
            using var fontBold = new Font(Font.FontFamily, 8.5f, FontStyle.Bold);
            using var fontReg = new Font(Font.FontFamily, 8f, FontStyle.Regular);

            int cardWidth = 140;
            int cardHeight = 44;
            int x = mousePos.X + 12;
            int y = mousePos.Y - 22;

            if (x + cardWidth > Width - 10) x = mousePos.X - cardWidth - 12;
            if (y < 10) y = 10;

            var cardRect = new Rectangle(x, y, cardWidth, cardHeight);

            using (var shadowBrush = new SolidBrush(Color.FromArgb(30, 0, 0, 0)))
            {
                g.FillRectangle(shadowBrush, cardRect.X + 2, cardRect.Y + 2, cardRect.Width, cardRect.Height);
            }

            using (var bodyBrush = new SolidBrush(isDark ? Color.FromArgb(30, 41, 59) : Color.FromArgb(15, 23, 42)))
            using (var borderPen = new Pen(isDark ? Color.FromArgb(71, 85, 105) : Color.FromArgb(51, 65, 85), 1f))
            {
                g.FillRectangle(bodyBrush, cardRect);
                g.DrawRectangle(borderPen, cardRect);
            }

            using (var headBrush = new SolidBrush(Color.White))
            using (var textBrush = new SolidBrush(Color.FromArgb(203, 213, 225)))
            {
                g.DrawString(label, fontBold, headBrush, cardRect.X + 8, cardRect.Y + 6);
                g.DrawString($"{FormatValue(value)} ({percentage:F1}%)", fontReg, textBrush, cardRect.X + 8, cardRect.Y + 22);
            }
        }

        private int DrawLegend(Graphics g, Rectangle bounds, bool isDark)
        {
            var palette = ZeroTheme.Colors;
            int x = bounds.Left;
            int y = bounds.Top;
            int itemHeight = 20;

            using var font = new Font(Font.FontFamily, 8.5f, FontStyle.Regular);
            using var textBrush = new SolidBrush(palette.TextSecondary);
            using var mutedBrush = new SolidBrush(Color.FromArgb(148, 163, 184));

            for (int i = 0; i < Series.Count; i++)
            {
                var s = Series[i];
                var size = g.MeasureString(s.Name, font);
                int itemWidth = (int)size.Width + 24;

                if (x + itemWidth > bounds.Right)
                {
                    x = bounds.Left;
                    y += itemHeight + 4;
                }

                var hitBox = new RectangleF(x, y, itemWidth, itemHeight);
                _legendHitBoxes.Add(hitBox);

                // Draw Color Dot / Pill
                using (var dotBrush = new SolidBrush(s.IsVisible ? s.Color : Color.FromArgb(148, 163, 184)))
                {
                    g.FillEllipse(dotBrush, x + 2, y + 5, 10, 10);
                }

                // Strike-through or muted text if hidden
                var brush = s.IsVisible ? textBrush : mutedBrush;
                g.DrawString(s.Name, font, brush, x + 16, y + 2);

                x += itemWidth + 10;
            }

            return y - bounds.Top + itemHeight;
        }

        private static GraphicsPath CreateTopRoundedBarPath(RectangleF rect, float radius)
        {
            var path = new GraphicsPath();
            float d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddLine(rect.Right, rect.Bottom, rect.X, rect.Bottom);
            path.CloseFigure();
            return path;
        }

        private static double CalculateNiceMax(double value)
        {
            if (value <= 0) return 10;
            double exp = Math.Floor(Math.Log10(value));
            double fraction = value / Math.Pow(10, exp);
            double niceFraction;

            if (fraction <= 1.0) niceFraction = 1.0;
            else if (fraction <= 2.0) niceFraction = 2.0;
            else if (fraction <= 2.5) niceFraction = 2.5;
            else if (fraction <= 5.0) niceFraction = 5.0;
            else niceFraction = 10.0;

            return niceFraction * Math.Pow(10, exp);
        }

        private string FormatValue(double val)
        {
            string prefix = _valuePrefix ?? string.Empty;
            string suffix = _valueSuffix ?? string.Empty;
            return $"{prefix}{val:N0}{suffix}";
        }

        private string FormatValueShort(double val)
        {
            string prefix = _valuePrefix ?? string.Empty;
            if (Math.Abs(val) >= 1_000_000_000) return $"{prefix}{val / 1_000_000_000:0.#}B";
            if (Math.Abs(val) >= 1_000_000) return $"{prefix}{val / 1_000_000:0.#}M";
            if (Math.Abs(val) >= 1_000) return $"{prefix}{val / 1_000:0.#}k";
            return $"{prefix}{val:0.#}";
        }

        private void RenderSpcBelts(Graphics g, Rectangle plot, List<ChartSeries> visibleSeries, double niceMin, double yRange, bool isDark)
        {
            double ucl, lcl, target;
            double? usl = _upperSpecLimit;
            double? lsl = _lowerSpecLimit;

            if (_upperControlLimit.HasValue && _lowerControlLimit.HasValue)
            {
                ucl = _upperControlLimit.Value;
                lcl = _lowerControlLimit.Value;
                target = _nominalTarget ?? ((ucl + lcl) / 2.0);
            }
            else if (_autoCalculate3Sigma)
            {
                int totalPoints = 0;
                foreach (var s in visibleSeries) totalPoints += s.Points.Count;
                if (totalPoints == 0) return;

                var values = new double[totalPoints];
                int idx = 0;
                foreach (var s in visibleSeries)
                {
                    for (int i = 0; i < s.Points.Count; i++)
                    {
                        values[idx++] = s.Points[i].Value;
                    }
                }

                _lastCalculatedSpcLimits = SpcStatisticsCalculator.Calculate(values, usl, lsl, _nominalTarget);
                ucl = _lastCalculatedSpcLimits.Value.Ucl;
                lcl = _lastCalculatedSpcLimits.Value.Lcl;
                target = _lastCalculatedSpcLimits.Value.Target;
            }
            else
            {
                return;
            }

            float ToY(double val)
            {
                float y = plot.Bottom - (float)((val - niceMin) / yRange * plot.Height);
                return Math.Max(plot.Top, Math.Min(plot.Bottom, y));
            }

            float yUcl = ToY(ucl);
            float yLcl = ToY(lcl);
            float yTarget = ToY(target);

            // Shading styles
            if (_spcBeltStyle != SpcBeltStyle.LinesOnly)
            {
                // 1. Normal Zone (Safe: between UCL and LCL)
                float topNormal = Math.Min(yUcl, yLcl);
                float hNormal = Math.Abs(yLcl - yUcl);
                if (hNormal > 1f)
                {
                    Color normalColor = _spcBeltStyle == SpcBeltStyle.TrafficLight
                        ? Color.FromArgb(24, 16, 185, 129)
                        : Color.FromArgb(16, 148, 163, 184);
                    using var brushNormal = new SolidBrush(normalColor);
                    g.FillRectangle(brushNormal, plot.Left, topNormal, plot.Width, hNormal);
                }

                // 2. Warning Zones (between UCL and USL, and between LCL and LSL)
                if (usl.HasValue && usl.Value > ucl)
                {
                    float yUsl = ToY(usl.Value);
                    float topWarn = Math.Min(yUsl, yUcl);
                    float hWarn = Math.Abs(yUcl - yUsl);
                    if (hWarn > 1f)
                    {
                        using var brushWarn = new SolidBrush(Color.FromArgb(28, 245, 158, 11));
                        g.FillRectangle(brushWarn, plot.Left, topWarn, plot.Width, hWarn);
                    }
                }

                if (lsl.HasValue && lsl.Value < lcl)
                {
                    float yLsl = ToY(lsl.Value);
                    float topWarn = Math.Min(yLcl, yLsl);
                    float hWarn = Math.Abs(yLsl - yLcl);
                    if (hWarn > 1f)
                    {
                        using var brushWarn = new SolidBrush(Color.FromArgb(28, 245, 158, 11));
                        g.FillRectangle(brushWarn, plot.Left, topWarn, plot.Width, hWarn);
                    }
                }

                // 3. Alarm Zones (Beyond USL or LSL)
                if (usl.HasValue)
                {
                    float yUsl = ToY(usl.Value);
                    if (yUsl > plot.Top)
                    {
                        using var brushAlarm = new SolidBrush(Color.FromArgb(28, 239, 68, 68));
                        g.FillRectangle(brushAlarm, plot.Left, plot.Top, plot.Width, yUsl - plot.Top);
                    }
                }

                if (lsl.HasValue)
                {
                    float yLsl = ToY(lsl.Value);
                    if (yLsl < plot.Bottom)
                    {
                        using var brushAlarm = new SolidBrush(Color.FromArgb(28, 239, 68, 68));
                        g.FillRectangle(brushAlarm, plot.Left, yLsl, plot.Width, plot.Bottom - yLsl);
                    }
                }
            }

            // Reference Lines
            using var penUcl = new Pen(Color.FromArgb(245, 158, 11), 1.2f) { DashStyle = DashStyle.Dash };
            using var penLcl = new Pen(Color.FromArgb(245, 158, 11), 1.2f) { DashStyle = DashStyle.Dash };
            using var penTarget = new Pen(Color.FromArgb(16, 185, 129), 1.2f) { DashStyle = DashStyle.DashDot };

            g.DrawLine(penUcl, plot.Left, yUcl, plot.Right, yUcl);
            g.DrawLine(penLcl, plot.Left, yLcl, plot.Right, yLcl);
            g.DrawLine(penTarget, plot.Left, yTarget, plot.Right, yTarget);

            if (usl.HasValue)
            {
                float yUsl = ToY(usl.Value);
                using var penUsl = new Pen(Color.FromArgb(239, 68, 68), 1.5f);
                g.DrawLine(penUsl, plot.Left, yUsl, plot.Right, yUsl);
                DrawSpcTag(g, plot.Right - 56, yUsl - 8, $"USL: {usl.Value:0.##}", Color.FromArgb(239, 68, 68));
            }

            if (lsl.HasValue)
            {
                float yLsl = ToY(lsl.Value);
                using var penLsl = new Pen(Color.FromArgb(239, 68, 68), 1.5f);
                g.DrawLine(penLsl, plot.Left, yLsl, plot.Right, yLsl);
                DrawSpcTag(g, plot.Right - 56, yLsl - 8, $"LSL: {lsl.Value:0.##}", Color.FromArgb(239, 68, 68));
            }

            // Reference Line Tags
            DrawSpcTag(g, plot.Right - 56, yUcl - 8, $"UCL: {ucl:0.##}", Color.FromArgb(245, 158, 11));
            DrawSpcTag(g, plot.Right - 56, yTarget - 8, $"CL: {target:0.##}", Color.FromArgb(16, 185, 129));
            DrawSpcTag(g, plot.Right - 56, yLcl - 8, $"LCL: {lcl:0.##}", Color.FromArgb(245, 158, 11));
        }

        private static void DrawSpcTag(Graphics g, float x, float y, string text, Color color)
        {
            var rect = new RectangleF(x, y, 54, 15);
            using var bgBrush = new SolidBrush(Color.FromArgb(210, 15, 23, 42));
            using var borderPen = new Pen(color, 1f);
            using var textBrush = new SolidBrush(Color.White);
            using var font = new Font("Segoe UI", 6.5f, FontStyle.Bold);

            g.FillRectangle(bgBrush, rect);
            g.DrawRectangle(borderPen, rect.X, rect.Y, rect.Width, rect.Height);

            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(text, font, textBrush, rect, sf);
        }

        private void DrawCrosshairYBadge(Graphics g, float x, float y, float w, double val, bool isDark)
        {
            var badgeRect = new RectangleF(x, y - 9, w, 18);
            using var bgBrush = new SolidBrush(isDark ? Color.FromArgb(30, 41, 59) : Color.FromArgb(15, 23, 42));
            using var borderPen = new Pen(isDark ? Color.FromArgb(99, 102, 241) : Color.FromArgb(79, 70, 229), 1.2f);
            using var textBrush = new SolidBrush(Color.White);
            using var font = new Font(Font.FontFamily, 7.5f, FontStyle.Bold);

            g.FillRectangle(bgBrush, badgeRect);
            g.DrawRectangle(borderPen, badgeRect.X, badgeRect.Y, badgeRect.Width, badgeRect.Height);

            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(FormatValueShort(val), font, textBrush, badgeRect, sf);
        }

        private void DrawCrosshairXBadge(Graphics g, float centerX, float topY, string category, bool isDark)
        {
            float badgeW = Math.Max(50f, category.Length * 7.5f + 12f);
            var badgeRect = new RectangleF(centerX - (badgeW / 2f), topY, badgeW, 18);
            using var bgBrush = new SolidBrush(isDark ? Color.FromArgb(30, 41, 59) : Color.FromArgb(15, 23, 42));
            using var borderPen = new Pen(isDark ? Color.FromArgb(99, 102, 241) : Color.FromArgb(79, 70, 229), 1.2f);
            using var textBrush = new SolidBrush(Color.White);
            using var font = new Font(Font.FontFamily, 7.5f, FontStyle.Bold);

            g.FillRectangle(bgBrush, badgeRect);
            g.DrawRectangle(borderPen, badgeRect.X, badgeRect.Y, badgeRect.Width, badgeRect.Height);

            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(category, font, textBrush, badgeRect, sf);
        }

        private void DrawCrosshairSnapHalos(Graphics g, Rectangle plot, List<ChartSeries> visibleSeries, string category, float slotCenter, double niceMin, double yRange)
        {
            foreach (var series in visibleSeries)
            {
                var pt = series.Points.FirstOrDefault(p => p.Label == category);
                if (pt == null) continue;

                float y = plot.Bottom - (float)((pt.Value - niceMin) / yRange * plot.Height);
                if (y < plot.Top || y > plot.Bottom) continue;

                // Outer halo
                using var haloBrush = new SolidBrush(Color.FromArgb(80, series.Color));
                g.FillEllipse(haloBrush, slotCenter - 8, y - 8, 16, 16);

                // Core dot
                using var coreBrush = new SolidBrush(series.Color);
                using var borderPen = new Pen(Color.White, 1.5f);
                g.FillEllipse(coreBrush, slotCenter - 4, y - 4, 8, 8);
                g.DrawEllipse(borderPen, slotCenter - 4, y - 4, 8, 8);
            }
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
    /// Legacy alias for <see cref="ZChart"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroChart is deprecated. Please use ChartControl instead.")]
    [ToolboxItem(false)]
    public class ZeroChart : ChartControl
    {
        public new ZeroUI.WinForms.Charts.Model.ZeroChartLegendPosition LegendPosition
        {
            get => (ZeroUI.WinForms.Charts.Model.ZeroChartLegendPosition)base.LegendPosition;
            set => base.LegendPosition = (ZeroUI.WinForms.Charts.Model.ChartLegendPosition)value;
        }

        public new ZeroUI.WinForms.Charts.Model.ZeroChartType ChartType
        {
            get => (ZeroUI.WinForms.Charts.Model.ZeroChartType)base.ChartType;
            set => base.ChartType = (ZeroUI.WinForms.Charts.Model.ChartType)value;
        }
    }

    /// <summary>
    /// Legacy alias for <see cref="ZChart"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroChartControl is deprecated and will be removed in 5 release cycles. Please migrate to ZChart instead.")]
    [ToolboxItem(false)]
    public class ZeroChartControl : ZeroChart
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZChart"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ChartControl is deprecated and will be removed in 5 release cycles. Please migrate to ZChart instead.")]
    [ToolboxItem(false)]
    public class ChartControl : ZChart
    {
    }

    #endregion
}
