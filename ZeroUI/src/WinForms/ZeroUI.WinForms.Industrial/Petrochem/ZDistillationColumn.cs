using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Petrochem;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Petrochem
{
    /// <summary>
    /// Multi-tray fractional distillation column visualizer for WinForms.
    /// Visualizes vertical pressure vessel shell, tray-by-tray temperature/pressure gradients,
    /// overhead condenser &amp; reflux drum balance, thermosiphon reboiler loop, and vapor flooding/weeping risk.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Oil & Gas")]
    [Description("Multi-tray fractional distillation column with reflux balance and hydraulic flood diagnostics")]
    public class ZDistillationColumn : VisualControlBase
    {
        private readonly DistillationEngine _engine = new DistillationEngine();
        private double _animationPhase;

        protected override bool AutoAnimate => true;

        public ZDistillationColumn()
        {
            Size = new Size(820, 480);
        }

        #region Public Properties

        [Category("Distillation")]
        [Description("Access to the underlying pure distillation computation engine")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public DistillationEngine Engine => _engine;

        [Category("Distillation")]
        [DefaultValue("T-101")]
        public string ColumnTag
        {
            get => _engine.ColumnTag;
            set
            {
                _engine.ColumnTag = value ?? "T-101";
                Invalidate();
            }
        }

        [Category("Distillation")]
        [DefaultValue(2.45)]
        public double RefluxRatio
        {
            get => _engine.RefluxRatio;
            set
            {
                _engine.RefluxRatio = Math.Max(0.1, value);
                Invalidate();
            }
        }

        [Category("Distillation")]
        [DefaultValue(168.0)]
        public double BottomsTempC
        {
            get => _engine.BottomsTempC;
            set
            {
                _engine.BottomsTempC = value;
                _engine.RecomputeTrays();
                Invalidate();
            }
        }

        [Category("Distillation")]
        [DefaultValue(42.0)]
        public double OverheadTempC
        {
            get => _engine.OverheadTempC;
            set
            {
                _engine.OverheadTempC = value;
                _engine.RecomputeTrays();
                Invalidate();
            }
        }

        [Category("Distillation")]
        [DefaultValue(1.45)]
        public double VaporVelocityMs
        {
            get => _engine.ActualVaporVelocityMs;
            set
            {
                _engine.ActualVaporVelocityMs = Math.Max(0.0, value);
                _engine.RecomputeTrays();
                Invalidate();
            }
        }

        #endregion

        protected override void OnAnimationTick(double delta, long frame)
        {
            _animationPhase = (_animationPhase + delta * 2.5) % (Math.PI * 2.0);
        }

        protected override void OnDrawVisual(Graphics g, Rectangle bounds, ZeroThemePalette palette)
        {
            // 1. Header
            int headerH = DrawHeader(g, bounds, palette);

            int mainY = bounds.Y + headerH;
            int mainH = bounds.Height - headerH - 12;

            if (bounds.Width < 540)
            {
                int colW = Math.Max(110, (int)(bounds.Width * 0.42));
                var colRect = new Rectangle(bounds.X + 12, mainY, colW, mainH);
                var hudRect = new Rectangle(colRect.Right + 10, mainY, bounds.Right - colRect.Right - 22, mainH);

                DrawColumnSchematic(g, colRect, palette);
                DrawProcessTelemetry(g, hudRect, palette);
            }
            else
            {
                int colW = Math.Min(280, (bounds.Width - 32) / 2);
                var colRect = new Rectangle(bounds.X + 16, mainY, colW, mainH);
                var hudRect = new Rectangle(colRect.Right + 16, mainY, bounds.Right - colRect.Right - 32, mainH);

                DrawColumnSchematic(g, colRect, palette);
                DrawProcessTelemetry(g, hudRect, palette);
            }
        }

        private int DrawHeader(Graphics g, Rectangle bounds, ZeroThemePalette palette)
        {
            string title = $"{_engine.ColumnTag} — {_engine.ServiceDescription.ToUpperInvariant()}";
            using (var font = new Font("Segoe UI", 10.5f, FontStyle.Bold))
            using (var subFont = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            {
                Size titleSize = TextRenderer.MeasureText(g, title, font);

                // Operating State Badge
                string badgeText = _engine.OperatingState.ToString().ToUpperInvariant();
                Color badgeBg, badgeFg;
                switch (_engine.OperatingState)
                {
                    case ColumnOperatingState.Normal:
                        badgeBg = palette.Success;
                        badgeFg = Color.White;
                        break;
                    case ColumnOperatingState.FloodingRisk:
                        badgeBg = palette.Danger;
                        badgeFg = Color.White;
                        break;
                    case ColumnOperatingState.WeepingRisk:
                        badgeBg = palette.Warning;
                        badgeFg = Color.Black;
                        break;
                    default:
                        badgeBg = palette.Border;
                        badgeFg = palette.TextSecondary;
                        break;
                }

                int badgeW = 140;
                int badgeH = 26;
                bool isWide = bounds.Width - badgeW - 20 >= 16 + titleSize.Width + 16;

                if (isWide)
                {
                    TextRenderer.DrawText(g, title, font, new Point(bounds.X + 16, bounds.Y + 12), palette.TextPrimary);
                    PaintHelper.DrawStatusBadge(g, new Rectangle(bounds.Right - badgeW - 16, bounds.Y + 10, badgeW, badgeH), badgeText, subFont, badgeBg, badgeFg);
                    return 48;
                }
                else
                {
                    Rectangle titleRect = new Rectangle(bounds.X + 16, bounds.Y + 8, bounds.Width - 32, 22);
                    TextRenderer.DrawText(g, title, font, titleRect, palette.TextPrimary, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                    Rectangle badgeRect = new Rectangle(bounds.X + 16, bounds.Y + 34, Math.Min(badgeW, bounds.Width - 32), badgeH);
                    PaintHelper.DrawStatusBadge(g, badgeRect, badgeText, subFont, badgeBg, badgeFg);
                    return 66;
                }
            }
        }

        private void DrawColumnSchematic(Graphics g, Rectangle rect, ZeroThemePalette palette)
        {
            using (var titleFont = new Font("Segoe UI", 9f, FontStyle.Bold))
            {
                PaintHelper.DrawCardBox(g, rect, "COLUMN PROFILE", titleFont, palette);
            }

            // Tower dimensions
            int towerW = Math.Max(70, rect.Width / 3);
            int towerX = rect.X + 40;
            int towerY = rect.Y + 36;
            int towerH = rect.Height - 72;

            // Draw Column Vessel
            using (var pen = new Pen(palette.TextSecondary, 2f))
            using (var fillBrush = new SolidBrush(Color.FromArgb(28, palette.Border)))
            {
                // Top Dome
                g.FillPie(fillBrush, towerX, towerY - 16, towerW, 32, 180, 180);
                g.DrawArc(pen, towerX, towerY - 16, towerW, 32, 180, 180);

                // Body
                g.FillRectangle(fillBrush, towerX, towerY, towerW, towerH);
                g.DrawLine(pen, towerX, towerY, towerX, towerY + towerH);
                g.DrawLine(pen, towerX + towerW, towerY, towerX + towerW, towerY + towerH);

                // Bottom Dome
                g.FillPie(fillBrush, towerX, towerY + towerH - 16, towerW, 32, 0, 180);
                g.DrawArc(pen, towerX, towerY + towerH - 16, towerW, 32, 0, 180);
            }

            // Sump Liquid Holdup
            double sumpPct = Math.Max(0.0, Math.Min(100.0, _engine.SumpLevelPct));
            int sumpHeight = (int)((towerH * 0.15) * (sumpPct / 100.0));
            int sumpY = towerY + towerH - sumpHeight;
            using (var sumpBrush = new SolidBrush(Color.FromArgb(140, 180, 75, 20)))
            {
                g.FillRectangle(sumpBrush, towerX + 1, sumpY, towerW - 2, sumpHeight + 10);
            }

            // Trays
            int trayCount = _engine.Trays.Count;
            if (trayCount > 0)
            {
                float traySpacing = (float)(towerH - 30) / (trayCount + 1);
                for (int i = 0; i < trayCount; i++)
                {
                    var tray = _engine.Trays[i];
                    float y = towerY + towerH - 20 - (tray.TrayIndex * traySpacing);

                    // Color gradient: Maroon/Red at bottom -> Orange/Yellow mid -> Cyan/Blue top
                    double factor = (double)(tray.TrayIndex - 1) / Math.Max(1, trayCount - 1);
                    Color trayColor = LerpRefineryTemp(factor);

                    using (var trayPen = new Pen(trayColor, 1.5f))
                    {
                        g.DrawLine(trayPen, towerX + 4, y, towerX + towerW - 4, y);
                    }

                    // Feed tray marker
                    if (tray.IsFeedTray)
                    {
                        using (var feedPen = new Pen(palette.Primary, 2f))
                        using (var feedBrush = new SolidBrush(palette.Primary))
                        {
                            g.DrawLine(feedPen, rect.X + 8, y, towerX, y);
                            g.FillPolygon(feedBrush, new[]
                            {
                                new PointF(towerX, y),
                                new PointF(towerX - 6, y - 4),
                                new PointF(towerX - 6, y + 4)
                            });
                        }
                    }
                }
            }

            // Overhead System (Top vapor pipe -> Condenser -> Reflux Drum)
            int pipeX = towerX + towerW / 2;
            int pipeTopY = towerY - 24;
            int condX = towerX + towerW + 28;
            int condY = towerY - 14;

            using (var pipePen = new Pen(palette.Border, 1.5f))
            {
                // Top vapor line
                g.DrawLine(pipePen, pipeX, towerY - 16, pipeX, pipeTopY);
                g.DrawLine(pipePen, pipeX, pipeTopY, condX + 18, pipeTopY);
                g.DrawLine(pipePen, condX + 18, pipeTopY, condX + 18, condY);

                // Condenser Fin Box
                using (var finBrush = new SolidBrush(Color.FromArgb(40, 56, 189, 248)))
                using (var finPen = new Pen(Color.FromArgb(56, 189, 248), 1.5f))
                {
                    g.FillRectangle(finBrush, condX, condY, 36, 22);
                    g.DrawRectangle(finPen, condX, condY, 36, 22);
                }

                // Reflux line down back to top tray
                g.DrawLine(pipePen, condX + 18, condY + 22, condX + 18, towerY + 8);
                g.DrawLine(pipePen, condX + 18, towerY + 8, towerX + towerW, towerY + 8);
            }

            // Animated Vapor Bubbles inside column
            using (var bubBrush = new SolidBrush(Color.FromArgb(160, 255, 255, 255)))
            {
                for (int b = 0; b < 6; b++)
                {
                    double bPhase = (_animationPhase + b * 1.05) % (Math.PI * 2.0);
                    float by = (float)(towerY + towerH - 24 - (bPhase / (Math.PI * 2.0)) * (towerH - 40));
                    float bx = towerX + 12 + (float)(Math.Sin(bPhase * 2.0 + b) * 16.0) + (b * 6) % (towerW - 24);
                    g.FillEllipse(bubBrush, bx, by, 3, 3);
                }
            }

            // Bottom Reboiler Loop
            int rebX = towerX + towerW + 20;
            int rebY = towerY + towerH - 46;
            using (var rebPen = new Pen(palette.Border, 1.5f))
            {
                // Bottoms pipe out
                g.DrawLine(rebPen, towerX + towerW / 2, towerY + towerH + 16, towerX + towerW / 2, towerY + towerH + 26);
                g.DrawLine(rebPen, towerX + towerW / 2, towerY + towerH + 26, rebX + 16, towerY + towerH + 26);
                g.DrawLine(rebPen, rebX + 16, towerY + towerH + 26, rebX + 16, rebY + 28);

                // Reboiler Kettle
                using (var rebBrush = new SolidBrush(Color.FromArgb(45, 239, 68, 68)))
                using (var coilPen = new Pen(Color.FromArgb(239, 68, 68), 1.5f))
                {
                    g.FillRectangle(rebBrush, rebX, rebY, 34, 28);
                    g.DrawRectangle(coilPen, rebX, rebY, 34, 28);
                }

                // Vapor return pipe
                g.DrawLine(rebPen, rebX + 16, rebY, rebX + 16, towerY + towerH - 18);
                g.DrawLine(rebPen, rebX + 16, towerY + towerH - 18, towerX + towerW, towerY + towerH - 18);
            }
        }

        private static Color LerpRefineryTemp(double factor)
        {
            if (factor <= 0.5)
            {
                double t = factor * 2.0;
                return Color.FromArgb(
                    (int)(239 - t * (239 - 245)),
                    (int)(68 + t * (158 - 68)),
                    (int)(68 - t * (68 - 11))
                );
            }
            else
            {
                double t = (factor - 0.5) * 2.0;
                return Color.FromArgb(
                    (int)(245 - t * (245 - 56)),
                    (int)(158 + t * (189 - 158)),
                    (int)(11 + t * (248 - 11))
                );
            }
        }

        private void DrawProcessTelemetry(Graphics g, Rectangle rect, ZeroThemePalette palette)
        {
            if (rect.Width < 50 || rect.Height < 50) return;

            using (var titleFont = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            using (var labelFont = new Font("Segoe UI", 8f))
            using (var valFont = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            {
                if (rect.Width < 280)
                {
                    // Narrow mode: 2 primary cards stacked vertically to maintain full label readability
                    int cardH = (rect.Height - 10) / 2;
                    var card1 = new Rectangle(rect.X, rect.Y, rect.Width, cardH);
                    var card2 = new Rectangle(rect.X, card1.Bottom + 10, rect.Width, rect.Bottom - card1.Bottom - 10);

                    // Card 1: Pressure & Hydraulic Gradient
                    PaintHelper.DrawCardBox(g, card1, "HYDRAULIC GRADIENT", titleFont, palette);
                    int rowY1 = card1.Y + 30;
                    int rowH = Math.Max(18, (card1.Height - 36) / 4);
                    PaintHelper.DrawDataRow(g, new Rectangle(card1.X + 10, rowY1, card1.Width - 20, rowH), "Diff Press (ΔP)", $"{_engine.DifferentialPressureKpa:F1} kPa", palette.TextSecondary, palette.TextPrimary, labelFont, valFont);
                    PaintHelper.DrawDataRow(g, new Rectangle(card1.X + 10, rowY1 + rowH, card1.Width - 20, rowH), "Top Press (Pt)", $"{_engine.TopPressureKpa:F1} kPa", palette.TextSecondary, palette.TextPrimary, labelFont, valFont);
                    PaintHelper.DrawDataRow(g, new Rectangle(card1.X + 10, rowY1 + rowH * 2, card1.Width - 20, rowH), "Bottom Press (Pb)", $"{_engine.BottomPressureKpa:F1} kPa", palette.TextSecondary, palette.TextPrimary, labelFont, valFont);
                    Color floodColor = _engine.FloodMarginPct >= 85.0 ? palette.Danger : palette.Success;
                    PaintHelper.DrawDataRow(g, new Rectangle(card1.X + 10, rowY1 + rowH * 3, card1.Width - 20, rowH), "Flood Margin", $"{_engine.FloodMarginPct:F1}%", palette.TextSecondary, floodColor, labelFont, valFont);

                    // Card 2: Thermal Profile & Fractionation
                    PaintHelper.DrawCardBox(g, card2, "THERMAL & FRACTIONATION", titleFont, palette);
                    int rowY2 = card2.Y + 30;
                    PaintHelper.DrawDataRow(g, new Rectangle(card2.X + 10, rowY2, card2.Width - 20, rowH), "Overhead Temp", $"{_engine.OverheadTempC:F1} °C", palette.TextSecondary, Color.FromArgb(56, 189, 248), labelFont, valFont);
                    PaintHelper.DrawDataRow(g, new Rectangle(card2.X + 10, rowY2 + rowH, card2.Width - 20, rowH), "Bottoms Temp", $"{_engine.BottomsTempC:F1} °C", palette.TextSecondary, Color.FromArgb(239, 68, 68), labelFont, valFont);
                    PaintHelper.DrawDataRow(g, new Rectangle(card2.X + 10, rowY2 + rowH * 2, card2.Width - 20, rowH), "Reflux Ratio", $"{_engine.RefluxRatio:F2}", palette.TextSecondary, palette.Primary, labelFont, valFont);
                    PaintHelper.DrawDataRow(g, new Rectangle(card2.X + 10, rowY2 + rowH * 3, card2.Width - 20, rowH), "Sump Level", $"{_engine.SumpLevelPct:F1}%", palette.TextSecondary, palette.TextPrimary, labelFont, valFont);
                }
                else
                {
                    int cardW = (rect.Width - 12) / 2;
                    int cardH = (rect.Height - 12) / 2;

                    // Card 1: Pressure & Hydraulic Gradient
                    var card1 = new Rectangle(rect.X, rect.Y, cardW, cardH);
                    PaintHelper.DrawCardBox(g, card1, "HYDRAULIC GRADIENT", titleFont, palette);
                    int rowY1 = card1.Y + 32;
                    int rowH = 22;
                    PaintHelper.DrawDataRow(g, new Rectangle(card1.X + 12, rowY1, card1.Width - 24, rowH), "Differential Pressure (ΔP)", $"{_engine.DifferentialPressureKpa:F1} kPa", palette.TextSecondary, palette.TextPrimary, labelFont, valFont);
                    PaintHelper.DrawDataRow(g, new Rectangle(card1.X + 12, rowY1 + rowH, card1.Width - 24, rowH), "Top Pressure (Pt)", $"{_engine.TopPressureKpa:F1} kPa", palette.TextSecondary, palette.TextPrimary, labelFont, valFont);
                    PaintHelper.DrawDataRow(g, new Rectangle(card1.X + 12, rowY1 + rowH * 2, card1.Width - 24, rowH), "Bottom Pressure (Pb)", $"{_engine.BottomPressureKpa:F1} kPa", palette.TextSecondary, palette.TextPrimary, labelFont, valFont);
                    Color floodColor = _engine.FloodMarginPct >= 85.0 ? palette.Danger : palette.Success;
                    PaintHelper.DrawDataRow(g, new Rectangle(card1.X + 12, rowY1 + rowH * 3, card1.Width - 24, rowH), "Flood Margin Index", $"{_engine.FloodMarginPct:F1}%", palette.TextSecondary, floodColor, labelFont, valFont);

                    // Card 2: Thermal Profile & Heat Balance
                    var card2 = new Rectangle(card1.Right + 12, rect.Y, cardW, cardH);
                    PaintHelper.DrawCardBox(g, card2, "THERMAL BALANCE", titleFont, palette);
                    int rowY2 = card2.Y + 32;
                    PaintHelper.DrawDataRow(g, new Rectangle(card2.X + 12, rowY2, card2.Width - 24, rowH), "Overhead Temp (To)", $"{_engine.OverheadTempC:F1} °C", palette.TextSecondary, Color.FromArgb(56, 189, 248), labelFont, valFont);
                    PaintHelper.DrawDataRow(g, new Rectangle(card2.X + 12, rowY2 + rowH, card2.Width - 24, rowH), "Bottoms Temp (Tb)", $"{_engine.BottomsTempC:F1} °C", palette.TextSecondary, Color.FromArgb(239, 68, 68), labelFont, valFont);
                    PaintHelper.DrawDataRow(g, new Rectangle(card2.X + 12, rowY2 + rowH * 2, card2.Width - 24, rowH), "Reboiler Duty (Qr)", $"{_engine.ReboilerDutyKw:F0} kW", palette.TextSecondary, palette.TextPrimary, labelFont, valFont);
                    PaintHelper.DrawDataRow(g, new Rectangle(card2.X + 12, rowY2 + rowH * 3, card2.Width - 24, rowH), "Condenser Duty (Qc)", $"{_engine.CondenserDutyKw:F0} kW", palette.TextSecondary, palette.TextPrimary, labelFont, valFont);

                    // Card 3: Mass Flow & Distillate Reflux
                    var card3 = new Rectangle(rect.X, card1.Bottom + 12, cardW, cardH);
                    PaintHelper.DrawCardBox(g, card3, "FRACTIONATION FLOWS", titleFont, palette);
                    int rowY3 = card3.Y + 32;
                    PaintHelper.DrawDataRow(g, new Rectangle(card3.X + 12, rowY3, card3.Width - 24, rowH), "Reflux Ratio (L/D)", $"{_engine.RefluxRatio:F2}", palette.TextSecondary, palette.Primary, labelFont, valFont);
                    PaintHelper.DrawDataRow(g, new Rectangle(card3.X + 12, rowY3 + rowH, card3.Width - 24, rowH), "Reflux Flow Rate", $"{_engine.RefluxFlowRateM3H:F1} m³/h", palette.TextSecondary, palette.TextPrimary, labelFont, valFont);
                    PaintHelper.DrawDataRow(g, new Rectangle(card3.X + 12, rowY3 + rowH * 2, card3.Width - 24, rowH), "Distillate Draw", $"{_engine.DistillateFlowRateM3H:F1} m³/h", palette.TextSecondary, palette.TextPrimary, labelFont, valFont);
                    PaintHelper.DrawDataRow(g, new Rectangle(card3.X + 12, rowY3 + rowH * 3, card3.Width - 24, rowH), "Feed Rate", $"{_engine.FeedFlowRateKgH:F0} kg/h", palette.TextSecondary, palette.TextPrimary, labelFont, valFont);

                    // Card 4: Sump & Vessel Level Telemetry
                    var card4 = new Rectangle(card3.Right + 12, card1.Bottom + 12, cardW, cardH);
                    PaintHelper.DrawCardBox(g, card4, "VESSEL INVENTORY", titleFont, palette);
                    int rowY4 = card4.Y + 32;
                    PaintHelper.DrawDataRow(g, new Rectangle(card4.X + 12, rowY4, card4.Width - 24, rowH), "Sump Liquid Level", $"{_engine.SumpLevelPct:F1}%", palette.TextSecondary, palette.TextPrimary, labelFont, valFont);
                    PaintHelper.DrawDataRow(g, new Rectangle(card4.X + 12, rowY4 + rowH, card4.Width - 24, rowH), "Reflux Drum Level", $"{_engine.RefluxDrumLevelPct:F1}%", palette.TextSecondary, palette.TextPrimary, labelFont, valFont);
                    PaintHelper.DrawDataRow(g, new Rectangle(card4.X + 12, rowY4 + rowH * 2, card4.Width - 24, rowH), "Bottoms Takeoff", $"{_engine.BottomsFlowRateM3H:F1} m³/h", palette.TextSecondary, palette.TextPrimary, labelFont, valFont);
                    PaintHelper.DrawDataRow(g, new Rectangle(card4.X + 12, rowY4 + rowH * 3, card4.Width - 24, rowH), "Active Tray Count", $"{_engine.TrayCount} Trays", palette.TextSecondary, palette.TextPrimary, labelFont, valFont);
                }
            }
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZDistillationColumn"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("DistillationColumn is deprecated and will be removed in 5 release cycles. Please migrate to ZDistillationColumn instead.")]
    [ToolboxItem(false)]
    public class DistillationColumn : ZDistillationColumn
    {
    }

    #endregion
}
