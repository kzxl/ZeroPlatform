using System;
using System.Windows;
using System.Windows.Media;
using ZeroUI.Core.Process;
using ZeroUI.Wpf.Base;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Process
{
    /// <summary>
    /// Clean-in-Place & Steam-in-Place (CIP/SIP) 4 TACT validation matrix for WPF.
    /// Visualizes Time, Action (turbulent scouring velocity), Chemical concentration (conductivity),
    /// and Temperature parameters alongside the FDA thermal sterilization equivalent lethality (F0 value).
    /// </summary>
    public class ZCipValidationMatrix : ZeroWpfVisualBase
    {
        private readonly CipValidationEngine _engine = new CipValidationEngine();

        protected override bool AutoAnimate => true;

        public ZCipValidationMatrix()
        {
        }

        #region Public Properties

        public CipValidationEngine Engine => _engine;

        public string SkidTag
        {
            get => _engine.SkidTag;
            set
            {
                _engine.SkidTag = value ?? "SKID-01";
                InvalidateVisual();
            }
        }

        public CipCyclePhase Phase
        {
            get => _engine.CurrentPhase;
            set
            {
                _engine.CurrentPhase = value;
                InvalidateVisual();
            }
        }

        public double ReturnTempC
        {
            get => _engine.Tact.ReturnTempC;
            set
            {
                _engine.Tact.ReturnTempC = value;
                InvalidateVisual();
            }
        }

        public double ConductivityMsCm
        {
            get => _engine.Tact.ConductivityMsCm;
            set
            {
                _engine.Tact.ConductivityMsCm = Math.Max(0.0, value);
                InvalidateVisual();
            }
        }

        #endregion

        protected override void OnAnimationTick(double delta, long frame)
        {
            _engine.AdvanceTime(delta);
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

            double hudW = 230;
            double matrixW = w - hudW - 28;

            if (matrixW < 120 || mainHeight < 80) return;

            Rect matrixRect = new Rect(14, mainTop, matrixW, mainHeight);
            Rect hudRect = new Rect(matrixRect.Right + 10, mainTop, hudW, mainHeight);

            // 4 TACT Cards
            DrawTactMatrix(dc, matrixRect, dpi);

            // F0 Sterility HUD
            DrawF0Hud(dc, hudRect, dpi);
        }

        private void DrawHeader(DrawingContext dc, double width, double dpi)
        {
            var titleFt = CreateFormattedText($"{_engine.SkidTag} — {_engine.CircuitTag} ({_engine.CurrentPhase})",
                ZeroWpfTheme.BoldTypeface, 11, ZeroWpfTheme.TextPrimary, dpi);
            dc.DrawText(titleFt, new Point(14, 12));

            bool allOk = _engine.Tact.AreAllTactCriteriaMet;
            Brush badgeBrush = allOk ? Brushes.LimeGreen : Brushes.Orange;
            string badgeStr = allOk ? "TACT VALIDATED ✓" : "PARAMETER DRIFT";

            Rect badgeRect = new Rect(width - 170, 10, 156, 22);
            DrawStatusBadge(dc, badgeRect, badgeStr, badgeBrush, ZeroWpfTheme.BoldTypeface, 8.5);
        }

        private void DrawTactMatrix(DrawingContext dc, Rect rect, double dpi)
        {
            double cellW = (rect.Width - 10) * 0.5;
            double cellH = (rect.Height - 10) * 0.5;

            Rect cardT1 = new Rect(rect.Left, rect.Top, cellW, cellH);
            Rect cardA = new Rect(rect.Left + cellW + 10, rect.Top, cellW, cellH);
            Rect cardC = new Rect(rect.Left, rect.Top + cellH + 10, cellW, cellH);
            Rect cardT2 = new Rect(rect.Left + cellW + 10, rect.Top + cellH + 10, cellW, cellH);

            DrawTactCell(dc, cardT1, "TIME (CONTACT PERIOD)", $"{_engine.Tact.ElapsedSec:F0}s / {_engine.Tact.TargetDurationSec:F0}s",
                _engine.Tact.IsTimeValid ? "VALIDATED" : "RUNNING",
                _engine.Tact.IsTimeValid ? Brushes.LimeGreen : Brushes.DodgerBlue, dpi);

            DrawTactCell(dc, cardA, "ACTION (TURBULENT VELOCITY)", $"{_engine.Tact.FlowVelocityMS:F2} m/s",
                _engine.Tact.IsActionValid ? "TURBULENT (Re > 10K)" : "INSUFFICIENT FLOW",
                _engine.Tact.IsActionValid ? Brushes.LimeGreen : Brushes.Crimson, dpi);

            DrawTactCell(dc, cardC, "CHEMICAL (CONDUCTIVITY)", $"{_engine.Tact.ConductivityMsCm:F1} mS/cm",
                _engine.Tact.IsChemicalValid ? "CONCENTRATION OK" : "LOW CONDUCTIVITY",
                _engine.Tact.IsChemicalValid ? Brushes.LimeGreen : Brushes.Crimson, dpi);

            DrawTactCell(dc, cardT2, "TEMPERATURE (RETURN LOOP)", $"{_engine.Tact.ReturnTempC:F1}°C (Target {_engine.Tact.TargetTempC:F0}°C)",
                _engine.Tact.IsTemperatureValid ? "TEMP ATTAINED" : "HEATING UP",
                _engine.Tact.IsTemperatureValid ? Brushes.LimeGreen : Brushes.Orange, dpi);
        }

        private void DrawTactCell(DrawingContext dc, Rect rect, string title, string value, string status, Brush statusBrush, double dpi)
        {
            Brush bgBrush = new SolidColorBrush(Color.FromArgb(25, 148, 163, 184));
            bgBrush.Freeze();
            dc.DrawRectangle(bgBrush, ZeroWpfTheme.BorderPen, rect);

            var titleFt = CreateFormattedText(title, ZeroWpfTheme.BoldTypeface, 7.5, ZeroWpfTheme.TextSecondary, dpi);
            dc.DrawText(titleFt, new Point(rect.Left + 8, rect.Top + 6));

            var valFt = CreateFormattedText(value, ZeroWpfTheme.BoldTypeface, 10.0, ZeroWpfTheme.TextPrimary, dpi);
            dc.DrawText(valFt, new Point(rect.Left + 8, rect.Top + 24));

            var statusFt = CreateFormattedText($"● {status}", ZeroWpfTheme.RegularTypeface, 7.5, statusBrush, dpi);
            dc.DrawText(statusFt, new Point(rect.Left + 8, rect.Bottom - 20));
        }

        private void DrawF0Hud(DrawingContext dc, Rect rect, double dpi)
        {
            DrawCardBox(dc, rect, "STERILITY ASSURANCE (F₀)", ZeroWpfTheme.BoldTypeface, ZeroWpfTheme.TextSecondary, ZeroWpfTheme.BgCard, ZeroWpfTheme.BorderPen);

            double pad = 10;
            double left = rect.Left + pad;
            double right = rect.Right - pad;
            double rowY = rect.Top + 30;
            double rowH = 22;

            // F0 Accumulated
            double f0 = _engine.AccumulatedF0Minutes;
            Brush f0Brush = f0 >= _engine.TargetF0Minutes ? Brushes.LimeGreen : Brushes.DodgerBlue;
            DrawDataRow(dc, left, right, rowY, "Accumulated F₀", $"{f0:F2} min",
                ZeroWpfTheme.RegularTypeface, ZeroWpfTheme.BoldTypeface, ZeroWpfTheme.TextSecondary, f0Brush);
            rowY += rowH;

            // Target F0
            DrawDataRow(dc, left, right, rowY, "Target F₀", $"{_engine.TargetF0Minutes:F1} min (SAL 10⁻⁶)",
                ZeroWpfTheme.RegularTypeface, ZeroWpfTheme.BoldTypeface, ZeroWpfTheme.TextSecondary, ZeroWpfTheme.TextPrimary);
            rowY += rowH;

            // Steam Pressure
            DrawDataRow(dc, left, right, rowY, "Steam Pressure", $"{_engine.SteamPressureBar:F1} bar gauge",
                ZeroWpfTheme.RegularTypeface, ZeroWpfTheme.BoldTypeface, ZeroWpfTheme.TextSecondary, ZeroWpfTheme.TextPrimary);
            rowY += rowH;

            // Lethality Rate L
            double rate = CipValidationEngine.CalculateLethalityRate(_engine.Tact.ReturnTempC);
            Brush rateBrush = rate >= 1.0 ? Brushes.LimeGreen : ZeroWpfTheme.TextSecondary;
            DrawDataRow(dc, left, right, rowY, "Lethality Rate L", $"{rate:F2} min/min",
                ZeroWpfTheme.RegularTypeface, ZeroWpfTheme.BoldTypeface, ZeroWpfTheme.TextSecondary, rateBrush);
            rowY += rowH + 8;

            // F0 Bar
            double f0Frac = Math.Max(0.0, Math.Min(1.0, f0 / _engine.TargetF0Minutes));
            Rect f0Track = new Rect(left, rowY, right - left, 9);
            Brush trackBrush = new SolidColorBrush(Color.FromArgb(40, 148, 163, 184));
            trackBrush.Freeze();
            dc.DrawRectangle(trackBrush, null, f0Track);

            Rect f0Fill = new Rect(f0Track.Left, f0Track.Top, f0Track.Width * f0Frac, f0Track.Height);
            dc.DrawRectangle(f0Brush, null, f0Fill);
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZCipValidationMatrix"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("CipValidationMatrix is deprecated and will be removed in 5 release cycles. Please migrate to ZCipValidationMatrix instead.")]
    public class CipValidationMatrix : ZCipValidationMatrix { }

    /// <summary>
    /// Legacy alias for <see cref="ZCipValidationMatrix"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroCipValidationMatrix is deprecated and will be removed in 5 release cycles. Please migrate to ZCipValidationMatrix instead.")]
    public class ZeroCipValidationMatrix : ZCipValidationMatrix { }

    #endregion

}
