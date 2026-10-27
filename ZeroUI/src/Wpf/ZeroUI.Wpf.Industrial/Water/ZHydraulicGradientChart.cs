using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Water;
using ZeroUI.Wpf.Base;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Water
{
    /// <summary>
    /// Pipeline Hydraulic Grade Line (HGL) & Energy Grade Line (EGL) profile chart for WPF.
    /// Visualizes ground topography, pipe invert/crown profile, water column head, booster pump lifts,
    /// and negative pressure cavitation risk detection with interactive chainage inspection.
    /// </summary>
    public class ZHydraulicGradientChart : ZeroWpfVisualBase
    {
        private readonly HydraulicGradientEngine _engine = new HydraulicGradientEngine();
        private string _pipelineTag = "TRANSMISSION-MAIN-01";
        private Point _hoverPoint = new Point(-1, -1);
        private bool _isHovering = false;

        public ZHydraulicGradientChart()
        {
            MouseMove += OnWpfMouseMove;
            MouseLeave += OnWpfMouseLeave;
        }

        #region Public Properties

        public HydraulicGradientEngine Engine => _engine;

        public string PipelineTag
        {
            get => _pipelineTag;
            set
            {
                _pipelineTag = value ?? "MAIN-01";
                InvalidateVisual();
            }
        }

        public double FlowRateM3S
        {
            get => _engine.FlowRateM3S;
            set
            {
                _engine.FlowRateM3S = Math.Max(0.001, value);
                InvalidateVisual();
            }
        }

        public double StartingHeadM
        {
            get => _engine.StartingHeadM;
            set
            {
                _engine.StartingHeadM = value;
                InvalidateVisual();
            }
        }

        #endregion

        private void OnWpfMouseMove(object sender, MouseEventArgs e)
        {
            _hoverPoint = e.GetPosition(this);
            _isHovering = true;
            InvalidateVisual();
        }

        private void OnWpfMouseLeave(object sender, MouseEventArgs e)
        {
            _isHovering = false;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            var profile = _engine.CalculateProfile();
            if (profile.Count == 0) return;

            double w = ActualWidth;
            double h = ActualHeight;
            if (w < 120 || h < 80) return;

            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            // Background
            dc.DrawRectangle(ZeroWpfTheme.BgPrimary, null, new Rect(0, 0, w, h));

            // Header
            DrawHeader(dc, w, dpi);

            double headerH = 44;
            double chartLeft = 52;
            double chartTop = headerH;
            double chartW = w - 68;
            double chartH = h - headerH - 32;

            if (chartW < 100 || chartH < 60) return;

            Rect chartArea = new Rect(chartLeft, chartTop, chartW, chartH);

            // Scales
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

            // Grid & Axes
            DrawGrid(dc, chartArea, minElev, maxElev, maxDist, dpi);

            // Terrain & Pipe
            DrawTerrainAndPipe(dc, chartArea, profile, minElev, rangeElev, maxDist);

            // HGL & EGL Lines
            DrawGradeLines(dc, chartArea, profile, minElev, rangeElev, maxDist);

            // Inspection Tooltip
            if (_isHovering && chartArea.Contains(_hoverPoint))
            {
                DrawHoverTooltip(dc, chartArea, profile, minElev, rangeElev, maxDist, _hoverPoint, dpi);
            }
        }

        private void DrawHeader(DrawingContext dc, double width, double dpi)
        {
            var titleFt = CreateFormattedText($"{_pipelineTag} — Hydraulic Grade Profile (HGL & EGL)",
                ZeroWpfTheme.BoldTypeface, 11, ZeroWpfTheme.TextPrimary, dpi);
            dc.DrawText(titleFt, new Point(14, 12));

            bool hasCavitation = _engine.HasNegativePressureAnomaly(out double worstSt, out double minHead);
            string statusStr = hasCavitation
                ? $"CAVITATION RISK (Min: {minHead:F1} m at {worstSt:N0}m)"
                : $"POSITIVE PRESSURE (Min Head: {minHead:F1} m)";
            Brush statusBrush = hasCavitation ? Brushes.Crimson : Brushes.LimeGreen;

            Rect badgeRect = new Rect(width - 270, 10, 256, 22);
            DrawStatusBadge(dc, badgeRect, statusStr, statusBrush, ZeroWpfTheme.BoldTypeface, 8.5);
        }

        private void DrawGrid(DrawingContext dc, Rect area, double minElev, double maxElev, double maxDist, double dpi)
        {
            dc.DrawRectangle(null, ZeroWpfTheme.BorderPen, area);

            Pen gridPen = new Pen(new SolidColorBrush(Color.FromArgb(25, 148, 163, 184)), 1.0);
            gridPen.DashStyle = DashStyles.Dot;
            gridPen.Freeze();

            // Y Axis
            int yDivs = 5;
            for (int i = 0; i <= yDivs; i++)
            {
                double y = area.Top + (double)i / yDivs * area.Height;
                dc.DrawLine(gridPen, new Point(area.Left, y), new Point(area.Right, y));

                double elevVal = maxElev - ((double)i / yDivs) * (maxElev - minElev);
                var elevFt = CreateFormattedText($"{elevVal:F0}m", ZeroWpfTheme.RegularTypeface, 7.5, ZeroWpfTheme.TextSecondary, dpi);
                dc.DrawText(elevFt, new Point(area.Left - elevFt.Width - 4, y - elevFt.Height * 0.5));
            }

            // X Axis
            int xDivs = 5;
            for (int i = 0; i <= xDivs; i++)
            {
                double x = area.Left + (double)i / xDivs * area.Width;
                dc.DrawLine(gridPen, new Point(x, area.Top), new Point(x, area.Bottom));

                double distVal = ((double)i / xDivs) * maxDist;
                var distFt = CreateFormattedText($"{distVal:N0}m", ZeroWpfTheme.RegularTypeface, 7.5, ZeroWpfTheme.TextSecondary, dpi);
                dc.DrawText(distFt, new Point(x - distFt.Width * 0.5, area.Bottom + 4));
            }
        }

        private void DrawTerrainAndPipe(DrawingContext dc, Rect area, List<HydraulicProfilePoint> profile, double minElev, double rangeElev, double maxDist)
        {
            Point[] groundPoints = new Point[profile.Count];
            Point[] invertPoints = new Point[profile.Count];
            Point[] crownPoints = new Point[profile.Count];

            for (int i = 0; i < profile.Count; i++)
            {
                var pt = profile[i];
                double x = area.Left + (pt.StationM / maxDist) * area.Width;
                double yGround = area.Bottom - ((pt.GroundElevationM - minElev) / rangeElev) * area.Height;
                double yInvert = area.Bottom - ((pt.PipeInvertM - minElev) / rangeElev) * area.Height;
                double yCrown = area.Bottom - ((pt.PipeCrownM - minElev) / rangeElev) * area.Height;

                groundPoints[i] = new Point(x, yGround);
                invertPoints[i] = new Point(x, yInvert);
                crownPoints[i] = new Point(x, yCrown);
            }

            // Pipe Body geometry
            Brush pipeBrush = new SolidColorBrush(Color.FromArgb(50, 71, 85, 105));
            pipeBrush.Freeze();
            Pen pipePen = new Pen(Brushes.SlateGray, 1.5);
            pipePen.Freeze();

            for (int i = 0; i < profile.Count - 1; i++)
            {
                StreamGeometry geom = new StreamGeometry();
                using (var ctx = geom.Open())
                {
                    ctx.BeginFigure(crownPoints[i], true, true);
                    ctx.LineTo(crownPoints[i + 1], true, false);
                    ctx.LineTo(invertPoints[i + 1], true, false);
                    ctx.LineTo(invertPoints[i], true, false);
                }
                geom.Freeze();
                dc.DrawGeometry(pipeBrush, pipePen, geom);
            }

            // Ground Profile dashed line
            Pen groundPen = new Pen(Brushes.DarkGray, 1.5);
            groundPen.DashStyle = DashStyles.Dash;
            groundPen.Freeze();

            for (int i = 0; i < profile.Count - 1; i++)
            {
                dc.DrawLine(groundPen, groundPoints[i], groundPoints[i + 1]);
            }
        }

        private void DrawGradeLines(DrawingContext dc, Rect area, List<HydraulicProfilePoint> profile, double minElev, double rangeElev, double maxDist)
        {
            Point[] hglPoints = new Point[profile.Count];
            Point[] eglPoints = new Point[profile.Count];

            for (int i = 0; i < profile.Count; i++)
            {
                var pt = profile[i];
                double x = area.Left + (pt.StationM / maxDist) * area.Width;
                double yHgl = area.Bottom - ((pt.HglElevationM - minElev) / rangeElev) * area.Height;
                double yEgl = area.Bottom - ((pt.EglElevationM - minElev) / rangeElev) * area.Height;

                hglPoints[i] = new Point(x, yHgl);
                eglPoints[i] = new Point(x, yEgl);
            }

            // EGL (Amber Line)
            Pen eglPen = new Pen(Brushes.Orange, 1.5);
            eglPen.Freeze();
            for (int i = 0; i < profile.Count - 1; i++)
            {
                dc.DrawLine(eglPen, eglPoints[i], eglPoints[i + 1]);
            }

            // HGL (Sky Blue Line)
            Pen hglPen = new Pen(Brushes.SkyBlue, 2.2);
            hglPen.Freeze();
            for (int i = 0; i < profile.Count - 1; i++)
            {
                dc.DrawLine(hglPen, hglPoints[i], hglPoints[i + 1]);
            }

            // Station Nodes
            for (int i = 0; i < profile.Count; i++)
            {
                Brush nodeBrush = profile[i].IsNegativePressure ? Brushes.Crimson : Brushes.SkyBlue;
                dc.DrawEllipse(nodeBrush, null, hglPoints[i], 3.5, 3.5);
            }
        }

        private void DrawHoverTooltip(DrawingContext dc, Rect area, List<HydraulicProfilePoint> profile, double minElev, double rangeElev, double maxDist, Point mouse, double dpi)
        {
            double mouseRatio = Math.Max(0.0, Math.Min(1.0, (mouse.X - area.Left) / area.Width));
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

            // Crosshair vertical cursor
            double nearestX = area.Left + (nearest.StationM / maxDist) * area.Width;
            Pen cursorPen = new Pen(ZeroWpfTheme.TextSecondary, 1.0);
            cursorPen.DashStyle = DashStyles.Dot;
            cursorPen.Freeze();
            dc.DrawLine(cursorPen, new Point(nearestX, area.Top), new Point(nearestX, area.Bottom));

            // Tooltip card
            double tipW = 180;
            double tipH = 72;
            double tipX = Math.Min(area.Right - tipW - 4, nearestX + 10);
            double tipY = Math.Max(area.Top + 4, mouse.Y - tipH * 0.5);

            Rect tipRect = new Rect(tipX, tipY, tipW, tipH);
            Brush tipBg = new SolidColorBrush(Color.FromArgb(240, 15, 23, 42));
            tipBg.Freeze();
            Pen tipBorder = new Pen(nearest.IsNegativePressure ? Brushes.Crimson : Brushes.SkyBlue, 1.2);
            tipBorder.Freeze();

            dc.DrawRectangle(tipBg, tipBorder, tipRect);

            var stFt = CreateFormattedText($"Station: {nearest.StationM:N0} m", ZeroWpfTheme.BoldTypeface, 8.5, Brushes.White, dpi);
            dc.DrawText(stFt, new Point(tipX + 6, tipY + 4));

            var hglFt = CreateFormattedText($"HGL: {nearest.HglElevationM:F1} m | EGL: {nearest.EglElevationM:F1} m", ZeroWpfTheme.RegularTypeface, 7.5, Brushes.SkyBlue, dpi);
            dc.DrawText(hglFt, new Point(tipX + 6, tipY + 22));

            var pipeFt = CreateFormattedText($"Pipe Invert: {nearest.PipeInvertM:F1} m", ZeroWpfTheme.RegularTypeface, 7.5, Brushes.SlateGray, dpi);
            dc.DrawText(pipeFt, new Point(tipX + 6, tipY + 38));

            Brush pressBrush = nearest.IsNegativePressure ? Brushes.Crimson : Brushes.LimeGreen;
            var pressFt = CreateFormattedText($"Head: {nearest.PressureHeadM:F1} m ({nearest.PressureKpa:F0} kPa)", ZeroWpfTheme.BoldTypeface, 8.5, pressBrush, dpi);
            dc.DrawText(pressFt, new Point(tipX + 6, tipY + 54));
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZHydraulicGradientChart"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("HydraulicGradientChart is deprecated and will be removed in 5 release cycles. Please migrate to ZHydraulicGradientChart instead.")]
    public class HydraulicGradientChart : ZHydraulicGradientChart { }

    /// <summary>
    /// Legacy alias for <see cref="ZHydraulicGradientChart"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroHydraulicGradientChart is deprecated and will be removed in 5 release cycles. Please migrate to ZHydraulicGradientChart instead.")]
    public class ZeroHydraulicGradientChart : ZHydraulicGradientChart { }

    #endregion

}
