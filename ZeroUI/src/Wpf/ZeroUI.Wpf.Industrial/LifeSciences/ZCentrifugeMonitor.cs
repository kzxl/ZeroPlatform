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
    /// Laboratory refrigerated centrifuge operational telemetry visualizer in WPF.
    /// Monitors rotor spinning rotation, relative centrifugal force (RCF/g-force),
    /// dynamic bucket weight balance, chamber temperature, and vibration accelerometers.
    /// </summary>
    public class ZCentrifugeMonitor : ZeroWpfVisualBase
    {
        private readonly CentrifugeEngine _engine = new CentrifugeEngine();

        protected override bool AutoAnimate => true;

        #region Properties

        public CentrifugeEngine Engine => _engine;

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

            double contentTop = 56;
            double contentW = w - 40;
            double contentH = h - contentTop - 16;
            if (contentW < 180 || contentH < 100) return;

            double rotorW = contentW * 0.48;
            double kpiW = contentW - rotorW - 16;

            Rect rotorRect = new Rect(20, contentTop, rotorW, contentH);
            Rect kpiRect = new Rect(20 + rotorW + 16, contentTop, kpiW, contentH);

            DrawRotorChamber(dc, rotorRect, tele, dpi);
            DrawTelemetryPanel(dc, kpiRect, tele, dpi);
        }

        private void DrawHeader(DrawingContext dc, CentrifugeTelemetry tele, double w, double dpi)
        {
            var titleText = CreateFormattedText(tele.ModelTag, ZeroWpfTheme.BoldTypeface, 13, ZeroWpfTheme.TextPrimary, dpi);
            dc.DrawText(titleText, new Point(20, 14));

            string subStr = $"Rotor: {tele.Rotor} (r_max = {tele.RotorRadiusMm:F0} mm) | Program: 14,500 RPM @ 4°C";
            var subText = CreateFormattedText(subStr, ZeroWpfTheme.RegularTypeface, 10.5, ZeroWpfTheme.TextSecondary, dpi);
            dc.DrawText(subText, new Point(20, 36));

            Brush statusBrush = tele.IsRunning ? ZeroWpfTheme.SuccessAccent : ZeroWpfTheme.TextSecondary;
            string statusText = tele.IsRunning ? "SPINNING (RUN)" : "STOPPED";

            Color statusCol = statusBrush is SolidColorBrush scb ? scb.Color : Color.FromRgb(34, 197, 94);
            Rect pillRect = new Rect(w - 160, 14, 140, 26);
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(25, statusCol.R, statusCol.G, statusCol.B)),
                new Pen(statusBrush, 1), pillRect, 4, 4);

            var sText = CreateFormattedText(statusText, ZeroWpfTheme.BoldTypeface, 10, statusBrush, dpi);
            dc.DrawText(sText, new Point(pillRect.X + (pillRect.Width - sText.Width) / 2, pillRect.Y + 4));
        }

        private void DrawRotorChamber(DrawingContext dc, Rect rect, CentrifugeTelemetry tele, double dpi)
        {
            dc.DrawRoundedRectangle(ZeroWpfTheme.BgCard, ZeroWpfTheme.BorderPen, rect, 6, 6);

            var titleText = CreateFormattedText("ROTOR CHAMBER & BUCKET BALANCE", ZeroWpfTheme.BoldTypeface, 9.5, ZeroWpfTheme.TextSecondary, dpi);
            dc.DrawText(titleText, new Point(rect.X + 14, rect.Y + 10));

            double cx = rect.X + rect.Width / 2.0;
            double cy = rect.Y + (rect.Height + 20) / 2.0;
            double chamberRadius = Math.Min(rect.Width, rect.Height - 40) * 0.38;

            // Outer vacuum chamber
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(15, 200, 200, 200)), ZeroWpfTheme.BorderPen, new Point(cx, cy), chamberRadius, chamberRadius);

            // Hub
            double hubRadius = chamberRadius * 0.22;
            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(59, 130, 246)), new Pen(Brushes.White, 2), new Point(cx, cy), hubRadius, hubRadius);

            // Rotation angle
            float baseAngle = tele.IsRunning ? (ZeroAnimationClock.FluidPhase * 360f * 4f) : 0f;
            double bucketDist = chamberRadius * 0.65;
            Pen armPen = new Pen(new SolidColorBrush(Color.FromRgb(180, 180, 180)), 3);

            for (int i = 0; i < 4; i++)
            {
                double angleRad = (baseAngle + i * 90.0) * Math.PI / 180.0;
                double bx = cx + Math.Cos(angleRad) * bucketDist;
                double by = cy + Math.Sin(angleRad) * bucketDist;

                dc.DrawLine(armPen, new Point(cx, cy), new Point(bx, by));

                double bRadius = 14;
                dc.DrawEllipse(ZeroWpfTheme.SuccessAccent, new Pen(Brushes.White, 1.5), new Point(bx, by), bRadius, bRadius);

                var numTxt = CreateFormattedText($"{i + 1}", ZeroWpfTheme.BoldTypeface, 9, Brushes.White, dpi);
                dc.DrawText(numTxt, new Point(bx - numTxt.Width / 2, by - numTxt.Height / 2));
            }

            // Dynamic balance badge
            double deltaG = _engine.MaxBucketImbalanceGrams;
            bool isBal = _engine.IsRotorBalanced(1.5);
            Brush balBrush = isBal ? ZeroWpfTheme.SuccessAccent : ZeroWpfTheme.DangerAccent;
            string balText = isBal ? $"Dynamic Balance: OK (Δ {deltaG:F2} g)" : $"IMBALANCE DETECTED (Δ {deltaG:F2} g)";

            Color balCol = balBrush is SolidColorBrush bscb ? bscb.Color : Color.FromRgb(34, 197, 94);
            Rect balRect = new Rect(rect.X + 16, rect.Bottom - 36, rect.Width - 32, 24);
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(25, balCol.R, balCol.G, balCol.B)),
                new Pen(balBrush, 1), balRect, 4, 4);

            var balTxt = CreateFormattedText(balText, ZeroWpfTheme.BoldTypeface, 10, balBrush, dpi);
            dc.DrawText(balTxt, new Point(balRect.X + (balRect.Width - balTxt.Width) / 2, balRect.Y + 4));
        }

        private void DrawTelemetryPanel(DrawingContext dc, Rect rect, CentrifugeTelemetry tele, double dpi)
        {
            dc.DrawRoundedRectangle(ZeroWpfTheme.BgCard, ZeroWpfTheme.BorderPen, rect, 6, 6);

            var headerText = CreateFormattedText("OPERATIONAL TELEMETRY & SAFETY INTERLOCKS", ZeroWpfTheme.BoldTypeface, 9.5, ZeroWpfTheme.TextSecondary, dpi);
            dc.DrawText(headerText, new Point(rect.X + 14, rect.Y + 10));

            double rowY = rect.Y + 36;

            // Speed
            var lblRpm = CreateFormattedText("Rotational Speed:", ZeroWpfTheme.RegularTypeface, 10, ZeroWpfTheme.TextSecondary, dpi);
            dc.DrawText(lblRpm, new Point(rect.X + 16, rowY));
            string rpmStr = $"{tele.CurrentRpm:N0} RPM (Set: {tele.TargetRpm:N0})";
            var valRpm = CreateFormattedText(rpmStr, ZeroWpfTheme.BoldTypeface, 13, new SolidColorBrush(Color.FromRgb(59, 130, 246)), dpi);
            dc.DrawText(valRpm, new Point(rect.X + 16, rowY + 18));

            // RCF
            rowY += 56;
            var lblRcf = CreateFormattedText("Relative Centrifugal Force (RCF):", ZeroWpfTheme.RegularTypeface, 10, ZeroWpfTheme.TextSecondary, dpi);
            dc.DrawText(lblRcf, new Point(rect.X + 16, rowY));
            string rcfStr = $"{_engine.CurrentRcfG:N0} x g";
            var valRcf = CreateFormattedText(rcfStr, ZeroWpfTheme.BoldTypeface, 13, ZeroWpfTheme.SuccessAccent, dpi);
            dc.DrawText(valRcf, new Point(rect.X + 16, rowY + 18));

            // Temperature
            rowY += 56;
            var lblTemp = CreateFormattedText("Chamber Temperature:", ZeroWpfTheme.RegularTypeface, 10, ZeroWpfTheme.TextSecondary, dpi);
            dc.DrawText(lblTemp, new Point(rect.X + 16, rowY));
            string tempStr = $"{tele.CurrentTempC:F1} °C (Set: {tele.TargetTempC:F1} °C) [Active Cooling]";
            var valTemp = CreateFormattedText(tempStr, ZeroWpfTheme.BoldTypeface, 10.5, ZeroWpfTheme.TextPrimary, dpi);
            dc.DrawText(valTemp, new Point(rect.X + 16, rowY + 20));

            // Vibration
            rowY += 50;
            var lblVib = CreateFormattedText("Vibration Accelerometer:", ZeroWpfTheme.RegularTypeface, 10, ZeroWpfTheme.TextSecondary, dpi);
            dc.DrawText(lblVib, new Point(rect.X + 16, rowY));

            bool hasAlarm = CentrifugeEngine.EvaluateVibrationAlarm(tele.VibrationMmS, out bool isTrip);
            Brush vibBrush = isTrip ? ZeroWpfTheme.DangerAccent :
                             hasAlarm ? ZeroWpfTheme.WarningAccent : ZeroWpfTheme.SuccessAccent;

            string vibStr = $"{tele.VibrationMmS:F2} mm/s RMS {(isTrip ? "[CRITICAL TRIP]" : hasAlarm ? "[WARNING]" : "[NORMAL]")}";
            var valVib = CreateFormattedText(vibStr, ZeroWpfTheme.BoldTypeface, 10.5, vibBrush, dpi);
            dc.DrawText(valVib, new Point(rect.X + 16, rowY + 20));

            // Bar
            Rect vibBar = new Rect(rect.X + 16, rowY + 44, rect.Width - 32, 8);
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(40, 128, 128, 128)), null, vibBar, 2, 2);
            double fillW = (vibBar.Width * Math.Min(6.0, tele.VibrationMmS)) / 6.0;
            if (fillW > 0)
            {
                dc.DrawRoundedRectangle(vibBrush, null, new Rect(vibBar.X, vibBar.Y, fillW, vibBar.Height), 2, 2);
            }

            // Lid safety lock
            rowY += 68;
            Rect lidRect = new Rect(rect.X + 16, rowY, rect.Width - 32, 44);
            Brush lidBrush = tele.LidState == LidLockState.SpeedInterlocked ? ZeroWpfTheme.SuccessAccent :
                             tele.LidState == LidLockState.Locked ? new SolidColorBrush(Color.FromRgb(59, 130, 246)) :
                             ZeroWpfTheme.DangerAccent;

            Color lidCol = lidBrush is SolidColorBrush lscb ? lscb.Color : Color.FromRgb(34, 197, 94);
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(20, lidCol.R, lidCol.G, lidCol.B)),
                new Pen(lidBrush, 1.5), lidRect, 4, 4);

            string lidText = tele.LidState == LidLockState.SpeedInterlocked ? "LID INTERLOCK: SPEED LOCKED (SAFE)" :
                             tele.LidState == LidLockState.Locked ? "LID LOCKED" : "LID UNLOCKED";
            var lidTxt = CreateFormattedText(lidText, ZeroWpfTheme.BoldTypeface, 10, lidBrush, dpi);
            dc.DrawText(lidTxt, new Point(lidRect.X + (lidRect.Width - lidTxt.Width) / 2, lidRect.Y + (lidRect.Height - lidTxt.Height) / 2));
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZCentrifugeMonitor"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("CentrifugeMonitor is deprecated and will be removed in 5 release cycles. Please migrate to ZCentrifugeMonitor instead.")]
    public class CentrifugeMonitor : ZCentrifugeMonitor { }

    /// <summary>
    /// Legacy alias for <see cref="ZCentrifugeMonitor"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroCentrifugeMonitor is deprecated and will be removed in 5 release cycles. Please migrate to ZCentrifugeMonitor instead.")]
    public class ZeroCentrifugeMonitor : ZCentrifugeMonitor { }

    #endregion

}
