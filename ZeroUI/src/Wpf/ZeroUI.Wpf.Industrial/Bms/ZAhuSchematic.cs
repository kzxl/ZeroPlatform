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
    /// Air Handling Unit (AHU) mechanical cross-section visualizer for WPF.
    /// Renders mixing dampers, filters, coils, and supply fans with real-time rotating blades,
    /// airflow vectors, and static pressure badges.
    /// </summary>
    public class ZAhuSchematic : ZeroWpfVisualBase
    {
        private readonly AhuEngine _engine = new AhuEngine();
        private string _unitTag = "AHU-01";

        protected override bool AutoAnimate => true;

        public ZAhuSchematic()
        {
        }

        #region Properties

        public AhuEngine Engine => _engine;

        public string UnitTag
        {
            get => _unitTag;
            set
            {
                _unitTag = value;
                InvalidateVisual();
            }
        }

        public AhuOperatingMode Mode
        {
            get => _engine.Mode;
            set
            {
                _engine.Mode = value;
                InvalidateVisual();
            }
        }

        public double OutsideAirPct
        {
            get => _engine.Dampers.OutsideAirPct;
            set
            {
                _engine.Dampers.OutsideAirPct = Math.Max(0, Math.Min(100, value));
                InvalidateVisual();
            }
        }

        public double CoolingValvePct
        {
            get => _engine.Coils.CoolingValvePct;
            set
            {
                _engine.Coils.CoolingValvePct = Math.Max(0, Math.Min(100, value));
                InvalidateVisual();
            }
        }

        public double HeatingValvePct
        {
            get => _engine.Coils.HeatingValvePct;
            set
            {
                _engine.Coils.HeatingValvePct = Math.Max(0, Math.Min(100, value));
                InvalidateVisual();
            }
        }

        public double SupplyFanRpm
        {
            get => _engine.Fans.SupplyFanRpm;
            set
            {
                _engine.Fans.SupplyFanRpm = Math.Max(0, value);
                InvalidateVisual();
            }
        }

        public double AirflowCfm
        {
            get => _engine.Air.AirflowCfm;
            set
            {
                _engine.Air.AirflowCfm = Math.Max(0, value);
                InvalidateVisual();
            }
        }

        public double SupplyTempC
        {
            get => _engine.Air.SupplyTempC;
            set
            {
                _engine.Air.SupplyTempC = value;
                InvalidateVisual();
            }
        }

        public double ReturnTempC
        {
            get => _engine.Air.ReturnTempC;
            set
            {
                _engine.Air.ReturnTempC = value;
                InvalidateVisual();
            }
        }

        #endregion

        #region Helpers

#if NETFRAMEWORK
        private static FormattedText CreateFormattedText(string text, Typeface typeface, double fontSize, Brush brush, double pixelsPerDip = 1.0)
        {
            return new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, fontSize, brush);
        }
#else
        private static FormattedText CreateFormattedText(string text, Typeface typeface, double fontSize, Brush brush, double pixelsPerDip = 1.0)
        {
            return new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, fontSize, brush, pixelsPerDip);
        }
