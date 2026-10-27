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
    /// Solar Photovoltaic (PV) utility/commercial generation plant string matrix visualizer in WPF.
    /// Monitors real-time string currents, voltages, MPPT performance, and string mismatch/shading detection.
    /// </summary>
    public class ZSolarPvMatrix : ZeroWpfVisualBase
    {
        private readonly SolarPvEngine _engine = new SolarPvEngine();

        protected override bool AutoAnimate => true;

        #region Properties

        public SolarPvEngine Engine => _engine;

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

            var plant = _engine.Plant;

            // 1. Header Banner
            string title = $"{plant.PlantId} — {plant.Name}";
            var titleText = CreateFormattedText(title, ZeroWpfTheme.BoldTypeface, 13, ZeroWpfTheme.TextPrimary, dpi);
            dc.DrawText(titleText, new Point(20, 14));

            // Status Pill
            Rect pillRect = new Rect(w - 190, 10, 170, 26);
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(25, 34, 197, 94)),
                new Pen(ZeroWpfTheme.SuccessAccent, 1), pillRect, 4, 4);

            var statText = CreateFormattedText("GENERATING (MPPT OK)", ZeroWpfTheme.BoldTypeface, 10, ZeroWpfTheme.SuccessAccent, dpi);
            dc.DrawText(statText, new Point(pillRect.X + (pillRect.Width - statText.Width) / 2, pillRect.Y + 5));

            // 2. KPI Strip
            double kpiY = 50;
            double kpiH = 50;
            DrawKpiStrip(dc, new Rect(20, kpiY, w - 40, kpiH), plant, dpi);

            // 3. String Grid Canvas
            double gridY = kpiY + kpiH + 14;
            double gridH = h - gridY - 16;
            if (gridH < 100 || w < 240) return;

            DrawStringGrid(dc, new Rect(20, gridY, w - 40, gridH), plant, dpi);
        }

        private void DrawKpiStrip(DrawingContext dc, Rect rect, SolarPvPlant plant, double dpi)
        {
            dc.DrawRoundedRectangle(ZeroWpfTheme.BgCard, ZeroWpfTheme.BorderPen, rect, 6, 6);

            double colW = rect.Width / 5.0;

            // KPI 1: Fleet Power
            DrawKpiCell(dc, new Rect(rect.X, rect.Y, colW, rect.Height), "FLEET POWER", $"{plant.TotalPowerKw:F1} kW", ZeroWpfTheme.SuccessAccent, dpi);

            // KPI 2: Irradiance
            DrawKpiCell(dc, new Rect(rect.X + colW, rect.Y, colW, rect.Height), "SOLAR IRRADIANCE", $"{plant.SolarIrradianceWm2:F0} W/m²", ZeroWpfTheme.WarningAccent, dpi);

            // KPI 3: PR %
            DrawKpiCell(dc, new Rect(rect.X + colW * 2, rect.Y, colW, rect.Height), "PERFORMANCE RATIO", $"{plant.PerformanceRatioPct:F1}%", ZeroWpfTheme.InfoAccent, dpi);

            // KPI 4: Cell Temp
            DrawKpiCell(dc, new Rect(rect.X + colW * 3, rect.Y, colW, rect.Height), "CELL TEMPERATURE", $"{plant.CellTempC:F1} °C", ZeroWpfTheme.TextPrimary, dpi);

            // KPI 5: Active strings
            int activeCount = 0;
            for (int i = 0; i < plant.Strings.Count; i++)
                if (plant.Strings[i].Status == PvStringStatus.Normal) activeCount++;

            string ratioStr = $"{activeCount}/{plant.Strings.Count} Healthy";
            Brush statBrush = activeCount == plant.Strings.Count ? ZeroWpfTheme.SuccessAccent : ZeroWpfTheme.WarningAccent;
            DrawKpiCell(dc, new Rect(rect.X + colW * 4, rect.Y, rect.Right - (rect.X + colW * 4), rect.Height), "FLEET STRINGS", ratioStr, statBrush, dpi);
        }


        private void DrawStringGrid(DrawingContext dc, Rect rect, SolarPvPlant plant, double dpi)
        {
            int cols = 4;
            int rows = 4;
            double gap = 8;
            double cellW = (rect.Width - (cols - 1) * gap) / cols;
            double cellH = (rect.Height - (rows - 1) * gap) / rows;

            for (int i = 0; i < plant.Strings.Count && i < 16; i++)
            {
                int r = i / cols;
                int c = i % cols;
                double cellX = rect.X + c * (cellW + gap);
                double cellY = rect.Y + r * (cellH + gap);
                Rect cellRect = new Rect(cellX, cellY, cellW, cellH);

                var pvStr = plant.Strings[i];
                DrawStringCellWpf(dc, cellRect, pvStr, dpi);
            }
        }

        private void DrawStringCellWpf(DrawingContext dc, Rect rect, PvString pvStr, double dpi)
        {
            Brush statBrush = pvStr.Status == PvStringStatus.Normal ? ZeroWpfTheme.SuccessAccent :
                              pvStr.Status == PvStringStatus.Shaded ? ZeroWpfTheme.WarningAccent :
                              pvStr.Status == PvStringStatus.BlownFuse ? ZeroWpfTheme.DangerAccent :
                              new SolidColorBrush(Color.FromRgb(168, 85, 247));

            dc.DrawRoundedRectangle(ZeroWpfTheme.BgCard, ZeroWpfTheme.BorderPen, rect, 4, 4);

            // Status bar on left edge
            Rect statusEdge = new Rect(rect.X, rect.Y, 4, rect.Height);
            dc.DrawRoundedRectangle(statBrush, null, statusEdge, 1, 1);

            // Title
            string title = $"STR-{pvStr.StringId:00} (INV-{pvStr.InverterId})";
            var tText = CreateFormattedText(title, ZeroWpfTheme.BoldTypeface, 10, ZeroWpfTheme.TextPrimary, dpi);
            dc.DrawText(tText, new Point(rect.X + 10, rect.Y + 6));

            // Status text
            var sText = CreateFormattedText(pvStr.Status.ToString().ToUpperInvariant(), ZeroWpfTheme.BoldTypeface, 9, statBrush, dpi);
            dc.DrawText(sText, new Point(rect.Right - 10 - sText.Width, rect.Y + 7));

            // Power kW
            double valY = rect.Y + 24;
            var pText = CreateFormattedText($"{pvStr.PowerKw:F2} kW", ZeroWpfTheme.BoldTypeface, 11, ZeroWpfTheme.TextPrimary, dpi);
            dc.DrawText(pText, new Point(rect.X + 10, valY));

            // Telemetry: Voltage & Current
            double teleY = valY + 18;
            string teleStr = $"{pvStr.VoltageV:F0} V | {pvStr.CurrentA:F1} A";
            var telText = CreateFormattedText(teleStr, ZeroWpfTheme.RegularTypeface, 9.5, ZeroWpfTheme.TextSecondary, dpi);
            dc.DrawText(telText, new Point(rect.X + 10, teleY));
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZSolarPvMatrix"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("SolarPvMatrix is deprecated and will be removed in 5 release cycles. Please migrate to ZSolarPvMatrix instead.")]
    public class SolarPvMatrix : ZSolarPvMatrix { }

    /// <summary>
    /// Legacy alias for <see cref="ZSolarPvMatrix"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroSolarPvMatrix is deprecated and will be removed in 5 release cycles. Please migrate to ZSolarPvMatrix instead.")]
    public class ZeroSolarPvMatrix : ZSolarPvMatrix { }

    #endregion

}
