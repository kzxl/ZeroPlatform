using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Scada;
using ZeroUI.Core.Scada.Safety;

namespace ZeroUI.Wpf.Industrial
{
    /// <summary>
    /// Industrial safety optical light curtain compliant with IEC 61496-1/-2 (Type 4 ESPE) for ZeroUI WPF.
    /// Features safety-yellow extruded housing, multi-beam optical array, dual OSSD output status,
    /// interactive obstacle simulation, and muting/blanking support.
    /// </summary>
    public class ZLightCurtainBar : FrameworkElement, IScadaBindable
    {
        #region Dependency Properties

        public static readonly DependencyProperty BeamCountProperty =
            DependencyProperty.Register(nameof(BeamCount), typeof(int), typeof(ZLightCurtainBar),
                new FrameworkPropertyMetadata(16, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty CurtainStateProperty =
            DependencyProperty.Register(nameof(CurtainState), typeof(LightCurtainState), typeof(ZLightCurtainBar),
                new FrameworkPropertyMetadata(LightCurtainState.Clear, FrameworkPropertyMetadataOptions.AffectsRender, OnCurtainStateChanged));

        public static readonly DependencyProperty RoleProperty =
            DependencyProperty.Register(nameof(Role), typeof(LightCurtainRole), typeof(ZLightCurtainBar),
                new FrameworkPropertyMetadata(LightCurtainRole.IntegratedPair, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty Ossd1ActiveProperty =
            DependencyProperty.Register(nameof(Ossd1Active), typeof(bool), typeof(ZLightCurtainBar),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty Ossd2ActiveProperty =
            DependencyProperty.Register(nameof(Ossd2Active), typeof(bool), typeof(ZLightCurtainBar),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty InterruptedBeamIndexProperty =
            DependencyProperty.Register(nameof(InterruptedBeamIndex), typeof(int), typeof(ZLightCurtainBar),
                new FrameworkPropertyMetadata(-1, FrameworkPropertyMetadataOptions.AffectsRender, OnInterruptionChanged));

        public static readonly DependencyProperty InterruptedBeamCountProperty =
            DependencyProperty.Register(nameof(InterruptedBeamCount), typeof(int), typeof(ZLightCurtainBar),
                new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender, OnInterruptionChanged));

        public static readonly DependencyProperty ProtectionHeightMmProperty =
            DependencyProperty.Register(nameof(ProtectionHeightMm), typeof(double), typeof(ZLightCurtainBar),
                new FrameworkPropertyMetadata(600.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ResolutionMmProperty =
            DependencyProperty.Register(nameof(ResolutionMm), typeof(double), typeof(ZLightCurtainBar),
                new FrameworkPropertyMetadata(30.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty InteractiveSimulationProperty =
            DependencyProperty.Register(nameof(InteractiveSimulation), typeof(bool), typeof(ZLightCurtainBar),
                new FrameworkPropertyMetadata(true));

        public static readonly DependencyProperty HousingColorProperty =
            DependencyProperty.Register(nameof(HousingColor), typeof(Color), typeof(ZLightCurtainBar),
                new FrameworkPropertyMetadata(Color.FromRgb(245, 196, 0), FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty BoundTagPathProperty =
            DependencyProperty.Register(nameof(BoundTagPath), typeof(string), typeof(ZLightCurtainBar),
                new FrameworkPropertyMetadata(null));

        #endregion

        #region Properties

        public int BeamCount
        {
            get => (int)GetValue(BeamCountProperty);
            set => SetValue(BeamCountProperty, Math.Max(4, Math.Min(64, value)));
        }

        public LightCurtainState CurtainState
        {
            get => (LightCurtainState)GetValue(CurtainStateProperty);
            set => SetValue(CurtainStateProperty, value);
        }

        public LightCurtainRole Role
        {
            get => (LightCurtainRole)GetValue(RoleProperty);
            set => SetValue(RoleProperty, value);
        }

        public bool Ossd1Active
        {
            get => (bool)GetValue(Ossd1ActiveProperty);
            set => SetValue(Ossd1ActiveProperty, value);
        }

        public bool Ossd2Active
        {
            get => (bool)GetValue(Ossd2ActiveProperty);
            set => SetValue(Ossd2ActiveProperty, value);
        }

        public int InterruptedBeamIndex
        {
            get => (int)GetValue(InterruptedBeamIndexProperty);
            set => SetValue(InterruptedBeamIndexProperty, value);
        }

        public int InterruptedBeamCount
        {
            get => (int)GetValue(InterruptedBeamCountProperty);
            set => SetValue(InterruptedBeamCountProperty, value);
        }

        public double ProtectionHeightMm
        {
            get => (double)GetValue(ProtectionHeightMmProperty);
            set => SetValue(ProtectionHeightMmProperty, Math.Max(100.0, value));
        }

        public double ResolutionMm
        {
            get => (double)GetValue(ResolutionMmProperty);
            set => SetValue(ResolutionMmProperty, Math.Max(10.0, value));
        }

        public bool InteractiveSimulation
        {
            get => (bool)GetValue(InteractiveSimulationProperty);
            set => SetValue(InteractiveSimulationProperty, value);
        }

        public Color HousingColor
        {
            get => (Color)GetValue(HousingColorProperty);
            set => SetValue(HousingColorProperty, value);
        }

        public string? BoundTagPath
        {
            get => (string?)GetValue(BoundTagPathProperty);
            set => SetValue(BoundTagPathProperty, value);
        }

        #endregion

        #region Events

        public event EventHandler<LightCurtainTrippedEventArgs>? Tripped;
        public event EventHandler? Cleared;

        #endregion

        private static readonly Typeface BoldTypeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        private static readonly Typeface RegularTypeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

        static ZLightCurtainBar()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ZLightCurtainBar), new FrameworkPropertyMetadata(typeof(ZLightCurtainBar)));
        }

        public ZLightCurtainBar()
        {
            Width = 180;
            Height = 280;
            Cursor = Cursors.Hand;
        }

        private static void OnCurtainStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZLightCurtainBar bar)
            {
                bool safe = bar.CurtainState == LightCurtainState.Clear || bar.CurtainState == LightCurtainState.Muted;
                bar.Ossd1Active = safe;
                bar.Ossd2Active = safe;
            }
        }

        private static void OnInterruptionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZLightCurtainBar bar)
            {
                if (bar.InterruptedBeamIndex >= 0 && bar.InterruptedBeamCount > 0)
                {
                    bar.CurtainState = LightCurtainState.Tripped;
                }
                else
                {
                    bar.CurtainState = LightCurtainState.Clear;
                }
            }
        }

        public void SimulateObstacle(int beamIndex, int beamSpan = 2)
        {
            if (beamIndex >= 0 && beamIndex < BeamCount)
            {
                InterruptedBeamIndex = beamIndex;
                InterruptedBeamCount = Math.Max(1, Math.Min(beamSpan, BeamCount - beamIndex));
                CurtainState = LightCurtainState.Tripped;
                Tripped?.Invoke(this, new LightCurtainTrippedEventArgs(InterruptedBeamIndex, InterruptedBeamCount));
            }
        }

        public void ClearObstacle()
        {
            if (InterruptedBeamIndex != -1 || CurtainState == LightCurtainState.Tripped)
            {
                InterruptedBeamIndex = -1;
                InterruptedBeamCount = 0;
                CurtainState = LightCurtainState.Clear;
                Cleared?.Invoke(this, EventArgs.Empty);
            }
        }

        public void OnTagValueChanged(IScadaTag tag)
        {
            if (tag?.Value == null) return;
            string valStr = tag.Value?.ToString() ?? string.Empty;
            if (Enum.TryParse<LightCurtainState>(valStr, true, out var st))
            {
                CurtainState = st;
            }
            else if (bool.TryParse(valStr, out var b))
            {
                CurtainState = b ? LightCurtainState.Clear : LightCurtainState.Tripped;
            }
        }

        #region Mouse Interaction

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);

            if (!InteractiveSimulation) return;

            Point pt = e.GetPosition(this);
            double topY = 40.0;
            double botY = ActualHeight - 36.0;
            double totalH = botY - topY;

            if (pt.Y >= topY && pt.Y <= botY)
            {
                double frac = (pt.Y - topY) / totalH;
                int clickedBeam = (int)(frac * BeamCount);
                clickedBeam = Math.Max(0, Math.Min(BeamCount - 1, clickedBeam));

                if (CurtainState == LightCurtainState.Tripped &&
                    clickedBeam >= InterruptedBeamIndex &&
                    clickedBeam < InterruptedBeamIndex + InterruptedBeamCount)
                {
                    ClearObstacle();
                }
                else
                {
                    SimulateObstacle(clickedBeam, 2);
                }
                e.Handled = true;
            }
        }

        #endregion

        #region Rendering

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double w = ActualWidth;
            double h = ActualHeight;
            if (w < 40 || h < 60) return;

            // Background
            var bgBrush = new SolidColorBrush(Color.FromRgb(20, 22, 26));
            bgBrush.Freeze();
            dc.DrawRectangle(bgBrush, null, new Rect(0, 0, w, h));

            // Header
            DrawHeader(dc, w);

            // Optical Window
            double topY = 40.0;
            double botY = h - 34.0;
            double opticalH = botY - topY;

            if (Role == LightCurtainRole.IntegratedPair)
            {
                DrawIntegratedPair(dc, w, topY, botY, opticalH);
            }
            else
            {
                DrawSingleBar(dc, w, topY, botY, opticalH);
            }

            // Footer
            DrawFooter(dc, w, h);
        }

        private void DrawHeader(DrawingContext dc, double w)
        {
            var headerBrush = new SolidColorBrush(Color.FromRgb(28, 32, 38));
            headerBrush.Freeze();
            dc.DrawRectangle(headerBrush, null, new Rect(0, 0, w, 36.0));

            var borderPen = new Pen(new SolidColorBrush(Color.FromRgb(48, 52, 60)), 1.0);
            borderPen.Freeze();
            dc.DrawLine(borderPen, new Point(0, 36.0), new Point(w, 36.0));

            // Title
            var titleBrush = new SolidColorBrush(Color.FromRgb(220, 225, 230));
            titleBrush.Freeze();
            var titleFt = CreateFormattedText("TYPE 4 ESPE", BoldTypeface, 9.0, titleBrush);
            dc.DrawText(titleFt, new Point(8.0, 10.0));

            // OSSD Pill
            Color ossdCol = Ossd1Active && Ossd2Active ? Color.FromRgb(40, 200, 60) : Color.FromRgb(235, 40, 45);
            string ossdText = Ossd1Active && Ossd2Active ? "OSSD ON" : "TRIPPED";

            var pillBrush = new SolidColorBrush(Color.FromArgb(50, ossdCol.R, ossdCol.G, ossdCol.B));
            pillBrush.Freeze();
            var pillPen = new Pen(new SolidColorBrush(ossdCol), 1.5);
            pillPen.Freeze();

            var pillRect = new Rect(w - 78.0, 6.0, 70.0, 22.0);
            dc.DrawRectangle(pillBrush, pillPen, pillRect);

            var statusBrush = new SolidColorBrush(ossdCol);
            statusBrush.Freeze();
            var statusFt = CreateFormattedText(ossdText, BoldTypeface, 9.0, statusBrush);
            dc.DrawText(statusFt, new Point(pillRect.X + (pillRect.Width - statusFt.Width) / 2.0, pillRect.Y + (pillRect.Height - statusFt.Height) / 2.0));
        }

        private void DrawIntegratedPair(DrawingContext dc, double w, double topY, double botY, double opticalH)
        {
            double barW = 28.0;
            double txLeft = 14.0;
            double rxLeft = w - barW - 14.0;

            DrawCurtainHousing(dc, txLeft, topY - 2.0, barW, opticalH + 4.0, "TX");
            DrawCurtainHousing(dc, rxLeft, topY - 2.0, barW, opticalH + 4.0, "RX");

            double beamSpan = opticalH / BeamCount;
            double beamLeft = txLeft + barW - 2.0;
            double beamRight = rxLeft + 2.0;

            for (int i = 0; i < BeamCount; i++)
            {
                double beamY = topY + i * beamSpan + beamSpan / 2.0;
                bool isBroken = CurtainState == LightCurtainState.Tripped &&
                                i >= InterruptedBeamIndex &&
                                i < InterruptedBeamIndex + InterruptedBeamCount;

                Color beamCol;
                double penThickness = 1.5;

                if (isBroken)
                {
                    beamCol = Color.FromRgb(255, 30, 40);
                    penThickness = 2.5;
                }
                else if (CurtainState == LightCurtainState.Muted)
                {
                    beamCol = Color.FromRgb(240, 180, 20);
                }
                else if (CurtainState == LightCurtainState.Blanked)
                {
                    beamCol = Color.FromRgb(160, 60, 220);
                }
                else
                {
                    beamCol = Color.FromRgb(40, 220, 90);
                }

                var beamPen = new Pen(new SolidColorBrush(beamCol), penThickness);
                beamPen.Freeze();

                if (isBroken)
                {
                    double obsX = (beamLeft + beamRight) / 2.0;
                    dc.DrawLine(beamPen, new Point(beamLeft, beamY), new Point(obsX - 12.0, beamY));
                    dc.DrawLine(beamPen, new Point(obsX + 12.0, beamY), new Point(beamRight, beamY));

                    // Obstacle icon
                    var obsBrush = new SolidColorBrush(Color.FromRgb(235, 40, 45));
                    obsBrush.Freeze();
                    dc.DrawEllipse(obsBrush, null, new Point(obsX, beamY), 9.0, 6.0);
                }
                else
                {
                    dc.DrawLine(beamPen, new Point(beamLeft, beamY), new Point(beamRight, beamY));
                }

                // LED lenses on faces
                Brush ledBrush = isBroken ? Brushes.Crimson : Brushes.LimeGreen;
                dc.DrawEllipse(ledBrush, null, new Point(txLeft + barW - 3.5, beamY), 2.5, 2.5);
                dc.DrawEllipse(ledBrush, null, new Point(rxLeft + 3.5, beamY), 2.5, 2.5);
            }
        }

        private void DrawSingleBar(DrawingContext dc, double w, double topY, double botY, double opticalH)
        {
            double barW = 34.0;
            double barX = (w - barW) / 2.0;
            string roleLabel = Role == LightCurtainRole.Transmitter ? "TX" : "RX";

            DrawCurtainHousing(dc, barX, topY - 2.0, barW, opticalH + 4.0, roleLabel);

            double beamSpan = opticalH / BeamCount;
            for (int i = 0; i < BeamCount; i++)
            {
                double beamY = topY + i * beamSpan + beamSpan / 2.0;
                bool isBroken = CurtainState == LightCurtainState.Tripped &&
                                i >= InterruptedBeamIndex &&
                                i < InterruptedBeamIndex + InterruptedBeamCount;

                Brush ledBrush = isBroken ? Brushes.Crimson : Brushes.LimeGreen;
                dc.DrawEllipse(ledBrush, null, new Point(barX + barW / 2.0, beamY), 4.0, 4.0);
            }
        }

        private void DrawCurtainHousing(DrawingContext dc, double x, double y, double w, double h, string label)
        {
            var housingBrush = new LinearGradientBrush(
                HousingColor,
                Color.FromRgb(210, 165, 0),
                new Point(0, 0), new Point(1, 0));
            housingBrush.Freeze();
            var housingPen = new Pen(new SolidColorBrush(Color.FromRgb(170, 130, 0)), 1.5);
            housingPen.Freeze();

            var rect = new Rect(x, y, w, h);
            dc.DrawRectangle(housingBrush, housingPen, rect);

            // Black End Caps
            var capBrush = new SolidColorBrush(Color.FromRgb(24, 26, 30));
            capBrush.Freeze();
            dc.DrawRectangle(capBrush, null, new Rect(x, y, w, 8.0));
            dc.DrawRectangle(capBrush, null, new Rect(x, y + h - 8.0, w, 8.0));

            // Optical Window
            double lensInset = 4.0;
            var lensBrush = new SolidColorBrush(Color.FromRgb(20, 22, 26));
            lensBrush.Freeze();
            dc.DrawRectangle(lensBrush, null, new Rect(x + lensInset, y + 10.0, w - lensInset * 2.0, h - 20.0));

            // Label
            var labelBrush = new SolidColorBrush(Color.FromRgb(24, 26, 30));
            labelBrush.Freeze();
            var labelFt = CreateFormattedText(label, BoldTypeface, 8.0, labelBrush);
            dc.DrawText(labelFt, new Point(x + (w - labelFt.Width) / 2.0, y + 1.0));
        }

        private void DrawFooter(DrawingContext dc, double w, double h)
        {
            double footerY = h - 30.0;
            var footerBrush = new SolidColorBrush(Color.FromRgb(24, 26, 30));
            footerBrush.Freeze();
            dc.DrawRectangle(footerBrush, null, new Rect(0, 0, w, 30.0));

            var borderPen = new Pen(new SolidColorBrush(Color.FromRgb(44, 48, 56)), 1.0);
            borderPen.Freeze();
            dc.DrawLine(borderPen, new Point(0, footerY), new Point(w, footerY));

            var textBrush = new SolidColorBrush(Color.FromRgb(160, 165, 175));
            textBrush.Freeze();

            string infoLeft = $"H: {ProtectionHeightMm:F0}mm | d: {ResolutionMm:F0}mm";
            var leftFt = CreateFormattedText(infoLeft, RegularTypeface, 8.5, textBrush);
            dc.DrawText(leftFt, new Point(8.0, footerY + 8.0));

            string infoRight = $"{BeamCount} Beams";
            var rightFt = CreateFormattedText(infoRight, RegularTypeface, 8.5, textBrush);
            dc.DrawText(rightFt, new Point(w - 8.0 - rightFt.Width, footerY + 8.0));
        }

        private static FormattedText CreateFormattedText(string text, Typeface typeface, double fontSize, Brush brush)
        {
#if NETFRAMEWORK
            return new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, fontSize, brush);
#else
            return new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, fontSize, brush, 1.0);
#endif
        }

        #endregion
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZLightCurtainBar"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("LightCurtainBar is deprecated and will be removed in 5 release cycles. Please migrate to ZLightCurtainBar instead.")]
    public class LightCurtainBar : ZLightCurtainBar { }

    /// <summary>
    /// Legacy alias for <see cref="ZLightCurtainBar"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroLightCurtainBar is deprecated and will be removed in 5 release cycles. Please migrate to ZLightCurtainBar instead.")]
    public class ZeroLightCurtainBar : ZLightCurtainBar { }

    #endregion

}
