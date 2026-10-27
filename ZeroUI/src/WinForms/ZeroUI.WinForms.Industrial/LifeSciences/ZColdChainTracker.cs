using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.LifeSciences;
using ZeroUI.Core.Rendering;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.LifeSciences
{
    /// <summary>
    /// Cold Chain storage telemetry and regulatory audit visualizer (-80°C ULT Freezers &amp; Cryo Storage).
    /// Features Mean Kinetic Temperature (MKT) calculations, temperature excursion thresholds,
    /// door open cycle tracking, and backup LN2 safety indicators.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Life Sciences")]
    [Description("Cold Chain -80°C ULT freezer telemetry ribbon with Mean Kinetic Temperature (MKT) and excursion alerts")]
    public class ZColdChainTracker : VisualControlBase
    {
        private readonly ColdChainEngine _engine = new ColdChainEngine();

        protected override bool AutoAnimate => true;

        public ZColdChainTracker()
        {
            Size = new Size(820, 420);
        }

        #region Public Properties

        [Category("Cold Chain")]
        [Description("Access to the underlying cold chain computation engine")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ColdChainEngine Engine => _engine;

        #endregion

        protected override void OnDrawVisual(Graphics g, Rectangle bounds, ZeroThemePalette palette)
        {
            var tele = _engine.Telemetry;

            // Header Banner
            int headerH = DrawHeader(g, bounds, tele, palette);

            // KPI Telemetry Strip
            int kpiY = bounds.Y + headerH + 6;
            int kpiHeight = 52;
            DrawKpiStrip(g, new Rectangle(bounds.X + 20, kpiY, bounds.Width - 40, kpiHeight), tele, palette);

            // Temperature History Ribbon Canvas
            int ribbonY = kpiY + kpiHeight + 12;
            int ribbonHeight = bounds.Height - (ribbonY - bounds.Y) - 16;
            if (ribbonHeight < 100 || bounds.Width < 240) return;

            DrawRibbonCanvas(g, new Rectangle(bounds.X + 20, ribbonY, bounds.Width - 40, ribbonHeight), tele, palette);
        }

        private int DrawHeader(Graphics g, Rectangle bounds, ColdChainTelemetry tele, ZeroThemePalette theme)
        {
            using (var fontTitle = new Font("Segoe UI", 10.5f, FontStyle.Bold))
            using (var fontBold = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            {
                string title = $"{tele.UnitTag} — Ultra-Low Temp (ULT) -80°C Freezer";
                Size titleSize = TextRenderer.MeasureText(g, title, fontTitle);

                bool isExcursion = _engine.IsInExcursion;
                Color badgeCol = isExcursion ? Color.FromArgb(239, 68, 68) : Color.FromArgb(34, 197, 94);
                string badgeText = isExcursion ? "EXCURSION BREACH" : "COMPLIANT (STABLE)";

                int badgeW = 170;
                int badgeH = 26;
                bool isWide = bounds.Width - badgeW - 20 >= 20 + titleSize.Width + 16;

                if (isWide)
                {
                    TextRenderer.DrawText(g, title, fontTitle, new Point(bounds.X + 20, bounds.Y + 14), theme.TextPrimary);

                    Rectangle pillRect = new Rectangle(bounds.Right - badgeW - 20, bounds.Y + 12, badgeW, badgeH);
                    using (var pillBrush = new SolidBrush(Color.FromArgb(25, badgeCol)))
                    using (var pillPen = new Pen(badgeCol, 1f))
                    {
                        g.FillRectangle(pillBrush, pillRect);
                        g.DrawRectangle(pillPen, pillRect);
                    }
                    TextRenderer.DrawText(g, badgeText, fontBold, pillRect, badgeCol, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    return 48;
                }
                else
                {
                    Rectangle titleRect = new Rectangle(bounds.X + 20, bounds.Y + 8, bounds.Width - 40, 22);
                    TextRenderer.DrawText(g, title, fontTitle, titleRect, theme.TextPrimary,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                    Rectangle pillRect = new Rectangle(bounds.X + 20, bounds.Y + 34, Math.Min(badgeW, bounds.Width - 40), badgeH);
                    using (var pillBrush = new SolidBrush(Color.FromArgb(25, badgeCol)))
                    using (var pillPen = new Pen(badgeCol, 1f))
                    {
                        g.FillRectangle(pillBrush, pillRect);
                        g.DrawRectangle(pillPen, pillRect);
                    }
                    TextRenderer.DrawText(g, badgeText, fontBold, pillRect, badgeCol,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                    return 66;
                }
            }
        }

        private void DrawKpiStrip(Graphics g, Rectangle rect, ColdChainTelemetry tele, ZeroThemePalette theme)
        {
            using (var cardBrush = new SolidBrush(Color.FromArgb(12, theme.TextPrimary)))
            using (var borderPen = new Pen(theme.Border, 1f))
            {
                g.FillRectangle(cardBrush, rect);
                g.DrawRectangle(borderPen, rect);
            }

            int cols = rect.Width < 420 ? 3 : 5;
            int colW = rect.Width / cols;
            using (var fontLabel = new Font("Segoe UI", 7.5f))
            using (var fontVal = new Font("Segoe UI", 11f, FontStyle.Bold))
            {
                // KPI 1: Current Temp
                Color tempColor = tele.CurrentTempC > tele.HighAlarmLimitC ? Color.FromArgb(239, 68, 68) : Color.FromArgb(59, 130, 246);
                DrawKpiCell(g, new Rectangle(rect.X, rect.Y, colW, rect.Height), "CURRENT TEMP", $"{tele.CurrentTempC:F1} °C", tempColor, fontLabel, fontVal, theme);

                // KPI 2: MKT
                double mkt = ColdChainEngine.CalculateMeanKineticTemperature(tele.TempHistory);
                DrawKpiCell(g, new Rectangle(rect.X + colW, rect.Y, colW, rect.Height), "MEAN KINETIC", $"{mkt:F1} °C", Color.FromArgb(34, 197, 94), fontLabel, fontVal, theme);

                if (cols >= 5)
                {
                    // KPI 3: Setpoint
                    DrawKpiCell(g, new Rectangle(rect.X + colW * 2, rect.Y, colW, rect.Height), "SETPOINT", $"{tele.SetpointTempC:F1} °C", theme.TextPrimary, fontLabel, fontVal, theme);

                    // KPI 4: Door Opens
                    DrawKpiCell(g, new Rectangle(rect.X + colW * 3, rect.Y, colW, rect.Height), "DOOR OPENS", $"{tele.DoorOpenCountToday} Cycles", theme.TextPrimary, fontLabel, fontVal, theme);

                    // KPI 5: LN2 & Battery Backup
                    string backupStr = $"Batt: {tele.BackupBatteryPct:F0}% | LN2 OK";
                    DrawKpiCell(g, new Rectangle(rect.X + colW * 4, rect.Y, rect.Right - (rect.X + colW * 4), rect.Height), "BACKUP SYSTEMS", backupStr, Color.FromArgb(16, 185, 129), fontLabel, fontVal, theme);
                }
                else
                {
                    // In compact 3-column mode, show Setpoint as the 3rd metric
                    DrawKpiCell(g, new Rectangle(rect.X + colW * 2, rect.Y, rect.Right - (rect.X + colW * 2), rect.Height), "SETPOINT", $"{tele.SetpointTempC:F1} °C", theme.TextPrimary, fontLabel, fontVal, theme);
                }
            }
        }


        private void DrawRibbonCanvas(Graphics g, Rectangle rect, ColdChainTelemetry tele, ZeroThemePalette theme)
        {
            using (var cardBrush = new SolidBrush(Color.FromArgb(8, theme.TextPrimary)))
            using (var borderPen = new Pen(theme.Border, 1f))
            using (var fontHeader = new Font("Segoe UI", 7.5f, FontStyle.Bold))
            using (var fontTag = new Font("Segoe UI", 7.5f))
            {
                g.FillRectangle(cardBrush, rect);
                g.DrawRectangle(borderPen, rect);

                Rectangle titleRect = new Rectangle(rect.X + 14, rect.Y + 8, Math.Max(20, rect.Width - 28), 18);
                TextRenderer.DrawText(g, "TEMPERATURE TELEMETRY RIBBON & EXCURSION LIMITS (21 CFR PART 11 AUDIT)", fontHeader, titleRect, theme.TextSecondary,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                int plotLeft = rect.X + 60;
                int plotTop = rect.Y + 32;
                int plotW = rect.Width - 80;
                int plotH = rect.Height - 44;

                if (plotW < 100 || plotH < 60) return;

                // Range: -95°C (bottom) to -65°C (top)
                double minT = -95.0;
                double maxT = -65.0;
                double spanT = maxT - minT;

                Func<double, float> tempToY = (t) =>
                {
                    double ratio = (maxT - t) / spanT;
                    return (float)(plotTop + ratio * plotH);
                };

                // Upper Excursion Limit (-70°C) Line
                float upperY = tempToY(tele.HighAlarmLimitC);
                using (var alarmPen = new Pen(Color.FromArgb(200, 239, 68, 68), 1f) { DashStyle = DashStyle.Dash })
                {
                    g.DrawLine(alarmPen, plotLeft, upperY, plotLeft + plotW, upperY);
                    TextRenderer.DrawText(g, $"High Alarm: {tele.HighAlarmLimitC:F0} °C", fontTag, new Point(plotLeft - 55, (int)upperY - 8), Color.FromArgb(239, 68, 68));
                }

                // Lower Excursion Limit (-88°C) Line
                float lowerY = tempToY(tele.LowAlarmLimitC);
                using (var lowPen = new Pen(Color.FromArgb(200, 59, 130, 246), 1f) { DashStyle = DashStyle.Dash })
                {
                    g.DrawLine(lowPen, plotLeft, lowerY, plotLeft + plotW, lowerY);
                    TextRenderer.DrawText(g, $"Low Alarm: {tele.LowAlarmLimitC:F0} °C", fontTag, new Point(plotLeft - 55, (int)lowerY - 8), Color.FromArgb(59, 130, 246));
                }

                // Setpoint (-80°C) Line
                float setY = tempToY(tele.SetpointTempC);
                using (var setPen = new Pen(Color.FromArgb(120, theme.TextSecondary), 1f) { DashStyle = DashStyle.Dot })
                {
                    g.DrawLine(setPen, plotLeft, setY, plotLeft + plotW, setY);
                    TextRenderer.DrawText(g, $"Set: {tele.SetpointTempC:F0} °C", fontTag, new Point(plotLeft - 55, (int)setY - 8), theme.TextSecondary);
                }

                // Plot Historical Ribbon Curve
                var history = tele.TempHistory;
                if (history != null && history.Count > 1)
                {
                    PointF[] points = new PointF[history.Count];
                    float stepX = (float)plotW / (history.Count - 1);

                    for (int i = 0; i < history.Count; i++)
                    {
                        float px = plotLeft + i * stepX;
                        float py = tempToY(history[i]);
                        points[i] = new PointF(px, py);
                    }

                    // Curve fill
                    using (var path = new GraphicsPath())
                    {
                        path.AddLines(points);
                        path.AddLine(points[points.Length - 1], new PointF(points[points.Length - 1].X, plotTop + plotH));
                        path.AddLine(new PointF(points[points.Length - 1].X, plotTop + plotH), new PointF(points[0].X, plotTop + plotH));
                        path.CloseFigure();

                        using (var fillBrush = new LinearGradientBrush(new Rectangle(plotLeft, plotTop, plotW, plotH),
                            Color.FromArgb(40, 59, 130, 246), Color.FromArgb(0, 59, 130, 246), LinearGradientMode.Vertical))
                        {
                            g.FillPath(fillBrush, path);
                        }
                    }

                    // Curve stroke
                    using (var strokePen = new Pen(Color.FromArgb(59, 130, 246), 2f))
                    {
                        g.DrawLines(strokePen, points);
                    }

                    // Live pulse dot at latest reading
                    PointF latestPt = points[points.Length - 1];
                    float pulseR = 5f + ZeroAnimationClock.PulsePhase * 3f;
                    using (var pBrush = new SolidBrush(Color.FromArgb(34, 197, 94)))
                    using (var ringPen = new Pen(Color.FromArgb(150, 34, 197, 94), 1.5f))
                    {
                        g.FillEllipse(pBrush, latestPt.X - 4f, latestPt.Y - 4f, 8f, 8f);
                        g.DrawEllipse(ringPen, latestPt.X - pulseR, latestPt.Y - pulseR, pulseR * 2, pulseR * 2);
                    }
                }
            }
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZColdChainTracker"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ColdChainTracker is deprecated and will be removed in 5 release cycles. Please migrate to ZColdChainTracker instead.")]
    [ToolboxItem(false)]
    public class ColdChainTracker : ZColdChainTracker
    {
    }

    #endregion
}
