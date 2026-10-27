using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Process;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Process
{
    /// <summary>
    /// ISO 14644-1 and EU GMP Annex 1 Cleanroom Environmental Monitor for WinForms.
    /// Visualizes multi-zone positive pressure cascade gradients, airborne non-viable particulate meters,
    /// and air change rate (ACH) compliance to prevent aseptic cross-contamination.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Process & Life Sciences")]
    [Description("Cleanroom multi-zone pressure cascade and particulate compliance monitor")]
    public class ZCleanroomEnvHud : VisualControlBase
    {
        private readonly CleanroomEngine _engine = new CleanroomEngine();
        private string _facilityTag = "ASEPTIC-SUITE-B";

        public ZCleanroomEnvHud()
        {
            Size = new Size(760, 360);
        }

        #region Public Properties

        [Category("Cleanroom")]
        [Description("Access to the underlying pure cleanroom environmental monitoring engine")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public CleanroomEngine Engine => _engine;

        [Category("Cleanroom")]
        [DefaultValue("ASEPTIC-SUITE-B")]
        public string FacilityTag
        {
            get => _facilityTag;
            set
            {
                _facilityTag = value ?? "SUITE-01";
                Invalidate();
            }
        }

        #endregion

        protected override void OnDrawVisual(Graphics g, Rectangle bounds, ZeroThemePalette palette)
        {
            // 1. Header
            DrawHeader(g, bounds, palette, out int headerH);

            int mainTop = bounds.Y + headerH;
            int mainHeight = bounds.Height - headerH - 12;

            int hudW = bounds.Width < 540 ? Math.Max(160, (int)(bounds.Width * 0.42)) : 260;
            int cascadeW = bounds.Width - hudW - 32;

            if (cascadeW < 80 || mainHeight < 80) return;

            Rectangle cascadeRect = new Rectangle(bounds.X + 16, mainTop, cascadeW, mainHeight);
            Rectangle hudRect = new Rectangle(cascadeRect.Right + 12, mainTop, hudW, mainHeight);

            // 2. Cascade Differential Pressure Staircase
            DrawCascadeChart(g, cascadeRect, palette);

            // 3. Environmental HUD (Particulates & ACH)
            DrawCleanroomHud(g, hudRect, palette);
        }

        private void DrawHeader(Graphics g, Rectangle bounds, ZeroThemePalette palette, out int headerH)
        {
            string title = $"{_facilityTag} — Cleanroom Cascade & Environmental Monitor";
            using (var fontTitle = new Font("Segoe UI", 10.5f, FontStyle.Bold))
            using (var fontSub = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            {
                Size titleSize = TextRenderer.MeasureText(g, title, fontTitle);

                bool isCompliant = _engine.IsOverallFacilityCompliant;
                Color badgeColor = isCompliant ? Color.FromArgb(34, 197, 94) : Color.FromArgb(239, 68, 68);
                string badgeStr = isCompliant ? "ISO 14644-1 COMPLIANT" : "CASCADE BREACH";

                int badgeW = 194;
                int badgeH = 24;

                if (bounds.Width - badgeW - 20 >= 20 + titleSize.Width + 16)
                {
                    TextRenderer.DrawText(g, title, fontTitle, new Point(bounds.X + 16, bounds.Y + 12), palette.TextPrimary);
                    Rectangle badgeRect = new Rectangle(bounds.Right - badgeW - 16, bounds.Y + 10, badgeW, badgeH);
                    PaintHelper.DrawStatusBadge(g, badgeRect, badgeStr, fontSub, badgeColor, badgeColor, 4);
                    headerH = 46;
                }
                else
                {
                    Rectangle titleRect = new Rectangle(bounds.X + 16, bounds.Y + 8, bounds.Width - 32, 20);
                    TextRenderer.DrawText(g, title, fontTitle, titleRect, palette.TextPrimary, TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.EndEllipsis);
                    Rectangle badgeRect = new Rectangle(bounds.X + 16, bounds.Y + 32, Math.Min(bounds.Width - 32, badgeW), badgeH);
                    PaintHelper.DrawStatusBadge(g, badgeRect, badgeStr, fontSub, badgeColor, badgeColor, 4);
                    headerH = 64;
                }
            }
        }

        private void DrawCascadeChart(Graphics g, Rectangle rect, ZeroThemePalette palette)
        {
            using (var fontTitle = new Font("Segoe UI", 9f, FontStyle.Bold))
            using (var fontZone = new Font("Segoe UI", 8f, FontStyle.Bold))
            using (var fontPa = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            using (var fontSub = new Font("Segoe UI", 7.5f))
            {
                PaintHelper.DrawCardBox(g, rect, "DIFFERENTIAL PRESSURE CASCADE (ΔP)", fontTitle, palette);

                int pad = 12;
                int innerW = rect.Width - pad * 2;
                int innerH = rect.Height - 48;
                int chartY = rect.Y + 34;

                int zoneCount = _engine.Zones.Count;
                if (zoneCount == 0) return;

                int barW = (innerW - (zoneCount - 1) * 8) / zoneCount;
                double maxPressure = 75.0; // 0 - 75 Pa scale

                for (int i = 0; i < zoneCount; i++)
                {
                    var z = _engine.Zones[i];
                    int x = rect.X + pad + i * (barW + 8);

                    double frac = Math.Max(0.0, Math.Min(1.0, z.DifferentialPressurePa / maxPressure));
                    int barH = (int)(innerH * frac);
                    Rectangle barRect = new Rectangle(x, chartY + innerH - barH - 24, barW, barH);

                    Color barCol = z.IsPressureOk ? Color.FromArgb(34, 197, 94) : Color.FromArgb(239, 68, 68);

                    // Bar Fill
                    using (var barBrush = new LinearGradientBrush(
                        new Point(barRect.X, barRect.Y),
                        new Point(barRect.X, barRect.Bottom),
                        Color.FromArgb(220, barCol),
                        Color.FromArgb(120, barCol)))
                    using (var barPen = new Pen(barCol, 1.2f))
                    {
                        g.FillRectangle(barBrush, barRect);
                        g.DrawRectangle(barPen, barRect);
                    }

                    // Pressure value on top of bar
                    string paStr = $"{z.DifferentialPressurePa:F1} Pa";
                    TextRenderer.DrawText(g, paStr, fontPa,
                        new Rectangle(x, barRect.Top - 18, barW, 16),
                        barCol, TextFormatFlags.HorizontalCenter);

                    // Zone tag & ISO Class underneath
                    string tagStr = z.ZoneTag;
                    string isoStr = z.IsoClass switch
                    {
                        IsoCleanroomClass.IsoClass5 => "ISO 5 (A/B)",
                        IsoCleanroomClass.IsoClass7 => "ISO 7 (C)",
                        IsoCleanroomClass.IsoClass8 => "ISO 8 (D)",
                        _ => "CNC"
                    };

                    TextRenderer.DrawText(g, tagStr, fontZone,
                        new Rectangle(x, chartY + innerH - 20, barW, 14),
                        palette.TextPrimary, TextFormatFlags.HorizontalCenter);
                    TextRenderer.DrawText(g, isoStr, fontSub,
                        new Rectangle(x, chartY + innerH - 6, barW, 14),
                        palette.TextSecondary, TextFormatFlags.HorizontalCenter);
                }
            }
        }

        private void DrawCleanroomHud(Graphics g, Rectangle rect, ZeroThemePalette palette)
        {
            using (var fontTitle = new Font("Segoe UI", 9f, FontStyle.Bold))
            using (var fontLabel = new Font("Segoe UI", 8f))
            using (var fontValue = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            {
                PaintHelper.DrawCardBox(g, rect, "CORE ZONE PARTICULATES & ACH", fontTitle, palette);

                int pad = 12;
                int innerW = rect.Width - pad * 2;
                int rowY = rect.Y + 34;
                int rowH = 24;

                // Pick highest grade zone (last zone: CORE-104)
                var coreZone = _engine.Zones[_engine.Zones.Count - 1];

                // Zone Name
                PaintHelper.DrawDataRow(g, new Rectangle(rect.X + pad, rowY, innerW, rowH),
                    "Monitored Zone", $"{coreZone.ZoneTag} ({coreZone.ZoneName})",
                    palette.TextSecondary, palette.TextPrimary, fontLabel, fontValue);
                rowY += rowH;

                // Particulate >= 0.5 um
                double p05 = coreZone.ParticleCount05Um;
                double max05 = coreZone.MaxAllowed05Um;
                Color p05Col = p05 <= max05 ? Color.FromArgb(34, 197, 94) : Color.FromArgb(239, 68, 68);
                PaintHelper.DrawDataRow(g, new Rectangle(rect.X + pad, rowY, innerW, rowH),
                    "Particles ≥ 0.5 μm", $"{p05:N0} / {max05:N0} m⁻³",
                    palette.TextSecondary, p05Col, fontLabel, fontValue);
                rowY += rowH;

                // Particulate >= 5.0 um
                double p50 = coreZone.ParticleCount50Um;
                double max50 = coreZone.MaxAllowed50Um;
                Color p50Col = p50 <= max50 ? Color.FromArgb(34, 197, 94) : Color.FromArgb(239, 68, 68);
                PaintHelper.DrawDataRow(g, new Rectangle(rect.X + pad, rowY, innerW, rowH),
                    "Particles ≥ 5.0 μm", $"{p50:F0} / {max50:F0} m⁻³",
                    palette.TextSecondary, p50Col, fontLabel, fontValue);
                rowY += rowH;

                // Air Change Rate (ACH)
                double ach = coreZone.AirChangesPerHour;
                Color achCol = coreZone.IsAchOk ? Color.FromArgb(34, 197, 94) : Color.FromArgb(245, 158, 11);
                PaintHelper.DrawDataRow(g, new Rectangle(rect.X + pad, rowY, innerW, rowH),
                    "Air Changes (ACH)", $"{ach:F0} ACH (Min {coreZone.MinRequiredAch:F0})",
                    palette.TextSecondary, achCol, fontLabel, fontValue);
                rowY += rowH;

                // Room Temp & Humidity
                PaintHelper.DrawDataRow(g, new Rectangle(rect.X + pad, rowY, innerW, rowH),
                    "Environment", $"{coreZone.RoomTempC:F1}°C | {coreZone.RelativeHumidityPct:F0}% RH",
                    palette.TextSecondary, palette.TextPrimary, fontLabel, fontValue);
            }
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZCleanroomEnvHud"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("CleanroomEnvHud is deprecated and will be removed in 5 release cycles. Please migrate to ZCleanroomEnvHud instead.")]
    [ToolboxItem(false)]
    public class CleanroomEnvHud : ZCleanroomEnvHud
    {
    }

    #endregion
}
