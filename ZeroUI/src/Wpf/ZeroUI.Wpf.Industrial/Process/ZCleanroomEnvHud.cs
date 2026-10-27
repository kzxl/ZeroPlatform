using System;
using System.Windows;
using System.Windows.Media;
using ZeroUI.Core.Process;
using ZeroUI.Wpf.Base;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Process
{
    /// <summary>
    /// ISO 14644-1 and EU GMP Annex 1 Cleanroom Environmental Monitor for WPF.
    /// Visualizes multi-zone positive pressure cascade gradients, airborne non-viable particulate meters,
    /// and air change rate (ACH) compliance to prevent aseptic cross-contamination.
    /// </summary>
    public class ZCleanroomEnvHud : ZeroWpfVisualBase
    {
        private readonly CleanroomEngine _engine = new CleanroomEngine();
        private string _facilityTag = "ASEPTIC-SUITE-B";

        public ZCleanroomEnvHud()
        {
        }

        #region Public Properties

        public CleanroomEngine Engine => _engine;

        public string FacilityTag
        {
            get => _facilityTag;
            set
            {
                _facilityTag = value ?? "SUITE-01";
                InvalidateVisual();
            }
        }

        #endregion

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

            double hudW = 250;
            double cascadeW = w - hudW - 28;

            if (cascadeW < 120 || mainHeight < 80) return;

            Rect cascadeRect = new Rect(14, mainTop, cascadeW, mainHeight);
            Rect hudRect = new Rect(cascadeRect.Right + 10, mainTop, hudW, mainHeight);

            // Cascade Staircase
            DrawCascadeChart(dc, cascadeRect, dpi);

            // Cleanroom HUD
            DrawCleanroomHud(dc, hudRect, dpi);
        }

        private void DrawHeader(DrawingContext dc, double width, double dpi)
        {
            var titleFt = CreateFormattedText($"{_facilityTag} — Cleanroom Cascade & Environmental Monitor",
                ZeroWpfTheme.BoldTypeface, 11, ZeroWpfTheme.TextPrimary, dpi);
            dc.DrawText(titleFt, new Point(14, 12));

            bool isCompliant = _engine.IsOverallFacilityCompliant;
            Brush badgeBrush = isCompliant ? Brushes.LimeGreen : Brushes.Crimson;
            string badgeStr = isCompliant ? "ISO 14644-1 COMPLIANT" : "CASCADE BREACH";

            Rect badgeRect = new Rect(width - 200, 10, 186, 22);
            DrawStatusBadge(dc, badgeRect, badgeStr, badgeBrush, ZeroWpfTheme.BoldTypeface, 8.5);
        }

        private void DrawCascadeChart(DrawingContext dc, Rect rect, double dpi)
        {
            DrawCardBox(dc, rect, "DIFFERENTIAL PRESSURE CASCADE (ΔP)", ZeroWpfTheme.BoldTypeface, ZeroWpfTheme.TextSecondary, ZeroWpfTheme.BgCard, ZeroWpfTheme.BorderPen);

            double pad = 10;
            double innerW = rect.Width - pad * 2;
            double innerH = rect.Height - 44;
            double chartY = rect.Top + 30;

            int zoneCount = _engine.Zones.Count;
            if (zoneCount == 0) return;

            double barW = (innerW - (zoneCount - 1) * 8) / zoneCount;
            double maxPressure = 75.0;

            for (int i = 0; i < zoneCount; i++)
            {
                var z = _engine.Zones[i];
                double x = rect.Left + pad + i * (barW + 8);

                double frac = Math.Max(0.0, Math.Min(1.0, z.DifferentialPressurePa / maxPressure));
                double barH = innerH * frac;
                Rect barRect = new Rect(x, chartY + innerH - barH - 22, barW, barH);

                Brush barCol = z.IsPressureOk ? Brushes.LimeGreen : Brushes.Crimson;

                Brush fillBrush = z.IsPressureOk
                    ? new LinearGradientBrush(Color.FromArgb(220, 34, 197, 94), Color.FromArgb(120, 34, 197, 94), 90)
                    : new LinearGradientBrush(Color.FromArgb(220, 239, 68, 68), Color.FromArgb(120, 239, 68, 68), 90);
                fillBrush.Freeze();

                dc.DrawRectangle(fillBrush, new Pen(barCol, 1.2), barRect);

                // Pressure value
                string paStr = $"{z.DifferentialPressurePa:F1} Pa";
                var paFt = CreateFormattedText(paStr, ZeroWpfTheme.BoldTypeface, 8.0, barCol, dpi);
                dc.DrawText(paFt, new Point(x + (barW - paFt.Width) * 0.5, barRect.Top - 16));

                // Zone Tag
                var tagFt = CreateFormattedText(z.ZoneTag, ZeroWpfTheme.BoldTypeface, 7.5, ZeroWpfTheme.TextPrimary, dpi);
                dc.DrawText(tagFt, new Point(x + (barW - tagFt.Width) * 0.5, chartY + innerH - 18));

                // ISO Class
                string isoStr = z.IsoClass switch
                {
                    IsoCleanroomClass.IsoClass5 => "ISO 5 (A/B)",
                    IsoCleanroomClass.IsoClass7 => "ISO 7 (C)",
                    IsoCleanroomClass.IsoClass8 => "ISO 8 (D)",
                    _ => "CNC"
                };
                var isoFt = CreateFormattedText(isoStr, ZeroWpfTheme.RegularTypeface, 7.0, ZeroWpfTheme.TextSecondary, dpi);
                dc.DrawText(isoFt, new Point(x + (barW - isoFt.Width) * 0.5, chartY + innerH - 5));
            }
        }

        private void DrawCleanroomHud(DrawingContext dc, Rect rect, double dpi)
        {
            DrawCardBox(dc, rect, "CORE ZONE PARTICULATES & ACH", ZeroWpfTheme.BoldTypeface, ZeroWpfTheme.TextSecondary, ZeroWpfTheme.BgCard, ZeroWpfTheme.BorderPen);

            double pad = 10;
            double left = rect.Left + pad;
            double right = rect.Right - pad;
            double rowY = rect.Top + 30;
            double rowH = 22;

            var coreZone = _engine.Zones[_engine.Zones.Count - 1];

            // Zone Name
            DrawDataRow(dc, left, right, rowY, "Monitored Zone", $"{coreZone.ZoneTag} ({coreZone.ZoneName})",
                ZeroWpfTheme.RegularTypeface, ZeroWpfTheme.BoldTypeface, ZeroWpfTheme.TextSecondary, ZeroWpfTheme.TextPrimary);
            rowY += rowH;

            // Particulate >= 0.5 um
            double p05 = coreZone.ParticleCount05Um;
            double max05 = coreZone.MaxAllowed05Um;
            Brush p05Brush = p05 <= max05 ? Brushes.LimeGreen : Brushes.Crimson;
            DrawDataRow(dc, left, right, rowY, "Particles ≥ 0.5 μm", $"{p05:N0} / {max05:N0} m⁻³",
                ZeroWpfTheme.RegularTypeface, ZeroWpfTheme.BoldTypeface, ZeroWpfTheme.TextSecondary, p05Brush);
            rowY += rowH;

            // Particulate >= 5.0 um
            double p50 = coreZone.ParticleCount50Um;
            double max50 = coreZone.MaxAllowed50Um;
            Brush p50Brush = p50 <= max50 ? Brushes.LimeGreen : Brushes.Crimson;
            DrawDataRow(dc, left, right, rowY, "Particles ≥ 5.0 μm", $"{p50:F0} / {max50:F0} m⁻³",
                ZeroWpfTheme.RegularTypeface, ZeroWpfTheme.BoldTypeface, ZeroWpfTheme.TextSecondary, p50Brush);
            rowY += rowH;

            // Air Changes (ACH)
            double ach = coreZone.AirChangesPerHour;
            Brush achBrush = coreZone.IsAchOk ? Brushes.LimeGreen : Brushes.Orange;
            DrawDataRow(dc, left, right, rowY, "Air Changes (ACH)", $"{ach:F0} ACH (Min {coreZone.MinRequiredAch:F0})",
                ZeroWpfTheme.RegularTypeface, ZeroWpfTheme.BoldTypeface, ZeroWpfTheme.TextSecondary, achBrush);
            rowY += rowH;

            // Room Environment
            DrawDataRow(dc, left, right, rowY, "Environment", $"{coreZone.RoomTempC:F1}°C | {coreZone.RelativeHumidityPct:F0}% RH",
                ZeroWpfTheme.RegularTypeface, ZeroWpfTheme.BoldTypeface, ZeroWpfTheme.TextSecondary, ZeroWpfTheme.TextPrimary);
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZCleanroomEnvHud"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("CleanroomEnvHud is deprecated and will be removed in 5 release cycles. Please migrate to ZCleanroomEnvHud instead.")]
    public class CleanroomEnvHud : ZCleanroomEnvHud { }

    /// <summary>
    /// Legacy alias for <see cref="ZCleanroomEnvHud"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroCleanroomEnvHud is deprecated and will be removed in 5 release cycles. Please migrate to ZCleanroomEnvHud instead.")]
    public class ZeroCleanroomEnvHud : ZCleanroomEnvHud { }

    #endregion

}
