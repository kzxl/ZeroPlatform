using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;
using ZeroCharts.Axis;
using ZeroCharts.DataModels;
using ZeroCharts.Interaction;

namespace ZeroCharts.Controls
{
    /// <summary>
    /// Hardware-accelerated industrial telemetry and analytical chart control for WinForms.
    /// Supports Line, Area, Bar, Candlestick, Scatter, Pie/Doughnut, Radar, Heatmap, and Gantt charts.
    /// </summary>
    public class ZeroChartControl : Control
    {
        public ChartAxis XAxis { get; } = new ChartAxis("X Axis");
        public ChartAxis YAxis { get; } = new ChartAxis("Y Axis");
        public CategoryAxis CategoryAxis { get; } = new CategoryAxis("Categories");

        // Series Collections
        public List<LineSeries> LineSeriesList { get; } = new List<LineSeries>();
        public List<AreaSeries> AreaSeriesList { get; } = new List<AreaSeries>();
        public List<BarSeries> BarSeriesList { get; } = new List<BarSeries>();
        public List<CandlestickSeries> CandlestickSeriesList { get; } = new List<CandlestickSeries>();
        public List<ScatterSeries> ScatterSeriesList { get; } = new List<ScatterSeries>();
        public List<PieSeries> PieSeriesList { get; } = new List<PieSeries>();
        public List<RadarSeries> RadarSeriesList { get; } = new List<RadarSeries>();
        public List<GanttSeries> GanttSeriesList { get; } = new List<GanttSeries>();
        public HeatmapSeries? ActiveHeatmap { get; set; }

        public ChartLegend Legend { get; } = new ChartLegend();

        public bool ShowCrosshair { get; set; } = true;
        public bool ShowGrid { get; set; } = true;
        public bool ShowTooltip { get; set; } = true;

        private Point _mousePos = new Point(-1, -1);
        private bool _isPanning = false;
        private Point _panStartPoint;

        public RectangleF PlotArea { get; private set; }

        public ZeroChartControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);

