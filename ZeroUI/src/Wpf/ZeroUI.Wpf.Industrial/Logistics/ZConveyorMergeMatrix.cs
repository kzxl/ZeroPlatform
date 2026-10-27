using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using ZeroUI.Core.Logistics;
using ZeroUI.Core.Rendering;
using ZeroUI.Wpf.Base;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Logistics
{
    /// <summary>
    /// High-speed parcel sorting conveyor merge visualizer for WPF.
    /// Renders animated conveyor belts, infrared photo-eyes, moving parcels,
    /// divert chutes, and live sorting throughput (PPH).
    /// </summary>
    public class ZConveyorMergeMatrix : ZeroWpfVisualBase
    {
        private readonly ConveyorMergeEngine _engine = new ConveyorMergeEngine();
        private string _lineName = "Main Infeed & Merge Line 1";

        protected override bool AutoAnimate => true;

        protected override void OnAnimationTick(double delta, long frame)
        {
            _engine.AdvanceParcels(delta);
        }

        public ZConveyorMergeMatrix()
        {
        }

        #region Properties

        public ConveyorMergeEngine Engine => _engine;

        public string LineName
        {
            get => _lineName;
            set
            {
                _lineName = value;
                InvalidateVisual();
            }
        }

        public double MainSpeedMpm
        {
            get => _engine.MainSpeedMpm;
            set
            {
                _engine.MainSpeedMpm = Math.Max(0, value);
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

            // Header
            var titleFt = CreateFormattedText(_lineName, ZeroWpfTheme.BoldTypeface, 11.5, ZeroWpfTheme.TextPrimary, dpi);
            dc.DrawText(titleFt, new Point(16, 12));

            double hudX = w - 360;
            if (hudX > 200)
            {
                string speedStr = $"Speed: {_engine.MainSpeedMpm:F0} m/min | Parcels: {_engine.Parcels.Count}";
                var speedFt = CreateFormattedText(speedStr, ZeroWpfTheme.RegularTypeface, 9.5, ZeroWpfTheme.TextSecondary, dpi);
                dc.DrawText(speedFt, new Point(hudX, 14));

                // PPH Badge
                Rect badgeRect = new Rect(w - 110, 10, 94, 24);
                dc.DrawRectangle(ZeroWpfTheme.BgCard, new Pen(Brushes.MediumSeaGreen, 1.0), badgeRect);
                var pphFt = CreateFormattedText($"{_engine.HourlyThroughputPph:N0} PPH", ZeroWpfTheme.BoldTypeface, 9.0, Brushes.MediumSeaGreen, dpi);
                dc.DrawText(pphFt, new Point(badgeRect.Left + 8, badgeRect.Top + 4));
            }

            // Conveyor Geometry
            double beltLeft = 24;
            double beltTop = 90;
            double beltW = w - 48;
            double beltH = 46;

            if (beltW < 160)
                return;

            Rect beltRect = new Rect(beltLeft, beltTop, beltW, beltH);

            // 1. Divert Chutes (Angled upwards)
            DrawDivertChutes(dc, beltRect, dpi);

            // 2. Merge Feeder
            DrawMergeFeeder(dc, beltRect, dpi);

            // 3. Main Belt Bed & Rollers
            DrawConveyorBelt(dc, beltRect);

            // 4. Photo-Eyes
            DrawPhotoEyes(dc, beltRect, dpi);

            // 5. Parcels
            DrawParcels(dc, beltRect, dpi);
        }

        private void DrawConveyorBelt(DrawingContext dc, Rect belt)
        {
            dc.DrawRectangle(ZeroWpfTheme.BgCard, ZeroWpfTheme.BorderPen, belt);

            // Animated slats
            float phase = ZeroAnimationClock.FluidPhase;
            double spacing = 22.0;
            double offset = phase * spacing;

            Pen slatPen = new Pen(ZeroWpfTheme.BorderSubtle, 1.2);
            slatPen.Freeze();

            for (double x = belt.Left + offset; x < belt.Right; x += spacing)
            {
                dc.DrawLine(slatPen, new Point(x, belt.Top + 2), new Point(x, belt.Bottom - 2));
            }
        }

        private void DrawDivertChutes(DrawingContext dc, Rect belt, double dpi)
        {
            Pen chutePen = new Pen(Brushes.SlateGray, 1.2);
            chutePen.Freeze();

            for (int i = 0; i < _engine.Chutes.Count; i++)
            {
                var ch = _engine.Chutes[i];
                double cx = belt.Left + ch.PositionRatio * belt.Width;

                PathGeometry geom = new PathGeometry();
                PathFigure fig = new PathFigure { StartPoint = new Point(cx - 14, belt.Top) };
                fig.Segments.Add(new LineSegment(new Point(cx + 14, belt.Top), true));
                fig.Segments.Add(new LineSegment(new Point(cx + 36, belt.Top - 36), true));
                fig.Segments.Add(new LineSegment(new Point(cx + 8, belt.Top - 36), true));
                fig.IsClosed = true;
                geom.Figures.Add(fig);

                Brush chuteBrush = ch.IsDiverting ? Brushes.Orange : ZeroWpfTheme.BgHover;
                dc.DrawGeometry(chuteBrush, chutePen, geom);

                var chFt = CreateFormattedText($"Chute {ch.ChuteId} ({ch.DivertedCount})", ZeroWpfTheme.RegularTypeface, 7.5, ZeroWpfTheme.TextSecondary, dpi);
                dc.DrawText(chFt, new Point(cx - 10, belt.Top - 50));
            }
        }

        private void DrawMergeFeeder(DrawingContext dc, Rect belt, double dpi)
        {
            double mergeX = belt.Left + belt.Width * 0.30;
            PathGeometry geom = new PathGeometry();
            PathFigure fig = new PathFigure { StartPoint = new Point(belt.Left + 16, belt.Bottom + 40) };
            fig.Segments.Add(new LineSegment(new Point(belt.Left + 48, belt.Bottom + 40), true));
            fig.Segments.Add(new LineSegment(new Point(mergeX, belt.Bottom), true));
            fig.Segments.Add(new LineSegment(new Point(mergeX - 24, belt.Bottom), true));
            fig.IsClosed = true;
            geom.Figures.Add(fig);

            dc.DrawGeometry(ZeroWpfTheme.BgHover, ZeroWpfTheme.BorderPen, geom);

            var ft = CreateFormattedText("Infeed Spur", ZeroWpfTheme.RegularTypeface, 7.5, ZeroWpfTheme.TextMuted, dpi);
            dc.DrawText(ft, new Point(belt.Left + 18, belt.Bottom + 44));
        }

        private void DrawPhotoEyes(DrawingContext dc, Rect belt, double dpi)
        {
            for (int i = 0; i < _engine.Sensors.Count; i++)
            {
                var pe = _engine.Sensors[i];
                double px = belt.Left + pe.PositionRatio * belt.Width;

                Brush beamBrush = pe.IsJamAlert ? Brushes.Red : (pe.IsBlocked ? Brushes.Tomato : Brushes.LimeGreen);
                Pen beamPen = new Pen(beamBrush, 1.8);
                beamPen.Freeze();

                // Bracket blocks
                dc.DrawRectangle(Brushes.Gray, null, new Rect(px - 2.5, belt.Top - 6, 5, 6));
                dc.DrawRectangle(Brushes.Gray, null, new Rect(px - 2.5, belt.Bottom, 5, 6));

                // Beam
                dc.DrawLine(beamPen, new Point(px, belt.Top), new Point(px, belt.Bottom));

                var nameFt = CreateFormattedText(pe.Name, ZeroWpfTheme.RegularTypeface, 7.0, beamBrush, dpi);
                dc.DrawText(nameFt, new Point(px - nameFt.Width / 2, belt.Bottom + 10));
            }
        }

        private void DrawParcels(DrawingContext dc, Rect belt, double dpi)
        {
            for (int i = 0; i < _engine.Parcels.Count; i++)
            {
                var p = _engine.Parcels[i];
                double px = belt.Left + p.PositionRatio * belt.Width;
                double py = belt.Top + 6;
                double pw = 30;
                double ph = belt.Height - 12;

                Rect pRect = new Rect(px - pw / 2, py, pw, ph);
                Brush cartonBrush = p.IsDiverted ? Brushes.Orange : Brushes.SkyBlue;

                dc.DrawRectangle(cartonBrush, new Pen(Brushes.White, 1.0), pRect);

                var badgeFt = CreateFormattedText($"C{p.DestinationChuteId}", ZeroWpfTheme.BoldTypeface, 7.5, Brushes.White, dpi);
                dc.DrawText(badgeFt, new Point(px - badgeFt.Width / 2, py + (ph - badgeFt.Height) / 2));
            }
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZConveyorMergeMatrix"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ConveyorMergeMatrix is deprecated and will be removed in 5 release cycles. Please migrate to ZConveyorMergeMatrix instead.")]
    public class ConveyorMergeMatrix : ZConveyorMergeMatrix { }

    /// <summary>
    /// Legacy alias for <see cref="ZConveyorMergeMatrix"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroConveyorMergeMatrix is deprecated and will be removed in 5 release cycles. Please migrate to ZConveyorMergeMatrix instead.")]
    public class ZeroConveyorMergeMatrix : ZConveyorMergeMatrix { }

    #endregion

}
