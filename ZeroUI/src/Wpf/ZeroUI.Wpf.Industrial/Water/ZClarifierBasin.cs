using System;
using System.Windows;
using System.Windows.Media;
using ZeroUI.Core.Water;
using ZeroUI.Wpf.Base;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Water
{
    /// <summary>
    /// Water & Wastewater Clarifier Sedimentation Basin visualizer for WPF.
    /// Renders circular basin geometry, perimeter V-notch effluent weir, animated rotating
    /// scraper bridge truss, sludge blanket depth gauge, and hydraulic loading rate HUD.
    /// </summary>
    public class ZClarifierBasin : ZeroWpfVisualBase
    {
        private readonly ClarifierEngine _engine = new ClarifierEngine();
        private string _basinTag = "SEC-CLAR-01";

        protected override bool AutoAnimate => true;

        public ZClarifierBasin()
        {
        }

        #region Public Properties

        public ClarifierEngine Engine => _engine;

        public string BasinTag
        {
            get => _basinTag;
            set
            {
                _basinTag = value ?? "CLAR-01";
                InvalidateVisual();
            }
        }

        public double BasinDiameterM
        {
            get => _engine.Hydraulics.DiameterM;
            set
            {
                _engine.Hydraulics.DiameterM = Math.Max(2.0, value);
                InvalidateVisual();
            }
        }

        public double InfluentFlowM3H
        {
            get => _engine.Hydraulics.InfluentFlowM3H;
            set
            {
                _engine.Hydraulics.InfluentFlowM3H = Math.Max(0.0, value);
                InvalidateVisual();
            }
        }

        public double SludgeBlanketDepthM
        {
            get => _engine.Blanket.BlanketDepthM;
            set
            {
                _engine.Blanket.BlanketDepthM = Math.Max(0.0, value);
                InvalidateVisual();
            }
        }

        public double ScraperTorquePct
        {
            get => _engine.Scraper.TorquePct;
            set
            {
                _engine.Scraper.TorquePct = Math.Max(0.0, Math.Min(100.0, value));
                InvalidateVisual();
            }
        }

        public bool IsScraperRunning
        {
            get => _engine.Scraper.IsRunning;
            set
            {
                _engine.Scraper.IsRunning = value;
                InvalidateVisual();
            }
        }

        #endregion

        protected override void OnAnimationTick(double delta, long frame)
        {
            _engine.AdvanceRotation(delta);
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double w = ActualWidth;
            double h = ActualHeight;
            if (w < 120 || h < 80) return;

            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            // Background
            dc.DrawRectangle(ZeroWpfTheme.BgPrimary, null, new Rect(0, 0, w, h));

            // Header
            DrawHeader(dc, w, dpi);

            double headerH = 44;
            double mainTop = headerH;
            double mainHeight = h - headerH - 12;
            double basinSize = Math.Min(w - 270, mainHeight);

            if (basinSize < 100) return;

            Rect basinRect = new Rect(16, mainTop + (mainHeight - basinSize) / 2, basinSize, basinSize);
            Rect hudRect = new Rect(basinRect.Right + 16, mainTop, w - basinRect.Right - 28, mainHeight);

            // Basin Visualizer
            DrawBasin(dc, basinRect);

            // Telemetry HUD Card
            DrawTelemetryHud(dc, hudRect, dpi);
        }

        private void DrawHeader(DrawingContext dc, double width, double dpi)
        {
            var titleFt = CreateFormattedText($"{_basinTag} — Circular Clarifier & Sedimentation Basin",
                ZeroWpfTheme.BoldTypeface, 11, ZeroWpfTheme.TextPrimary, dpi);
            dc.DrawText(titleFt, new Point(16, 12));

            Brush statusBrush = _engine.Scraper.TorqueStatus switch
            {
                ScraperTorqueStatus.OverTorqueTrip => Brushes.Crimson,
                ScraperTorqueStatus.HighTorqueWarning => Brushes.Orange,
                _ => _engine.Scraper.IsRunning ? Brushes.LimeGreen : Brushes.Gray
            };

            string statusText = _engine.Scraper.TorqueStatus switch
            {
                ScraperTorqueStatus.OverTorqueTrip => "OVERTORQUE TRIP",
                ScraperTorqueStatus.HighTorqueWarning => "HIGH TORQUE WARNING",
                _ => _engine.Scraper.IsRunning ? "BRIDGE ROTATING" : "STOPPED"
            };

            Rect badgeRect = new Rect(width - 180, 10, 164, 22);
            DrawStatusBadge(dc, badgeRect, statusText, statusBrush, ZeroWpfTheme.BoldTypeface, 8.5);
        }

        private void DrawBasin(DrawingContext dc, Rect rect)
        {
            Point center = new Point(rect.Left + rect.Width * 0.5, rect.Top + rect.Height * 0.5);
            double outerRadius = Math.Min(rect.Width, rect.Height) * 0.46;
            double launderRadius = outerRadius * 0.90;
            double centerWellRadius = outerRadius * 0.22;

            // Outer Concrete Tank Wall
            Pen wallPen = new Pen(ZeroWpfTheme.BorderDefault, 4.0);
            wallPen.Freeze();
            dc.DrawEllipse(null, wallPen, center, outerRadius, outerRadius);

            // Effluent Launder Ring
            Brush launderBrush = new SolidColorBrush(Color.FromArgb(40, 14, 165, 233));
            launderBrush.Freeze();
            Pen launderPen = new Pen(Brushes.DodgerBlue, 1.2);
            launderPen.Freeze();
            dc.DrawEllipse(launderBrush, launderPen, center, outerRadius, outerRadius);
            dc.DrawEllipse(null, launderPen, center, launderRadius, launderRadius);

            // Water Basin Fill
            Brush waterBrush = new LinearGradientBrush(
                Color.FromArgb(200, 15, 45, 75),
                Color.FromArgb(220, 10, 25, 45),
                new Point(0, 0), new Point(1, 1));
            waterBrush.Freeze();
            dc.DrawEllipse(waterBrush, null, center, launderRadius, launderRadius);

            // Center Feed Well
            Brush wellBrush = new SolidColorBrush(Color.FromArgb(220, 30, 41, 59));
            wellBrush.Freeze();
            dc.DrawEllipse(wellBrush, ZeroWpfTheme.BorderPen, center, centerWellRadius, centerWellRadius);

            // Rotating Bridge Truss
            double angleRad = _engine.Scraper.CurrentAngleDeg * Math.PI / 180.0;
            Point p1 = new Point(center.X - Math.Cos(angleRad) * (launderRadius * 0.3), center.Y - Math.Sin(angleRad) * (launderRadius * 0.3));
            Point p2 = new Point(center.X + Math.Cos(angleRad) * (launderRadius * 0.98), center.Y + Math.Sin(angleRad) * (launderRadius * 0.98));

            Pen bridgePen = new Pen(Brushes.Gold, 3.5);
            bridgePen.Freeze();
            dc.DrawLine(bridgePen, p1, p2);

            // Scraper Rake Blades
            Pen rakePen = new Pen(new SolidColorBrush(Color.FromArgb(180, 250, 204, 21)), 1.5);
            rakePen.Freeze();
            int rakes = 6;
            for (int i = 1; i <= rakes; i++)
            {
                double frac = (double)i / (rakes + 1);
                double rx = center.X + (p2.X - center.X) * frac;
                double ry = center.Y + (p2.Y - center.Y) * frac;
                double perpRad = angleRad + Math.PI / 2.0;
                double rakeLen = 7.0;
                dc.DrawLine(rakePen,
                    new Point(rx - Math.Cos(perpRad) * rakeLen, ry - Math.Sin(perpRad) * rakeLen),
                    new Point(rx + Math.Cos(perpRad) * rakeLen, ry + Math.Sin(perpRad) * rakeLen));
            }

            // Center Pivot
            dc.DrawEllipse(Brushes.SkyBlue, null, center, 4, 4);
        }

        private void DrawTelemetryHud(DrawingContext dc, Rect rect, double dpi)
        {
            DrawCardBox(dc, rect, "PROCESS TELEMETRY & LOADING", ZeroWpfTheme.BoldTypeface, ZeroWpfTheme.TextSecondary, ZeroWpfTheme.BgCard, ZeroWpfTheme.BorderPen);

            double pad = 12;
            double left = rect.Left + pad;
            double right = rect.Right - pad;
            double rowY = rect.Top + 32;
            double rowH = 22;

            // Influent Flow
            DrawDataRow(dc, left, right, rowY, "Influent Flow", $"{_engine.Hydraulics.InfluentFlowM3H:N0} m³/h",
                ZeroWpfTheme.RegularTypeface, ZeroWpfTheme.BoldTypeface, ZeroWpfTheme.TextSecondary, ZeroWpfTheme.TextPrimary);
            rowY += rowH;

            // Surface Overflow Rate (SOR)
            double sor = _engine.SurfaceOverflowRateM3M2Day;
            Brush sorBrush = sor <= 35.0 ? Brushes.LimeGreen : sor <= 50.0 ? Brushes.Orange : Brushes.Crimson;
            DrawDataRow(dc, left, right, rowY, "Overflow Rate (SOR)", $"{sor:F1} m³/(m²·d)",
                ZeroWpfTheme.RegularTypeface, ZeroWpfTheme.BoldTypeface, ZeroWpfTheme.TextSecondary, sorBrush);
            rowY += rowH;

            // Hydraulic Retention Time (HRT)
            double hrt = _engine.HydraulicRetentionTimeHours;
            Brush hrtBrush = hrt >= 2.0 && hrt <= 4.5 ? Brushes.LimeGreen : Brushes.Orange;
            DrawDataRow(dc, left, right, rowY, "Retention Time (HRT)", $"{hrt:F1} hours",
                ZeroWpfTheme.RegularTypeface, ZeroWpfTheme.BoldTypeface, ZeroWpfTheme.TextSecondary, hrtBrush);
            rowY += rowH;

            // Solids Loading Rate (SLR)
            double slr = _engine.SolidsLoadingRateKgM2Day;
            DrawDataRow(dc, left, right, rowY, "Solids Load (SLR)", $"{slr:F0} kg/(m²·d)",
                ZeroWpfTheme.RegularTypeface, ZeroWpfTheme.BoldTypeface, ZeroWpfTheme.TextSecondary, ZeroWpfTheme.TextPrimary);
            rowY += rowH;

            // Sludge Blanket Depth
            double blanketDepth = _engine.Blanket.BlanketDepthM;
            Brush blanketBrush = _engine.Blanket.IsBlanketHigh ? Brushes.Crimson : Brushes.SkyBlue;
            DrawDataRow(dc, left, right, rowY, "Sludge Blanket", $"{blanketDepth:F2} m / {_engine.Hydraulics.SideWaterDepthM:F1} m",
                ZeroWpfTheme.RegularTypeface, ZeroWpfTheme.BoldTypeface, ZeroWpfTheme.TextSecondary, blanketBrush);
            rowY += rowH;

            // Effluent Turbidity
            double ntu = _engine.Blanket.EffluentTurbidityNtu;
            Brush ntuBrush = ntu <= 2.0 ? Brushes.LimeGreen : ntu <= 5.0 ? Brushes.Orange : Brushes.Crimson;
            DrawDataRow(dc, left, right, rowY, "Effluent Turbidity", $"{ntu:F2} NTU",
                ZeroWpfTheme.RegularTypeface, ZeroWpfTheme.BoldTypeface, ZeroWpfTheme.TextSecondary, ntuBrush);
            rowY += rowH + 6;

            // Bridge Scraper Torque Gauge Bar
            var tLabelFt = CreateFormattedText("Scraper Bridge Torque", ZeroWpfTheme.RegularTypeface, 8.0, ZeroWpfTheme.TextSecondary, dpi);
            dc.DrawText(tLabelFt, new Point(left, rowY));

            var tValFt = CreateFormattedText($"{_engine.Scraper.TorquePct:F1}%", ZeroWpfTheme.BoldTypeface, 8.5, ZeroWpfTheme.TextPrimary, dpi);
            dc.DrawText(tValFt, new Point(right - tValFt.Width, rowY));
            rowY += 16;

            Rect torqueTrack = new Rect(left, rowY, right - left, 7);
            Brush trackBrush = new SolidColorBrush(Color.FromArgb(40, 148, 163, 184));
            trackBrush.Freeze();
            dc.DrawRectangle(trackBrush, null, torqueTrack);

            double torqueFrac = Math.Max(0.0, Math.Min(1.0, _engine.Scraper.TorquePct / 100.0));
            Rect torqueFill = new Rect(torqueTrack.Left, torqueTrack.Top, torqueTrack.Width * torqueFrac, torqueTrack.Height);
            Brush barBrush = _engine.Scraper.TorqueStatus switch
            {
                ScraperTorqueStatus.OverTorqueTrip => Brushes.Crimson,
                ScraperTorqueStatus.HighTorqueWarning => Brushes.Orange,
                _ => Brushes.LimeGreen
            };
            dc.DrawRectangle(barBrush, null, torqueFill);
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZClarifierBasin"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ClarifierBasin is deprecated and will be removed in 5 release cycles. Please migrate to ZClarifierBasin instead.")]
    public class ClarifierBasin : ZClarifierBasin { }

    /// <summary>
    /// Legacy alias for <see cref="ZClarifierBasin"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroClarifierBasin is deprecated and will be removed in 5 release cycles. Please migrate to ZClarifierBasin instead.")]
    public class ZeroClarifierBasin : ZClarifierBasin { }

    #endregion

}
