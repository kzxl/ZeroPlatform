using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using ZeroUI.Core.Rendering;
using ZeroUI.Core.Bms;
using ZeroUI.Wpf.Base;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Bms
{
    /// <summary>
    /// Central Chiller Plant schematic visualizer for WPF.
    /// Renders chillers, cooling towers, and live thermodynamic efficiency metrics (COP and kW/Ton).
    /// </summary>
    public class ZChillerPlant : ZeroWpfVisualBase
    {
        private readonly ChillerPlantEngine _engine = new ChillerPlantEngine();
        private string _plantTitle = "Central Utility Plant";

        protected override bool AutoAnimate => true;

        public ZChillerPlant()
        {
        }

        #region Properties

        public ChillerPlantEngine Engine => _engine;

        public string PlantTitle
        {
            get => _plantTitle;
            set
            {
                _plantTitle = value;
                InvalidateVisual();
            }
        }

        #endregion

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double w = ActualWidth;
            double h = ActualHeight;
            if (w < 120 || h < 80)
                return;

            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            // Background
            dc.DrawRectangle(ZeroWpfTheme.BgPrimary, null, new Rect(0, 0, w, h));

            // Top Header & Efficiency HUD
            DrawHeaderAndHud(dc, w, dpi);

            // Layout Areas
            double mainTop = 50;
            double mainHeight = h - mainTop - 14;
            double halfWidth = (w - 36) / 2;

            if (halfWidth < 120 || mainHeight < 60)
                return;

            Rect chillerArea = new Rect(14, mainTop, halfWidth, mainHeight);
            Rect towerArea = new Rect(14 + halfWidth + 8, mainTop, halfWidth, mainHeight);

            DrawChillersSection(dc, chillerArea, dpi);
            DrawTowersSection(dc, towerArea, dpi);

            // Pipes
            DrawInterconnectPipes(dc, chillerArea, towerArea);
        }

        private void DrawHeaderAndHud(DrawingContext dc, double w, double dpi)
        {
            var titleFt = CreateFormattedText(_plantTitle, ZeroWpfTheme.BoldTypeface, 11.5, ZeroWpfTheme.TextPrimary, dpi);
            dc.DrawText(titleFt, new Point(14, 12));

            double hudX = w - 400;
            if (hudX > 200)
            {
                string loadStr = $"Load: {_engine.TotalActualTons:N0} TR | {_engine.TotalPowerKw:N0} kW";
                var loadFt = CreateFormattedText(loadStr, ZeroWpfTheme.RegularTypeface, 9.5, ZeroWpfTheme.TextSecondary, dpi);
                dc.DrawText(loadFt, new Point(hudX, 14));

                double kwTon = _engine.PlantAverageKwPerTon;
                double cop = _engine.PlantAverageCop;

                Brush effBrush = kwTon < 0.65 ? Brushes.LimeGreen :
                                 kwTon < 0.80 ? Brushes.Orange :
                                 Brushes.Tomato;

                Rect badgeRect = new Rect(w - 150, 10, 136, 24);
                dc.DrawRectangle(ZeroWpfTheme.BgCard, new Pen(effBrush, 1.0), badgeRect);

                string effStr = $"{kwTon:F2} kW/TR | COP {cop:F1}";
                var effFt = CreateFormattedText(effStr, ZeroWpfTheme.BoldTypeface, 9.0, effBrush, dpi);
                dc.DrawText(effFt, new Point(badgeRect.Left + 8, badgeRect.Top + 4));
            }
        }

        private void DrawChillersSection(DrawingContext dc, Rect rect, double dpi)
        {
            dc.DrawRectangle(ZeroWpfTheme.BgCard, ZeroWpfTheme.BorderPen, rect);

            var headerFt = CreateFormattedText("WATER CHILLERS (CHW CIRCUIT)", ZeroWpfTheme.BoldTypeface, 9.0, Brushes.SkyBlue, dpi);
            dc.DrawText(headerFt, new Point(rect.Left + 10, rect.Top + 8));

            int count = _engine.Chillers.Count;
            if (count == 0) return;

            double itemH = (rect.Height - 36) / count;

            for (int i = 0; i < count; i++)
            {
                var ch = _engine.Chillers[i];
                Rect itemRect = new Rect(rect.Left + 8, rect.Top + 28 + i * itemH, rect.Width - 16, itemH - 6);

                dc.DrawRectangle(ZeroWpfTheme.BgHover, ZeroWpfTheme.BorderPen, itemRect);

                // Run indicator dot
                Brush statusBrush = ch.Status == ChillerStatus.Running ? Brushes.LimeGreen : Brushes.Gray;
                dc.DrawEllipse(statusBrush, null, new Point(itemRect.Left + 18, itemRect.Top + itemRect.Height / 2), 6, 6);

                var nameFt = CreateFormattedText(ch.Name, ZeroWpfTheme.BoldTypeface, 9.5, ZeroWpfTheme.TextPrimary, dpi);
                dc.DrawText(nameFt, new Point(itemRect.Left + 32, itemRect.Top + 6));

                string line2 = $"{ch.ActualTons:N0} TR ({ch.PowerKw:N0} kW) | {ch.EfficiencyKwPerTon:F2} kW/TR";
                var line2Ft = CreateFormattedText(line2, ZeroWpfTheme.RegularTypeface, 8.5, ZeroWpfTheme.TextSecondary, dpi);
                dc.DrawText(line2Ft, new Point(itemRect.Left + 32, itemRect.Top + 22));

                string tempStr = $"CHW: {ch.ChwSupplyTempC:F1}°C / {ch.ChwReturnTempC:F1}°C";
                var tempFt = CreateFormattedText(tempStr, ZeroWpfTheme.RegularTypeface, 8.0, Brushes.SkyBlue, dpi);
                dc.DrawText(tempFt, new Point(itemRect.Left + 32, itemRect.Top + 36));
            }
        }

        private void DrawTowersSection(DrawingContext dc, Rect rect, double dpi)
        {
            dc.DrawRectangle(ZeroWpfTheme.BgCard, ZeroWpfTheme.BorderPen, rect);

            var headerFt = CreateFormattedText("COOLING TOWERS (CW CIRCUIT)", ZeroWpfTheme.BoldTypeface, 9.0, Brushes.MediumSeaGreen, dpi);
            dc.DrawText(headerFt, new Point(rect.Left + 10, rect.Top + 8));

            int count = _engine.Towers.Count;
            if (count == 0) return;

            double itemH = (rect.Height - 36) / count;

            for (int i = 0; i < count; i++)
            {
                var tw = _engine.Towers[i];
                Rect itemRect = new Rect(rect.Left + 8, rect.Top + 28 + i * itemH, rect.Width - 16, itemH - 6);

                dc.DrawRectangle(ZeroWpfTheme.BgHover, ZeroWpfTheme.BorderPen, itemRect);

                // Rotating fan icon
                double fanAngle = tw.IsFanRunning
                    ? (ZeroAnimationClock.TotalElapsedTime * (tw.FanRpm / 60.0) * 360.0) % 360.0
                    : 0;

                double fx = itemRect.Left + 18;
                double fy = itemRect.Top + itemRect.Height / 2;
                double rad = fanAngle * Math.PI / 180.0;

                Pen fanPen = new Pen(tw.IsFanRunning ? Brushes.MediumSeaGreen : Brushes.Gray, 1.5);
                fanPen.Freeze();

                dc.DrawEllipse(null, fanPen, new Point(fx, fy), 10, 10);
                dc.DrawLine(fanPen, new Point(fx, fy), new Point(fx + Math.Cos(rad) * 9, fy + Math.Sin(rad) * 9));
                dc.DrawLine(fanPen, new Point(fx, fy), new Point(fx - Math.Cos(rad) * 9, fy - Math.Sin(rad) * 9));

                var nameFt = CreateFormattedText(tw.Name, ZeroWpfTheme.BoldTypeface, 9.5, ZeroWpfTheme.TextPrimary, dpi);
                dc.DrawText(nameFt, new Point(itemRect.Left + 34, itemRect.Top + 6));

                string line2 = tw.IsFanRunning ? $"{tw.FanRpm:N0} RPM (Running)" : "STOPPED";
                var line2Ft = CreateFormattedText(line2, ZeroWpfTheme.RegularTypeface, 8.5, ZeroWpfTheme.TextSecondary, dpi);
                dc.DrawText(line2Ft, new Point(itemRect.Left + 34, itemRect.Top + 22));

                string appStr = $"Approach: {tw.ApproachTempC:F1}°C (In: {tw.WaterTempInC:F1}°C / Out: {tw.WaterTempOutC:F1}°C)";
                var appFt = CreateFormattedText(appStr, ZeroWpfTheme.RegularTypeface, 8.0, Brushes.MediumSeaGreen, dpi);
                dc.DrawText(appFt, new Point(itemRect.Left + 34, itemRect.Top + 36));
            }
        }

        private void DrawInterconnectPipes(DrawingContext dc, Rect chillers, Rect towers)
        {
            float phase = ZeroAnimationClock.FluidPhase;
            double py = chillers.Top + chillers.Height * 0.5;

            Pen pipePen = new Pen(Brushes.MediumSeaGreen, 1.5) { DashStyle = DashStyles.Dash };
            pipePen.Freeze();

            dc.DrawLine(pipePen, new Point(chillers.Right, py), new Point(towers.Left, py));

            // Pulse dot
            double dx = chillers.Right + (towers.Left - chillers.Right) * phase;
            dc.DrawEllipse(Brushes.LimeGreen, null, new Point(dx, py), 3, 3);
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZChillerPlant"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ChillerPlant is deprecated and will be removed in 5 release cycles. Please migrate to ZChillerPlant instead.")]
    public class ChillerPlant : ZChillerPlant { }

    /// <summary>
    /// Legacy alias for <see cref="ZChillerPlant"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroChillerPlant is deprecated and will be removed in 5 release cycles. Please migrate to ZChillerPlant instead.")]
    public class ZeroChillerPlant : ZChillerPlant { }

    #endregion

}