            BackColor = ChartPalette.BackgroundDark;
            ForeColor = ChartPalette.AxisText;
            Font = new Font("Segoe UI", 9f, FontStyle.Regular);
        }

        #region Add Series Helpers

        public void AddSeries(LineSeries series)
        {
            if (series != null) { LineSeriesList.Add(series); AutoFit(); }
        }

        public void AddSeries(AreaSeries series)
        {
            if (series != null) { AreaSeriesList.Add(series); AutoFit(); }
        }

        public void AddSeries(BarSeries series)
        {
            if (series != null) { BarSeriesList.Add(series); AutoFit(); }
        }

        public void AddSeries(CandlestickSeries series)
        {
            if (series != null) { CandlestickSeriesList.Add(series); AutoFit(); }
        }

        public void AddSeries(ScatterSeries series)
        {
            if (series != null) { ScatterSeriesList.Add(series); AutoFit(); }
        }

        public void AddSeries(PieSeries series)
        {
            if (series != null) { PieSeriesList.Add(series); Invalidate(); }
        }

        public void AddSeries(RadarSeries series)
        {
            if (series != null) { RadarSeriesList.Add(series); Invalidate(); }
        }

        public void AddSeries(GanttSeries series)
        {
            if (series != null) { GanttSeriesList.Add(series); AutoFit(); }
        }

        #endregion

        public void AutoFit()
        {
            double minX = double.MaxValue, maxX = double.MinValue;
            double minY = double.MaxValue, maxY = double.MinValue;

            foreach (var ls in LineSeriesList)
            {
                if (ls.Count > 0 && ls.IsVisible)
                {
                    if (ls.MinX < minX) minX = ls.MinX;
                    if (ls.MaxX > maxX) maxX = ls.MaxX;
                    if (ls.MinY < minY) minY = ls.MinY;
                    if (ls.MaxY > maxY) maxY = ls.MaxY;
                }
            }

            foreach (var as_ in AreaSeriesList)
            {
                if (as_.Count > 0 && as_.IsVisible)
                {
                    if (as_.MinX < minX) minX = as_.MinX;
                    if (as_.MaxX > maxX) maxX = as_.MaxX;
                    if (as_.MinY < minY) minY = as_.MinY;
                    if (as_.MaxY > maxY) maxY = as_.MaxY;
                    if (as_.Baseline < minY) minY = as_.Baseline;
                }
            }

            foreach (var sc in ScatterSeriesList)
            {
                if (sc.Count > 0 && sc.IsVisible)
                {
                    if (sc.MinX < minX) minX = sc.MinX;
                    if (sc.MaxX > maxX) maxX = sc.MaxX;
                    if (sc.MinY < minY) minY = sc.MinY;
                    if (sc.MaxY > maxY) maxY = sc.MaxY;
                }
            }

            // Sync categories for Bar series
            var catSet = new List<string>();
            foreach (var bs in BarSeriesList)
            {
                if (bs.Count > 0 && bs.IsVisible)
                {
                    if (bs.MinValue < minY) minY = bs.MinValue;
                    if (bs.MaxValue > maxY) maxY = bs.MaxValue;

                    for (int i = 0; i < bs.Count; i++)
                    {
                        string c = bs.Items[i].Category;
                        if (!catSet.Contains(c)) catSet.Add(c);
                    }
                }
            }
            if (catSet.Count > 0)
            {
                CategoryAxis.SetCategories(catSet);
                if (minX == double.MaxValue)
                {
                    minX = 0;
                    maxX = Math.Max(1, catSet.Count);
                }
            }

            foreach (var cs in CandlestickSeriesList)
            {
                if (cs.Count > 0 && cs.IsVisible)
                {
                    if (cs.MinPrice < minY) minY = cs.MinPrice;
                    if (cs.MaxPrice > maxY) maxY = cs.MaxPrice;
                    double tMin = cs.MinTime.Ticks;
                    double tMax = cs.MaxTime.Ticks;
                    if (tMin < minX) minX = tMin;
                    if (tMax > maxX) maxX = tMax;
                }
            }

            if (minX < maxX) XAxis.SetRange(minX, maxX);
            if (minY < maxY)
            {
                double margin = (maxY - minY) * 0.06;
                YAxis.SetRange(minY - margin, maxY + margin);
            }

            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdatePlotArea();
        }

        private void UpdatePlotArea()
        {
            float paddingLeft = 60f;
            float paddingBottom = 40f;
            float paddingTop = 20f;
            float paddingRight = 20f;

            if (Legend.Placement == LegendPlacement.Top) paddingTop += 28f;
            else if (Legend.Placement == LegendPlacement.Bottom) paddingBottom += 28f;
            else if (Legend.Placement == LegendPlacement.Right) paddingRight += 120f;
            else if (Legend.Placement == LegendPlacement.Left) paddingLeft += 120f;

            PlotArea = new RectangleF(
                paddingLeft,
                paddingTop,
                Math.Max(10f, Width - paddingLeft - paddingRight),
                Math.Max(10f, Height - paddingTop - paddingBottom));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            RenderChart(e.Graphics, ClientRectangle);
        }

        public void RenderChart(Graphics g, Rectangle bounds)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            UpdatePlotArea();

            // Background & Plot Area
            g.Clear(ChartPalette.BackgroundDark);
            using (var plotBrush = new SolidBrush(ChartPalette.PlotAreaDark))
            {
                g.FillRectangle(plotBrush, PlotArea);
            }

            // Sync Legend Entries
            SyncLegend();

            // Grid & Ticks
            if (ShowGrid && RadarSeriesList.Count == 0 && PieSeriesList.Count == 0)
            {
                DrawGrid(g);
            }

            // Series Data (Clipped to PlotArea)
            var state = g.Save();
            g.SetClip(PlotArea);

            if (ActiveHeatmap != null && ActiveHeatmap.IsVisible)
            {
                DrawHeatmap(g, ActiveHeatmap);
            }

            // Area Series
            for (int i = 0; i < AreaSeriesList.Count; i++)
            {
                if (AreaSeriesList[i].IsVisible && AreaSeriesList[i].Count > 1)
                    DrawAreaSeries(g, AreaSeriesList[i]);
            }

            // Bar Series (Clustered & Categorical)
            if (BarSeriesList.Count > 0)
            {
                DrawBarSeriesClustered(g);
            }

            // Line Series
            for (int i = 0; i < LineSeriesList.Count; i++)
            {
                if (LineSeriesList[i].IsVisible && LineSeriesList[i].Count > 1)
                    DrawLineSeries(g, LineSeriesList[i]);
            }

            // Candlestick Series
            for (int i = 0; i < CandlestickSeriesList.Count; i++)
            {
                if (CandlestickSeriesList[i].IsVisible && CandlestickSeriesList[i].Count > 0)
                    DrawCandlestickSeries(g, CandlestickSeriesList[i]);
            }

            // Scatter Series
            for (int i = 0; i < ScatterSeriesList.Count; i++)
            {
                if (ScatterSeriesList[i].IsVisible && ScatterSeriesList[i].Count > 0)
                    DrawScatterSeries(g, ScatterSeriesList[i]);
            }

            // Pie Series
            for (int i = 0; i < PieSeriesList.Count; i++)
            {
                if (PieSeriesList[i].IsVisible && PieSeriesList[i].Count > 0)
                    DrawPieSeries(g, PieSeriesList[i]);
            }

            // Radar Series
            for (int i = 0; i < RadarSeriesList.Count; i++)
            {
                if (RadarSeriesList[i].IsVisible && RadarSeriesList[i].Count > 2)
                    DrawRadarSeries(g, RadarSeriesList[i]);
            }

            // Gantt Series
            for (int i = 0; i < GanttSeriesList.Count; i++)
            {
                if (GanttSeriesList[i].Count > 0)
                    DrawGanttSeries(g, GanttSeriesList[i]);
            }

            g.Restore(state);

            // Border
            using (var borderPen = new Pen(ChartPalette.GridLine, 1f))
            {
                g.DrawRectangle(borderPen, PlotArea.X, PlotArea.Y, PlotArea.Width, PlotArea.Height);
            }

            // Render Legend
            if (Legend.Placement != LegendPlacement.None)
            {
                var legendArea = new RectangleF(bounds.X + 8, bounds.Y + 4, bounds.Width - 16, bounds.Height - 8);
                var layoutBounds = Legend.MeasureAndLayout(g, Font, legendArea);
                Legend.Render(g, Font, layoutBounds);
            }

            // Crosshair & Tooltip
            if (_mousePos.X >= PlotArea.Left && _mousePos.X <= PlotArea.Right &&
                _mousePos.Y >= PlotArea.Top && _mousePos.Y <= PlotArea.Bottom)
            {
                if (ShowCrosshair) DrawCrosshair(g);
                if (ShowTooltip) DrawInspectorTooltip(g);
            }
        }

        private void SyncLegend()
        {
            Legend.Clear();
            foreach (var s in LineSeriesList)
                Legend.AddEntry(s.Name, s.StrokeColor, s.IsVisible, v => { s.IsVisible = v; Invalidate(); });
            foreach (var s in AreaSeriesList)
                Legend.AddEntry(s.Name, s.StrokeColor, s.IsVisible, v => { s.IsVisible = v; Invalidate(); });
            foreach (var s in BarSeriesList)
                Legend.AddEntry(s.Name, s.FillColor, s.IsVisible, v => { s.IsVisible = v; Invalidate(); });
            foreach (var s in ScatterSeriesList)
                Legend.AddEntry(s.Name, s.DefaultColor, s.IsVisible, v => { s.IsVisible = v; Invalidate(); });
            foreach (var s in CandlestickSeriesList)
                Legend.AddEntry("Candlestick", s.BullishColor, s.IsVisible, v => { s.IsVisible = v; Invalidate(); });
            foreach (var s in RadarSeriesList)
                Legend.AddEntry(s.Name, s.StrokeColor, s.IsVisible, v => { s.IsVisible = v; Invalidate(); });
            foreach (var ps in PieSeriesList)
            {
                if (ps.IsVisible)
                {
                    for (int i = 0; i < ps.Count; i++)
                    {
                        var slice = ps.Slices[i];
                        Legend.AddEntry(slice.Label, slice.Color, true);
                    }
                }
            }
        }

        #region Render Routines

        private void DrawGrid(Graphics g)
        {
            using (var gridPen = new Pen(ChartPalette.GridLine, 1f) { DashStyle = DashStyle.Dash })
            using (var textBrush = new SolidBrush(ChartPalette.AxisText))
            using (var font = new Font("Segoe UI", 8f))
            {
                // Y Ticks
                var yTicks = YAxis.GenerateMajorTicks(6);
                foreach (var yVal in yTicks)
                {
                    float yPixel = YAxis.ToScreen(yVal, PlotArea.Top, PlotArea.Height, invert: true);
                    if (yPixel >= PlotArea.Top && yPixel <= PlotArea.Bottom)
                    {
                        g.DrawLine(gridPen, PlotArea.Left, yPixel, PlotArea.Right, yPixel);
                        string label = Math.Abs(yVal) >= 1000 ? $"{yVal / 1000.0:F1}k" : $"{yVal:F1}";
                        g.DrawString(label, font, textBrush, 5, yPixel - 7);
                    }
                }

                // X Ticks (Categorical vs Linear)
                if (CategoryAxis.Count > 0 && BarSeriesList.Count > 0)
                {
                    for (int i = 0; i < CategoryAxis.Count; i++)
                    {
                        float xPixel = CategoryAxis.GetSlotCenter(i, PlotArea.Left, PlotArea.Width);
                        g.DrawLine(gridPen, xPixel, PlotArea.Top, xPixel, PlotArea.Bottom);
                        string cat = CategoryAxis.Categories[i];
                        var sz = g.MeasureString(cat, font);
                        g.DrawString(cat, font, textBrush, xPixel - (sz.Width * 0.5f), PlotArea.Bottom + 5);
                    }
                }
                else
                {
                    var xTicks = XAxis.GenerateMajorTicks(6);
                    foreach (var xVal in xTicks)
                    {
                        float xPixel = XAxis.ToScreen(xVal, PlotArea.Left, PlotArea.Width, invert: false);
                        if (xPixel >= PlotArea.Left && xPixel <= PlotArea.Right)
                        {
                            g.DrawLine(gridPen, xPixel, PlotArea.Top, xPixel, PlotArea.Bottom);
                            string label = $"{xVal:F0}";
                            g.DrawString(label, font, textBrush, xPixel - 15, PlotArea.Bottom + 5);
                        }
                    }
                }
            }
        }

        private void DrawLineSeries(Graphics g, LineSeries ls)
        {
            var points = ls.Count > 2000 ? ls.DownsampleLttb((int)PlotArea.Width) : ls.Points;
            if (points.Count < 2) return;

            PointF[] screenPoints = new PointF[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                float px = XAxis.ToScreen(points[i].X, PlotArea.Left, PlotArea.Width, invert: false);
                float py = YAxis.ToScreen(points[i].Y, PlotArea.Top, PlotArea.Height, invert: true);
                screenPoints[i] = new PointF(px, py);
            }

            using (var pen = new Pen(ls.StrokeColor, ls.StrokeThickness))
            {
                g.DrawLines(pen, screenPoints);
            }
        }

        private void DrawAreaSeries(Graphics g, AreaSeries as_)
        {
            var points = as_.Count > 2000 ? as_.DownsampleLttb((int)PlotArea.Width) : as_.Points;
            if (points.Count < 2) return;

            float baselineY = YAxis.ToScreen(as_.Baseline, PlotArea.Top, PlotArea.Height, invert: true);

            using (var path = new GraphicsPath())
            {
                float firstX = XAxis.ToScreen(points[0].X, PlotArea.Left, PlotArea.Width, invert: false);
                float lastX = XAxis.ToScreen(points[points.Count - 1].X, PlotArea.Left, PlotArea.Width, invert: false);

                path.AddLine(firstX, baselineY, firstX, YAxis.ToScreen(points[0].Y, PlotArea.Top, PlotArea.Height, invert: true));

                for (int i = 1; i < points.Count; i++)
                {
                    float px = XAxis.ToScreen(points[i].X, PlotArea.Left, PlotArea.Width, invert: false);
                    float py = YAxis.ToScreen(points[i].Y, PlotArea.Top, PlotArea.Height, invert: true);
                    path.AddLine(path.GetLastPoint().X, path.GetLastPoint().Y, px, py);
                }

                path.AddLine(lastX, YAxis.ToScreen(points[points.Count - 1].Y, PlotArea.Top, PlotArea.Height, invert: true), lastX, baselineY);
                path.CloseFigure();

                Color topColor = Color.FromArgb(as_.FillAlpha, as_.FillColor);
                Color botColor = as_.UseGradient ? Color.FromArgb(10, as_.FillColor) : topColor;

                using (var brush = new LinearGradientBrush(
                    new PointF(PlotArea.Left, PlotArea.Top),
                    new PointF(PlotArea.Left, PlotArea.Bottom),
                    topColor, botColor))
                {
                    g.FillPath(brush, path);
                }

                using (var strokePen = new Pen(as_.StrokeColor, as_.StrokeThickness))
                {
                    PointF[] curvePoints = new PointF[points.Count];
                    for (int i = 0; i < points.Count; i++)
                    {
                        curvePoints[i] = new PointF(
                            XAxis.ToScreen(points[i].X, PlotArea.Left, PlotArea.Width, invert: false),
                            YAxis.ToScreen(points[i].Y, PlotArea.Top, PlotArea.Height, invert: true));
                    }
                    g.DrawLines(strokePen, curvePoints);
                }
            }
        }

        private void DrawBarSeriesClustered(Graphics g)
        {
            var visibleBarSeries = new List<BarSeries>();
            for (int i = 0; i < BarSeriesList.Count; i++)
                if (BarSeriesList[i].IsVisible && BarSeriesList[i].Count > 0)
                    visibleBarSeries.Add(BarSeriesList[i]);

            if (visibleBarSeries.Count == 0 || CategoryAxis.Count == 0) return;

            float slotWidth = CategoryAxis.GetSlotWidth(PlotArea.Width);
            float zeroY = YAxis.ToScreen(0.0, PlotArea.Top, PlotArea.Height, invert: true);
            using (var font = new Font("Segoe UI", 7.5f))
            using (var labelBrush = new SolidBrush(Color.White))
            {
                for (int catIdx = 0; catIdx < CategoryAxis.Count; catIdx++)
                {
                    string cat = CategoryAxis.Categories[catIdx];
                    float slotLeft = PlotArea.Left + (catIdx * slotWidth);
                    float availableWidth = slotWidth * 0.85f;
                    float barW = availableWidth / visibleBarSeries.Count;
                    float startX = slotLeft + (slotWidth - availableWidth) * 0.5f;

                    for (int sIdx = 0; sIdx < visibleBarSeries.Count; sIdx++)
                    {
                        var series = visibleBarSeries[sIdx];
                        var item = series.FindByCategory(cat);
                        if (item == null) continue;

                        float bx = startX + (sIdx * barW) + (barW * 0.05f);
                        float bw = barW * 0.9f;

                        float valY = YAxis.ToScreen(item.Value, PlotArea.Top, PlotArea.Height, invert: true);
                        float topY = Math.Min(zeroY, valY);
                        float bh = Math.Max(1.5f, Math.Abs(valY - zeroY));

                        Color fillColor = item.CustomColor ?? series.FillColor;
                        using (var brush = new SolidBrush(fillColor))
                        using (var pen = new Pen(series.BorderColor, series.BorderWidth))
                        {
                            g.FillRectangle(brush, bx, topY, bw, bh);
                            g.DrawRectangle(pen, bx, topY, bw, bh);
                        }

                        // Data label on bar top
                        if (series.ShowDataLabels)
                        {
                            string text = item.CustomLabel ?? $"{item.Value:F1}";
                            var sz = g.MeasureString(text, font);
                            float labelX = bx + (bw - sz.Width) * 0.5f;
                            float labelY = item.Value >= 0 ? topY - sz.Height - 1 : topY + bh + 1;
                            g.DrawString(text, font, labelBrush, labelX, labelY);
                        }
                    }
                }
            }
        }

        private void DrawScatterSeries(Graphics g, ScatterSeries sc)
        {
            using (var font = new Font("Segoe UI", 7.5f))
            using (var textBrush = new SolidBrush(ChartPalette.AxisText))
            {
                for (int i = 0; i < sc.Count; i++)
                {
                    var pt = sc.Points[i];
                    float px = XAxis.ToScreen(pt.X, PlotArea.Left, PlotArea.Width, invert: false);
                    float py = YAxis.ToScreen(pt.Y, PlotArea.Top, PlotArea.Height, invert: true);
                    float r = pt.Radius > 0 ? pt.Radius : sc.DefaultRadius;

                    Color c = pt.CustomColor ?? sc.DefaultColor;
                    using (var brush = new SolidBrush(c))
                    using (var pen = new Pen(Color.FromArgb(200, Color.White), 1f))
                    {
                        switch (sc.Shape)
                        {
                            case MarkerShape.Square:
                                g.FillRectangle(brush, px - r, py - r, r * 2, r * 2);
                                g.DrawRectangle(pen, px - r, py - r, r * 2, r * 2);
                                break;
                            case MarkerShape.Diamond:
                                PointF[] diamond = {
                                    new PointF(px, py - r), new PointF(px + r, py),
                                    new PointF(px, py + r), new PointF(px - r, py)
                                };
                                g.FillPolygon(brush, diamond);
                                g.DrawPolygon(pen, diamond);
                                break;
                            case MarkerShape.Triangle:
                                PointF[] tri = {
                                    new PointF(px, py - r), new PointF(px + r, py + r), new PointF(px - r, py + r)
                                };
                                g.FillPolygon(brush, tri);
                                g.DrawPolygon(pen, tri);
                                break;
                            case MarkerShape.Cross:
                                g.DrawLine(pen, px - r, py, px + r, py);
                                g.DrawLine(pen, px, py - r, px, py + r);
                                break;
                            case MarkerShape.Circle:
                            default:
                                g.FillEllipse(brush, px - r, py - r, r * 2, r * 2);
                                g.DrawEllipse(pen, px - r, py - r, r * 2, r * 2);
                                break;
                        }
                    }

                    if (!string.IsNullOrEmpty(pt.Label))
                    {
                        g.DrawString(pt.Label, font, textBrush, px + r + 2, py - r);
                    }
                }
            }
        }

        private void DrawPieSeries(Graphics g, PieSeries ps)
        {
            float cx = PlotArea.Left + (PlotArea.Width * 0.5f);
            float cy = PlotArea.Top + (PlotArea.Height * 0.5f);
            float radius = Math.Min(PlotArea.Width, PlotArea.Height) * 0.42f;

            using (var font = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            using (var textBrush = new SolidBrush(Color.White))
            using (var borderPen = new Pen(ChartPalette.BackgroundDark, 1.5f))
            {
                for (int i = 0; i < ps.Count; i++)
                {
                    var slice = ps.Slices[i];
                    if (slice.SweepAngle <= 0.01f) continue;

                    float ox = cx, oy = cy;
                    if (slice.IsExploded)
                    {
                        double midRad = (slice.StartAngle + slice.SweepAngle * 0.5) * (Math.PI / 180.0);
                        ox += (float)(Math.Cos(midRad) * ps.ExplodeDistance);
                        oy += (float)(Math.Sin(midRad) * ps.ExplodeDistance);
                    }

                    using (var brush = new SolidBrush(slice.Color))
                    {
                        g.FillPie(brush, ox - radius, oy - radius, radius * 2, radius * 2, slice.StartAngle, slice.SweepAngle);
                        g.DrawPie(borderPen, ox - radius, oy - radius, radius * 2, radius * 2, slice.StartAngle, slice.SweepAngle);
                    }

                    // Draw percentage label inside slice
                    if (ps.ShowPercentages && slice.Percentage >= 4.0)
                    {
                        double midRad = (slice.StartAngle + slice.SweepAngle * 0.5) * (Math.PI / 180.0);
                        float labelR = radius * (ps.HoleRadiusRatio > 0 ? (ps.HoleRadiusRatio + 1.0f) * 0.5f : 0.65f);
                        float lx = ox + (float)(Math.Cos(midRad) * labelR);
                        float ly = oy + (float)(Math.Sin(midRad) * labelR);

                        string pctText = $"{slice.Percentage:F1}%";
                        var sz = g.MeasureString(pctText, font);
                        g.DrawString(pctText, font, textBrush, lx - sz.Width * 0.5f, ly - sz.Height * 0.5f);
                    }
                }

                // Hole for Doughnut
                if (ps.HoleRadiusRatio > 0.05f)
                {
                    float innerR = radius * ps.HoleRadiusRatio;
                    using (var holeBrush = new SolidBrush(ChartPalette.PlotAreaDark))
                    using (var holeBorder = new Pen(ChartPalette.GridLine, 1f))
                    {
                        g.FillEllipse(holeBrush, cx - innerR, cy - innerR, innerR * 2, innerR * 2);
                        g.DrawEllipse(holeBorder, cx - innerR, cy - innerR, innerR * 2, innerR * 2);
                    }
                }
            }
        }

        private void DrawRadarSeries(Graphics g, RadarSeries rs)
        {
            float cx = PlotArea.Left + (PlotArea.Width * 0.5f);
            float cy = PlotArea.Top + (PlotArea.Height * 0.5f);
            float radius = Math.Min(PlotArea.Width, PlotArea.Height) * 0.40f;
            int n = rs.Count;
            if (n < 3) return;

            // Draw radial concentric spider grid
            using (var gridPen = new Pen(ChartPalette.GridLine, 1f) { DashStyle = DashStyle.Dot })
            using (var axisPen = new Pen(ChartPalette.GridLine, 1f))
            using (var textBrush = new SolidBrush(ChartPalette.AxisText))
            using (var font = new Font("Segoe UI", 8f))
            {
                // Web circles / polygons (25%, 50%, 75%, 100%)
                for (int level = 1; level <= 4; level++)
                {
                    float r = radius * (level * 0.25f);
                    PointF[] webPts = new PointF[n];
                    for (int i = 0; i < n; i++)
                    {
                        double angle = (-Math.PI * 0.5) + (i * 2.0 * Math.PI / n);
                        webPts[i] = new PointF(cx + (float)(Math.Cos(angle) * r), cy + (float)(Math.Sin(angle) * r));
                    }
                    g.DrawPolygon(gridPen, webPts);
                }

                // Axis spokes and labels
                for (int i = 0; i < n; i++)
                {
                    double angle = (-Math.PI * 0.5) + (i * 2.0 * Math.PI / n);
                    float sx = cx + (float)(Math.Cos(angle) * radius);
                    float sy = cy + (float)(Math.Sin(angle) * radius);
                    g.DrawLine(axisPen, cx, cy, sx, sy);

                    // Label
                    string name = rs.Items[i].AxisName;
                    var sz = g.MeasureString(name, font);
                    float lx = cx + (float)(Math.Cos(angle) * (radius + 14f)) - (sz.Width * 0.5f);
                    float ly = cy + (float)(Math.Sin(angle) * (radius + 14f)) - (sz.Height * 0.5f);
                    g.DrawString(name, font, textBrush, lx, ly);
                }
            }

            // Draw Data Polygon
            PointF[] dataPts = new PointF[n];
            for (int i = 0; i < n; i++)
            {
                dataPts[i] = rs.GetCartesianPoint(i, cx, cy, radius);
            }

            Color fillColor = Color.FromArgb(rs.FillAlpha, rs.FillColor);
            using (var brush = new SolidBrush(fillColor))
            using (var pen = new Pen(rs.StrokeColor, rs.StrokeThickness))
            using (var dotBrush = new SolidBrush(rs.StrokeColor))
            {
                g.FillPolygon(brush, dataPts);
                g.DrawPolygon(pen, dataPts);

                for (int i = 0; i < n; i++)
                {
                    g.FillEllipse(dotBrush, dataPts[i].X - 3.5f, dataPts[i].Y - 3.5f, 7f, 7f);
                }
            }
        }

        private void DrawCandlestickSeries(Graphics g, CandlestickSeries cs)
        {
            float barWidth = Math.Max(2f, (PlotArea.Width / Math.Max(1, cs.Count)) * 0.7f);

            for (int i = 0; i < cs.Count; i++)
            {
                var item = cs.Items[i];
                float x = XAxis.ToScreen(item.Timestamp.Ticks, PlotArea.Left, PlotArea.Width, invert: false);
                float highY = YAxis.ToScreen(item.High, PlotArea.Top, PlotArea.Height, invert: true);
                float lowY = YAxis.ToScreen(item.Low, PlotArea.Top, PlotArea.Height, invert: true);
                float topY = YAxis.ToScreen(item.BodyTop, PlotArea.Top, PlotArea.Height, invert: true);
                float botY = YAxis.ToScreen(item.BodyBottom, PlotArea.Top, PlotArea.Height, invert: true);

                Color color = item.IsBullish ? cs.BullishColor : cs.BearishColor;

                using (var pen = new Pen(color, 1f))
                using (var brush = new SolidBrush(color))
                {
                    g.DrawLine(pen, x, highY, x, lowY);
                    float rectHeight = Math.Max(1f, botY - topY);
                    g.FillRectangle(brush, x - (barWidth / 2), topY, barWidth, rectHeight);
                }
            }
        }

        private void DrawHeatmap(Graphics g, HeatmapSeries hm)
        {
            float cellWidth = PlotArea.Width / hm.Width;
            float cellHeight = PlotArea.Height / hm.Height;

            for (int x = 0; x < hm.Width; x++)
            {
                for (int y = 0; y < hm.Height; y++)
                {
                    Color c = hm.GetThermalColor(hm[x, y]);
                    using (var brush = new SolidBrush(c))
                    {
                        g.FillRectangle(brush, PlotArea.Left + (x * cellWidth), PlotArea.Top + (y * cellHeight),
                            cellWidth + 0.5f, cellHeight + 0.5f);
                    }
                }
            }
        }

        private void DrawGanttSeries(Graphics g, GanttSeries gs)
        {
            var groups = gs.GetDistinctTrackGroups();
            if (groups.Count == 0) return;

            float trackHeight = PlotArea.Height / groups.Count;

            for (int i = 0; i < gs.Count; i++)
            {
                var t = gs.Tasks[i];
                int trackIndex = groups.IndexOf(t.TrackGroup);
                if (trackIndex < 0) trackIndex = 0;

                float xStart = XAxis.ToScreen(t.StartTime.Ticks, PlotArea.Left, PlotArea.Width, invert: false);
                float xEnd = XAxis.ToScreen(t.EndTime.Ticks, PlotArea.Left, PlotArea.Width, invert: false);
                float width = Math.Max(2f, xEnd - xStart);
                float y = PlotArea.Top + (trackIndex * trackHeight) + (trackHeight * 0.15f);
                float height = trackHeight * 0.7f;

                using (var brush = new SolidBrush(t.GetStateColor()))
                using (var textBrush = new SolidBrush(Color.Black))
                using (var font = new Font("Segoe UI", 8f, FontStyle.Bold))
                {
                    g.FillRectangle(brush, xStart, y, width, height);
                    if (width > 30)
                    {
                        g.DrawString(t.Label, font, textBrush, xStart + 4, y + 2);
                    }
                }
            }
        }

        private void DrawCrosshair(Graphics g)
        {
            using (var pen = new Pen(Color.FromArgb(180, ChartPalette.CrosshairLine)) { DashStyle = DashStyle.Dot })
            {
                g.DrawLine(pen, PlotArea.Left, _mousePos.Y, PlotArea.Right, _mousePos.Y);
                g.DrawLine(pen, _mousePos.X, PlotArea.Top, _mousePos.X, PlotArea.Bottom);
            }
        }

        private void DrawInspectorTooltip(Graphics g)
        {
            var hit = FindNearestHit(_mousePos);
            if (hit == null) return;

            using (var bgBrush = new SolidBrush(Color.FromArgb(230, 20, 24, 32)))
            using (var borderPen = new Pen(hit.SeriesColor, 1.2f))
            using (var textBrush = new SolidBrush(Color.White))
            using (var subBrush = new SolidBrush(ChartPalette.AxisText))
            using (var fontTitle = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            using (var fontVal = new Font("Segoe UI", 8f))
            using (var badgeBrush = new SolidBrush(hit.SeriesColor))
            {
                string header = hit.SeriesName;
                string line = $"{hit.CategoryOrX}: {hit.FormattedValue}";

                var sz1 = g.MeasureString(header, fontTitle);
                var sz2 = g.MeasureString(line, fontVal);

                float cardW = Math.Max(sz1.Width, sz2.Width) + 24f;
                float cardH = sz1.Height + sz2.Height + 12f;

                float tx = _mousePos.X + 12f;
                float ty = _mousePos.Y - cardH - 8f;

                if (tx + cardW > PlotArea.Right) tx = _mousePos.X - cardW - 12f;
                if (ty < PlotArea.Top) ty = _mousePos.Y + 12f;

                g.FillRectangle(bgBrush, tx, ty, cardW, cardH);
                g.DrawRectangle(borderPen, tx, ty, cardW, cardH);

                g.FillRectangle(badgeBrush, tx + 6, ty + 6, 8, 8);
                g.DrawString(header, fontTitle, textBrush, tx + 18, ty + 4);
                g.DrawString(line, fontVal, subBrush, tx + 6, ty + sz1.Height + 5);

                // Highlight circle at hit location
                g.DrawEllipse(borderPen, hit.ScreenLocation.X - 4f, hit.ScreenLocation.Y - 4f, 8f, 8f);
            }
        }

        private HitTestResult? FindNearestHit(Point mousePos)
        {
            double mouseDataX = XAxis.FromScreen(mousePos.X, PlotArea.Left, PlotArea.Width, invert: false);
            double mouseDataY = YAxis.FromScreen(mousePos.Y, PlotArea.Top, PlotArea.Height, invert: true);

            // 1. Check Bar Series
            if (BarSeriesList.Count > 0 && CategoryAxis.Count > 0)
            {
                int catIdx = CategoryAxis.GetCategoryAtScreen(mousePos.X, PlotArea.Left, PlotArea.Width);
                if (catIdx >= 0 && catIdx < CategoryAxis.Count)
                {
                    string cat = CategoryAxis.Categories[catIdx];
                    for (int i = 0; i < BarSeriesList.Count; i++)
                    {
                        var bs = BarSeriesList[i];
                        if (!bs.IsVisible) continue;
                        var item = bs.FindByCategory(cat);
                        if (item != null)
                        {
                            float px = CategoryAxis.GetSlotCenter(catIdx, PlotArea.Left, PlotArea.Width);
                            float py = YAxis.ToScreen(item.Value, PlotArea.Top, PlotArea.Height, invert: true);
                            return new HitTestResult(bs.Name, cat, $"{item.Value:F1}", new PointF(px, py), bs.FillColor);
                        }
                    }
                }
            }

            // 2. Check Scatter Series
            foreach (var sc in ScatterSeriesList)
            {
                if (!sc.IsVisible) continue;
                var pt = sc.FindNearest(mouseDataX, mouseDataY, 100.0);
                if (pt != null)
                {
                    float px = XAxis.ToScreen(pt.X, PlotArea.Left, PlotArea.Width, invert: false);
                    float py = YAxis.ToScreen(pt.Y, PlotArea.Top, PlotArea.Height, invert: true);
                    return new HitTestResult(sc.Name, $"X: {pt.X:F1}", $"Y: {pt.Y:F1}", new PointF(px, py), sc.DefaultColor);
                }
            }

            // 3. Fallback: Generic coordinate
            return new HitTestResult("Cursor", $"X: {mouseDataX:F1}", $"Y: {mouseDataY:F1}", mousePos, ChartPalette.CrosshairLine);
        }

        #endregion

        #region Export & Snapshot APIs

        /// <summary>
        /// Captures the chart directly into a System.Drawing.Bitmap in memory.
        /// </summary>
        public Bitmap ToBitmap(int width = 0, int height = 0)
        {
            int w = width > 0 ? width : Math.Max(Width, 400);
            int h = height > 0 ? height : Math.Max(Height, 300);

            var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                RenderChart(g, new Rectangle(0, 0, w, h));
            }
            return bmp;
        }

        /// <summary>
        /// Saves high-resolution chart snapshot to disk.
        /// </summary>
        public void SaveImage(string filePath, ImageFormat? format = null, int width = 0, int height = 0)
        {
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentNullException(nameof(filePath));
            using var bmp = ToBitmap(width, height);
            bmp.Save(filePath, format ?? ImageFormat.Png);
        }

        #endregion

        #region Mouse Interactions

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            // Check Legend Click
            if (e.Button == MouseButtons.Left && Legend.HandleClick(e.Location))
            {
                Invalidate();
                return;
            }

            if (e.Button == MouseButtons.Right)
            {
                _isPanning = true;
                _panStartPoint = e.Location;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            _mousePos = e.Location;

            if (_isPanning)
            {
                float dx = e.X - _panStartPoint.X;
                float dy = e.Y - _panStartPoint.Y;

                double shiftX = -(dx / PlotArea.Width) * XAxis.Range;
                double shiftY = (dy / PlotArea.Height) * YAxis.Range;

                XAxis.SetRange(XAxis.Min + shiftX, XAxis.Max + shiftX);
                YAxis.SetRange(YAxis.Min + shiftY, YAxis.Max + shiftY);
                _panStartPoint = e.Location;
            }

            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Right)
            {
                _isPanning = false;
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            double zoomFactor = e.Delta > 0 ? 0.85 : 1.15;

            double midX = XAxis.FromScreen(e.X, PlotArea.Left, PlotArea.Width, invert: false);
            double midY = YAxis.FromScreen(e.Y, PlotArea.Top, PlotArea.Height, invert: true);

            double newSpanX = XAxis.Range * zoomFactor;
            double newSpanY = YAxis.Range * zoomFactor;

            XAxis.SetRange(midX - (newSpanX * 0.5), midX + (newSpanX * 0.5));
            YAxis.SetRange(midY - (newSpanY * 0.5), midY + (newSpanY * 0.5));

            Invalidate();
        }

        #endregion
    }
}
