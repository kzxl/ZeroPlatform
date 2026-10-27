using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Water;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Water
{
    /// <summary>
    /// Pipeline Hydraulic Grade Line (HGL) & Energy Grade Line (EGL) profile chart for WinForms.
    /// Visualizes ground topography, pipe invert/crown profile, water column head, booster pump lifts,
    /// and negative pressure cavitation risk detection with interactive chainage inspection.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Water & Wastewater")]
    [Description("Pipeline Hydraulic Grade Line (HGL) & Energy Grade Line (EGL) elevation profile chart")]
    public class ZHydraulicGradientChart : VisualControlBase
    {
        private readonly HydraulicGradientEngine _engine = new HydraulicGradientEngine();
        private string _pipelineTag = "TRANSMISSION-MAIN-01";
        private Point _hoverPoint = Point.Empty;
        private bool _isHovering = false;

        public ZHydraulicGradientChart()
        {
            Size = new Size(760, 360);
        }

        #region Public Properties

        [Category("Water")]
        [Description("Access to the underlying pure hydraulic gradient profile engine")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public HydraulicGradientEngine Engine => _engine;

        [Category("Water")]
        [DefaultValue("TRANSMISSION-MAIN-01")]
        public string PipelineTag
        {
            get => _pipelineTag;
            set
            {
                if (_pipelineTag != value)
                {
                    _pipelineTag = value ?? "MAIN-01";
                    Invalidate();
                }
            }
        }

        [Category("Water")]
        [DefaultValue(0.28)]
        public double FlowRateM3S
        {
            get => _engine.FlowRateM3S;
            set
            {
                _engine.FlowRateM3S = Math.Max(0.001, value);
                Invalidate();
            }
        }

        [Category("Water")]
        [DefaultValue(65.0)]
        public double StartingHeadM
        {
            get => _engine.StartingHeadM;
            set
            {
                _engine.StartingHeadM = value;
                Invalidate();
            }
        }

        #endregion

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            _hoverPoint = e.Location;
            _isHovering = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isHovering = false;
            Invalidate();
        }

        protected override void OnDrawVisual(Graphics g, Rectangle bounds, ZeroThemePalette palette)
        {
            var profile = _engine.CalculateProfile();
            if (profile.Count == 0) return;

            // 1. Top Header
            int headerH = DrawHeader(g, bounds, palette);

            int chartLeft = bounds.X + 54;
            int chartTop = bounds.Y + headerH;
            int chartW = bounds.Width - 70;
            int chartH = bounds.Height - headerH - 36;

            if (chartW < 120 || chartH < 80) return;

            Rectangle chartArea = new Rectangle(chartLeft, chartTop, chartW, chartH);

            // Calculate min/max scales
            double maxDist = profile[profile.Count - 1].StationM;
            if (maxDist <= 0.0) maxDist = 1.0;

            double minElev = double.MaxValue;
            double maxElev = double.MinValue;
            for (int i = 0; i < profile.Count; i++)
            {
                var pt = profile[i];
                minElev = Math.Min(minElev, Math.Min(pt.PipeInvertM, pt.GroundElevationM));
                maxElev = Math.Max(maxElev, Math.Max(pt.HglElevationM, pt.EglElevationM));
            }
            minElev = Math.Floor(minElev - 8.0);
            maxElev = Math.Ceiling(maxElev + 10.0);
            double rangeElev = Math.Max(10.0, maxElev - minElev);

            // 2. Gridlines & Axes
            DrawGrid(g, chartArea, minElev, maxElev, maxDist, palette);

            // 3. Ground Terrain & Pipe Profile
            DrawTerrainAndPipe(g, chartArea, profile, minElev, rangeElev, maxDist, palette);

            // 4. HGL & EGL Lines
            DrawGradeLines(g, chartArea, profile, minElev, rangeElev, maxDist);

            // 5. Interactive Inspection Tooltip
            if (_isHovering && chartArea.Contains(_hoverPoint))
            {
                DrawHoverTooltip(g, chartArea, profile, minElev, rangeElev, maxDist, _hoverPoint, palette);
            }
        }

        private int DrawHeader(Graphics g, Rectangle bounds, ZeroThemePalette palette)
        {
            using (var fontTitle = new Font("Segoe UI", 10.5f, FontStyle.Bold))
            using (var fontSub = new Font("Segoe UI", 8.5f))
            {
                string title = $"{_pipelineTag} — Hydraulic Grade Profile (HGL & EGL)";
                Size titleSize = TextRenderer.MeasureText(g, title, fontTitle);

                bool hasCavitation = _engine.HasNegativePressureAnomaly(out double worstSt, out double minHead);
                string statusStr = hasCavitation
                    ? $"CAVITATION RISK (Min: {minHead:F1} m at {worstSt:N0}m)"
                    : $"POSITIVE PRESSURE (Min Head: {minHead:F1} m)";
                Color badgeColor = hasCavitation ? Color.FromArgb(239, 68, 68) : Color.FromArgb(34, 197, 94);

                int badgeW = 260;
                int badgeH = 24;
                bool isWide = bounds.Width - badgeW - 20 >= 16 + titleSize.Width + 12;

                if (isWide)
                {
                    TextRenderer.DrawText(g, title, fontTitle, new Point(bounds.X + 16, bounds.Y + 12), palette.TextPrimary);

                    Rectangle badgeRect = new Rectangle(bounds.Right - badgeW - 16, bounds.Y + 10, badgeW, badgeH);
                    using (var brush = new SolidBrush(Color.FromArgb(30, badgeColor)))
                    using (var pen = new Pen(badgeColor, 1.2f))
                    {
                        g.FillRectangle(brush, badgeRect);
                        g.DrawRectangle(pen, badgeRect);
                    }

                    TextRenderer.DrawText(g, statusStr, fontSub, badgeRect, badgeColor,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    return 46;
                }
                else
                {
                    Rectangle titleRect = new Rectangle(bounds.X + 16, bounds.Y + 8, bounds.Width - 32, 22);
                    TextRenderer.DrawText(g, title, fontTitle, titleRect, palette.TextPrimary,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                    int actualBadgeW = Math.Min(badgeW, bounds.Width - 32);
                    Rectangle badgeRect = new Rectangle(bounds.X + 16, bounds.Y + 34, actualBadgeW, badgeH);
                    using (var brush = new SolidBrush(Color.FromArgb(30, badgeColor)))
                    using (var pen = new Pen(badgeColor, 1.2f))
                    {
                        g.FillRectangle(brush, badgeRect);
                        g.DrawRectangle(pen, badgeRect);
                    }

                    TextRenderer.DrawText(g, statusStr, fontSub, badgeRect, badgeColor,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                    return 66;
                }
            }
        }

        private void DrawGrid(Graphics g, Rectangle area, double minElev, double maxElev, double maxDist, ZeroThemePalette palette)
        {
            using (var fontAxis = new Font("Segoe UI", 7.5f))
            using (var borderPen = new Pen(palette.Border, 1f))
            using (var gridPen = new Pen(Color.FromArgb(25, palette.TextSecondary), 1f) { DashStyle = DashStyle.Dot })
            {
                g.DrawRectangle(borderPen, area);

                // Elevation grid lines (Y-axis)
                int yDivs = 5;
                for (int i = 0; i <= yDivs; i++)
                {
                    float y = area.Y + (float)i / yDivs * area.Height;
                    g.DrawLine(gridPen, area.Left, y, area.Right, y);

                    double elevVal = maxElev - ((double)i / yDivs) * (maxElev - minElev);
                    string elevStr = $"{elevVal:F0}m";
                    var sz = TextRenderer.MeasureText(g, elevStr, fontAxis);
                    TextRenderer.DrawText(g, elevStr, fontAxis, new Point(area.Left - sz.Width - 4, (int)y - sz.Height / 2), palette.TextSecondary);
                }

                // Station distance marks (X-axis)
                int xDivs = 5;
                for (int i = 0; i <= xDivs; i++)
                {
                    float x = area.Left + (float)i / xDivs * area.Width;
                    g.DrawLine(gridPen, x, area.Top, x, area.Bottom);

                    double distVal = ((double)i / xDivs) * maxDist;
                    string distStr = $"{distVal:N0}m";
                    var sz = TextRenderer.MeasureText(g, distStr, fontAxis);
                    TextRenderer.DrawText(g, distStr, fontAxis, new Point((int)x - sz.Width / 2, area.Bottom + 4), palette.TextSecondary);
                }
            }
        }

        private void DrawTerrainAndPipe(Graphics g, Rectangle area, List<HydraulicProfilePoint> profile, double minElev, double rangeElev, double maxDist, ZeroThemePalette palette)
        {
            PointF[] groundPoints = new PointF[profile.Count];
            PointF[] invertPoints = new PointF[profile.Count];
            PointF[] crownPoints = new PointF[profile.Count];

            for (int i = 0; i < profile.Count; i++)
            {
                var pt = profile[i];
                float x = area.Left + (float)(pt.StationM / maxDist) * area.Width;
                float yGround = area.Bottom - (float)((pt.GroundElevationM - minElev) / rangeElev) * area.Height;
                float yInvert = area.Bottom - (float)((pt.PipeInvertM - minElev) / rangeElev) * area.Height;
                float yCrown = area.Bottom - (float)((pt.PipeCrownM - minElev) / rangeElev) * area.Height;

                groundPoints[i] = new PointF(x, yGround);
                invertPoints[i] = new PointF(x, yInvert);
                crownPoints[i] = new PointF(x, yCrown);
            }

            // Pipe Body Fill
            using (var pipeBrush = new SolidBrush(Color.FromArgb(50, 71, 85, 105)))
            using (var pipePen = new Pen(Color.FromArgb(148, 163, 184), 1.5f))
            {
                for (int i = 0; i < profile.Count - 1; i++)
                {
                    PointF[] poly = new PointF[]
                    {
                        crownPoints[i], crownPoints[i + 1],
                        invertPoints[i + 1], invertPoints[i]
                    };
                    g.FillPolygon(pipeBrush, poly);
                    g.DrawLine(pipePen, crownPoints[i], crownPoints[i + 1]);
                    g.DrawLine(pipePen, invertPoints[i], invertPoints[i + 1]);
                }
            }

            // Ground Surface Line
            using (var groundPen = new Pen(Color.FromArgb(161, 161, 170), 1.5f) { DashStyle = DashStyle.Dash })
            {
                g.DrawLines(groundPen, groundPoints);
            }
        }

        private void DrawGradeLines(Graphics g, Rectangle area, List<HydraulicProfilePoint> profile, double minElev, double rangeElev, double maxDist)
        {
            PointF[] hglPoints = new PointF[profile.Count];
            PointF[] eglPoints = new PointF[profile.Count];

            for (int i = 0; i < profile.Count; i++)
            {
                var pt = profile[i];
                float x = area.Left + (float)(pt.StationM / maxDist) * area.Width;
                float yHgl = area.Bottom - (float)((pt.HglElevationM - minElev) / rangeElev) * area.Height;
                float yEgl = area.Bottom - (float)((pt.EglElevationM - minElev) / rangeElev) * area.Height;

                hglPoints[i] = new PointF(x, yHgl);
                eglPoints[i] = new PointF(x, yEgl);
            }

            // Draw EGL (Amber Line)
            using (var eglPen = new Pen(Color.FromArgb(245, 158, 11), 1.5f))
            {
                g.DrawLines(eglPen, eglPoints);
            }

            // Draw HGL (Sky Blue Line)
            using (var hglPen = new Pen(Color.FromArgb(56, 189, 248), 2.2f))
            using (var nodeBrush = new SolidBrush(Color.FromArgb(56, 189, 248)))
            using (var negBrush = new SolidBrush(Color.FromArgb(239, 68, 68)))
            {
                g.DrawLines(hglPen, hglPoints);

                for (int i = 0; i < profile.Count; i++)
                {
                    Brush b = profile[i].IsNegativePressure ? negBrush : nodeBrush;
                    g.FillEllipse(b, hglPoints[i].X - 3.5f, hglPoints[i].Y - 3.5f, 7f, 7f);
                }
            }
        }

        private void DrawHoverTooltip(Graphics g, Rectangle area, List<HydraulicProfilePoint> profile, double minElev, double rangeElev, double maxDist, Point mouse, ZeroThemePalette palette)
        {
            // Find nearest station
            double mouseRatio = Math.Max(0.0, Math.Min(1.0, (double)(mouse.X - area.Left) / area.Width));
            double targetDist = mouseRatio * maxDist;

            HydraulicProfilePoint nearest = profile[0];
            double bestDelta = Math.Abs(nearest.StationM - targetDist);
            for (int i = 1; i < profile.Count; i++)
            {
                double d = Math.Abs(profile[i].StationM - targetDist);
                if (d < bestDelta)
                {
                    bestDelta = d;
                    nearest = profile[i];
                }
            }

            // Crosshair vertical line
            float nearestX = area.Left + (float)(nearest.StationM / maxDist) * area.Width;
            using (var cursorPen = new Pen(Color.FromArgb(180, palette.TextSecondary), 1f) { DashStyle = DashStyle.Dot })
            {
                g.DrawLine(cursorPen, nearestX, area.Top, nearestX, area.Bottom);
            }

            // Tooltip box
            using (var fontBold = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            using (var fontSmall = new Font("Segoe UI", 7.5f))
            {
                int tipW = 180;
                int tipH = 74;
                int tipX = Math.Min(area.Right - tipW - 4, (int)nearestX + 10);
                int tipY = Math.Max(area.Top + 4, mouse.Y - tipH / 2);

                Rectangle tipRect = new Rectangle(tipX, tipY, tipW, tipH);
                using (var tipBg = new SolidBrush(Color.FromArgb(240, 15, 23, 42)))
                using (var tipBorder = new Pen(nearest.IsNegativePressure ? Color.FromArgb(239, 68, 68) : Color.FromArgb(56, 189, 248), 1.2f))
                {
                    g.FillRectangle(tipBg, tipRect);
                    g.DrawRectangle(tipBorder, tipRect);
                }

                TextRenderer.DrawText(g, $"Station: {nearest.StationM:N0} m", fontBold, new Point(tipX + 6, tipY + 4), Color.White);
                TextRenderer.DrawText(g, $"HGL: {nearest.HglElevationM:F1} m | EGL: {nearest.EglElevationM:F1} m", fontSmall, new Point(tipX + 6, tipY + 22), Color.FromArgb(56, 189, 248));
                TextRenderer.DrawText(g, $"Pipe Invert: {nearest.PipeInvertM:F1} m", fontSmall, new Point(tipX + 6, tipY + 38), Color.FromArgb(148, 163, 184));

                Color pressCol = nearest.IsNegativePressure ? Color.FromArgb(239, 68, 68) : Color.FromArgb(34, 197, 94);
                string pressStr = $"Head: {nearest.PressureHeadM:F1} m ({nearest.PressureKpa:F0} kPa)";
                TextRenderer.DrawText(g, pressStr, fontBold, new Point(tipX + 6, tipY + 54), pressCol);
            }
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZHydraulicGradientChart"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("HydraulicGradientChart is deprecated and will be removed in 5 release cycles. Please migrate to ZHydraulicGradientChart instead.")]
    [ToolboxItem(false)]
    public class HydraulicGradientChart : ZHydraulicGradientChart
    {
    }

    #endregion
}
