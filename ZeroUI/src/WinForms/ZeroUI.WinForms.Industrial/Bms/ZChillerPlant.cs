using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Rendering;
using ZeroUI.Core.Bms;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Bms
{
    /// <summary>
    /// Central Chiller Plant schematic visualizer.
    /// Renders water chillers, cooling towers, primary/secondary pumps, and live thermodynamic
    /// efficiency meters (Coefficient of Performance COP and kW/Ton).
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - BMS & HVAC")]
    [Description("Central Chiller Plant visualizer with animated water loops and efficiency gauges")]
    public class ZChillerPlant : VisualControlBase
    {
        private readonly ChillerPlantEngine _engine = new ChillerPlantEngine();
        private string _plantTitle = "Central Utility Plant";

        protected override bool AutoAnimate => true;

        public ZChillerPlant()
        {
            Size = new Size(720, 320);
        }

        #region Public Properties

        [Category("BMS")]
        [Description("Access to the underlying thermodynamic chiller plant computation engine")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ChillerPlantEngine Engine => _engine;

        [Category("BMS")]
        [DefaultValue("Central Utility Plant")]
        public string PlantTitle
        {
            get => _plantTitle;
            set
            {
                _plantTitle = value;
                Invalidate();
            }
        }

        #endregion

        protected override void OnDrawVisual(Graphics g, Rectangle bounds, ZeroThemePalette palette)
        {
            // Top Header & Efficiency HUD
            int headerH = DrawHeaderAndHud(g, bounds, palette);

            int mainTop = bounds.Y + headerH + 8;
            int mainHeight = bounds.Height - headerH - 16;
            if (mainHeight < 80 || bounds.Width < 200)
                return;

            bool isStacked = bounds.Width < 520;
            if (!isStacked)
            {
                int halfWidth = (bounds.Width - 44) / 2;
                if (halfWidth < 160) return;

                Rectangle chillerArea = new Rectangle(bounds.X + 16, mainTop, halfWidth, mainHeight);
                Rectangle towerArea = new Rectangle(bounds.X + 16 + halfWidth + 12, mainTop, halfWidth, mainHeight);

                DrawChillersSection(g, chillerArea, palette);
                DrawTowersSection(g, towerArea, palette);
                DrawInterconnectPipes(g, chillerArea, towerArea, palette);
            }
            else
            {
                int halfHeight = (mainHeight - 10) / 2;
                if (halfHeight < 60) return;

                Rectangle chillerArea = new Rectangle(bounds.X + 16, mainTop, bounds.Width - 32, halfHeight);
                Rectangle towerArea = new Rectangle(bounds.X + 16, mainTop + halfHeight + 10, bounds.Width - 32, halfHeight);

                DrawChillersSection(g, chillerArea, palette);
                DrawTowersSection(g, towerArea, palette);
            }
        }

        private int DrawHeaderAndHud(Graphics g, Rectangle bounds, ZeroThemePalette theme)
        {
            using (var fontTitle = new Font("Segoe UI", 10.5f, FontStyle.Bold))
            using (var fontSmall = new Font("Segoe UI", 8.5f))
            using (var fontBold = new Font("Segoe UI", 9f, FontStyle.Bold))
            {
                Size titleSize = TextRenderer.MeasureText(g, _plantTitle, fontTitle);
                double kwTon = _engine.PlantAverageKwPerTon;
                double cop = _engine.PlantAverageCop;
                Color effColor = kwTon < 0.65 ? Color.FromArgb(34, 197, 94) :
                                 kwTon < 0.80 ? Color.FromArgb(245, 158, 11) :
                                 Color.FromArgb(239, 68, 68);
                string effStr = $"{kwTon:F2} kW/TR | COP {cop:F1}";

                // Wide header (1 line)
                if (bounds.Width >= 560)
                {
                    TextRenderer.DrawText(g, _plantTitle, fontTitle, new Point(bounds.X + 16, bounds.Y + 12), theme.TextPrimary);

                    int hudX = bounds.Right - 420;
                    string loadStr = $"Load: {_engine.TotalActualTons:N0}/{_engine.TotalRatedTons:N0} TR";
                    TextRenderer.DrawText(g, loadStr, fontSmall, new Point(hudX, bounds.Y + 16), theme.TextSecondary);

                    string pwrStr = $"{_engine.TotalPowerKw:N0} kW";
                    TextRenderer.DrawText(g, pwrStr, fontSmall, new Point(hudX + 140, bounds.Y + 16), theme.TextSecondary);

                    Rectangle effBadge = new Rectangle(bounds.Right - 156, bounds.Y + 10, 140, 26);
                    using (var badgeBrush = new SolidBrush(Color.FromArgb(30, effColor)))
                    using (var badgePen = new Pen(effColor, 1f))
                    {
                        g.FillRectangle(badgeBrush, effBadge);
                        g.DrawRectangle(badgePen, effBadge);
                    }
                    TextRenderer.DrawText(g, effStr, fontBold, effBadge, effColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    return 48;
                }
                else
                {
                    // Compact stacked header (2 lines)
                    Rectangle titleRect = new Rectangle(bounds.X + 16, bounds.Y + 8, bounds.Width - 32, 22);
                    TextRenderer.DrawText(g, _plantTitle, fontTitle, titleRect, theme.TextPrimary,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                    string summaryStr = $"Load: {_engine.TotalActualTons:N0} TR | {_engine.TotalPowerKw:N0} kW";
                    TextRenderer.DrawText(g, summaryStr, fontSmall, new Point(bounds.X + 16, bounds.Y + 34), theme.TextSecondary);

                    int badgeW = 140;
                    Rectangle effBadge = new Rectangle(bounds.Right - badgeW - 16, bounds.Y + 30, badgeW, 24);
                    if (effBadge.Left > bounds.X + 160)
                    {
                        using (var badgeBrush = new SolidBrush(Color.FromArgb(30, effColor)))
                        using (var badgePen = new Pen(effColor, 1f))
                        {
                            g.FillRectangle(badgeBrush, effBadge);
                            g.DrawRectangle(badgePen, effBadge);
                        }
                        TextRenderer.DrawText(g, effStr, fontBold, effBadge, effColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                    }
                    return 62;
                }
            }
        }

        private void DrawChillersSection(Graphics g, Rectangle rect, ZeroThemePalette theme)
        {
            using (var casingBrush = new SolidBrush(theme.Surface))
            using (var borderPen = new Pen(theme.Border, 1f))
            using (var fontHeader = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            using (var fontSmall = new Font("Segoe UI", 8f))
            {
                g.FillRectangle(casingBrush, rect);
                g.DrawRectangle(borderPen, rect);

                TextRenderer.DrawText(g, "WATER CHILLERS (CHW CIRCUIT)", fontHeader, new Point(rect.Left + 12, rect.Top + 8), Color.FromArgb(56, 189, 248));

                int chillerCount = _engine.Chillers.Count;
                if (chillerCount == 0) return;

                int itemHeight = (rect.Height - 44) / chillerCount;

                for (int i = 0; i < chillerCount; i++)
                {
                    var ch = _engine.Chillers[i];
                    Rectangle itemRect = new Rectangle(rect.Left + 10, rect.Top + 32 + i * itemHeight, rect.Width - 20, itemHeight - 8);

                    // Chiller Vessel Shell
                    using (var vesselBrush = new SolidBrush(Color.FromArgb(25, 56, 189, 248)))
                    using (var vesselPen = new Pen(ch.Status == ChillerStatus.Running ? Color.FromArgb(56, 189, 248) : theme.Border, 1.5f))
                    {
                        g.FillRectangle(vesselBrush, itemRect);
                        g.DrawRectangle(vesselPen, itemRect);
                    }

                    // Compressor Icon circle
                    int compRadius = 14;
                    int compX = itemRect.Left + 24;
                    int compY = itemRect.Top + itemRect.Height / 2;

                    using (var compBrush = new SolidBrush(ch.Status == ChillerStatus.Running ? Color.FromArgb(34, 197, 94) : Color.FromArgb(100, 116, 139)))
                    {
                        g.FillEllipse(compBrush, compX - compRadius, compY - compRadius, compRadius * 2, compRadius * 2);
                    }

                    // Chiller Name & Status
                    Rectangle nameRect = new Rectangle(itemRect.Left + 48, itemRect.Top + 4, Math.Max(20, itemRect.Width - 54), 18);
                    TextRenderer.DrawText(g, ch.Name, fontHeader, nameRect, theme.TextPrimary,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                    string statusStr = ch.Status == ChillerStatus.Running
                        ? $"{ch.ActualTons:N0} TR ({ch.PowerKw:N0} kW) | {ch.EfficiencyKwPerTon:F2} kW/TR"
                        : "OFFLINE / STANDBY";
                    Rectangle statusRect = new Rectangle(itemRect.Left + 48, itemRect.Top + 22, Math.Max(20, itemRect.Width - 54), 16);
                    TextRenderer.DrawText(g, statusStr, fontSmall, statusRect,
                        ch.Status == ChillerStatus.Running ? theme.TextSecondary : Color.FromArgb(148, 163, 184),
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                    // Temperatures CHWST / CHWRT
                    string tempStr = $"CHW: {ch.ChwSupplyTempC:F1}°C / {ch.ChwReturnTempC:F1}°C (ΔT {ch.ChwDeltaTempC:F1}°C)";
                    Rectangle tempRect = new Rectangle(itemRect.Left + 48, itemRect.Top + 38, Math.Max(20, itemRect.Width - 54), 16);
                    TextRenderer.DrawText(g, tempStr, fontSmall, tempRect, Color.FromArgb(56, 189, 248),
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                }
            }
        }

        private void DrawTowersSection(Graphics g, Rectangle rect, ZeroThemePalette theme)
        {
            using (var casingBrush = new SolidBrush(theme.Surface))
            using (var borderPen = new Pen(theme.Border, 1f))
            using (var fontHeader = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            using (var fontSmall = new Font("Segoe UI", 8f))
            {
                g.FillRectangle(casingBrush, rect);
                g.DrawRectangle(borderPen, rect);

                TextRenderer.DrawText(g, "COOLING TOWERS (CW CIRCUIT)", fontHeader, new Point(rect.Left + 12, rect.Top + 8), Color.FromArgb(34, 197, 94));

                int towerCount = _engine.Towers.Count;
                if (towerCount == 0) return;

                int itemHeight = (rect.Height - 44) / towerCount;

                for (int i = 0; i < towerCount; i++)
                {
                    var tw = _engine.Towers[i];
                    Rectangle itemRect = new Rectangle(rect.Left + 10, rect.Top + 32 + i * itemHeight, rect.Width - 20, itemHeight - 8);

                    using (var towerBrush = new SolidBrush(Color.FromArgb(25, 34, 197, 94)))
                    using (var towerPen = new Pen(tw.IsFanRunning ? Color.FromArgb(34, 197, 94) : theme.Border, 1.5f))
                    {
                        g.FillRectangle(towerBrush, itemRect);
                        g.DrawRectangle(towerPen, itemRect);
                    }

                    // Rotating Fan on Cooling Tower
                    int fanRadius = 14;
                    int fanX = itemRect.Left + 24;
                    int fanY = itemRect.Top + itemRect.Height / 2;

                    float fanAngle = tw.IsFanRunning
                        ? (float)(ZeroAnimationClock.TotalElapsedTime * (tw.FanRpm / 60.0) * 360.0) % 360f
                        : 0f;

                    using (var fanPen = new Pen(tw.IsFanRunning ? Color.FromArgb(34, 197, 94) : theme.TextSecondary, 2f))
                    {
                        g.DrawEllipse(fanPen, fanX - fanRadius, fanY - fanRadius, fanRadius * 2, fanRadius * 2);
                        for (int b = 0; b < 4; b++)
                        {
                            float rad = (fanAngle + b * 90f) * (float)Math.PI / 180f;
                            g.DrawLine(fanPen, fanX, fanY, fanX + (float)Math.Cos(rad) * fanRadius, fanY + (float)Math.Sin(rad) * fanRadius);
                        }
                    }

                    // Tower Name & Status
                    Rectangle nameRect = new Rectangle(itemRect.Left + 48, itemRect.Top + 4, Math.Max(20, itemRect.Width - 54), 18);
                    TextRenderer.DrawText(g, tw.Name, fontHeader, nameRect, theme.TextPrimary,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                    string rpmStr = tw.IsFanRunning ? $"{tw.FanRpm:N0} RPM (Running)" : "FAN STOPPED";
                    Rectangle rpmRect = new Rectangle(itemRect.Left + 48, itemRect.Top + 22, Math.Max(20, itemRect.Width - 54), 16);
                    TextRenderer.DrawText(g, rpmStr, fontSmall, rpmRect,
                        tw.IsFanRunning ? theme.TextSecondary : Color.FromArgb(148, 163, 184),
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                    string approachStr = $"Approach: {tw.ApproachTempC:F1}°C (In: {tw.WaterTempInC:F1}°C / Out: {tw.WaterTempOutC:F1}°C)";
                    Rectangle appRect = new Rectangle(itemRect.Left + 48, itemRect.Top + 38, Math.Max(20, itemRect.Width - 54), 16);
                    TextRenderer.DrawText(g, approachStr, fontSmall, appRect, Color.FromArgb(34, 197, 94),
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                }
            }
        }

        private void DrawInterconnectPipes(Graphics g, Rectangle chillers, Rectangle towers, ZeroThemePalette theme)
        {
            float phase = ZeroAnimationClock.FluidPhase;

            // Pipe 1: Condenser Water Loop between Chillers and Towers (Green)
            float pipeY = chillers.Top + chillers.Height * 0.5f;
            using (var pipePen = new Pen(Color.FromArgb(120, 34, 197, 94), 2f) { DashStyle = DashStyle.Dash })
            {
                g.DrawLine(pipePen, chillers.Right, pipeY, towers.Left, pipeY);
            }

            // Pulse dot on pipe
            float dotX = chillers.Right + (towers.Left - chillers.Right) * phase;
            g.FillEllipse(Brushes.LimeGreen, dotX - 3, pipeY - 3, 6, 6);
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZChillerPlant"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ChillerPlant is deprecated and will be removed in 5 release cycles. Please migrate to ZChillerPlant instead.")]
    [ToolboxItem(false)]
    public class ChillerPlant : ZChillerPlant
    {
    }

    #endregion
}
