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
    /// IEC 61850 Substation Single-Line Diagram (SLD) vector canvas in WPF.
    /// Visualizes voltage busbars with standard coloring, transformers, circuit breakers,
    /// and animated active/reactive power flows.
    /// </summary>
    public class ZSingleLineDiagram : ZeroWpfVisualBase
    {
        private readonly SldEngine _engine = new SldEngine();
        private string _substationName = "110kV / 22kV Primary Substation";

        protected override bool AutoAnimate => true;

        #region Properties

        public SldEngine Engine => _engine;

        public string SubstationName
        {
            get => _substationName;
            set
            {
                _substationName = value;
                InvalidateVisual();
            }
        }

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

            // 1. Header Banner
            var titleText = CreateFormattedText(_substationName, ZeroWpfTheme.BoldTypeface, 13, ZeroWpfTheme.TextPrimary, dpi);
            dc.DrawText(titleText, new Point(20, 14));

            if (w > 450)
            {
                string freqStr = "Freq: 50.02 Hz | Power: 42.0 MW (8.5 MVAr)";
                var freqText = CreateFormattedText(freqStr, ZeroWpfTheme.RegularTypeface, 11, ZeroWpfTheme.TextSecondary, dpi);
                dc.DrawText(freqText, new Point(w - 380, 15));

                // Status pill
                Rect pillRect = new Rect(w - 110, 11, 88, 24);
                Brush pillBg = new SolidColorBrush(Color.FromArgb(30, 34, 197, 94));
                Pen pillBorder = new Pen(ZeroWpfTheme.SuccessAccent, 1);
                dc.DrawRoundedRectangle(pillBg, pillBorder, pillRect, 4, 4);

                var badgeText = CreateFormattedText("ENERGIZED", ZeroWpfTheme.BoldTypeface, 10, ZeroWpfTheme.SuccessAccent, dpi);
                dc.DrawText(badgeText, new Point(pillRect.X + (pillRect.Width - badgeText.Width) / 2, pillRect.Y + 4));
            }

            // Canvas layout
            double sldLeft = 30;
            double sldTop = 56;
            double sldWidth = w - 60;
            double sldHeight = h - sldTop - 20;

            if (sldWidth < 180 || sldHeight < 100) return;

            // Voltage Colors
            Color col110 = HexToColor(SldEngine.GetVoltageColorHex(VoltageLevel.HV_110kV));
            Color col22 = HexToColor(SldEngine.GetVoltageColorHex(VoltageLevel.MV_22kV));

            Brush brush110 = new SolidColorBrush(col110);
            Brush brush22 = new SolidColorBrush(col22);

            // 110kV Busbar (Top)
            double bus110Y = sldTop + 30;
            Pen penBus110 = new Pen(brush110, 5);
            dc.DrawLine(penBus110, new Point(sldLeft + 20, bus110Y), new Point(sldLeft + sldWidth - 20, bus110Y));

            var lbl110 = CreateFormattedText("BUS 110kV-A (111.8 kV)", ZeroWpfTheme.BoldTypeface, 10, brush110, dpi);
            dc.DrawText(lbl110, new Point(sldLeft + 24, bus110Y - 18));

            // 22kV Busbar (Bottom)
            double bus22Y = sldTop + sldHeight - 35;
            Pen penBus22 = new Pen(brush22, 5);
            dc.DrawLine(penBus22, new Point(sldLeft + 20, bus22Y), new Point(sldLeft + sldWidth - 20, bus22Y));

            var lbl22 = CreateFormattedText("BUS 22kV-A (22.4 kV)", ZeroWpfTheme.BoldTypeface, 10, brush22, dpi);
            dc.DrawText(lbl22, new Point(sldLeft + 24, bus22Y - 18));

            // Transformer Bay
            double bayX = sldLeft + sldWidth * 0.40;
            Pen penWire110 = new Pen(brush110, 2);
            Pen penWire22 = new Pen(brush22, 2);

            // 110kV Breaker
            double cb110Y = bus110Y + 35;
            dc.DrawLine(penWire110, new Point(bayX, bus110Y), new Point(bayX, cb110Y));
            DrawBreakerSymbolWpf(dc, bayX, cb110Y, BreakerState.Closed, brush110);

            var cb110Lbl = CreateFormattedText("CB-110", ZeroWpfTheme.RegularTypeface, 9.5, ZeroWpfTheme.TextSecondary, dpi);
            dc.DrawText(cb110Lbl, new Point(bayX + 16, cb110Y - 8));

            // Line down to Transformer
            double tTopY = cb110Y + 35;
            dc.DrawLine(penWire110, new Point(bayX, cb110Y + 14), new Point(bayX, tTopY));

            // Transformer dual overlapping circles
            double tRadius = 18;
            double tCenter1Y = tTopY + tRadius;
            double tCenter2Y = tCenter1Y + tRadius * 1.1;

            dc.DrawEllipse(null, new Pen(brush110, 2.5), new Point(bayX, tCenter1Y), tRadius, tRadius);
            dc.DrawEllipse(null, new Pen(brush22, 2.5), new Point(bayX, tCenter2Y), tRadius, tRadius);

            var tLbl = CreateFormattedText("T1 63 MVA (67%)", ZeroWpfTheme.RegularTypeface, 9.5, ZeroWpfTheme.TextPrimary, dpi);
            dc.DrawText(tLbl, new Point(bayX + 24, tCenter1Y));

            // Line from Transformer to 22kV Breaker
            double cb22Y = tCenter2Y + tRadius + 20;
            dc.DrawLine(penWire22, new Point(bayX, tCenter2Y + tRadius), new Point(bayX, cb22Y));
            DrawBreakerSymbolWpf(dc, bayX, cb22Y, BreakerState.Closed, brush22);

            var cb22Lbl = CreateFormattedText("CB-22", ZeroWpfTheme.RegularTypeface, 9.5, ZeroWpfTheme.TextSecondary, dpi);
            dc.DrawText(cb22Lbl, new Point(bayX + 16, cb22Y - 8));

            // Line to 22kV Bus
            dc.DrawLine(penWire22, new Point(bayX, cb22Y + 14), new Point(bayX, bus22Y));

            // Power flow particle
            float phase = ZeroAnimationClock.FluidPhase;
            double flowY = bus110Y + (bus22Y - bus110Y) * phase;
            dc.DrawEllipse(Brushes.White, null, new Point(bayX, flowY), 3.5, 3.5);

            // Feeder Outgoer Bay
            double feederX = sldLeft + sldWidth * 0.75;
            double cbFeedY = bus22Y - 45;
            dc.DrawLine(penWire22, new Point(feederX, bus22Y), new Point(feederX, cbFeedY + 14));
            DrawBreakerSymbolWpf(dc, feederX, cbFeedY, BreakerState.Closed, brush22);

            var feedLbl = CreateFormattedText("FEEDER-01", ZeroWpfTheme.RegularTypeface, 9.5, ZeroWpfTheme.TextSecondary, dpi);
            dc.DrawText(feedLbl, new Point(feederX + 16, cbFeedY - 8));

            dc.DrawLine(penWire22, new Point(feederX, cbFeedY), new Point(feederX, cbFeedY - 30));
            dc.DrawLine(penWire22, new Point(feederX - 6, cbFeedY - 30), new Point(feederX + 6, cbFeedY - 30));
        }

        private void DrawBreakerSymbolWpf(DrawingContext dc, double cx, double cy, BreakerState state, Brush brush)
        {
            double size = 14;
            Rect rect = new Rect(cx - size / 2, cy - size / 2, size, size);

            if (state == BreakerState.Closed)
            {
                dc.DrawRectangle(brush, null, rect);
            }
            else
            {
                dc.DrawRectangle(null, new Pen(brush, 1.5), rect);
            }
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZSingleLineDiagram"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("SingleLineDiagram is deprecated and will be removed in 5 release cycles. Please migrate to ZSingleLineDiagram instead.")]
    public class SingleLineDiagram : ZSingleLineDiagram { }

    /// <summary>
    /// Legacy alias for <see cref="ZSingleLineDiagram"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroSingleLineDiagram is deprecated and will be removed in 5 release cycles. Please migrate to ZSingleLineDiagram instead.")]
    public class ZeroSingleLineDiagram : ZSingleLineDiagram { }

    #endregion

}
