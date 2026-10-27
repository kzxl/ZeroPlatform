using System;
using System.Windows;
using System.Windows.Media;
using ZeroUI.Core.Water;
using ZeroUI.Wpf.Base;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Water
{
    /// <summary>
    /// Precision Chemical Dosing Skid visualizer for WPF.
    /// Visualizes bulk storage day-tank with fluid level, duty/standby diaphragm metering pumps
    /// with animated reciprocating stroke pulse, discharge piping, and flow-proportional pacing HUD.
    /// </summary>
    public class ZChemicalDosingSkid : ZeroWpfVisualBase
    {
        private readonly ChemicalDosingEngine _engine = new ChemicalDosingEngine();
        private string _skidTag = "CHEM-SKID-01";

        protected override bool AutoAnimate => true;

        public ZChemicalDosingSkid()
        {
        }

        #region Public Properties

        public ChemicalDosingEngine Engine => _engine;

        public string SkidTag
        {
            get => _skidTag;
            set
            {
                _skidTag = value ?? "SKID-01";
                InvalidateVisual();
            }
        }

        public ChemicalType Chemical
        {
            get => _engine.Chemical;
            set
            {
                _engine.Chemical = value;
                InvalidateVisual();
            }
        }

        public double WaterFlowM3H
        {
            get => _engine.WaterFlowM3H;
            set
            {
                _engine.WaterFlowM3H = Math.Max(0.0, value);
                _engine.AdjustPumpsToTargetRate();
                InvalidateVisual();
            }
        }

        public double TargetDoseMgL
        {
            get => _engine.TargetDoseMgL;
            set
            {
                _engine.TargetDoseMgL = Math.Max(0.0, value);
                _engine.AdjustPumpsToTargetRate();
                InvalidateVisual();
            }
        }

        public double TankCurrentLevelL
        {
            get => _engine.Tank.CurrentLevelL;
            set
            {
                _engine.Tank.CurrentLevelL = Math.Max(0.0, Math.Min(_engine.Tank.CapacityL, value));
                InvalidateVisual();
            }
        }

        #endregion

        protected override void OnAnimationTick(double delta, long frame)
        {
            _engine.AdvanceConsumption(delta);
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

            double tankW = 130;
            double hudW = 220;
            double skidW = w - tankW - hudW - 36;

            if (skidW < 120 || mainHeight < 80) return;

            Rect tankRect = new Rect(14, mainTop, tankW, mainHeight);
            Rect skidRect = new Rect(tankRect.Right + 10, mainTop, skidW, mainHeight);
            Rect hudRect = new Rect(skidRect.Right + 10, mainTop, hudW, mainHeight);

            // 1. Chemical Storage Tank
            DrawTank(dc, tankRect, dpi);

            // 2. Pumps Skid
            DrawPumpsSkid(dc, skidRect, dpi);

            // 3. Telemetry HUD
            DrawTelemetryHud(dc, hudRect, dpi);
        }

        private void DrawHeader(DrawingContext dc, double width, double dpi)
        {
            string chemName = _engine.Chemical switch
            {
                ChemicalType.Disinfectant => "Sodium Hypochlorite (NaOCl)",
                ChemicalType.Coagulant => "Coagulant (Alum / PAC)",
                ChemicalType.Polymer => "Polymer Flocculant",
                ChemicalType.PhAdjustment => "pH Adjustment (NaOH)",
                ChemicalType.Fluoride => "Fluoride (H2SiF6)",
                _ => "Treatment Chemical"
            };

            var titleFt = CreateFormattedText($"{_skidTag} — {chemName} Dosing Skid",
                ZeroWpfTheme.BoldTypeface, 11, ZeroWpfTheme.TextPrimary, dpi);
            dc.DrawText(titleFt, new Point(14, 12));

            double reqLh = _engine.CalculateRequiredDosingRateLh();
            string doseStr = $"Target: {_engine.TargetDoseMgL:F1} mg/L ({reqLh:F1} L/h)";
            Rect badgeRect = new Rect(width - 200, 10, 186, 22);
            DrawStatusBadge(dc, badgeRect, doseStr, Brushes.DodgerBlue, ZeroWpfTheme.BoldTypeface, 8.5);
        }

        private void DrawTank(DrawingContext dc, Rect rect, double dpi)
        {
            DrawCardBox(dc, rect, "STORAGE TANK", ZeroWpfTheme.BoldTypeface, ZeroWpfTheme.TextSecondary, ZeroWpfTheme.BgCard, ZeroWpfTheme.BorderPen);

            double pad = 12;
            Rect cylRect = new Rect(rect.Left + pad, rect.Top + 30, rect.Width - pad * 2, rect.Height - 58);

            // Tank outline
            Brush cylBg = new SolidColorBrush(Color.FromArgb(30, 148, 163, 184));
            cylBg.Freeze();
            dc.DrawRectangle(cylBg, ZeroWpfTheme.BorderPen, cylRect);

            // Liquid fill
            double lvlPct = Math.Max(0.0, Math.Min(100.0, _engine.Tank.LevelPct));
            double fillH = cylRect.Height * (lvlPct / 100.0);
            Rect fillRect = new Rect(cylRect.Left + 2, cylRect.Bottom - fillH, cylRect.Width - 4, fillH);

            Color chemColor = _engine.Chemical switch
            {
                ChemicalType.Disinfectant => Color.FromRgb(234, 179, 8),
                ChemicalType.Coagulant => Color.FromRgb(249, 115, 22),
                ChemicalType.Polymer => Color.FromRgb(168, 85, 247),
                ChemicalType.PhAdjustment => Color.FromRgb(59, 130, 246),
                _ => Color.FromRgb(34, 197, 94)
            };

            if (fillH > 0)
            {
                Brush fillBrush = new SolidColorBrush(Color.FromArgb(180, chemColor.R, chemColor.G, chemColor.B));
                fillBrush.Freeze();
                dc.DrawRectangle(fillBrush, null, fillRect);
            }

            // Level text
            string lvlText = $"{_engine.Tank.CurrentLevelL:N0} L ({lvlPct:F0}%)";
            Brush textBrush = _engine.Tank.IsLowLevel ? Brushes.Crimson : ZeroWpfTheme.TextPrimary;
            var lvlFt = CreateFormattedText(lvlText, ZeroWpfTheme.BoldTypeface, 8.0, textBrush, dpi);
            dc.DrawText(lvlFt, new Point(rect.Left + (rect.Width - lvlFt.Width) / 2, rect.Bottom - 22));
        }

        private void DrawPumpsSkid(DrawingContext dc, Rect rect, double dpi)
        {
            DrawCardBox(dc, rect, "METERING PUMPS & PIPING SKID", ZeroWpfTheme.BoldTypeface, ZeroWpfTheme.TextSecondary, ZeroWpfTheme.BgCard, ZeroWpfTheme.BorderPen);

            double midY = rect.Top + rect.Height * 0.5;
            double pumpW = 70;
            double pumpH = 58;

            // Suction Line from Left
            Pen suctionPen = new Pen(Brushes.DodgerBlue, 2.5);
            suctionPen.Freeze();
            dc.DrawLine(suctionPen, new Point(rect.Left, midY), new Point(rect.Left + 24, midY));
            dc.DrawLine(suctionPen, new Point(rect.Left + 24, midY - 32), new Point(rect.Left + 24, midY + 32));
            dc.DrawLine(suctionPen, new Point(rect.Left + 24, midY - 32), new Point(rect.Left + 52, midY - 32));
            dc.DrawLine(suctionPen, new Point(rect.Left + 24, midY + 32), new Point(rect.Left + 52, midY + 32));

            // Pump A
            Rect pumpARect = new Rect(rect.Left + 52, midY - 32 - pumpH * 0.5, pumpW, pumpH);
            DrawPumpUnit(dc, pumpARect, _engine.PumpA, dpi);

            // Pump B
            Rect pumpBRect = new Rect(rect.Left + 52, midY + 32 - pumpH * 0.5, pumpW, pumpH);
            DrawPumpUnit(dc, pumpBRect, _engine.PumpB, dpi);

            // Discharge Line merging to Right
            Pen dischargePen = new Pen(Brushes.SkyBlue, 2.5);
            dischargePen.Freeze();
            double rightX = rect.Left + 52 + pumpW;
            dc.DrawLine(dischargePen, new Point(rightX, midY - 32), new Point(rightX + 20, midY - 32));
            dc.DrawLine(dischargePen, new Point(rightX, midY + 32), new Point(rightX + 20, midY + 32));
            dc.DrawLine(dischargePen, new Point(rightX + 20, midY - 32), new Point(rightX + 20, midY + 32));
            dc.DrawLine(dischargePen, new Point(rightX + 20, midY), new Point(rect.Right - 14, midY));

            // Backpressure valve circle
            dc.DrawEllipse(null, dischargePen, new Point(rightX + 42, midY), 5, 5);

            // Main injection quill block
            Rect quillRect = new Rect(rect.Right - 14, midY - 12, 8, 24);
            dc.DrawRectangle(Brushes.SlateGray, null, quillRect);
        }

        private void DrawPumpUnit(DrawingContext dc, Rect rect, DosingPump pump, double dpi)
        {
            Brush pumpBrush = pump.Mode switch
            {
                DosingPumpMode.Fault => Brushes.Crimson,
                DosingPumpMode.Standby => Brushes.Gray,
                _ => pump.IsRunning ? Brushes.LimeGreen : Brushes.Orange
            };

            Pen pumpPen = new Pen(pumpBrush, 1.2);
            pumpPen.Freeze();

            Brush bgBrush = new SolidColorBrush(Color.FromArgb(35, 148, 163, 184));
            bgBrush.Freeze();
            dc.DrawRectangle(bgBrush, pumpPen, rect);

            // Diaphragm head indicator
            Point headCenter = new Point(rect.Left + 12, rect.Top + rect.Height * 0.5);
            dc.DrawEllipse(pumpBrush, null, headCenter, 7, 7);

            var tagFt = CreateFormattedText(pump.PumpTag, ZeroWpfTheme.BoldTypeface, 7.5, ZeroWpfTheme.TextPrimary, dpi);
            dc.DrawText(tagFt, new Point(rect.Left + 24, rect.Top + 6));

            var modeFt = CreateFormattedText(pump.Mode.ToString().ToUpperInvariant(), ZeroWpfTheme.RegularTypeface, 7.0, pumpBrush, dpi);
            dc.DrawText(modeFt, new Point(rect.Left + 24, rect.Top + 20));

            string spmStr = pump.IsRunning ? $"{pump.StrokeRateSpm:F0} SPM" : "OFF";
            var spmFt = CreateFormattedText(spmStr, ZeroWpfTheme.RegularTypeface, 7.0, ZeroWpfTheme.TextSecondary, dpi);
            dc.DrawText(spmFt, new Point(rect.Left + 24, rect.Top + 34));
        }

        private void DrawTelemetryHud(DrawingContext dc, Rect rect, double dpi)
        {
            DrawCardBox(dc, rect, "DOSING TELEMETRY", ZeroWpfTheme.BoldTypeface, ZeroWpfTheme.TextSecondary, ZeroWpfTheme.BgCard, ZeroWpfTheme.BorderPen);

            double pad = 10;
            double left = rect.Left + pad;
            double right = rect.Right - pad;
            double rowY = rect.Top + 30;
            double rowH = 22;

            // Main Water Flow
            DrawDataRow(dc, left, right, rowY, "Main Flow", $"{_engine.WaterFlowM3H:N0} m³/h",
                ZeroWpfTheme.RegularTypeface, ZeroWpfTheme.BoldTypeface, ZeroWpfTheme.TextSecondary, ZeroWpfTheme.TextPrimary);
            rowY += rowH;

            // Target Dose Setpoint
            DrawDataRow(dc, left, right, rowY, "Target Dose", $"{_engine.TargetDoseMgL:F2} mg/L",
                ZeroWpfTheme.RegularTypeface, ZeroWpfTheme.BoldTypeface, ZeroWpfTheme.TextSecondary, Brushes.DodgerBlue);
            rowY += rowH;

            // Actual Delivery Rate
            double delivered = _engine.TotalDeliveredDosingRateLh;
            double req = _engine.CalculateRequiredDosingRateLh();
            Brush rateBrush = Math.Abs(delivered - req) < 1.0 ? Brushes.LimeGreen : Brushes.Orange;
            DrawDataRow(dc, left, right, rowY, "Actual Rate", $"{delivered:F1} / {req:F1} L/h",
                ZeroWpfTheme.RegularTypeface, ZeroWpfTheme.BoldTypeface, ZeroWpfTheme.TextSecondary, rateBrush);
            rowY += rowH;

            // Active Discharge Pressure
            double press = _engine.PumpA.IsRunning ? _engine.PumpA.DischargePressureBar : _engine.PumpB.DischargePressureBar;
            DrawDataRow(dc, left, right, rowY, "Discharge Press", $"{press:F1} bar",
                ZeroWpfTheme.RegularTypeface, ZeroWpfTheme.BoldTypeface, ZeroWpfTheme.TextSecondary, ZeroWpfTheme.TextPrimary);
            rowY += rowH;

            // Solution Concentration
            DrawDataRow(dc, left, right, rowY, "Concentration", $"{_engine.Tank.SolutionConcentrationPct:F1}% (SG {_engine.Tank.SpecificGravity:F2})",
                ZeroWpfTheme.RegularTypeface, ZeroWpfTheme.BoldTypeface, ZeroWpfTheme.TextSecondary, ZeroWpfTheme.TextPrimary);
            rowY += rowH;

            // Estimated Storage Autonomy
            double hoursLeft = _engine.EstimatedRunHoursRemaining;
            string autoStr = double.IsPositiveInfinity(hoursLeft) ? "∞" : $"{hoursLeft:F1} hrs";
            Brush autoBrush = hoursLeft < 24.0 ? Brushes.Crimson : Brushes.LimeGreen;
            DrawDataRow(dc, left, right, rowY, "Tank Autonomy", autoStr,
                ZeroWpfTheme.RegularTypeface, ZeroWpfTheme.BoldTypeface, ZeroWpfTheme.TextSecondary, autoBrush);
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZChemicalDosingSkid"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ChemicalDosingSkid is deprecated and will be removed in 5 release cycles. Please migrate to ZChemicalDosingSkid instead.")]
    public class ChemicalDosingSkid : ZChemicalDosingSkid { }

    /// <summary>
    /// Legacy alias for <see cref="ZChemicalDosingSkid"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroChemicalDosingSkid is deprecated and will be removed in 5 release cycles. Please migrate to ZChemicalDosingSkid instead.")]
    public class ZeroChemicalDosingSkid : ZChemicalDosingSkid { }

    #endregion

}
