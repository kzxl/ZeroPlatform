using System;
using System.Windows;
using System.Windows.Media;
using ZeroUI.Core.Process;
using ZeroUI.Wpf.Base;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Process
{
    /// <summary>
    /// Sanitary Fermentation Bioreactor Vessel visualizer for WPF.
    /// Visualizes stainless steel dished vessel geometry, thermal jacket, rotating Rushton turbine impellers,
    /// ascending sparging aeration micro-bubbles, and critical DO / pH / Temp process telemetry.
    /// </summary>
    public class ZBioreactorVessel : ZeroWpfVisualBase
    {
        private readonly BioreactorEngine _engine = new BioreactorEngine();

        protected override bool AutoAnimate => true;

        public ZBioreactorVessel()
        {
        }

        #region Public Properties

        public BioreactorEngine Engine => _engine;

        public string VesselTag
        {
            get => _engine.VesselTag;
            set
            {
                _engine.VesselTag = value ?? "BR-01";
                InvalidateVisual();
            }
        }

        public double AgitationRpm
        {
            get => _engine.AgitationRpm;
            set
            {
                _engine.AgitationRpm = Math.Max(0.0, Math.Min(1200.0, value));
                InvalidateVisual();
            }
        }

        public double DissolvedOxygenPct
        {
            get => _engine.DissolvedOxygenPct;
            set
            {
                _engine.DissolvedOxygenPct = Math.Max(0.0, Math.Min(100.0, value));
                InvalidateVisual();
            }
        }

        public double PhValue
        {
            get => _engine.PhValue;
            set
            {
                _engine.PhValue = Math.Max(0.0, Math.Min(14.0, value));
                InvalidateVisual();
            }
        }

        public double VesselTempC
        {
            get => _engine.VesselTempC;
            set
            {
                _engine.VesselTempC = value;
                InvalidateVisual();
            }
        }

        #endregion

        protected override void OnAnimationTick(double delta, long frame)
        {
            _engine.AdvanceSimulation(delta);
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

            double hudW = 220;
            double vesselW = w - hudW - 28;

            if (vesselW < 120 || mainHeight < 100) return;

            Rect vesselRect = new Rect(14, mainTop, vesselW, mainHeight);
            Rect hudRect = new Rect(vesselRect.Right + 10, mainTop, hudW, mainHeight);

            // Vessel Cutaway
            DrawVessel(dc, vesselRect);

            // Process HUD
            DrawProcessHud(dc, hudRect, dpi);
        }

        private void DrawHeader(DrawingContext dc, double width, double dpi)
        {
            var titleFt = CreateFormattedText($"{_engine.VesselTag} — 500L Single-Use Bioreactor ({_engine.Phase})",
                ZeroWpfTheme.BoldTypeface, 11, ZeroWpfTheme.TextPrimary, dpi);
            dc.DrawText(titleFt, new Point(14, 12));

            Brush badgeBrush = _engine.AlarmStatus switch
            {
                BioreactorAlarmStatus.OutOfSpecAlarm => Brushes.Crimson,
                BioreactorAlarmStatus.Warning => Brushes.Orange,
                _ => Brushes.LimeGreen
            };

            Rect badgeRect = new Rect(width - 150, 10, 136, 22);
            DrawStatusBadge(dc, badgeRect, _engine.AlarmStatus.ToString().ToUpperInvariant(), badgeBrush, ZeroWpfTheme.BoldTypeface, 8.5);
        }

        private void DrawVessel(DrawingContext dc, Rect rect)
        {
            double tankW = Math.Min(200, rect.Width - 30);
            double tankH = rect.Height - 36;
            double tankX = rect.Left + (rect.Width - tankW) * 0.5;
            double tankY = rect.Top + 24;

            Rect tankBox = new Rect(tankX, tankY, tankW, tankH);

            // 1. Thermal Jacket Outer Annulus
            Rect jacketBox = new Rect(tankBox.Left - 6, tankBox.Top + 16, tankBox.Width + 12, tankBox.Height - 32);
            Brush jacketBg = new SolidColorBrush(Color.FromArgb(25, 249, 115, 22));
            jacketBg.Freeze();
            Pen jacketPen = new Pen(Brushes.Orange, 1.0);
            jacketPen.DashStyle = DashStyles.Dash;
            jacketPen.Freeze();
            dc.DrawRectangle(jacketBg, jacketPen, jacketBox);

            // 2. Stainless Steel Vessel Body
            Brush tankBg = new SolidColorBrush(Color.FromArgb(25, 148, 163, 184));
            tankBg.Freeze();
            Pen tankPen = new Pen(Brushes.LightSlateGray, 2.0);
            tankPen.Freeze();
            dc.DrawRectangle(tankBg, tankPen, tankBox);

            // 3. Culture Liquid Fill
            double fillFrac = Math.Max(0.0, Math.Min(1.0, _engine.VolumeFillPct / 100.0));
            double fillH = tankBox.Height * fillFrac;
            Rect fillBox = new Rect(tankBox.Left + 2, tankBox.Bottom - fillH, tankBox.Width - 4, fillH);

            if (fillH > 0)
            {
                Brush brothBrush = new LinearGradientBrush(
                    Color.FromArgb(180, 234, 179, 8),
                    Color.FromArgb(220, 202, 138, 4),
                    new Point(0, 0), new Point(1, 0));
                brothBrush.Freeze();
                dc.DrawRectangle(brothBrush, null, fillBox);
            }

            // 4. Center Agitator Drive & Shaft
            double midX = tankBox.Left + tankBox.Width * 0.5;
            dc.DrawRectangle(Brushes.SlateGray, null, new Rect(midX - 14, tankBox.Top - 16, 28, 16));

            Pen shaftPen = new Pen(Brushes.WhiteSmoke, 3.0);
            shaftPen.Freeze();
            dc.DrawLine(shaftPen, new Point(midX, tankBox.Top), new Point(midX, tankBox.Bottom - 14));

            // 5. Rotating Rushton Impellers
            double rad = _engine.ImpellerAngleDeg * Math.PI / 180.0;
            double cos = Math.Cos(rad);
            double impW = 28.0 * Math.Abs(cos) + 6.0;

            DrawImpeller(dc, midX, tankBox.Bottom - fillH * 0.3, impW);
            DrawImpeller(dc, midX, tankBox.Bottom - fillH * 0.7, impW);

            // 6. Aeration Sparger Bubbles
            if (_engine.SpargingAirLpm > 0)
            {
                double spargeY = tankBox.Bottom - 14;
                for (int b = -16; b <= 16; b += 8)
                {
                    dc.DrawEllipse(Brushes.White, null, new Point(midX + b, spargeY - 10), 1.5, 1.5);
                    dc.DrawEllipse(Brushes.White, null, new Point(midX + b + 3, spargeY - 22), 1.2, 1.2);
                }
            }
        }

        private static void DrawImpeller(DrawingContext dc, double cx, double cy, double width)
        {
            Rect impRect = new Rect(cx - width, cy - 3.5, width * 2.0, 7.0);
            dc.DrawRectangle(Brushes.Gold, ZeroWpfTheme.BorderPen, impRect);
        }

        private void DrawProcessHud(DrawingContext dc, Rect rect, double dpi)
        {
            DrawCardBox(dc, rect, "PROCESS TELEMETRY", ZeroWpfTheme.BoldTypeface, ZeroWpfTheme.TextSecondary, ZeroWpfTheme.BgCard, ZeroWpfTheme.BorderPen);

            double pad = 10;
            double left = rect.Left + pad;
            double right = rect.Right - pad;
            double rowY = rect.Top + 30;
            double rowH = 22;

            // Agitation RPM
            DrawDataRow(dc, left, right, rowY, "Agitation", $"{_engine.AgitationRpm:F0} RPM",
                ZeroWpfTheme.RegularTypeface, ZeroWpfTheme.BoldTypeface, ZeroWpfTheme.TextSecondary, Brushes.DodgerBlue);
            rowY += rowH;

            // Dissolved Oxygen
            double doVal = _engine.DissolvedOxygenPct;
            Brush doBrush = doVal >= 35.0 && doVal <= 55.0 ? Brushes.LimeGreen : Brushes.Orange;
            DrawDataRow(dc, left, right, rowY, "Dissolved O₂", $"{doVal:F1}% (Set {_engine.TargetDoPct:F0}%)",
                ZeroWpfTheme.RegularTypeface, ZeroWpfTheme.BoldTypeface, ZeroWpfTheme.TextSecondary, doBrush);
            rowY += rowH;

            // pH Value
            double ph = _engine.PhValue;
            Brush phBrush = Math.Abs(ph - _engine.TargetPh) < 0.1 ? Brushes.LimeGreen : Brushes.Orange;
            DrawDataRow(dc, left, right, rowY, "pH Regulation", $"{ph:F2} pH",
                ZeroWpfTheme.RegularTypeface, ZeroWpfTheme.BoldTypeface, ZeroWpfTheme.TextSecondary, phBrush);
            rowY += rowH;

            // Temperature °C
            double temp = _engine.VesselTempC;
            Brush tempBrush = Math.Abs(temp - _engine.TargetTempC) < 0.5 ? Brushes.LimeGreen : Brushes.Crimson;
            DrawDataRow(dc, left, right, rowY, "Vessel Temp", $"{temp:F1}°C (Jkt {_engine.JacketTempC:F1}°C)",
                ZeroWpfTheme.RegularTypeface, ZeroWpfTheme.BoldTypeface, ZeroWpfTheme.TextSecondary, tempBrush);
            rowY += rowH;

            // Sparging Flow
            DrawDataRow(dc, left, right, rowY, "Air / O₂ Sparge", $"{_engine.SpargingAirLpm:F1} / {_engine.SpargingO2Lpm:F1} LPM",
                ZeroWpfTheme.RegularTypeface, ZeroWpfTheme.BoldTypeface, ZeroWpfTheme.TextSecondary, ZeroWpfTheme.TextPrimary);
            rowY += rowH;

            // Mass Transfer kLa
            DrawDataRow(dc, left, right, rowY, "Oxygen kLa", $"{_engine.EstimatedKLaHr:F0} hr⁻¹",
                ZeroWpfTheme.RegularTypeface, ZeroWpfTheme.BoldTypeface, ZeroWpfTheme.TextSecondary, ZeroWpfTheme.TextPrimary);
            rowY += rowH;

            // Working Volume
            DrawDataRow(dc, left, right, rowY, "Working Volume", $"{_engine.WorkingVolumeL:N0} L / {_engine.TotalCapacityL:N0} L",
                ZeroWpfTheme.RegularTypeface, ZeroWpfTheme.BoldTypeface, ZeroWpfTheme.TextSecondary, ZeroWpfTheme.TextPrimary);
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZBioreactorVessel"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("BioreactorVessel is deprecated and will be removed in 5 release cycles. Please migrate to ZBioreactorVessel instead.")]
    public class BioreactorVessel : ZBioreactorVessel { }

    /// <summary>
    /// Legacy alias for <see cref="ZBioreactorVessel"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroBioreactorVessel is deprecated and will be removed in 5 release cycles. Please migrate to ZBioreactorVessel instead.")]
    public class ZeroBioreactorVessel : ZBioreactorVessel { }

    #endregion

}
