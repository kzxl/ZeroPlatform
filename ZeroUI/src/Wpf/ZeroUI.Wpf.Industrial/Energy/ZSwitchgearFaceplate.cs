using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using ZeroUI.Core.Energy;
using ZeroUI.Core.Rendering;
using ZeroUI.Wpf.Base;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Energy
{
    /// <summary>
    /// Medium/High-Voltage Vacuum Circuit Breaker (VCB) Switchgear front faceplate visualizer in WPF.
    /// Displays operating spring charge mechanical flag, vacuum bottle contact wear gauge,
    /// trip coil continuous supervision LEDs, LOTO padlock interlock, and protection relay telemetry.
    /// </summary>
    public class ZSwitchgearFaceplate : ZeroWpfVisualBase
    {
        private readonly SwitchgearEngine _engine = new SwitchgearEngine();
        private string _breakerTag = "VCB-BAY-04 (110kV Incomer)";

        protected override bool AutoAnimate => true;

        #region Properties

        public SwitchgearEngine Engine => _engine;

        public string BreakerTag
        {
            get => _breakerTag;
            set
            {
                _breakerTag = value;
                InvalidateVisual();
            }
        }

        #endregion

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double w = ActualWidth;
            double h = ActualHeight;
            if (w < 200 || h < 120)
                return;

            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            // Background
            dc.DrawRectangle(ZeroWpfTheme.BgPrimary, null, new Rect(0, 0, w, h));

            // 1. Header Banner
            var tagText = CreateFormattedText(_breakerTag, ZeroWpfTheme.BoldTypeface, 13, ZeroWpfTheme.TextPrimary, dpi);
            dc.DrawText(tagText, new Point(20, 14));

            bool permit = SwitchgearEngine.ValidateClosePermit(_engine.Mechanism, out string reason);
            Brush permitBrush = permit ? ZeroWpfTheme.SuccessAccent : ZeroWpfTheme.DangerAccent;

            Rect permitRect = new Rect(w - 280, 10, 260, 26);
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(25, permitBrush is SolidColorBrush scb ? scb.Color.R : (byte)34, permitBrush is SolidColorBrush scb2 ? scb2.Color.G : (byte)197, permitBrush is SolidColorBrush scb3 ? scb3.Color.B : (byte)94)),
                new Pen(permitBrush, 1), permitRect, 4, 4);

            var reasonText = CreateFormattedText(reason, ZeroWpfTheme.BoldTypeface, 10, permitBrush, dpi);
            dc.DrawText(reasonText, new Point(permitRect.X + (permitRect.Width - reasonText.Width) / 2, permitRect.Y + 5));

            // 3-Column layout
            double contentTop = 52;
            double contentW = w - 40;
            double contentH = h - contentTop - 16;
            if (contentW < 180 || contentH < 80) return;

            double col1W = contentW * 0.32;
            double col2W = contentW * 0.36;
            double col3W = contentW - col1W - col2W - 24;

            double col1X = 20;
            double col2X = col1X + col1W + 12;
            double col3X = col2X + col2W + 12;

            Rect r1 = new Rect(col1X, contentTop, col1W, contentH);
            Rect r2 = new Rect(col2X, contentTop, col2W, contentH);
            Rect r3 = new Rect(col3X, contentTop, col3W, contentH);

            DrawMechanicalPanel(dc, r1, dpi);
            DrawVacuumHealthPanel(dc, r2, dpi);
            DrawRelaySafetyPanel(dc, r3, dpi);
        }

        private void DrawMechanicalPanel(DrawingContext dc, Rect rect, double dpi)
        {
            DrawCardBox(dc, rect, "MECHANISM & CHARGE", dpi);
            var mech = _engine.Mechanism;

            // Spring flag box
            Rect flagRect = new Rect(rect.X + 16, rect.Y + 36, rect.Width - 32, 48);
            Color flagBg = mech.IsSpringCharged ? Color.FromRgb(245, 158, 11) : Color.FromRgb(75, 85, 99);
            dc.DrawRoundedRectangle(new SolidColorBrush(flagBg), new Pen(new SolidColorBrush(Color.FromArgb(200, flagBg.R, flagBg.G, flagBg.B)), 2), flagRect, 6, 6);

            string flagStr = mech.IsSpringCharged ? "SPR. CHARGED" : "DISCHARGED";
            var flagTxt = CreateFormattedText(flagStr, ZeroWpfTheme.BoldTypeface, 12, Brushes.White, dpi);
            dc.DrawText(flagTxt, new Point(flagRect.X + (flagRect.Width - flagTxt.Width) / 2, flagRect.Y + (flagRect.Height - flagTxt.Height) / 2));

            // Motor charge
            double rowY = flagRect.Bottom + 16;
            DrawDataRow(dc, rect.X + 16, rect.Right - 16, rowY, "Charge Motor:", mech.IsMotorCharging ? "Charging..." : "Idle", ZeroWpfTheme.TextSecondary, ZeroWpfTheme.TextPrimary, dpi);

            rowY += 24;
            DrawDataRow(dc, rect.X + 16, rect.Right - 16, rowY, "Operations Count:", mech.TotalOperationsCount.ToString("N0"), ZeroWpfTheme.TextSecondary, ZeroWpfTheme.TextPrimary, dpi);

            rowY += 24;
            DrawDataRow(dc, rect.X + 16, rect.Right - 16, rowY, "Mech Temp:", $"{mech.MechanismTemperatureC:F1} °C", ZeroWpfTheme.TextSecondary, ZeroWpfTheme.TextPrimary, dpi);

            rowY += 24;
            DrawDataRow(dc, rect.X + 16, rect.Right - 16, rowY, "Rated Breaking:", $"{mech.RatedShortCircuitKa:F1} kA", ZeroWpfTheme.TextSecondary, ZeroWpfTheme.TextPrimary, dpi);
        }

        private void DrawVacuumHealthPanel(DrawingContext dc, Rect rect, double dpi)
        {
            DrawCardBox(dc, rect, "VACUUM BOTTLE HEALTH", dpi);
            var mech = _engine.Mechanism;

            double barY = rect.Y + 38;
            var lblWear = CreateFormattedText("Contact Erosion Wear:", ZeroWpfTheme.RegularTypeface, 10, ZeroWpfTheme.TextSecondary, dpi);
            dc.DrawText(lblWear, new Point(rect.X + 16, barY));

            Brush wearBrush = mech.VacuumBottleContactWearPct < 60 ? ZeroWpfTheme.SuccessAccent :
                              mech.VacuumBottleContactWearPct < 85 ? ZeroWpfTheme.WarningAccent :
                              ZeroWpfTheme.DangerAccent;

            var valWear = CreateFormattedText($"{mech.VacuumBottleContactWearPct:F1}%", ZeroWpfTheme.BoldTypeface, 10.5, wearBrush, dpi);
            dc.DrawText(valWear, new Point(rect.Right - 16 - valWear.Width, barY));

            // Wear Progress Bar
            Rect barRect = new Rect(rect.X + 16, barY + 22, rect.Width - 32, 12);
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(40, 128, 128, 128)), null, barRect, 3, 3);

            double fillW = (barRect.Width * Math.Min(100.0, mech.VacuumBottleContactWearPct)) / 100.0;
            if (fillW > 0)
            {
                Rect fillRect = new Rect(barRect.X, barRect.Y, fillW, barRect.Height);
                dc.DrawRoundedRectangle(wearBrush, null, fillRect, 3, 3);
            }

            // Trip coils
            double coilY = barRect.Bottom + 18;
            DrawTripCoilRow(dc, rect.X + 16, rect.Right - 16, coilY, "Trip Coil 1 (TC1):", mech.TripCoil1, dpi);

            coilY += 26;
            DrawTripCoilRow(dc, rect.X + 16, rect.Right - 16, coilY, "Trip Coil 2 (TC2):", mech.TripCoil2, dpi);

            coilY += 28;
            Rect chamberRect = new Rect(rect.X + 16, coilY, rect.Width - 32, 36);
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(20, 59, 130, 246)), new Pen(new SolidColorBrush(Color.FromRgb(59, 130, 246)), 1), chamberRect, 4, 4);

            var integText = CreateFormattedText("Vacuum Integrity: HIGH-INTEGRITY", ZeroWpfTheme.BoldTypeface, 9.5, new SolidColorBrush(Color.FromRgb(59, 130, 246)), dpi);
            dc.DrawText(integText, new Point(chamberRect.X + (chamberRect.Width - integText.Width) / 2, chamberRect.Y + 10));
        }

        private void DrawRelaySafetyPanel(DrawingContext dc, Rect rect, double dpi)
        {
            DrawCardBox(dc, rect, "RELAY & LOTO SAFETY", dpi);
            var relay = _engine.Relay;
            var mech = _engine.Mechanism;

            double rowY = rect.Y + 36;
            DrawDataRow(dc, rect.X + 16, rect.Right - 16, rowY, "Relay Type:", relay.RelayTag, ZeroWpfTheme.TextSecondary, ZeroWpfTheme.TextPrimary, dpi);

            rowY += 24;
            DrawDataRow(dc, rect.X + 16, rect.Right - 16, rowY, "ANSI Protection:", relay.ActiveAnsiCode, ZeroWpfTheme.TextSecondary, new SolidColorBrush(Color.FromRgb(59, 130, 246)), dpi);

            rowY += 24;
            string pickStr = relay.IsPickupActive ? "PICKUP ACTIVE" : "Normal";
            Brush pickBrush = relay.IsPickupActive ? ZeroWpfTheme.DangerAccent : ZeroWpfTheme.SuccessAccent;
            DrawDataRow(dc, rect.X + 16, rect.Right - 16, rowY, "Pickup / Alarm:", pickStr, ZeroWpfTheme.TextSecondary, pickBrush, dpi);

            // LOTO box
            rowY += 32;
            Rect lotoRect = new Rect(rect.X + 16, rowY, rect.Width - 32, 50);
            Brush lotoBrush = mech.LotoState == LotoLockState.LockedOut ? ZeroWpfTheme.DangerAccent :
                              mech.LotoState == LotoLockState.PermitActive ? ZeroWpfTheme.WarningAccent :
                              ZeroWpfTheme.SuccessAccent;

            Color lotoCol = lotoBrush is SolidColorBrush lscb ? lscb.Color : Color.FromRgb(34, 197, 94);
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(20, lotoCol.R, lotoCol.G, lotoCol.B)),
                new Pen(lotoBrush, 1.5), lotoRect, 4, 4);

            string lotoText = mech.LotoState == LotoLockState.LockedOut ? "[LOCKED OUT] Padlock Applied" :
                              mech.LotoState == LotoLockState.PermitActive ? "[PERMIT ACTIVE] Tagged" :
                              "[UNLOCKED] No Interlock";
            var lotoTxt = CreateFormattedText(lotoText, ZeroWpfTheme.BoldTypeface, 10, lotoBrush, dpi);
            dc.DrawText(lotoTxt, new Point(lotoRect.X + (lotoRect.Width - lotoTxt.Width) / 2, lotoRect.Y + (lotoRect.Height - lotoTxt.Height) / 2));
        }


        private void DrawTripCoilRow(DrawingContext dc, double left, double right, double y, string label, TripCoilState state, double dpi)
        {
            var lText = CreateFormattedText(label, ZeroWpfTheme.RegularTypeface, 10, ZeroWpfTheme.TextSecondary, dpi);
            dc.DrawText(lText, new Point(left, y));

            Brush ledBrush = state == TripCoilState.Healthy ? ZeroWpfTheme.SuccessAccent :
                             state == TripCoilState.OpenCircuit ? ZeroWpfTheme.DangerAccent :
                             ZeroWpfTheme.WarningAccent;

            double ledX = right - 50;
            dc.DrawEllipse(ledBrush, null, new Point(ledX, y + 6), 5, 5);

            string statText = state == TripCoilState.Healthy ? "OK" : "FAULT";
            var sText = CreateFormattedText(statText, ZeroWpfTheme.BoldTypeface, 9, ledBrush, dpi);
            dc.DrawText(sText, new Point(ledX + 12, y));
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZSwitchgearFaceplate"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("SwitchgearFaceplate is deprecated and will be removed in 5 release cycles. Please migrate to ZSwitchgearFaceplate instead.")]
    public class SwitchgearFaceplate : ZSwitchgearFaceplate { }

    /// <summary>
    /// Legacy alias for <see cref="ZSwitchgearFaceplate"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroSwitchgearFaceplate is deprecated and will be removed in 5 release cycles. Please migrate to ZSwitchgearFaceplate instead.")]
    public class ZeroSwitchgearFaceplate : ZSwitchgearFaceplate { }

    #endregion

}
