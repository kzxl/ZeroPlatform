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
    /// Battery Energy Storage System (BESS) high-voltage rack and module health visualizer in WPF.
    /// Features SoC/SoH gauges, 16-cell module balancing heatmaps, and early thermal runaway precursor detection.
    /// </summary>
    public class ZBessRackMonitor : ZeroWpfVisualBase
    {
        private readonly BessEngine _engine = new BessEngine();

        protected override bool AutoAnimate => true;

        #region Properties

        public BessEngine Engine => _engine;

        #endregion

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double w = ActualWidth;
            double h = ActualHeight;
            if (w < 200 || h < 140)
                return;

            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            // Background
            dc.DrawRectangle(ZeroWpfTheme.BgPrimary, null, new Rect(0, 0, w, h));

            var rack = _engine.Rack;
            var worstRisk = _engine.OverallRisk;

            // 1. Header Banner
            string title = $"{rack.RackId} — {rack.Name}";
            var titleText = CreateFormattedText(title, ZeroWpfTheme.BoldTypeface, 13, ZeroWpfTheme.TextPrimary, dpi);
            dc.DrawText(titleText, new Point(20, 14));

            // Runaway Risk Pill
            Brush riskBrush = worstRisk == ThermalRunawayRisk.Normal ? ZeroWpfTheme.SuccessAccent :
                             worstRisk == ThermalRunawayRisk.Elevated ? ZeroWpfTheme.InfoAccent :
                             worstRisk == ThermalRunawayRisk.Warning ? ZeroWpfTheme.WarningAccent :
                             ZeroWpfTheme.DangerAccent;

            string riskStr = worstRisk == ThermalRunawayRisk.Normal ? "THERMAL: STABLE" :
                             worstRisk == ThermalRunawayRisk.Elevated ? "THERMAL: ELEVATED" :
                             worstRisk == ThermalRunawayRisk.Warning ? "THERMAL: WARNING" :
                             "RUNAWAY PRECURSOR ALARM";

            Color riskCol = riskBrush is SolidColorBrush rscb ? rscb.Color : Color.FromRgb(34, 197, 94);
            Rect pillRect = new Rect(w - 240, 10, 220, 26);
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(25, riskCol.R, riskCol.G, riskCol.B)),
                new Pen(riskBrush, 1.2), pillRect, 4, 4);

            var riskText = CreateFormattedText(riskStr, ZeroWpfTheme.BoldTypeface, 10, riskBrush, dpi);
            dc.DrawText(riskText, new Point(pillRect.X + (pillRect.Width - riskText.Width) / 2, pillRect.Y + 5));

            // 2. Telemetry KPIs Strip
            double kpiY = 50;
            double kpiH = 50;
            DrawKpiStrip(dc, new Rect(20, kpiY, w - 40, kpiH), rack, dpi);

            // 3. Module Balancing Heatmap List
            double modY = kpiY + kpiH + 14;
            double modH = h - modY - 16;
            if (modH < 80 || w < 240) return;

            DrawModuleList(dc, new Rect(20, modY, w - 40, modH), rack, dpi);
        }

        private void DrawKpiStrip(DrawingContext dc, Rect rect, BessRack rack, double dpi)
        {
            dc.DrawRoundedRectangle(ZeroWpfTheme.BgCard, ZeroWpfTheme.BorderPen, rect, 6, 6);

            double colW = rect.Width / 5.0;

            // KPI 1: SoC
            DrawKpiCell(dc, new Rect(rect.X, rect.Y, colW, rect.Height), "STATE OF CHARGE", $"{rack.SocPct:F1}%", ZeroWpfTheme.SuccessAccent, dpi);

            // KPI 2: SoH
            DrawKpiCell(dc, new Rect(rect.X + colW, rect.Y, colW, rect.Height), "BATTERY HEALTH (SoH)", $"{rack.SohPct:F1}%", ZeroWpfTheme.InfoAccent, dpi);

            // KPI 3: Voltage
            DrawKpiCell(dc, new Rect(rect.X + colW * 2, rect.Y, colW, rect.Height), "STRING VOLTAGE", $"{rack.StringVoltageV:F1} V", ZeroWpfTheme.TextPrimary, dpi);

            // KPI 4: Power
            string pwr = $"{rack.PowerKw:F1} kW ({rack.CurrentAmps:F1} A)";
            DrawKpiCell(dc, new Rect(rect.X + colW * 3, rect.Y, colW, rect.Height), "POWER FLOW", pwr, ZeroWpfTheme.TextPrimary, dpi);

            // KPI 5: Delta mV
            double maxDelta = _engine.RackMaxDeltaCellMv;
            Brush deltaBrush = maxDelta < 25.0 ? ZeroWpfTheme.SuccessAccent :
                               maxDelta < 45.0 ? ZeroWpfTheme.WarningAccent : ZeroWpfTheme.DangerAccent;
            DrawKpiCell(dc, new Rect(rect.X + colW * 4, rect.Y, rect.Right - (rect.X + colW * 4), rect.Height), "MAX CELL DELTA", $"{maxDelta:F1} mV", deltaBrush, dpi);
        }


        private void DrawModuleList(DrawingContext dc, Rect rect, BessRack rack, double dpi)
        {
            dc.DrawRoundedRectangle(ZeroWpfTheme.BgCard, ZeroWpfTheme.BorderPen, rect, 6, 6);

            var headerText = CreateFormattedText("MODULE BALANCING & THERMAL SUPERVISION (16 CELLS PER MODULE)", ZeroWpfTheme.BoldTypeface, 9.5, ZeroWpfTheme.TextSecondary, dpi);
            dc.DrawText(headerText, new Point(rect.X + 14, rect.Y + 8));

            int count = rack.Modules.Count;
            if (count == 0) return;

            double listTop = rect.Y + 28;
            double listHeight = rect.Height - 34;
            double rowH = listHeight / count;
            if (rowH < 22) rowH = 22;

            Pen linePen = new Pen(new SolidColorBrush(Color.FromArgb(20, 200, 200, 200)), 1);

            for (int i = 0; i < count; i++)
            {
                double rowY = listTop + i * rowH;
                if (rowY + rowH > rect.Bottom) break;

                var mod = rack.Modules[i];

                if (i > 0)
                {
                    dc.DrawLine(linePen, new Point(rect.X + 10, rowY), new Point(rect.Right - 10, rowY));
                }

                // Module name
                var nameTxt = CreateFormattedText(mod.Name, ZeroWpfTheme.RegularTypeface, 10, ZeroWpfTheme.TextPrimary, dpi);
                dc.DrawText(nameTxt, new Point(rect.X + 14, rowY + 4));

                // Temperature & rate
                string tempStr = $"{mod.TemperatureC:F1} °C ({mod.RateOfTemperatureRiseCpm:F1} °C/m)";
                Brush tempBrush = mod.TemperatureC >= 50.0 ? ZeroWpfTheme.DangerAccent : ZeroWpfTheme.TextSecondary;
                var tempTxt = CreateFormattedText(tempStr, ZeroWpfTheme.RegularTypeface, 10, tempBrush, dpi);
                dc.DrawText(tempTxt, new Point(rect.X + 85, rowY + 4));

                // 16-Cell micro heatmap
                double heatX = rect.X + 240;
                double heatW = rect.Width - 360;
                if (heatW > 80 && mod.CellVoltages != null && mod.CellVoltages.Length > 0)
                {
                    DrawCellHeatmapWpf(dc, heatX, rowY + 5, heatW, rowH - 10, mod);
                }

                // Delta mV
                Brush dBrush = mod.DeltaCellMv < 25.0 ? ZeroWpfTheme.SuccessAccent :
                               mod.DeltaCellMv < 45.0 ? ZeroWpfTheme.WarningAccent : ZeroWpfTheme.DangerAccent;
                var deltaTxt = CreateFormattedText($"Δ {mod.DeltaCellMv:F0} mV", ZeroWpfTheme.BoldTypeface, 10, dBrush, dpi);
                dc.DrawText(deltaTxt, new Point(rect.Right - 90, rowY + 4));
            }
        }

        private void DrawCellHeatmapWpf(DrawingContext dc, double x, double y, double width, double height, BessModule mod)
        {
            int cellCount = mod.CellVoltages.Length;
            if (cellCount == 0) return;

            double cellW = width / cellCount;
            double minV = 3.20;
            double maxV = 3.30;

            for (int c = 0; c < cellCount; c++)
            {
                double v = mod.CellVoltages[c];
                float norm = (float)Math.Max(0.0, Math.Min(1.0, (v - minV) / (maxV - minV)));

                int r = (int)(20 + (1.0f - norm) * 40);
                int gr = (int)(150 + norm * 80);
                int b = (int)(200 - norm * 100);
                Color cellCol = Color.FromRgb((byte)Math.Min(255, r), (byte)Math.Min(255, gr), (byte)Math.Min(255, b));

                Rect cRect = new Rect(x + c * cellW + 1, y, Math.Max(2, cellW - 2), height);
                dc.DrawRoundedRectangle(new SolidColorBrush(cellCol), null, cRect, 1, 1);
            }
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZBessRackMonitor"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("BessRackMonitor is deprecated and will be removed in 5 release cycles. Please migrate to ZBessRackMonitor instead.")]
    public class BessRackMonitor : ZBessRackMonitor { }

    /// <summary>
    /// Legacy alias for <see cref="ZBessRackMonitor"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroBessRackMonitor is deprecated and will be removed in 5 release cycles. Please migrate to ZBessRackMonitor instead.")]
    public class ZeroBessRackMonitor : ZBessRackMonitor { }

    #endregion

}
