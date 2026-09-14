using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroPlatform.Charts.Axis;
using ZeroPlatform.Charts.DataModels;

namespace ZeroPlatform.Charts.Controls
{
    /// <summary>
    /// Hardware-accelerated industrial telemetry and analytical chart control for WinForms.
    /// </summary>
    public class ZeroChartControl : Control
    {
        public ChartAxis XAxis { get; } = new ChartAxis("X Axis");
        public ChartAxis YAxis { get; } = new ChartAxis("Y Axis");

        public List<LineSeries> LineSeriesList { get; } = new List<LineSeries>();
        public List<CandlestickSeries> CandlestickSeriesList { get; } = new List<CandlestickSeries>();
        public List<GanttSeries> GanttSeriesList { get; } = new List<GanttSeries>();
        public HeatmapSeries? ActiveHeatmap { get; set; }

        public bool ShowCrosshair { get; set; } = true;
        public bool ShowGrid { get; set; } = true;

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

        public void AddSeries(LineSeries series)
        {
            if (series != null)
            {
                LineSeriesList.Add(series);
                AutoFit();
            }
        }

        public void AddSeries(CandlestickSeries series)
        {
            if (series != null)
            {
                CandlestickSeriesList.Add(series);
                AutoFit();
            }
        }

        public void AddSeries(GanttSeries series)
        {
            if (series != null)
            {
                GanttSeriesList.Add(series);
                AutoFit();
            }
        }

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
                double margin = (maxY - minY) * 0.05;
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

            PlotArea = new RectangleF(
                paddingLeft,
                paddingTop,
                Math.Max(10f, Width - paddingLeft - paddingRight),
                Math.Max(10f, Height - paddingTop - paddingBottom));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            UpdatePlotArea();

            // Background & Plot Area
            g.Clear(ChartPalette.BackgroundDark);
            using (var plotBrush = new SolidBrush(ChartPalette.PlotAreaDark))
            {
                g.FillRectangle(plotBrush, PlotArea);
            }

            // Grid & Ticks
            if (ShowGrid)
            {
                DrawGrid(g);
            }

            // Series Data
            var state = g.Save();
            g.SetClip(PlotArea);

            if (ActiveHeatmap != null && ActiveHeatmap.IsVisible)
            {
                DrawHeatmap(g, ActiveHeatmap);
            }

            foreach (var ls in LineSeriesList)
            {
                if (ls.IsVisible && ls.Count > 1)
                {
                    DrawLineSeries(g, ls);
                }
            }

            foreach (var cs in CandlestickSeriesList)
            {
                if (cs.IsVisible && cs.Count > 0)
                {
                    DrawCandlestickSeries(g, cs);
                }
            }

            foreach (var gs in GanttSeriesList)
            {
                if (gs.Count > 0)
                {
                    DrawGanttSeries(g, gs);
                }
            }

            g.Restore(state);

            // Border
            using (var borderPen = new Pen(ChartPalette.GridLine, 1f))
            {
                g.DrawRectangle(borderPen, PlotArea.X, PlotArea.Y, PlotArea.Width, PlotArea.Height);
            }

            // Crosshair & Tooltip
            if (ShowCrosshair && _mousePos.X >= PlotArea.Left && _mousePos.X <= PlotArea.Right &&
                _mousePos.Y >= PlotArea.Top && _mousePos.Y <= PlotArea.Bottom)
            {
                DrawCrosshair(g);
            }
        }

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
                        string label = yVal >= 1000 ? $"{yVal / 1000.0:F1}k" : $"{yVal:F1}";
                        g.DrawString(label, font, textBrush, 5, yPixel - 7);
                    }
                }

                // X Ticks
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
                    // Wick line
                    g.DrawLine(pen, x, highY, x, lowY);

                    // Body rect
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
            using (var textBrush = new SolidBrush(Color.White))
            using (var bgBrush = new SolidBrush(Color.FromArgb(200, 20, 24, 32)))
            using (var font = new Font("Segoe UI", 8f))
            {
                g.DrawLine(pen, PlotArea.Left, _mousePos.Y, PlotArea.Right, _mousePos.Y);
                g.DrawLine(pen, _mousePos.X, PlotArea.Top, _mousePos.X, PlotArea.Bottom);

                double dataX = XAxis.FromScreen(_mousePos.X, PlotArea.Left, PlotArea.Width, invert: false);
                double dataY = YAxis.FromScreen(_mousePos.Y, PlotArea.Top, PlotArea.Height, invert: true);

                string tooltip = $"X: {dataX:F1}\nY: {dataY:F1}";
                var size = g.MeasureString(tooltip, font);
                g.FillRectangle(bgBrush, _mousePos.X + 8, _mousePos.Y - size.Height - 4, size.Width + 8, size.Height + 4);
                g.DrawString(tooltip, font, textBrush, _mousePos.X + 12, _mousePos.Y - size.Height - 2);
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

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Right)
            {
                _isPanning = true;
                _panStartPoint = e.Location;
            }
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
    }
}