#endif

        #endregion

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double w = ActualWidth;
            double h = ActualHeight;
            if (w < 100 || h < 60)
                return;

            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            // Background
            dc.DrawRectangle(ZeroWpfTheme.BgPrimary, null, new Rect(0, 0, w, h));

            // Header
            var titleFt = CreateFormattedText($"{_unitTag} — Air Handling Unit", ZeroWpfTheme.BoldTypeface, 11.5, ZeroWpfTheme.TextPrimary, dpi);
            dc.DrawText(titleFt, new Point(16, 12));

            string modeStr = $"MODE: {_engine.Mode.ToString().ToUpperInvariant()}";
            Brush modeBrush = _engine.Mode switch
            {
                AhuOperatingMode.Cooling => Brushes.SkyBlue,
                AhuOperatingMode.Heating => Brushes.Orange,
                AhuOperatingMode.Economizer => Brushes.LightGreen,
                _ => ZeroWpfTheme.TextSecondary
            };
            var modeFt = CreateFormattedText(modeStr, ZeroWpfTheme.BoldTypeface, 9.5, modeBrush, dpi);
            dc.DrawText(modeFt, new Point(240, 14));

            string flowStr = $"{_engine.Air.AirflowCfm:N0} CFM | SP: {_engine.Fans.SupplyStaticPressureInWg:F2}\"";
            var flowFt = CreateFormattedText(flowStr, ZeroWpfTheme.RegularTypeface, 9.5, ZeroWpfTheme.TextSecondary, dpi);
            dc.DrawText(flowFt, new Point(w - flowFt.Width - 16, 14));

            // Tunnel casing
            double tunnelLeft = 16;
            double tunnelTop = 44;
            double tunnelW = w - 32;
            double tunnelH = h - 60;
            if (tunnelW < 150 || tunnelH < 50)
                return;

            Rect tunnelRect = new Rect(tunnelLeft, tunnelTop, tunnelW, tunnelH);
            dc.DrawRectangle(ZeroWpfTheme.BgCard, ZeroWpfTheme.BorderPen, tunnelRect);

            double secW = tunnelW / 5.0;

            // 1. Mixing Box / Dampers
            DrawDampers(dc, tunnelLeft, tunnelTop, secW, tunnelH, dpi);

            // 2. Filters
            DrawFilters(dc, tunnelLeft + secW, tunnelTop, secW * 0.8, tunnelH, dpi);

            // 3. Coils
            DrawCoils(dc, tunnelLeft + secW * 1.8, tunnelTop, secW * 1.2, tunnelH, dpi);

            // 4. Supply Fan
            DrawFan(dc, tunnelLeft + secW * 3.0, tunnelTop, secW * 1.1, tunnelH, dpi);

            // 5. Discharge
            DrawDischarge(dc, tunnelLeft + secW * 4.1, tunnelTop, secW * 0.9, tunnelH, dpi);

            // Airflow particles
            DrawAirflowParticles(dc, tunnelRect);
        }

        private void DrawDampers(DrawingContext dc, double x, double y, double w, double h, double dpi)
        {
            var labelFt = CreateFormattedText("Mixing Box", ZeroWpfTheme.RegularTypeface, 8.5, ZeroWpfTheme.TextSecondary, dpi);
            dc.DrawText(labelFt, new Point(x + 8, y + 6));

            double dW = 32;
            double dH = h - 44;
            double dx = x + 16;
            double dy = y + 24;

            dc.DrawRectangle(null, ZeroWpfTheme.BorderPen, new Rect(dx, dy, dW, dH));

            int blades = 4;
            double spacing = dH / blades;
            double angle = (OutsideAirPct / 100.0) * 80.0;
            double rad = angle * Math.PI / 180.0;
            Pen bladePen = new Pen(Brushes.DodgerBlue, 2.0);
            bladePen.Freeze();

            for (int i = 0; i < blades; i++)
            {
                double cy = dy + (i + 0.5) * spacing;
                double bx = (dW * 0.4) * Math.Cos(rad);
                double by = (dW * 0.4) * Math.Sin(rad);
                dc.DrawLine(bladePen, new Point(dx + dW / 2 - bx, cy - by), new Point(dx + dW / 2 + bx, cy + by));
            }

            var oaFt = CreateFormattedText($"OA: {OutsideAirPct:0}%", ZeroWpfTheme.BoldTypeface, 8.0, Brushes.DodgerBlue, dpi);
            dc.DrawText(oaFt, new Point(x + 10, y + h - 16));
        }

        private void DrawFilters(DrawingContext dc, double x, double y, double w, double h, double dpi)
        {
            var labelFt = CreateFormattedText("Filter", ZeroWpfTheme.RegularTypeface, 8.5, ZeroWpfTheme.TextSecondary, dpi);
            dc.DrawText(labelFt, new Point(x + 8, y + 6));

            double midX = x + w * 0.5;
            double fTop = y + 24;
            double fH = h - 44;

            Brush filterBrush = _engine.Filters.IsDirty ? Brushes.Tomato : Brushes.MediumSeaGreen;
            Pen filterPen = new Pen(filterBrush, 1.8);
            filterPen.Freeze();

            int pleats = 8;
            double pleatH = fH / pleats;
            for (int i = 0; i < pleats; i++)
            {
                double y1 = fTop + i * pleatH;
                double y2 = y1 + pleatH * 0.5;
                double y3 = y1 + pleatH;
                dc.DrawLine(filterPen, new Point(midX - 7, y1), new Point(midX + 7, y2));
                dc.DrawLine(filterPen, new Point(midX + 7, y2), new Point(midX - 7, y3));
            }

            var dpFt = CreateFormattedText($"ΔP: {_engine.Filters.FinalFilterDeltaP:F2}\"", ZeroWpfTheme.BoldTypeface, 8.0, filterBrush, dpi);
            dc.DrawText(dpFt, new Point(x + 4, y + h - 16));
        }

        private void DrawCoils(DrawingContext dc, double x, double y, double w, double h, double dpi)
        {
            // Heating Coil
            double heatX = x + w * 0.28;
            Pen heatPen = new Pen(Brushes.Crimson, 2.0);
            heatPen.Freeze();
            DrawSerpentine(dc, heatX, y + 24, 12, h - 44, heatPen);

            var heatFt = CreateFormattedText($"H: {HeatingValvePct:0}%", ZeroWpfTheme.BoldTypeface, 8.0, Brushes.Crimson, dpi);
            dc.DrawText(heatFt, new Point(x + 4, y + h - 16));

            // Cooling Coil
            double coolX = x + w * 0.72;
            Pen coolPen = new Pen(Brushes.DodgerBlue, 2.0);
            coolPen.Freeze();
            DrawSerpentine(dc, coolX, y + 24, 12, h - 44, coolPen);

            var coolFt = CreateFormattedText($"C: {CoolingValvePct:0}%", ZeroWpfTheme.BoldTypeface, 8.0, Brushes.DodgerBlue, dpi);
            dc.DrawText(coolFt, new Point(x + w * 0.5, y + h - 16));
        }

        private static void DrawSerpentine(DrawingContext dc, double cx, double y, double w, double h, Pen pen)
        {
            int loops = 6;
            double loopH = h / loops;
            for (int i = 0; i < loops; i++)
            {
                double topY = y + i * loopH;
                dc.DrawLine(pen, new Point(cx - w / 2, topY), new Point(cx + w / 2, topY + loopH * 0.5));
                dc.DrawLine(pen, new Point(cx + w / 2, topY + loopH * 0.5), new Point(cx - w / 2, topY + loopH));
            }
        }

        private void DrawFan(DrawingContext dc, double x, double y, double w, double h, double dpi)
        {
            var fanFt = CreateFormattedText("Supply Fan", ZeroWpfTheme.RegularTypeface, 8.5, ZeroWpfTheme.TextSecondary, dpi);
            dc.DrawText(fanFt, new Point(x + 6, y + 6));

            double cx = x + w * 0.5;
            double cy = y + h * 0.5;
            double radius = Math.Min(w, h) * 0.32;

            dc.DrawEllipse(null, ZeroWpfTheme.BorderPen, new Point(cx, cy), radius, radius);

            double baseAngle = 0;
            if (_engine.Fans.IsSupplyFanRunning)
            {
                baseAngle = (ZeroAnimationClock.TotalElapsedTime * (_engine.Fans.SupplyFanRpm / 60.0) * 360.0) % 360.0;
            }

            int blades = 6;
            Brush bladeBrush = _engine.Fans.IsSupplyFanRunning ? Brushes.LimeGreen : Brushes.Gray;
            Pen bladePen = new Pen(bladeBrush, 2.0);
            bladePen.Freeze();

            for (int i = 0; i < blades; i++)
            {
                double rad = (baseAngle + (i * 360.0 / blades)) * Math.PI / 180.0;
                double bx = cx + Math.Cos(rad) * (radius - 2);
                double by = cy + Math.Sin(rad) * (radius - 2);
                dc.DrawLine(bladePen, new Point(cx, cy), new Point(bx, by));
            }

            dc.DrawEllipse(Brushes.White, null, new Point(cx, cy), 3, 3);

            var rpmFt = CreateFormattedText($"{_engine.Fans.SupplyFanRpm:0} RPM", ZeroWpfTheme.BoldTypeface, 8.0, bladeBrush, dpi);
            dc.DrawText(rpmFt, new Point(x + 6, y + h - 16));
        }

        private void DrawDischarge(DrawingContext dc, double x, double y, double w, double h, double dpi)
        {
            Rect supplyCard = new Rect(x + 6, y + 20, w - 12, 38);
            dc.DrawRectangle(ZeroWpfTheme.BgHover, ZeroWpfTheme.BorderPen, supplyCard);

            var supLabel = CreateFormattedText("SUPPLY", ZeroWpfTheme.RegularTypeface, 7.5, ZeroWpfTheme.TextMuted, dpi);
            dc.DrawText(supLabel, new Point(supplyCard.Left + 4, supplyCard.Top + 2));

            var supVal = CreateFormattedText($"{SupplyTempC:F1}°C", ZeroWpfTheme.BoldTypeface, 9.5, Brushes.DodgerBlue, dpi);
            dc.DrawText(supVal, new Point(supplyCard.Left + 4, supplyCard.Top + 16));

            Rect retCard = new Rect(x + 6, y + 64, w - 12, 38);
            dc.DrawRectangle(ZeroWpfTheme.BgHover, ZeroWpfTheme.BorderPen, retCard);

            var retLabel = CreateFormattedText("RETURN", ZeroWpfTheme.RegularTypeface, 7.5, ZeroWpfTheme.TextMuted, dpi);
            dc.DrawText(retLabel, new Point(retCard.Left + 4, retCard.Top + 2));

            var retVal = CreateFormattedText($"{ReturnTempC:F1}°C", ZeroWpfTheme.BoldTypeface, 9.5, ZeroWpfTheme.TextPrimary, dpi);
            dc.DrawText(retVal, new Point(retCard.Left + 4, retCard.Top + 16));
        }

        private void DrawAirflowParticles(DrawingContext dc, Rect tunnel)
        {
            if (!_engine.Fans.IsSupplyFanRunning)
                return;

            float phase = ZeroAnimationClock.FluidPhase;
            int count = 5;
            double spacing = tunnel.Width / count;
            double cy = tunnel.Top + tunnel.Height * 0.5;

            Pen arrowPen = new Pen(Brushes.SkyBlue, 1.5);
            arrowPen.Freeze();

            for (int i = 0; i < count; i++)
            {
                double px = tunnel.Left + ((i + phase) * spacing) % tunnel.Width;
                dc.DrawLine(arrowPen, new Point(px - 5, cy - 5), new Point(px, cy));
                dc.DrawLine(arrowPen, new Point(px, cy), new Point(px - 5, cy + 5));
            }
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZAhuSchematic"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("AhuSchematic is deprecated and will be removed in 5 release cycles. Please migrate to ZAhuSchematic instead.")]
    public class AhuSchematic : ZAhuSchematic { }

    /// <summary>
    /// Legacy alias for <see cref="ZAhuSchematic"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroAhuSchematic is deprecated and will be removed in 5 release cycles. Please migrate to ZAhuSchematic instead.")]
    public class ZeroAhuSchematic : ZAhuSchematic { }

    #endregion

}
