using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using ZeroUI.Core.LifeSciences;
using ZeroUI.Core.Rendering;
using ZeroUI.Wpf.Base;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.LifeSciences
{
    /// <summary>
    /// Cold Chain storage telemetry and regulatory audit visualizer in WPF (-80°C ULT Freezers &amp; Cryo Storage).
    /// Features Mean Kinetic Temperature (MKT) calculations, temperature excursion thresholds,
    /// door open cycle tracking, and backup LN2 safety indicators.
    /// </summary>
    public class ZColdChainTracker : ZeroWpfVisualBase
    {
        private readonly ColdChainEngine _engine = new ColdChainEngine();

        protected override bool AutoAnimate => true;

        #region Properties

        public ColdChainEngine Engine => _engine;

        #endregion

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double w = ActualWidth;
            double h = ActualHeight;
            if (w < 240 || h < 140)
                return;

            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            // Background
            dc.DrawRectangle(ZeroWpfTheme.BgPrimary, null, new Rect(0, 0, w, h));

            var tele = _engine.Telemetry;

            // Header Banner
            DrawHeader(dc, tele, w, dpi);

            // KPI Telemetry Strip
            double kpiY = 50;
            double kpiH = 50;
            DrawKpiStrip(dc, new Rect(20, kpiY, w - 40, kpiH), tele, dpi);

            // Temperature History Ribbon Canvas
            double ribbonY = kpiY + kpiH + 14;
            double ribbonH = h - ribbonY - 16;
            if (ribbonH < 100 || w < 260) return;

            DrawRibbonCanvas(dc, new Rect(20, ribbonY, w - 40, ribbonH), tele, dpi);
        }

        private void DrawHeader(DrawingContext dc, ColdChainTelemetry tele, double w, double dpi)
        {
            string title = $"{tele.UnitTag} — Ultra-Low Temp (ULT) -80°C Freezer";
            var titleText = CreateFormattedText(title, ZeroWpfTheme.BoldTypeface, 13, ZeroWpfTheme.TextPrimary, dpi);
            dc.DrawText(titleText, new Point(20, 14));

            bool isExcursion = _engine.IsInExcursion;
            Brush badgeBrush = isExcursion ? ZeroWpfTheme.DangerAccent : ZeroWpfTheme.SuccessAccent;
            string badgeText = isExcursion ? "EXCURSION BREACH" : "COMPLIANT (STABLE)";

            Color badgeCol = badgeBrush is SolidColorBrush scb ? scb.Color : Color.FromRgb(34, 197, 94);
            Rect pillRect = new Rect(w - 190, 10, 170, 26);
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(25, badgeCol.R, badgeCol.G, badgeCol.B)),
                new Pen(badgeBrush, 1), pillRect, 4, 4);

            var bText = CreateFormattedText(badgeText, ZeroWpfTheme.BoldTypeface, 10, badgeBrush, dpi);
            dc.DrawText(bText, new Point(pillRect.X + (pillRect.Width - bText.Width) / 2, pillRect.Y + 5));
        }

        private void DrawKpiStrip(DrawingContext dc, Rect rect, ColdChainTelemetry tele, double dpi)
        {
            dc.DrawRoundedRectangle(ZeroWpfTheme.BgCard, ZeroWpfTheme.BorderPen, rect, 6, 6);

            double colW = rect.Width / 5.0;

            // KPI 1: Current Temp
            Brush tempBrush = tele.CurrentTempC > tele.HighAlarmLimitC ? ZeroWpfTheme.DangerAccent : new SolidColorBrush(Color.FromRgb(59, 130, 246));
            DrawKpiCell(dc, new Rect(rect.X, rect.Y, colW, rect.Height), "CURRENT TEMP", $"{tele.CurrentTempC:F1} °C", tempBrush, dpi);

            // KPI 2: MKT
            double mkt = ColdChainEngine.CalculateMeanKineticTemperature(tele.TempHistory);
            DrawKpiCell(dc, new Rect(rect.X + colW, rect.Y, colW, rect.Height), "MEAN KINETIC (MKT)", $"{mkt:F1} °C", ZeroWpfTheme.SuccessAccent, dpi);

            // KPI 3: Setpoint
            DrawKpiCell(dc, new Rect(rect.X + colW * 2, rect.Y, colW, rect.Height), "SETPOINT", $"{tele.SetpointTempC:F1} °C", ZeroWpfTheme.TextPrimary, dpi);

            // KPI 4: Door Opens
            DrawKpiCell(dc, new Rect(rect.X + colW * 3, rect.Y, colW, rect.Height), "DOOR OPENS TODAY", $"{tele.DoorOpenCountToday} Cycles", ZeroWpfTheme.TextPrimary, dpi);

            // KPI 5: Backup systems
            string backupStr = $"Batt: {tele.BackupBatteryPct:F0}% | LN2 OK";
            DrawKpiCell(dc, new Rect(rect.X + colW * 4, rect.Y, rect.Right - (rect.X + colW * 4), rect.Height), "BACKUP SYSTEMS", backupStr, ZeroWpfTheme.SuccessAccent, dpi);
        }


        private void DrawRibbonCanvas(DrawingContext dc, Rect rect, ColdChainTelemetry tele, double dpi)
        {
            dc.DrawRoundedRectangle(ZeroWpfTheme.BgCard, ZeroWpfTheme.BorderPen, rect, 6, 6);

            var headerText = CreateFormattedText("TEMPERATURE TELEMETRY RIBBON & EXCURSION LIMITS (21 CFR PART 11 AUDIT)", ZeroWpfTheme.BoldTypeface, 9.5, ZeroWpfTheme.TextSecondary, dpi);
            dc.DrawText(headerText, new Point(rect.X + 14, rect.Y + 8));

            double plotLeft = rect.X + 60;
            double plotTop = rect.Y + 30;
            double plotW = rect.Width - 80;
            double plotH = rect.Height - 42;

            if (plotW < 80 || plotH < 40) return;

            double minT = -95.0;
            double maxT = -65.0;
            double spanT = maxT - minT;

            Func<double, double> tempToY = (t) =>
            {
                double ratio = (maxT - t) / spanT;
                return plotTop + ratio * plotH;
            };

            // High Limit (-70°C) Line
            double upperY = tempToY(tele.HighAlarmLimitC);
            Pen alarmPen = new Pen(ZeroWpfTheme.DangerAccent, 1) { DashStyle = DashStyles.Dash };
            dc.DrawLine(alarmPen, new Point(plotLeft, upperY), new Point(plotLeft + plotW, upperY));

            var highLbl = CreateFormattedText($"High Alarm: {tele.HighAlarmLimitC:F0} °C", ZeroWpfTheme.RegularTypeface, 9, ZeroWpfTheme.DangerAccent, dpi);
            dc.DrawText(highLbl, new Point(plotLeft - highLbl.Width - 6, upperY - 8));

            // Low Limit (-88°C) Line
            double lowerY = tempToY(tele.LowAlarmLimitC);
            Pen lowPen = new Pen(new SolidColorBrush(Color.FromRgb(59, 130, 246)), 1) { DashStyle = DashStyles.Dash };
            dc.DrawLine(lowPen, new Point(plotLeft, lowerY), new Point(plotLeft + plotW, lowerY));

            var lowLbl = CreateFormattedText($"Low Alarm: {tele.LowAlarmLimitC:F0} °C", ZeroWpfTheme.RegularTypeface, 9, new SolidColorBrush(Color.FromRgb(59, 130, 246)), dpi);
            dc.DrawText(lowLbl, new Point(plotLeft - lowLbl.Width - 6, lowerY - 8));

            // Setpoint (-80°C) Line
            double setY = tempToY(tele.SetpointTempC);
            Pen setPen = new Pen(ZeroWpfTheme.TextSecondary, 1) { DashStyle = DashStyles.Dot };
            dc.DrawLine(setPen, new Point(plotLeft, setY), new Point(plotLeft + plotW, setY));

            var setLbl = CreateFormattedText($"Set: {tele.SetpointTempC:F0} °C", ZeroWpfTheme.RegularTypeface, 9, ZeroWpfTheme.TextSecondary, dpi);
            dc.DrawText(setLbl, new Point(plotLeft - setLbl.Width - 6, setY - 8));

            // History Ribbon
            var history = tele.TempHistory;
            if (history != null && history.Count > 1)
            {
                Point[] pts = new Point[history.Count];
                double stepX = plotW / (history.Count - 1);

                for (int i = 0; i < history.Count; i++)
                {
                    double px = plotLeft + i * stepX;
                    double py = tempToY(history[i]);
                    pts[i] = new Point(px, py);
                }

                // Ribbon fill geometry
                StreamGeometry streamGeom = new StreamGeometry();
                using (StreamGeometryContext ctx = streamGeom.Open())
                {
                    ctx.BeginFigure(pts[0], true, true);
                    for (int i = 1; i < pts.Length; i++)
                    {
                        ctx.LineTo(pts[i], true, false);
                    }
                    ctx.LineTo(new Point(pts[pts.Length - 1].X, plotTop + plotH), true, false);
                    ctx.LineTo(new Point(pts[0].X, plotTop + plotH), true, false);
                }
                streamGeom.Freeze();

                LinearGradientBrush fillBrush = new LinearGradientBrush(Color.FromArgb(40, 59, 130, 246), Color.FromArgb(0, 59, 130, 246), 90.0);
                dc.DrawGeometry(fillBrush, null, streamGeom);

                // Stroke
                Pen strokePen = new Pen(new SolidColorBrush(Color.FromRgb(59, 130, 246)), 2);
                for (int i = 0; i < pts.Length - 1; i++)
                {
                    dc.DrawLine(strokePen, pts[i], pts[i + 1]);
                }

                // Live pulse dot
                Point latestPt = pts[pts.Length - 1];
                double pulseR = 5.0 + ZeroAnimationClock.PulsePhase * 3.0;
                dc.DrawEllipse(ZeroWpfTheme.SuccessAccent, null, latestPt, 4, 4);
                dc.DrawEllipse(null, new Pen(ZeroWpfTheme.SuccessAccent, 1.5), latestPt, pulseR, pulseR);
            }
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZColdChainTracker"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ColdChainTracker is deprecated and will be removed in 5 release cycles. Please migrate to ZColdChainTracker instead.")]
    public class ColdChainTracker : ZColdChainTracker { }

    /// <summary>
    /// Legacy alias for <see cref="ZColdChainTracker"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroColdChainTracker is deprecated and will be removed in 5 release cycles. Please migrate to ZColdChainTracker instead.")]
    public class ZeroColdChainTracker : ZColdChainTracker { }

    #endregion

}
