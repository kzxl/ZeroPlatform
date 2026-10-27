using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Process;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Process
{
    /// <summary>
    /// Clean-in-Place & Steam-in-Place (CIP/SIP) 4 TACT validation matrix for WinForms.
    /// Visualizes Time, Action (turbulent scouring velocity), Chemical concentration (conductivity),
    /// and Temperature parameters alongside the FDA thermal sterilization equivalent lethality (F0 value).
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Process & Life Sciences")]
    [Description("CIP/SIP 4 TACT validation matrix and thermal sterilization F0 lethality accumulator")]
    public class ZCipValidationMatrix : VisualControlBase
    {
        private readonly CipValidationEngine _engine = new CipValidationEngine();

        protected override bool AutoAnimate => true;

        public ZCipValidationMatrix()
        {
            Size = new Size(740, 360);
        }

        #region Public Properties

        [Category("CIP/SIP")]
        [Description("Access to the underlying pure CIP/SIP validation engine")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public CipValidationEngine Engine => _engine;

        [Category("CIP/SIP")]
        [DefaultValue("CIP-SKID-02")]
        public string SkidTag
        {
            get => _engine.SkidTag;
            set
            {
                _engine.SkidTag = value ?? "SKID-01";
                Invalidate();
            }
        }

        [Category("CIP/SIP")]
        [DefaultValue(CipCyclePhase.CausticWash)]
        public CipCyclePhase Phase
        {
            get => _engine.CurrentPhase;
            set
            {
                _engine.CurrentPhase = value;
                Invalidate();
            }
        }

        [Category("CIP/SIP")]
        [DefaultValue(81.2)]
        public double ReturnTempC
        {
            get => _engine.Tact.ReturnTempC;
            set
            {
                _engine.Tact.ReturnTempC = value;
                Invalidate();
            }
        }

        [Category("CIP/SIP")]
        [DefaultValue(46.2)]
        public double ConductivityMsCm
        {
            get => _engine.Tact.ConductivityMsCm;
            set
            {
                _engine.Tact.ConductivityMsCm = Math.Max(0.0, value);
                Invalidate();
            }
        }

        #endregion

        protected override void OnAnimationTick(double delta, long frame)
        {
            _engine.AdvanceTime(delta);
        }

        protected override void OnDrawVisual(Graphics g, Rectangle bounds, ZeroThemePalette palette)
        {
            // 1. Header
            DrawHeader(g, bounds, palette, out int headerH);

            int mainTop = bounds.Y + headerH;
            int mainHeight = bounds.Height - headerH - 12;

            int hudW = bounds.Width < 540 ? Math.Max(160, (int)(bounds.Width * 0.42)) : 240;
            int matrixW = bounds.Width - hudW - 32;

            if (matrixW < 80 || mainHeight < 80) return;

            Rectangle matrixRect = new Rectangle(bounds.X + 16, mainTop, matrixW, mainHeight);
            Rectangle hudRect = new Rectangle(matrixRect.Right + 12, mainTop, hudW, mainHeight);

            // 2. 4 TACT Parameters Grid
            DrawTactMatrix(g, matrixRect, palette);

            // 3. F0 Sterilization & Sterility HUD
            DrawF0Hud(g, hudRect, palette);
        }

        private void DrawHeader(Graphics g, Rectangle bounds, ZeroThemePalette palette, out int headerH)
        {
            string title = $"{_engine.SkidTag} — {_engine.CircuitTag} ({_engine.CurrentPhase})";
            using (var fontTitle = new Font("Segoe UI", 10.5f, FontStyle.Bold))
            using (var fontSub = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            {
                Size titleSize = TextRenderer.MeasureText(g, title, fontTitle);

                bool allOk = _engine.Tact.AreAllTactCriteriaMet;
                Color badgeColor = allOk ? Color.FromArgb(34, 197, 94) : Color.FromArgb(245, 158, 11);
                string badgeStr = allOk ? "TACT VALIDATED ✓" : "PARAMETER DRIFT";

                int badgeW = 164;
                int badgeH = 24;

                if (bounds.Width - badgeW - 20 >= 20 + titleSize.Width + 16)
                {
                    TextRenderer.DrawText(g, title, fontTitle, new Point(bounds.X + 16, bounds.Y + 12), palette.TextPrimary);
                    Rectangle badgeRect = new Rectangle(bounds.Right - badgeW - 16, bounds.Y + 10, badgeW, badgeH);
                    PaintHelper.DrawStatusBadge(g, badgeRect, badgeStr, fontSub, badgeColor, badgeColor, 4);
                    headerH = 46;
                }
                else
                {
                    Rectangle titleRect = new Rectangle(bounds.X + 16, bounds.Y + 8, bounds.Width - 32, 20);
                    TextRenderer.DrawText(g, title, fontTitle, titleRect, palette.TextPrimary, TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.EndEllipsis);
                    Rectangle badgeRect = new Rectangle(bounds.X + 16, bounds.Y + 32, Math.Min(bounds.Width - 32, badgeW), badgeH);
                    PaintHelper.DrawStatusBadge(g, badgeRect, badgeStr, fontSub, badgeColor, badgeColor, 4);
                    headerH = 64;
                }
            }
        }

        private void DrawTactMatrix(Graphics g, Rectangle rect, ZeroThemePalette palette)
        {
            int cellW = (rect.Width - 10) / 2;
            int cellH = (rect.Height - 10) / 2;

            Rectangle cardT1 = new Rectangle(rect.X, rect.Y, cellW, cellH);
            Rectangle cardA = new Rectangle(rect.X + cellW + 10, rect.Y, cellW, cellH);
            Rectangle cardC = new Rectangle(rect.X, rect.Y + cellH + 10, cellW, cellH);
            Rectangle cardT2 = new Rectangle(rect.X + cellW + 10, rect.Y + cellH + 10, cellW, cellH);

            DrawTactCell(g, cardT1, "TIME (CONTACT PERIOD)", $"{_engine.Tact.ElapsedSec:F0}s / {_engine.Tact.TargetDurationSec:F0}s",
                _engine.Tact.IsTimeValid ? "VALIDATED" : "RUNNING",
                _engine.Tact.IsTimeValid ? Color.FromArgb(34, 197, 94) : Color.FromArgb(56, 189, 248), palette);

            DrawTactCell(g, cardA, "ACTION (TURBULENT VELOCITY)", $"{_engine.Tact.FlowVelocityMS:F2} m/s",
                _engine.Tact.IsActionValid ? "TURBULENT (Re > 10K)" : "INSUFFICIENT FLOW",
                _engine.Tact.IsActionValid ? Color.FromArgb(34, 197, 94) : Color.FromArgb(239, 68, 68), palette);

            DrawTactCell(g, cardC, "CHEMICAL (CONDUCTIVITY)", $"{_engine.Tact.ConductivityMsCm:F1} mS/cm",
                _engine.Tact.IsChemicalValid ? "CONCENTRATION OK" : "LOW CONDUCTIVITY",
                _engine.Tact.IsChemicalValid ? Color.FromArgb(34, 197, 94) : Color.FromArgb(239, 68, 68), palette);

            DrawTactCell(g, cardT2, "TEMPERATURE (RETURN LOOP)", $"{_engine.Tact.ReturnTempC:F1}°C (Target {_engine.Tact.TargetTempC:F0}°C)",
                _engine.Tact.IsTemperatureValid ? "TEMP ATTAINED" : "HEATING UP",
                _engine.Tact.IsTemperatureValid ? Color.FromArgb(34, 197, 94) : Color.FromArgb(245, 158, 11), palette);
        }

        private static void DrawTactCell(Graphics g, Rectangle rect, string title, string value, string status, Color statusColor, ZeroThemePalette palette)
        {
            using (var fontTitle = new Font("Segoe UI", 7.5f, FontStyle.Bold))
            using (var fontVal = new Font("Segoe UI", 10.5f, FontStyle.Bold))
            using (var fontSub = new Font("Segoe UI", 7.5f))
            using (var bgBrush = new SolidBrush(Color.FromArgb(25, palette.Border)))
            using (var borderPen = new Pen(palette.Border, 1f))
            {
                g.FillRectangle(bgBrush, rect);
                g.DrawRectangle(borderPen, rect);

                Rectangle titleRect = new Rectangle(rect.X + 8, rect.Y + 6, Math.Max(20, rect.Width - 16), 16);
                TextRenderer.DrawText(g, title, fontTitle, titleRect, palette.TextSecondary,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                Rectangle valRect = new Rectangle(rect.X + 8, rect.Y + 24, Math.Max(20, rect.Width - 16), 22);
                TextRenderer.DrawText(g, value, fontVal, valRect, palette.TextPrimary,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                Rectangle tagRect = new Rectangle(rect.X + 8, rect.Bottom - 22, Math.Max(20, rect.Width - 16), 16);
                TextRenderer.DrawText(g, $"● {status}", fontSub, tagRect, statusColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }

        private void DrawF0Hud(Graphics g, Rectangle rect, ZeroThemePalette palette)
        {
            using (var fontTitle = new Font("Segoe UI", 9f, FontStyle.Bold))
            using (var fontLabel = new Font("Segoe UI", 8f))
            using (var fontValue = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            {
                PaintHelper.DrawCardBox(g, rect, "STERILITY ASSURANCE (F₀)", fontTitle, palette);

                int pad = 12;
                int innerW = rect.Width - pad * 2;
                int rowY = rect.Y + 34;
                int rowH = 24;

                // F0 Accumulated
                double f0 = _engine.AccumulatedF0Minutes;
                Color f0Color = f0 >= _engine.TargetF0Minutes ? Color.FromArgb(34, 197, 94) : Color.FromArgb(56, 189, 248);
                PaintHelper.DrawDataRow(g, new Rectangle(rect.X + pad, rowY, innerW, rowH),
                    "Accumulated F₀", $"{f0:F2} min",
                    palette.TextSecondary, f0Color, fontLabel, fontValue);
                rowY += rowH;

                // Target F0
                PaintHelper.DrawDataRow(g, new Rectangle(rect.X + pad, rowY, innerW, rowH),
                    "Target F₀", $"{_engine.TargetF0Minutes:F1} min (SAL 10⁻⁶)",
                    palette.TextSecondary, palette.TextPrimary, fontLabel, fontValue);
                rowY += rowH;

                // Steam Pressure
                PaintHelper.DrawDataRow(g, new Rectangle(rect.X + pad, rowY, innerW, rowH),
                    "Steam Pressure", $"{_engine.SteamPressureBar:F1} bar gauge",
                    palette.TextSecondary, palette.TextPrimary, fontLabel, fontValue);
                rowY += rowH;

                // Instant Lethality Rate
                double rate = CipValidationEngine.CalculateLethalityRate(_engine.Tact.ReturnTempC);
                PaintHelper.DrawDataRow(g, new Rectangle(rect.X + pad, rowY, innerW, rowH),
                    "Lethality Rate L", $"{rate:F2} min/min",
                    palette.TextSecondary, rate >= 1.0 ? Color.FromArgb(34, 197, 94) : Color.FromArgb(148, 163, 184), fontLabel, fontValue);
                rowY += rowH + 8;

                // F0 Progress Bar
                double f0Frac = Math.Max(0.0, Math.Min(1.0, f0 / _engine.TargetF0Minutes));
                Rectangle f0Track = new Rectangle(rect.X + pad, rowY, innerW, 10);
                using (var trackBrush = new SolidBrush(Color.FromArgb(40, palette.Border)))
                {
                    g.FillRectangle(trackBrush, f0Track);
                }

                Rectangle f0Fill = new Rectangle(f0Track.X, f0Track.Y, (int)(f0Track.Width * f0Frac), f0Track.Height);
                using (var fillBrush = new SolidBrush(f0Color))
                {
                    g.FillRectangle(fillBrush, f0Fill);
                }
            }
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZCipValidationMatrix"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("CipValidationMatrix is deprecated and will be removed in 5 release cycles. Please migrate to ZCipValidationMatrix instead.")]
    [ToolboxItem(false)]
    public class CipValidationMatrix : ZCipValidationMatrix
    {
    }

    #endregion
}
