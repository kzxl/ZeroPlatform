using System;
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
    /// Water & Wastewater Clarifier Sedimentation Basin visualizer for WinForms.
    /// Renders circular basin geometry, perimeter V-notch effluent weir, animated rotating
    /// scraper bridge truss, sludge blanket depth gauge, and hydraulic loading rate HUD.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Water & Wastewater")]
    [Description("Clarifier sedimentation basin with animated rotating scraper bridge and hydraulic loading HUD")]
    public class ZClarifierBasin : VisualControlBase
    {
        private readonly ClarifierEngine _engine = new ClarifierEngine();
        private string _basinTag = "SEC-CLAR-01";

        protected override bool AutoAnimate => true;

        public ZClarifierBasin()
        {
            Size = new Size(680, 360);
        }

        #region Public Properties

        [Category("Water")]
        [Description("Access to the underlying pure clarifier computation engine")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ClarifierEngine Engine => _engine;

        [Category("Water")]
        [DefaultValue("SEC-CLAR-01")]
        public string BasinTag
        {
            get => _basinTag;
            set
            {
                if (_basinTag != value)
                {
                    _basinTag = value ?? "CLAR-01";
                    Invalidate();
                }
            }
        }

        [Category("Water")]
        [DefaultValue(28.0)]
        public double BasinDiameterM
        {
            get => _engine.Hydraulics.DiameterM;
            set
            {
                _engine.Hydraulics.DiameterM = Math.Max(2.0, value);
                Invalidate();
            }
        }

        [Category("Water")]
        [DefaultValue(850.0)]
        public double InfluentFlowM3H
        {
            get => _engine.Hydraulics.InfluentFlowM3H;
            set
            {
                _engine.Hydraulics.InfluentFlowM3H = Math.Max(0.0, value);
                Invalidate();
            }
        }

        [Category("Water")]
        [DefaultValue(1.25)]
        public double SludgeBlanketDepthM
        {
            get => _engine.Blanket.BlanketDepthM;
            set
            {
                _engine.Blanket.BlanketDepthM = Math.Max(0.0, value);
                Invalidate();
            }
        }

        [Category("Water")]
        [DefaultValue(38.5)]
        public double ScraperTorquePct
        {
            get => _engine.Scraper.TorquePct;
            set
            {
                _engine.Scraper.TorquePct = Math.Max(0.0, Math.Min(100.0, value));
                Invalidate();
            }
        }

        [Category("Water")]
        [DefaultValue(true)]
        public bool IsScraperRunning
        {
            get => _engine.Scraper.IsRunning;
            set
            {
                _engine.Scraper.IsRunning = value;
                Invalidate();
            }
        }

        #endregion

        protected override void OnAnimationTick(double delta, long frame)
        {
            _engine.AdvanceRotation(delta);
        }

        protected override void OnDrawVisual(Graphics g, Rectangle bounds, ZeroThemePalette palette)
        {
            // Header
            int headerH = DrawHeader(g, bounds, palette);

            int mainTop = bounds.Y + headerH;
            int mainHeight = bounds.Height - headerH - 12;

            if (bounds.Width < 180 || mainHeight < 80) return;

            if (bounds.Width >= 480)
            {
                int basinSize = Math.Min((int)((bounds.Width - 50) * 0.48f), mainHeight);
                Rectangle basinRect = new Rectangle(bounds.X + 16, mainTop + (mainHeight - basinSize) / 2, basinSize, basinSize);
                Rectangle hudRect = new Rectangle(basinRect.Right + 16, mainTop, bounds.Right - basinRect.Right - 28, mainHeight);

                DrawBasin(g, basinRect, palette);
                DrawTelemetryHud(g, hudRect, palette);
            }
            else
            {
                int halfH = (mainHeight - 12) / 2;
                int basinSize = Math.Min(halfH, bounds.Width - 32);
                Rectangle basinRect = new Rectangle(bounds.X + (bounds.Width - basinSize) / 2, mainTop, basinSize, halfH);
                Rectangle hudRect = new Rectangle(bounds.X + 16, mainTop + halfH + 8, bounds.Width - 32, halfH - 8);

                DrawBasin(g, basinRect, palette);
                DrawTelemetryHud(g, hudRect, palette);
            }
        }

        private int DrawHeader(Graphics g, Rectangle bounds, ZeroThemePalette palette)
        {
            using (var fontTitle = new Font("Segoe UI", 10.5f, FontStyle.Bold))
            using (var fontSub = new Font("Segoe UI", 8.5f))
            {
                string title = $"{_basinTag} — Circular Clarifier Basin";
                Size titleSize = TextRenderer.MeasureText(g, title, fontTitle);

                Color badgeColor = _engine.Scraper.TorqueStatus switch
                {
                    ScraperTorqueStatus.OverTorqueTrip => Color.FromArgb(239, 68, 68),
                    ScraperTorqueStatus.HighTorqueWarning => Color.FromArgb(245, 158, 11),
                    _ => _engine.Scraper.IsRunning ? Color.FromArgb(34, 197, 94) : Color.FromArgb(100, 116, 139)
                };

                string statusText = _engine.Scraper.TorqueStatus switch
                {
                    ScraperTorqueStatus.OverTorqueTrip => "OVERTORQUE TRIP",
                    ScraperTorqueStatus.HighTorqueWarning => "HIGH TORQUE",
                    _ => _engine.Scraper.IsRunning ? "BRIDGE ROTATING" : "STOPPED"
                };

                int badgeW = 150;
                int badgeH = 24;

                if (bounds.Width - badgeW - 20 >= 16 + titleSize.Width + 16)
                {
                    TextRenderer.DrawText(g, title, fontTitle, new Point(bounds.X + 16, bounds.Y + 12), palette.TextPrimary);

                    Rectangle badgeRect = new Rectangle(bounds.Right - badgeW - 16, bounds.Y + 10, badgeW, badgeH);
                    using (var brush = new SolidBrush(Color.FromArgb(35, badgeColor)))
                    using (var pen = new Pen(badgeColor, 1.2f))
                    {
                        g.FillRectangle(brush, badgeRect);
                        g.DrawRectangle(pen, badgeRect);
                    }
                    TextRenderer.DrawText(g, statusText, fontSub, badgeRect, badgeColor,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    return 46;
                }
                else
                {
                    Rectangle titleRect = new Rectangle(bounds.X + 16, bounds.Y + 8, bounds.Width - 32, 22);
                    TextRenderer.DrawText(g, title, fontTitle, titleRect, palette.TextPrimary,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                    Rectangle badgeRect = new Rectangle(bounds.X + 16, bounds.Y + 32, Math.Min(badgeW, bounds.Width - 32), badgeH);
                    using (var brush = new SolidBrush(Color.FromArgb(35, badgeColor)))
                    using (var pen = new Pen(badgeColor, 1.2f))
                    {
                        g.FillRectangle(brush, badgeRect);
                        g.DrawRectangle(pen, badgeRect);
                    }
                    TextRenderer.DrawText(g, statusText, fontSub, badgeRect, badgeColor,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                    return 64;
                }
            }
        }

        private void DrawBasin(Graphics g, Rectangle rect, ZeroThemePalette palette)
        {
            float cx = rect.X + rect.Width * 0.5f;
            float cy = rect.Y + rect.Height * 0.5f;
            float outerRadius = Math.Min(rect.Width, rect.Height) * 0.46f;
            float launderRadius = outerRadius * 0.90f;
            float centerWellRadius = outerRadius * 0.22f;

            // Outer concrete wall
            using (var wallPen = new Pen(palette.Border, 4f))
            {
                g.DrawEllipse(wallPen, cx - outerRadius, cy - outerRadius, outerRadius * 2f, outerRadius * 2f);
            }

            // Effluent Launder Channel (Annular Ring)
            using (var launderBrush = new SolidBrush(Color.FromArgb(40, 14, 165, 233)))
            using (var launderPen = new Pen(Color.FromArgb(14, 165, 233), 1.5f))
            {
                g.FillEllipse(launderBrush, cx - outerRadius, cy - outerRadius, outerRadius * 2f, outerRadius * 2f);
                g.DrawEllipse(launderPen, cx - launderRadius, cy - launderRadius, launderRadius * 2f, launderRadius * 2f);
            }

            // Water Surface Basin Fill with depth gradient
            using (var waterBrush = new LinearGradientBrush(
                new PointF(cx - launderRadius, cy - launderRadius),
                new PointF(cx + launderRadius, cy + launderRadius),
                Color.FromArgb(200, 15, 45, 75),
                Color.FromArgb(220, 10, 25, 45)))
            {
                g.FillEllipse(waterBrush, cx - launderRadius, cy - launderRadius, launderRadius * 2f, launderRadius * 2f);
            }

            // Center Feed Well
            using (var wellBrush = new SolidBrush(Color.FromArgb(220, 30, 41, 59)))
            using (var wellPen = new Pen(palette.Border, 1.5f))
            {
                g.FillEllipse(wellBrush, cx - centerWellRadius, cy - centerWellRadius, centerWellRadius * 2f, centerWellRadius * 2f);
                g.DrawEllipse(wellPen, cx - centerWellRadius, cy - centerWellRadius, centerWellRadius * 2f, centerWellRadius * 2f);
            }

            // Rotating Scraper Bridge Truss
            double angleRad = _engine.Scraper.CurrentAngleDeg * Math.PI / 180.0;
            float bx1 = cx - (float)Math.Cos(angleRad) * (launderRadius * 0.3f);
            float by1 = cy - (float)Math.Sin(angleRad) * (launderRadius * 0.3f);
            float bx2 = cx + (float)Math.Cos(angleRad) * (launderRadius * 0.98f);
            float by2 = cy + (float)Math.Sin(angleRad) * (launderRadius * 0.98f);

            // Bridge beam
            using (var bridgePen = new Pen(Color.FromArgb(250, 204, 21), 3.5f)) // Safety Amber
            {
                g.DrawLine(bridgePen, bx1, by1, bx2, by2);
            }

            // Scraper Rake Blades perpendicular along bridge
            using (var rakePen = new Pen(Color.FromArgb(200, 250, 204, 21), 1.5f))
            {
                int rakes = 6;
                for (int i = 1; i <= rakes; i++)
                {
                    float frac = (float)i / (rakes + 1);
                    float rx = cx + (bx2 - cx) * frac;
                    float ry = cy + (by2 - cy) * frac;
                    float perpRad = (float)(angleRad + Math.PI / 2.0);
                    float rakeLen = 8f;
                    g.DrawLine(rakePen,
                        rx - (float)Math.Cos(perpRad) * rakeLen,
                        ry - (float)Math.Sin(perpRad) * rakeLen,
                        rx + (float)Math.Cos(perpRad) * rakeLen,
                        ry + (float)Math.Sin(perpRad) * rakeLen);
                }
            }

            // Center Pivot Pin
            using (var pivotBrush = new SolidBrush(Color.FromArgb(56, 189, 248)))
            {
                g.FillEllipse(pivotBrush, cx - 4, cy - 4, 8, 8);
            }

            // Inflow Vector Arrow at Center
            using (var arrowPen = new Pen(Color.FromArgb(56, 189, 248), 2f))
            {
                g.DrawLine(arrowPen, cx - 12, cy, cx + 12, cy);
                g.DrawLine(arrowPen, cx, cy - 12, cx, cy + 12);
            }
        }

        private void DrawTelemetryHud(Graphics g, Rectangle rect, ZeroThemePalette palette)
        {
            using (var fontTitle = new Font("Segoe UI", 9f, FontStyle.Bold))
            using (var fontLabel = new Font("Segoe UI", 8f))
            using (var fontValue = new Font("Segoe UI", 9f, FontStyle.Bold))
            {
                PaintHelper.DrawCardBox(g, rect, "PROCESS TELEMETRY & LOADING", fontTitle, palette);

                int pad = 12;
                int innerW = rect.Width - pad * 2;
                int rowY = rect.Y + 34;
                int rowH = 24;

                // Influent Flow
                PaintHelper.DrawDataRow(g, new Rectangle(rect.X + pad, rowY, innerW, rowH),
                    "Influent Flow", $"{_engine.Hydraulics.InfluentFlowM3H:N0} m³/h",
                    palette.TextSecondary, palette.TextPrimary, fontLabel, fontValue);
                rowY += rowH;

                // Surface Overflow Rate (SOR)
                double sor = _engine.SurfaceOverflowRateM3M2Day;
                Color sorColor = sor <= 35.0 ? Color.FromArgb(34, 197, 94) :
                                 sor <= 50.0 ? Color.FromArgb(245, 158, 11) : Color.FromArgb(239, 68, 68);
                PaintHelper.DrawDataRow(g, new Rectangle(rect.X + pad, rowY, innerW, rowH),
                    "Overflow Rate (SOR)", $"{sor:F1} m³/(m²·d)",
                    palette.TextSecondary, sorColor, fontLabel, fontValue);
                rowY += rowH;

                // Hydraulic Retention Time (HRT)
                double hrt = _engine.HydraulicRetentionTimeHours;
                Color hrtColor = hrt >= 2.0 && hrt <= 4.5 ? Color.FromArgb(34, 197, 94) : Color.FromArgb(245, 158, 11);
                PaintHelper.DrawDataRow(g, new Rectangle(rect.X + pad, rowY, innerW, rowH),
                    "Retention Time (HRT)", $"{hrt:F1} hours",
                    palette.TextSecondary, hrtColor, fontLabel, fontValue);
                rowY += rowH;

                // Solids Loading Rate (SLR)
                double slr = _engine.SolidsLoadingRateKgM2Day;
                PaintHelper.DrawDataRow(g, new Rectangle(rect.X + pad, rowY, innerW, rowH),
                    "Solids Load (SLR)", $"{slr:F0} kg/(m²·d)",
                    palette.TextSecondary, palette.TextPrimary, fontLabel, fontValue);
                rowY += rowH;

                // Sludge Blanket Depth
                double blanketDepth = _engine.Blanket.BlanketDepthM;
                Color blanketColor = _engine.Blanket.IsBlanketHigh ? Color.FromArgb(239, 68, 68) : Color.FromArgb(56, 189, 248);
                PaintHelper.DrawDataRow(g, new Rectangle(rect.X + pad, rowY, innerW, rowH),
                    "Sludge Blanket", $"{blanketDepth:F2} m / {_engine.Hydraulics.SideWaterDepthM:F1} m",
                    palette.TextSecondary, blanketColor, fontLabel, fontValue);
                rowY += rowH;

                // Effluent Turbidity
                double ntu = _engine.Blanket.EffluentTurbidityNtu;
                Color ntuColor = ntu <= 2.0 ? Color.FromArgb(34, 197, 94) :
                                 ntu <= 5.0 ? Color.FromArgb(245, 158, 11) : Color.FromArgb(239, 68, 68);
                PaintHelper.DrawDataRow(g, new Rectangle(rect.X + pad, rowY, innerW, rowH),
                    "Effluent Turbidity", $"{ntu:F2} NTU",
                    palette.TextSecondary, ntuColor, fontLabel, fontValue);
                rowY += rowH + 6;

                // Bridge Scraper Torque Gauge Bar
                TextRenderer.DrawText(g, "Scraper Bridge Torque", fontLabel, new Point(rect.X + pad, rowY), palette.TextSecondary);
                string torqueStr = $"{_engine.Scraper.TorquePct:F1}%";
                var tSz = TextRenderer.MeasureText(g, torqueStr, fontValue);
                TextRenderer.DrawText(g, torqueStr, fontValue, new Point(rect.Right - pad - tSz.Width, rowY), palette.TextPrimary);
                rowY += 18;

                Rectangle torqueTrack = new Rectangle(rect.X + pad, rowY, innerW, 8);
                using (var trackBrush = new SolidBrush(Color.FromArgb(40, palette.Border)))
                {
                    g.FillRectangle(trackBrush, torqueTrack);
                }

                double torqueFrac = Math.Max(0.0, Math.Min(1.0, _engine.Scraper.TorquePct / 100.0));
                Rectangle torqueFill = new Rectangle(torqueTrack.X, torqueTrack.Y, (int)(torqueTrack.Width * torqueFrac), torqueTrack.Height);
                Color barColor = _engine.Scraper.TorqueStatus switch
                {
                    ScraperTorqueStatus.OverTorqueTrip => Color.FromArgb(239, 68, 68),
                    ScraperTorqueStatus.HighTorqueWarning => Color.FromArgb(245, 158, 11),
                    _ => Color.FromArgb(34, 197, 94)
                };
                using (var barBrush = new SolidBrush(barColor))
                {
                    g.FillRectangle(barBrush, torqueFill);
                }
            }
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZClarifierBasin"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ClarifierBasin is deprecated and will be removed in 5 release cycles. Please migrate to ZClarifierBasin instead.")]
    [ToolboxItem(false)]
    public class ClarifierBasin : ZClarifierBasin
    {
    }

    #endregion
}
