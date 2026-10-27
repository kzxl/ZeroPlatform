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
    /// Laboratory refrigerated centrifuge operational telemetry visualizer.
    /// Monitors rotor spinning rotation, relative centrifugal force (RCF/g-force),
    /// dynamic bucket weight balance, chamber temperature, and vibration accelerometers.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Life Sciences")]
    [Description("Refrigerated centrifuge monitor with animated rotor, RCF g-force, bucket balance, and vibration sensors")]
    public class ZCentrifugeMonitor : VisualControlBase
    {
        private readonly CentrifugeEngine _engine = new CentrifugeEngine();

        protected override bool AutoAnimate => true;

        public ZCentrifugeMonitor()
        {
            Size = new Size(780, 420);
        }

        #region Public Properties

        [Category("Centrifuge")]
        [Description("Access to the underlying centrifuge computation engine")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public CentrifugeEngine Engine => _engine;

        #endregion

        protected override void OnDrawVisual(Graphics g, Rectangle bounds, ZeroThemePalette palette)
        {
            var tele = _engine.Telemetry;

            // Header Banner
            int headerH = DrawHeader(g, bounds, tele, palette);

            int contentTop = bounds.Y + headerH;
            int contentWidth = bounds.Width - 40;
            int contentHeight = bounds.Height - headerH - 16;
            if (contentWidth < 160 || contentHeight < 100) return;

            // Divide into 2 columns:
            // Left: Animated Rotor & Bucket Chamber (width 48%)
            // Right: Telemetry KPIs, Vibration, and Lid Safety (width 52%)
            int rotorW = (int)(contentWidth * 0.48);
            int kpiW = contentWidth - rotorW - 16;

            Rectangle rotorRect = new Rectangle(bounds.X + 20, contentTop, rotorW, contentHeight);
            Rectangle kpiRect = new Rectangle(bounds.X + 20 + rotorW + 16, contentTop, kpiW, contentHeight);

            DrawRotorChamber(g, rotorRect, tele, palette);
            DrawTelemetryPanel(g, kpiRect, tele, palette);
        }

        private int DrawHeader(Graphics g, Rectangle bounds, CentrifugeTelemetry tele, ZeroThemePalette theme)
        {
            using (var fontTitle = new Font("Segoe UI", 10.5f, FontStyle.Bold))
            using (var fontSmall = new Font("Segoe UI", 8.5f))
            using (var fontBold = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            {
                Size titleSize = TextRenderer.MeasureText(g, tele.ModelTag, fontTitle);

                Color statusCol = tele.IsRunning ? Color.FromArgb(34, 197, 94) : Color.FromArgb(156, 163, 175);
                string statusText = tele.IsRunning ? "SPINNING (RUN)" : "STOPPED";
                string subStr = $"Rotor: {tele.Rotor} (r_max = {tele.RotorRadiusMm:F0} mm) | Program: 14,500 RPM @ 4°C";

                int badgeW = 140;
                int badgeH = 26;
                bool isWide = bounds.Width - badgeW - 20 >= 20 + titleSize.Width + 16;

                if (isWide)
                {
                    TextRenderer.DrawText(g, tele.ModelTag, fontTitle, new Point(bounds.X + 20, bounds.Y + 12), theme.TextPrimary);
                    TextRenderer.DrawText(g, subStr, fontSmall, new Point(bounds.X + 20, bounds.Y + 34), theme.TextSecondary);

                    Rectangle pillRect = new Rectangle(bounds.Right - badgeW - 20, bounds.Y + 14, badgeW, badgeH);
                    using (var pillBrush = new SolidBrush(Color.FromArgb(25, statusCol)))
                    using (var pillPen = new Pen(statusCol, 1f))
                    {
                        g.FillRectangle(pillBrush, pillRect);
                        g.DrawRectangle(pillPen, pillRect);
                    }
                    TextRenderer.DrawText(g, statusText, fontBold, pillRect, statusCol, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    return 58;
                }
                else
                {
                    Rectangle titleRect = new Rectangle(bounds.X + 20, bounds.Y + 8, bounds.Width - 40, 22);
                    TextRenderer.DrawText(g, tele.ModelTag, fontTitle, titleRect, theme.TextPrimary,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                    Rectangle subRect = new Rectangle(bounds.X + 20, bounds.Y + 30, bounds.Width - badgeW - 48, 22);
                    if (subRect.Width < 80)
                    {
                        Rectangle fullSubRect = new Rectangle(bounds.X + 20, bounds.Y + 30, bounds.Width - 40, 20);
                        TextRenderer.DrawText(g, subStr, fontSmall, fullSubRect, theme.TextSecondary,
                            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                        Rectangle pillRect = new Rectangle(bounds.X + 20, bounds.Y + 52, Math.Min(badgeW, bounds.Width - 40), 24);
                        using (var pillBrush = new SolidBrush(Color.FromArgb(25, statusCol)))
                        using (var pillPen = new Pen(statusCol, 1f))
                        {
                            g.FillRectangle(pillBrush, pillRect);
                            g.DrawRectangle(pillPen, pillRect);
                        }
                        TextRenderer.DrawText(g, statusText, fontBold, pillRect, statusCol, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                        return 82;
                    }
                    else
                    {
                        TextRenderer.DrawText(g, subStr, fontSmall, subRect, theme.TextSecondary,
                            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                        Rectangle pillRect = new Rectangle(bounds.Right - badgeW - 20, bounds.Y + 28, badgeW, 24);
                        using (var pillBrush = new SolidBrush(Color.FromArgb(25, statusCol)))
                        using (var pillPen = new Pen(statusCol, 1f))
                        {
                            g.FillRectangle(pillBrush, pillRect);
                            g.DrawRectangle(pillPen, pillRect);
                        }
                        TextRenderer.DrawText(g, statusText, fontBold, pillRect, statusCol, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                        return 58;
                    }
                }
            }
        }

        private void DrawRotorChamber(Graphics g, Rectangle rect, CentrifugeTelemetry tele, ZeroThemePalette theme)
        {
            // Box card
            using (var boxBrush = new SolidBrush(Color.FromArgb(12, theme.TextPrimary)))
            using (var borderPen = new Pen(theme.Border, 1f))
            using (var fontTitle = new Font("Segoe UI", 7.5f, FontStyle.Bold))
            {
                g.FillRectangle(boxBrush, rect);
                g.DrawRectangle(borderPen, rect);
                TextRenderer.DrawText(g, "ROTOR CHAMBER & BUCKET BALANCE", fontTitle, new Point(rect.X + 14, rect.Y + 10), theme.TextSecondary);
            }

            float cx = rect.X + rect.Width / 2f;
            float cy = rect.Y + (rect.Height + 20) / 2f;
            float chamberRadius = Math.Min(rect.Width, rect.Height - 40) * 0.38f;

            // Outer vacuum chamber circle
            using (var chBrush = new SolidBrush(Color.FromArgb(15, theme.TextPrimary)))
            using (var chPen = new Pen(theme.Border, 2f))
            {
                g.FillEllipse(chBrush, cx - chamberRadius, cy - chamberRadius, chamberRadius * 2, chamberRadius * 2);
                g.DrawEllipse(chPen, cx - chamberRadius, cy - chamberRadius, chamberRadius * 2, chamberRadius * 2);
            }

            // Central drive hub
            float hubRadius = chamberRadius * 0.22f;
            using (var hubBrush = new SolidBrush(Color.FromArgb(59, 130, 246)))
            using (var hubPen = new Pen(Color.White, 2f))
            {
                g.FillEllipse(hubBrush, cx - hubRadius, cy - hubRadius, hubRadius * 2, hubRadius * 2);
                g.DrawEllipse(hubPen, cx - hubRadius, cy - hubRadius, hubRadius * 2, hubRadius * 2);
            }

            // Rotor angle driven by ZeroAnimationClock
            float baseAngle = tele.IsRunning ? (ZeroAnimationClock.FluidPhase * 360f * 4f) : 0f;
            float bucketDist = chamberRadius * 0.65f;

            using (var armPen = new Pen(Color.FromArgb(180, 180, 180), 3f))
            using (var fontBucket = new Font("Segoe UI", 7.5f, FontStyle.Bold))
            {
                for (int i = 0; i < 4; i++)
                {
                    double angleRad = (baseAngle + i * 90f) * Math.PI / 180.0;
                    float bx = cx + (float)(Math.Cos(angleRad) * bucketDist);
                    float by = cy + (float)(Math.Sin(angleRad) * bucketDist);

                    // Rotor arm from hub to bucket
                    g.DrawLine(armPen, cx, cy, bx, by);

                    // Bucket circle
                    float bRadius = 14f;
                    RectangleF bRect = new RectangleF(bx - bRadius, by - bRadius, bRadius * 2, bRadius * 2);

                    var bucket = _engine.Buckets[i];
                    Color bColor = Color.FromArgb(34, 197, 94); // balanced
                    using (var bBrush = new SolidBrush(bColor))
                    using (var bPen = new Pen(Color.White, 1.5f))
                    {
                        g.FillEllipse(bBrush, bRect);
                        g.DrawEllipse(bPen, bRect);
                    }

                    TextRenderer.DrawText(g, $"{i + 1}", fontBucket, new Point((int)bx - 5, (int)by - 6), Color.White);
                }
            }

            // Imbalance summary badge at bottom
            double deltaG = _engine.MaxBucketImbalanceGrams;
            bool isBal = _engine.IsRotorBalanced(1.5);
            Color balCol = isBal ? Color.FromArgb(34, 197, 94) : Color.FromArgb(239, 68, 68);
            string balText = isBal ? $"Dynamic Balance: OK (Δ {deltaG:F2} g)" : $"IMBALANCE DETECTED (Δ {deltaG:F2} g)";

            using (var fontBal = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            {
                Rectangle balRect = new Rectangle(rect.X + 16, rect.Bottom - 36, Math.Max(20, rect.Width - 32), 24);
                using (var balBrush = new SolidBrush(Color.FromArgb(25, balCol)))
                using (var balPen = new Pen(balCol, 1f))
                {
                    g.FillRectangle(balBrush, balRect);
                    g.DrawRectangle(balPen, balRect);
                }
                TextRenderer.DrawText(g, balText, fontBal, balRect, balCol,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }

        private void DrawTelemetryPanel(Graphics g, Rectangle rect, CentrifugeTelemetry tele, ZeroThemePalette theme)
        {
            using (var boxBrush = new SolidBrush(Color.FromArgb(12, theme.TextPrimary)))
            using (var borderPen = new Pen(theme.Border, 1f))
            using (var fontTitle = new Font("Segoe UI", 7.5f, FontStyle.Bold))
            using (var fontLabel = new Font("Segoe UI", 8.5f))
            using (var fontVal = new Font("Segoe UI", 12f, FontStyle.Bold))
            using (var fontBold = new Font("Segoe UI", 9f, FontStyle.Bold))
            {
                g.FillRectangle(boxBrush, rect);
                g.DrawRectangle(borderPen, rect);

                Rectangle titlePanelRect = new Rectangle(rect.X + 14, rect.Y + 8, Math.Max(20, rect.Width - 28), 18);
                TextRenderer.DrawText(g, "OPERATIONAL TELEMETRY & SAFETY INTERLOCKS", fontTitle, titlePanelRect, theme.TextSecondary,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                int rowY = rect.Y + 32;

                // KPI 1: Speed RPM
                Rectangle r1Label = new Rectangle(rect.X + 16, rowY, Math.Max(20, rect.Width - 32), 16);
                TextRenderer.DrawText(g, "Rotational Speed:", fontLabel, r1Label, theme.TextSecondary,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                string rpmStr = $"{tele.CurrentRpm:N0} RPM (Set: {tele.TargetRpm:N0})";
                Rectangle r1Val = new Rectangle(rect.X + 16, rowY + 18, Math.Max(20, rect.Width - 32), 22);
                TextRenderer.DrawText(g, rpmStr, fontVal, r1Val, Color.FromArgb(59, 130, 246),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                // KPI 2: Centrifugal Force RCF (g)
                rowY += 50;
                Rectangle r2Label = new Rectangle(rect.X + 16, rowY, Math.Max(20, rect.Width - 32), 16);
                TextRenderer.DrawText(g, "Relative Centrifugal Force (RCF):", fontLabel, r2Label, theme.TextSecondary,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                string rcfStr = $"{_engine.CurrentRcfG:N0} x g";
                Rectangle r2Val = new Rectangle(rect.X + 16, rowY + 18, Math.Max(20, rect.Width - 32), 22);
                TextRenderer.DrawText(g, rcfStr, fontVal, r2Val, Color.FromArgb(16, 185, 129),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                // KPI 3: Chamber Temperature
                rowY += 50;
                Rectangle r3Label = new Rectangle(rect.X + 16, rowY, Math.Max(20, rect.Width - 32), 16);
                TextRenderer.DrawText(g, "Chamber Temperature:", fontLabel, r3Label, theme.TextSecondary,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                string tempStr = $"{tele.CurrentTempC:F1} °C (Set: {tele.TargetTempC:F1} °C)";
                Rectangle r3Val = new Rectangle(rect.X + 16, rowY + 18, Math.Max(20, rect.Width - 32), 20);
                TextRenderer.DrawText(g, tempStr, fontBold, r3Val, theme.TextPrimary,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                // KPI 4: Vibration Sensor
                rowY += 46;
                Rectangle r4Label = new Rectangle(rect.X + 16, rowY, Math.Max(20, rect.Width - 32), 16);
                TextRenderer.DrawText(g, "Vibration Accelerometer:", fontLabel, r4Label, theme.TextSecondary,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                bool hasAlarm = CentrifugeEngine.EvaluateVibrationAlarm(tele.VibrationMmS, out bool isTrip);
                Color vibCol = isTrip ? Color.FromArgb(239, 68, 68) :
                               hasAlarm ? Color.FromArgb(245, 158, 11) : Color.FromArgb(34, 197, 94);

                string vibStr = $"{tele.VibrationMmS:F2} mm/s RMS {(isTrip ? "[CRITICAL TRIP]" : hasAlarm ? "[WARNING]" : "[NORMAL]")}";
                Rectangle r4Val = new Rectangle(rect.X + 16, rowY + 18, Math.Max(20, rect.Width - 32), 20);
                TextRenderer.DrawText(g, vibStr, fontBold, r4Val, vibCol,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                // Progress Bar for Vibration
                Rectangle vibBar = new Rectangle(rect.X + 16, rowY + 40, Math.Max(10, rect.Width - 32), 8);
                using (var vBg = new SolidBrush(Color.FromArgb(40, 128, 128, 128)))
                using (var vFill = new SolidBrush(vibCol))
                {
                    g.FillRectangle(vBg, vibBar);
                    int fillW = (int)((vibBar.Width * Math.Min(6.0, tele.VibrationMmS)) / 6.0);
                    if (fillW > 0) g.FillRectangle(vFill, vibBar.X, vibBar.Y, fillW, vibBar.Height);
                }

                // Lid Lock Safety Interlock Box
                rowY += 56;
                if (rect.Bottom - rowY >= 34)
                {
                    Rectangle lidRect = new Rectangle(rect.X + 16, rowY, Math.Max(10, rect.Width - 32), Math.Min(40, rect.Bottom - rowY - 6));
                    Color lidColor = tele.LidState == LidLockState.SpeedInterlocked ? Color.FromArgb(34, 197, 94) :
                                     tele.LidState == LidLockState.Locked ? Color.FromArgb(59, 130, 246) :
                                     Color.FromArgb(239, 68, 68);

                    using (var lidBrush = new SolidBrush(Color.FromArgb(20, lidColor)))
                    using (var lidPen = new Pen(lidColor, 1.5f))
                    {
                        g.FillRectangle(lidBrush, lidRect);
                        g.DrawRectangle(lidPen, lidRect);
                    }

                    string lidText = tele.LidState == LidLockState.SpeedInterlocked ? "LID INTERLOCK: SPEED LOCKED (SAFE)" :
                                     tele.LidState == LidLockState.Locked ? "LID LOCKED" : "LID UNLOCKED";
                    TextRenderer.DrawText(g, lidText, fontBold, lidRect, lidColor,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                }
            }
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZCentrifugeMonitor"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("CentrifugeMonitor is deprecated and will be removed in 5 release cycles. Please migrate to ZCentrifugeMonitor instead.")]
    [ToolboxItem(false)]
    public class CentrifugeMonitor : ZCentrifugeMonitor
    {
    }

    #endregion
}
