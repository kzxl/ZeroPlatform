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
    /// Sanitary Fermentation Bioreactor Vessel visualizer for WinForms.
    /// Visualizes stainless steel dished vessel geometry, thermal jacket, rotating Rushton turbine impellers,
    /// ascending sparging aeration micro-bubbles, and critical DO / pH / Temp process telemetry.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Process & Life Sciences")]
    [Description("Sanitary bioreactor vessel with animated rotating impellers and biological process HUD")]
    public class ZBioreactorVessel : VisualControlBase
    {
        private readonly BioreactorEngine _engine = new BioreactorEngine();

        protected override bool AutoAnimate => true;

        public ZBioreactorVessel()
        {
            Size = new Size(720, 380);
        }

        #region Public Properties

        [Category("Bioreactor")]
        [Description("Access to the underlying pure bioreactor computation engine")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public BioreactorEngine Engine => _engine;

        [Category("Bioreactor")]
        [DefaultValue("BR-301")]
        public string VesselTag
        {
            get => _engine.VesselTag;
            set
            {
                _engine.VesselTag = value ?? "BR-01";
                Invalidate();
            }
        }

        [Category("Bioreactor")]
        [DefaultValue(420.0)]
        public double AgitationRpm
        {
            get => _engine.AgitationRpm;
            set
            {
                _engine.AgitationRpm = Math.Max(0.0, Math.Min(1200.0, value));
                Invalidate();
            }
        }

        [Category("Bioreactor")]
        [DefaultValue(45.0)]
        public double DissolvedOxygenPct
        {
            get => _engine.DissolvedOxygenPct;
            set
            {
                _engine.DissolvedOxygenPct = Math.Max(0.0, Math.Min(100.0, value));
                Invalidate();
            }
        }

        [Category("Bioreactor")]
        [DefaultValue(7.02)]
        public double PhValue
        {
            get => _engine.PhValue;
            set
            {
                _engine.PhValue = Math.Max(0.0, Math.Min(14.0, value));
                Invalidate();
            }
        }

        [Category("Bioreactor")]
        [DefaultValue(37.0)]
        public double VesselTempC
        {
            get => _engine.VesselTempC;
            set
            {
                _engine.VesselTempC = value;
                Invalidate();
            }
        }

        #endregion

        protected override void OnAnimationTick(double delta, long frame)
        {
            _engine.AdvanceSimulation(delta);
        }

        protected override void OnDrawVisual(Graphics g, Rectangle bounds, ZeroThemePalette palette)
        {
            // 1. Header
            int headerH = DrawHeader(g, bounds, palette);

            int mainTop = bounds.Y + headerH;
            int mainHeight = bounds.Height - headerH - 12;

            if (bounds.Width < 180 || mainHeight < 80) return;

            int hudW = bounds.Width < 520 ? Math.Max(140, (int)((bounds.Width - 44) * 0.50f)) : 220;
            int vesselW = bounds.Width - hudW - 44;

            Rectangle vesselRect = new Rectangle(bounds.X + 16, mainTop, vesselW, mainHeight);
            Rectangle hudRect = new Rectangle(vesselRect.Right + 12, mainTop, hudW, mainHeight);

            // 2. Bioreactor Vessel Cutaway
            DrawVessel(g, vesselRect, palette);

            // 3. Process HUD Card
            DrawProcessHud(g, hudRect, palette);
        }

        private int DrawHeader(Graphics g, Rectangle bounds, ZeroThemePalette palette)
        {
            using (var fontTitle = new Font("Segoe UI", 10.5f, FontStyle.Bold))
            using (var fontSub = new Font("Segoe UI", 8.5f))
            {
                string title = $"{_engine.VesselTag} — 500L Single-Use Bioreactor ({_engine.Phase})";
                Size titleSize = TextRenderer.MeasureText(g, title, fontTitle);

                Color badgeColor = _engine.AlarmStatus switch
                {
                    BioreactorAlarmStatus.OutOfSpecAlarm => Color.FromArgb(239, 68, 68),
                    BioreactorAlarmStatus.Warning => Color.FromArgb(245, 158, 11),
                    _ => Color.FromArgb(34, 197, 94)
                };

                int badgeW = 144;
                int badgeH = 24;

                if (bounds.Width - badgeW - 20 >= 16 + titleSize.Width + 16)
                {
                    TextRenderer.DrawText(g, title, fontTitle, new Point(bounds.X + 16, bounds.Y + 12), palette.TextPrimary);

                    Rectangle badgeRect = new Rectangle(bounds.Right - badgeW - 16, bounds.Y + 10, badgeW, badgeH);
                    using (var brush = new SolidBrush(Color.FromArgb(30, badgeColor)))
                    using (var pen = new Pen(badgeColor, 1.2f))
                    {
                        g.FillRectangle(brush, badgeRect);
                        g.DrawRectangle(pen, badgeRect);
                    }
                    TextRenderer.DrawText(g, _engine.AlarmStatus.ToString().ToUpperInvariant(), fontSub, badgeRect, badgeColor,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    return 46;
                }
                else
                {
                    Rectangle titleRect = new Rectangle(bounds.X + 16, bounds.Y + 8, bounds.Width - 32, 22);
                    TextRenderer.DrawText(g, title, fontTitle, titleRect, palette.TextPrimary,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                    Rectangle badgeRect = new Rectangle(bounds.X + 16, bounds.Y + 32, Math.Min(badgeW, bounds.Width - 32), badgeH);
                    using (var brush = new SolidBrush(Color.FromArgb(30, badgeColor)))
                    using (var pen = new Pen(badgeColor, 1.2f))
                    {
                        g.FillRectangle(brush, badgeRect);
                        g.DrawRectangle(pen, badgeRect);
                    }
                    TextRenderer.DrawText(g, _engine.AlarmStatus.ToString().ToUpperInvariant(), fontSub, badgeRect, badgeColor,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                    return 64;
                }
            }
        }

        private void DrawVessel(Graphics g, Rectangle rect, ZeroThemePalette palette)
        {
            int tankW = Math.Min(220, rect.Width - 40);
            int tankH = rect.Height - 40;
            int tankX = rect.X + (rect.Width - tankW) / 2;
            int tankY = rect.Y + 30;

            Rectangle tankBox = new Rectangle(tankX, tankY, tankW, tankH);

            // 1. Thermal Jacket Outer Annulus
            Rectangle jacketBox = new Rectangle(tankBox.X - 8, tankBox.Y + 20, tankBox.Width + 16, tankBox.Height - 40);
            using (var jacketBrush = new SolidBrush(Color.FromArgb(30, 249, 115, 22)))
            using (var jacketPen = new Pen(Color.FromArgb(249, 115, 22), 1.2f) { DashStyle = DashStyle.Dash })
            {
                g.FillRectangle(jacketBrush, jacketBox);
                g.DrawRectangle(jacketPen, jacketBox);
            }

            // 2. Stainless Steel Dished Vessel Body
            using (var tankBrush = new SolidBrush(Color.FromArgb(25, palette.Border)))
            using (var tankPen = new Pen(Color.FromArgb(148, 163, 184), 2.5f))
            {
                g.FillRectangle(tankBrush, tankBox);
                g.DrawRectangle(tankPen, tankBox);
            }

            // 3. Liquid Broth Culture Fill
            double fillFrac = Math.Max(0.0, Math.Min(1.0, _engine.VolumeFillPct / 100.0));
            int fillH = (int)(tankBox.Height * fillFrac);
            Rectangle fillBox = new Rectangle(tankBox.X + 2, tankBox.Bottom - fillH, tankBox.Width - 4, fillH);

            if (fillH > 0)
            {
                using (var liquidBrush = new LinearGradientBrush(
                    new Point(fillBox.X, fillBox.Y),
                    new Point(fillBox.Right, fillBox.Y),
                    Color.FromArgb(180, 234, 179, 8),  // Golden cell broth
                    Color.FromArgb(220, 202, 138, 4)))
                {
                    g.FillRectangle(liquidBrush, fillBox);
                }
            }

            // 4. Center Agitator Drive & Shaft
            int midX = tankBox.X + tankBox.Width / 2;
            using (var motorBrush = new SolidBrush(Color.FromArgb(100, 116, 139)))
            using (var shaftPen = new Pen(Color.FromArgb(203, 213, 225), 3.5f))
            {
                // Top Motor
                g.FillRectangle(motorBrush, midX - 16, tankBox.Top - 18, 32, 18);
                // Center Shaft
                g.DrawLine(shaftPen, midX, tankBox.Top, midX, tankBox.Bottom - 16);
            }

            // 5. Rotating Rushton Turbine Impellers (Upper & Lower)
            double rad = _engine.ImpellerAngleDeg * Math.PI / 180.0;
            float cos = (float)Math.Cos(rad);
            float impW = 32f * Math.Abs(cos) + 8f; // Visual foreshortening

            DrawImpeller(g, midX, tankBox.Bottom - (int)(fillH * 0.3), impW);
            DrawImpeller(g, midX, tankBox.Bottom - (int)(fillH * 0.7), impW);

            // 6. Aeration Sparger & Micro-Bubbles
            if (_engine.SpargingAirLpm > 0)
            {
                int spargeY = tankBox.Bottom - 18;
                using (var bubbleBrush = new SolidBrush(Color.FromArgb(200, 255, 255, 255)))
                {
                    for (int b = -20; b <= 20; b += 10)
                    {
                        g.FillEllipse(bubbleBrush, midX + b, spargeY - 14, 3, 3);
                        g.FillEllipse(bubbleBrush, midX + b + 4, spargeY - 28, 2, 2);
                    }
                }
            }
        }

        private static void DrawImpeller(Graphics g, int cx, int cy, float width)
        {
            using (var impBrush = new SolidBrush(Color.FromArgb(250, 204, 21)))
            using (var impPen = new Pen(Color.FromArgb(161, 98, 7), 1f))
            {
                RectangleF impRect = new RectangleF(cx - width, cy - 4, width * 2f, 8);
                g.FillRectangle(impBrush, impRect);
                g.DrawRectangle(impPen, impRect.X, impRect.Y, impRect.Width, impRect.Height);
            }
        }

        private void DrawProcessHud(Graphics g, Rectangle rect, ZeroThemePalette palette)
        {
            using (var fontTitle = new Font("Segoe UI", 9f, FontStyle.Bold))
            using (var fontLabel = new Font("Segoe UI", 8f))
            using (var fontValue = new Font("Segoe UI", 9f, FontStyle.Bold))
            {
                PaintHelper.DrawCardBox(g, rect, "PROCESS TELEMETRY", fontTitle, palette);

                int pad = 12;
                int innerW = rect.Width - pad * 2;
                int rowY = rect.Y + 34;
                int rowH = 24;

                // Agitation RPM
                PaintHelper.DrawDataRow(g, new Rectangle(rect.X + pad, rowY, innerW, rowH),
                    "Agitation", $"{_engine.AgitationRpm:F0} RPM",
                    palette.TextSecondary, Color.FromArgb(56, 189, 248), fontLabel, fontValue);
                rowY += rowH;

                // Dissolved Oxygen (DO %)
                double doVal = _engine.DissolvedOxygenPct;
                Color doCol = doVal >= 35.0 && doVal <= 55.0 ? Color.FromArgb(34, 197, 94) : Color.FromArgb(245, 158, 11);
                PaintHelper.DrawDataRow(g, new Rectangle(rect.X + pad, rowY, innerW, rowH),
                    "Dissolved O₂", $"{doVal:F1}% (Set {_engine.TargetDoPct:F0}%)",
                    palette.TextSecondary, doCol, fontLabel, fontValue);
                rowY += rowH;

                // pH Value
                double ph = _engine.PhValue;
                Color phCol = Math.Abs(ph - _engine.TargetPh) < 0.1 ? Color.FromArgb(34, 197, 94) : Color.FromArgb(245, 158, 11);
                PaintHelper.DrawDataRow(g, new Rectangle(rect.X + pad, rowY, innerW, rowH),
                    "pH Regulation", $"{ph:F2} pH",
                    palette.TextSecondary, phCol, fontLabel, fontValue);
                rowY += rowH;

                // Temperature °C
                double temp = _engine.VesselTempC;
                Color tempCol = Math.Abs(temp - _engine.TargetTempC) < 0.5 ? Color.FromArgb(34, 197, 94) : Color.FromArgb(239, 68, 68);
                PaintHelper.DrawDataRow(g, new Rectangle(rect.X + pad, rowY, innerW, rowH),
                    "Vessel Temp", $"{temp:F1}°C (Jkt {_engine.JacketTempC:F1}°C)",
                    palette.TextSecondary, tempCol, fontLabel, fontValue);
                rowY += rowH;

                // Sparging Flow
                PaintHelper.DrawDataRow(g, new Rectangle(rect.X + pad, rowY, innerW, rowH),
                    "Air / O₂ Sparge", $"{_engine.SpargingAirLpm:F1} / {_engine.SpargingO2Lpm:F1} LPM",
                    palette.TextSecondary, palette.TextPrimary, fontLabel, fontValue);
                rowY += rowH;

                // Mass Transfer kLa
                PaintHelper.DrawDataRow(g, new Rectangle(rect.X + pad, rowY, innerW, rowH),
                    "Oxygen kLa", $"{_engine.EstimatedKLaHr:F0} hr⁻¹",
                    palette.TextSecondary, palette.TextPrimary, fontLabel, fontValue);
                rowY += rowH;

                // Working Volume
                PaintHelper.DrawDataRow(g, new Rectangle(rect.X + pad, rowY, innerW, rowH),
                    "Working Volume", $"{_engine.WorkingVolumeL:N0} L / {_engine.TotalCapacityL:N0} L",
                    palette.TextSecondary, palette.TextPrimary, fontLabel, fontValue);
            }
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZBioreactorVessel"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("BioreactorVessel is deprecated and will be removed in 5 release cycles. Please migrate to ZBioreactorVessel instead.")]
    [ToolboxItem(false)]
    public class BioreactorVessel : ZBioreactorVessel
    {
    }

    #endregion
}
