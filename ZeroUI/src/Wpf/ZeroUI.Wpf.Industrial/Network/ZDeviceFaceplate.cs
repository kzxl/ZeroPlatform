using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using ZeroUI.Core.Network;
using ZeroUI.Core.Rendering;
using ZeroUI.Wpf.Base;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Network
{
    /// <summary>
    /// Compact Chassis Operating Health & Hardware Telemetry HUD for WPF.
    /// Displays dual redundant PSU failover status, fan tachometer gauges with rotating blades,
    /// and SFP digital optical monitoring (DDM) power budget meters.
    /// </summary>
    public class ZDeviceFaceplate : ZeroWpfVisualBase
    {
        private readonly ChassisHealthProfile _profile = new ChassisHealthProfile();
        private double _fanBladeAngle = 0.0;

        #region Properties

        public ChassisHealthProfile Profile => _profile;

        #endregion

        protected override bool AutoAnimate => true;

        protected override void OnAnimationTick(double delta, long frame)
        {
            _fanBladeAngle = (_fanBladeAngle + delta * 360.0 * 2.0) % 360.0;
        }

        public ZDeviceFaceplate()
        {
        }

        #if NETFRAMEWORK
        private static FormattedText CreateFormattedText(string text, Typeface typeface, double fontSize, Brush brush, double pixelsPerDip = 1.0)
        {
            return new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, fontSize, brush);
        }
        #else
        private static FormattedText CreateFormattedText(string text, Typeface typeface, double fontSize, Brush brush, double pixelsPerDip = 1.0)
        {
            return new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, fontSize, brush, pixelsPerDip);
        }
        #endif

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double w = ActualWidth;
            double h = ActualHeight;
            if (w < 200 || h < 150) return;

            #if NETFRAMEWORK
            double dpi = 1.0;
            #else
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            #endif

            // Outer Chassis Container
            var chassisBg = ZeroWpfTheme.BgCard;
            var chassisPen = ZeroWpfTheme.BorderPen;
            dc.DrawRoundedRectangle(chassisBg, chassisPen, new Rect(0.5, 0.5, w - 1, h - 1), 6, 6);

            // 1. Chassis Header & Overall Status
            double headerH = 44;
            DrawHeader(dc, new Rect(10, 10, w - 20, headerH), dpi);

            // 2. Dual Redundant PSUs
            double psuTop = headerH + 18;
            double psuH = 80;
            DrawPsuSection(dc, new Rect(10, psuTop, w - 20, psuH), dpi);

            // 3. Fans Tachometers & Optical DDM split
            double lowerTop = psuTop + psuH + 10;
            double lowerH = h - lowerTop - 10;
            double halfW = (w - 28) / 2.0;

            if (lowerH > 30)
            {
                DrawFansSection(dc, new Rect(10, lowerTop, halfW, lowerH), dpi);
                DrawOpticalDdmSection(dc, new Rect(18 + halfW, lowerTop, halfW, lowerH), dpi);
            }
        }

        private void DrawHeader(DrawingContext dc, Rect r, double dpi)
        {
            var headerBg = ZeroWpfTheme.BgInput;
            var headerPen = ZeroWpfTheme.BorderPen;
            dc.DrawRoundedRectangle(headerBg, headerPen, r, 4, 4);

            var titleFt = CreateFormattedText($"{_profile.DeviceName} ({_profile.Model})", ZeroWpfTheme.BoldTypeface, 11.0, ZeroWpfTheme.TextPrimary, dpi);
            double totalPower = _profile.Psu1.PowerWatts + _profile.Psu2.PowerWatts;
            var subFt = CreateFormattedText($"Serial: {_profile.SerialNumber} | Total Power: {totalPower:F0}W", ZeroWpfTheme.RegularTypeface, 9.5, ZeroWpfTheme.TextSecondary, dpi);

            dc.DrawText(titleFt, new Point(r.Left + 8, r.Top + 6));
            dc.DrawText(subFt, new Point(r.Left + 8, r.Top + 24));

            // Overall Health Pill
            ChassisOverallHealth status = _profile.EvaluateOverallHealth();
            Brush pillBrush = status switch
            {
                ChassisOverallHealth.Healthy => ZeroWpfTheme.SuccessAccent,
                ChassisOverallHealth.Degraded => ZeroWpfTheme.WarningAccent,
                _ => ZeroWpfTheme.DangerAccent
            };

            double pillW = 90;
            double pillH = 24;
            Rect pillRect = new Rect(r.Right - pillW - 8, r.Top + (r.Height - pillH) / 2.0, pillW, pillH);
            var pillBg = ZeroWpfTheme.BgCard;
            var pillPen = new Pen(pillBrush, 1.0);
            pillPen.Freeze();

            dc.DrawRoundedRectangle(pillBg, pillPen, pillRect, 4, 4);
            var pillFt = CreateFormattedText(status.ToString(), ZeroWpfTheme.BoldTypeface, 9.5, pillBrush, dpi);
            dc.DrawText(pillFt, new Point(pillRect.Left + (pillRect.Width - pillFt.Width) / 2.0, pillRect.Top + (pillRect.Height - pillFt.Height) / 2.0));
        }

        private void DrawPsuSection(DrawingContext dc, Rect r, double dpi)
        {
            var secBg = ZeroWpfTheme.BgInput;
            var secPen = ZeroWpfTheme.BorderPen;
            dc.DrawRoundedRectangle(secBg, secPen, r, 4, 4);

            var titleFt = CreateFormattedText("Dual Redundant Power Supplies", ZeroWpfTheme.BoldTypeface, 9.5, ZeroWpfTheme.TextSecondary, dpi);
            dc.DrawText(titleFt, new Point(r.Left + 8, r.Top + 6));

            double psuW = (r.Width - 24.0) / 2.0;
            double psuH = r.Height - 30.0;

            DrawSinglePsu(dc, _profile.Psu1, new Rect(r.Left + 8, r.Top + 22, psuW, psuH), dpi);
            DrawSinglePsu(dc, _profile.Psu2, new Rect(r.Left + 16 + psuW, r.Top + 22, psuW, psuH), dpi);
        }

        private void DrawSinglePsu(DrawingContext dc, PsuStatus psu, Rect r, double dpi)
        {
            var psuBg = ZeroWpfTheme.BgCard;
            var psuPen = ZeroWpfTheme.BorderPen;
            dc.DrawRoundedRectangle(psuBg, psuPen, r, 3, 3);

            Brush led = psu.Status == PsuHealthStatus.Normal ? ZeroWpfTheme.SuccessAccent :
                        psu.Status == PsuHealthStatus.Warning ? ZeroWpfTheme.WarningAccent : ZeroWpfTheme.DangerAccent;

            dc.DrawEllipse(led, null, new Point(r.Left + 10, r.Top + 12), 3.5, 3.5);

            var nameFt = CreateFormattedText($"PSU {psu.PsuIndex} ({psu.Status})", ZeroWpfTheme.BoldTypeface, 9.0, ZeroWpfTheme.TextPrimary, dpi);
            dc.DrawText(nameFt, new Point(r.Left + 20, r.Top + 5));

            string psuInfo = $"{psu.PowerWatts:F0}W | {psu.InputVoltageVolts:F0}V | {psu.TemperatureCelsius:F1}°C";
            var infoFt = CreateFormattedText(psuInfo, ZeroWpfTheme.RegularTypeface, 8.5, ZeroWpfTheme.TextSecondary, dpi);
            dc.DrawText(infoFt, new Point(r.Left + 8, r.Top + 24));
        }

        private void DrawFansSection(DrawingContext dc, Rect r, double dpi)
        {
            var secBg = ZeroWpfTheme.BgInput;
            var secPen = ZeroWpfTheme.BorderPen;
            dc.DrawRoundedRectangle(secBg, secPen, r, 4, 4);

            var titleFt = CreateFormattedText("Chassis Fan Tachometers", ZeroWpfTheme.BoldTypeface, 9.5, ZeroWpfTheme.TextSecondary, dpi);
            dc.DrawText(titleFt, new Point(r.Left + 8, r.Top + 6));

            int count = _profile.Fans.Count;
            if (count == 0) return;

            double slotW = (r.Width - 16.0) / count;
            for (int i = 0; i < count; i++)
            {
                var fan = _profile.Fans[i];
                Rect fanRect = new Rect(r.Left + 8 + i * slotW, r.Top + 22, slotW - 4, r.Height - 30);
                DrawFanTachometer(dc, fan, fanRect, dpi);
            }
        }

        private void DrawFanTachometer(DrawingContext dc, FanStatus fan, Rect r, double dpi)
        {
            double cx = r.Left + r.Width / 2.0;
            double cy = r.Top + 24;
            double radius = 16.0;

            // Outer ring
            var ringPen = ZeroWpfTheme.BorderPen;
            dc.DrawEllipse(ZeroWpfTheme.BgCard, ringPen, new Point(cx, cy), radius, radius);

            // Rotating fan blades
            var bladePen = new Pen(fan.Status == FanHealthStatus.Normal ? ZeroWpfTheme.PrimaryAccent : ZeroWpfTheme.DangerAccent, 2.0);
            bladePen.Freeze();

            for (int b = 0; b < 4; b++)
            {
                double angle = (_fanBladeAngle + b * 90.0) * Math.PI / 180.0;
                Point pEnd = new Point(cx + (radius - 3) * Math.Cos(angle), cy + (radius - 3) * Math.Sin(angle));
                dc.DrawLine(bladePen, new Point(cx, cy), pEnd);
            }

            dc.DrawEllipse(ZeroWpfTheme.TextPrimary, null, new Point(cx, cy), 2.5, 2.5);

            // RPM text
            var rpmFt = CreateFormattedText($"{fan.CurrentRpm} RPM", ZeroWpfTheme.BoldTypeface, 8.5, ZeroWpfTheme.TextPrimary, dpi);
            dc.DrawText(rpmFt, new Point(cx - rpmFt.Width / 2.0, cy + radius + 6));
        }

        private void DrawOpticalDdmSection(DrawingContext dc, Rect r, double dpi)
        {
            var secBg = ZeroWpfTheme.BgInput;
            var secPen = ZeroWpfTheme.BorderPen;
            dc.DrawRoundedRectangle(secBg, secPen, r, 4, 4);

            var titleFt = CreateFormattedText("SFP+ Optical DDM Telemetry", ZeroWpfTheme.BoldTypeface, 9.5, ZeroWpfTheme.TextSecondary, dpi);
            dc.DrawText(titleFt, new Point(r.Left + 8, r.Top + 6));

            int count = _profile.OpticalTransceivers.Count;
            if (count == 0) return;

            double slotH = (r.Height - 26.0) / count;
            for (int i = 0; i < count; i++)
            {
                var sfp = _profile.OpticalTransceivers[i];
                double y = r.Top + 22 + i * slotH;

                var portFt = CreateFormattedText(sfp.PortName, ZeroWpfTheme.BoldTypeface, 9.0, ZeroWpfTheme.TextPrimary, dpi);
                dc.DrawText(portFt, new Point(r.Left + 8, y));

                string metrics = $"Tx: {sfp.TxPowerDbm:F1} dBm | Rx: {sfp.RxPowerDbm:F1} dBm | {sfp.TransceiverTempCelsius:F1}°C";
                var metricFt = CreateFormattedText(metrics, ZeroWpfTheme.RegularTypeface, 8.5, ZeroWpfTheme.TextSecondary, dpi);
                dc.DrawText(metricFt, new Point(r.Left + 56, y));
            }
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZDeviceFaceplate"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("DeviceFaceplate is deprecated and will be removed in 5 release cycles. Please migrate to ZDeviceFaceplate instead.")]
    public class DeviceFaceplate : ZDeviceFaceplate { }

    /// <summary>
    /// Legacy alias for <see cref="ZDeviceFaceplate"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroDeviceFaceplate is deprecated and will be removed in 5 release cycles. Please migrate to ZDeviceFaceplate instead.")]
    public class ZeroDeviceFaceplate : ZDeviceFaceplate { }

    #endregion

}
