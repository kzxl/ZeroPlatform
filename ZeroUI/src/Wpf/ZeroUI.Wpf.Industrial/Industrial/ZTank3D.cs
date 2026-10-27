using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using ZeroUI.Core.Rendering;
using ZeroUI.Core.Scada;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Industrial
{
    /// <summary>
    /// 3D cylindrical storage vessel and process tank component with realistic fluid volume rendering,
    /// dynamic liquid surface wave motion, and telemetry readouts for ZeroUI WPF.
    /// </summary>
    public class ZTank3D : FrameworkElement, IScadaBindable
    {
        public static readonly DependencyProperty LevelPercentProperty =
            DependencyProperty.Register(nameof(LevelPercent), typeof(double), typeof(ZTank3D),
                new FrameworkPropertyMetadata(68.5, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty TemperatureCProperty =
            DependencyProperty.Register(nameof(TemperatureC), typeof(double), typeof(ZTank3D),
                new FrameworkPropertyMetadata(42.3, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty PressureBarProperty =
            DependencyProperty.Register(nameof(PressureBar), typeof(double), typeof(ZTank3D),
                new FrameworkPropertyMetadata(2.4, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty CapacityLitersProperty =
            DependencyProperty.Register(nameof(CapacityLiters), typeof(double), typeof(ZTank3D),
                new FrameworkPropertyMetadata(15000.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty MediumColorProperty =
            DependencyProperty.Register(nameof(MediumColor), typeof(Color), typeof(ZTank3D),
                new FrameworkPropertyMetadata(Color.FromRgb(14, 165, 233), FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty TagLabelProperty =
            DependencyProperty.Register(nameof(TagLabel), typeof(string), typeof(ZTank3D),
                new FrameworkPropertyMetadata("TK-401", FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty BoundTagPathProperty =
            DependencyProperty.Register(nameof(BoundTagPath), typeof(string), typeof(ZTank3D),
                new FrameworkPropertyMetadata(null));

        public double LevelPercent
        {
            get => (double)GetValue(LevelPercentProperty);
            set => SetValue(LevelPercentProperty, Math.Max(0.0, Math.Min(100.0, value)));
        }

        public double TemperatureC
        {
            get => (double)GetValue(TemperatureCProperty);
            set => SetValue(TemperatureCProperty, value);
        }

        public double PressureBar
        {
            get => (double)GetValue(PressureBarProperty);
            set => SetValue(PressureBarProperty, value);
        }

        public double CapacityLiters
        {
            get => (double)GetValue(CapacityLitersProperty);
            set => SetValue(CapacityLitersProperty, Math.Max(0.0, value));
        }

        public Color MediumColor
        {
            get => (Color)GetValue(MediumColorProperty);
            set => SetValue(MediumColorProperty, value);
        }

        public string TagLabel
        {
            get => (string)GetValue(TagLabelProperty);
            set => SetValue(TagLabelProperty, value ?? "");
        }

        public string? BoundTagPath
        {
            get => (string?)GetValue(BoundTagPathProperty);
            set => SetValue(BoundTagPathProperty, value);
        }

        private float _wavePhase;
        private IDisposable? _clockToken;

        private static readonly Typeface SegoeBold = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        private static readonly Typeface SegoeNormal = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

        private static readonly Brush DarkText = Freeze(new SolidColorBrush(Color.FromRgb(248, 250, 252)));
        private static readonly Brush LightText = Freeze(new SolidColorBrush(Color.FromRgb(15, 23, 42)));
        private static readonly Pen DarkBorderPen = Freeze(new Pen(new SolidColorBrush(Color.FromRgb(71, 85, 105)), 1.5));
        private static readonly Pen LightBorderPen = Freeze(new Pen(new SolidColorBrush(Color.FromRgb(203, 213, 225)), 1.5));

        private static T Freeze<T>(T freezable) where T : Freezable
        {
            if (freezable.CanFreeze) freezable.Freeze();
            return freezable;
        }

        public ZTank3D()
        {
            ClipToBounds = true;
            Loaded += (s, e) =>
            {
                _clockToken = ZeroAnimationClock.Subscribe(OnAnimationFrame);
                ZeroTagEngine.RegisterBindable(this);
            };
            Unloaded += (s, e) =>
            {
                _clockToken?.Dispose();
                _clockToken = null;
                ZeroTagEngine.UnregisterBindable(this);
            };
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnAnimationFrame(double deltaSeconds, long frameCount)
        {
            _wavePhase += (float)(deltaSeconds * 3.0);
            InvalidateVisual();
        }

        public void OnTagValueChanged(IScadaTag tag)
        {
            if (tag?.Value == null) return;
            if (double.TryParse(tag.Value.ToString(), out var lvl))
            {
                LevelPercent = lvl;
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            return new Size(160, 220);
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            bool isDark = ZeroWpfTheme.IsDark;
            Brush textBrush = isDark ? DarkText : LightText;
            Pen borderPen = isDark ? DarkBorderPen : LightBorderPen;

            #if !NETFRAMEWORK
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            #endif

            // 1. Tag Label Header
            var tagText = new FormattedText(TagLabel, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, SegoeBold, 12, textBrush
            #if !NETFRAMEWORK
            , dpi
            #endif
            );
            dc.DrawText(tagText, new Point(10, 6));

            // 2. Tank Vessel Geometry
            double tankLeft = 14;
            double tankTop = 32;
            double tankWidth = Math.Min(w * 0.58, 90.0);
            double tankHeight = h - 65;
            double tankRight = tankLeft + tankWidth;
            double tankBottom = tankTop + tankHeight;
            double ellipseHeight = tankWidth * 0.28;

            var tankRect = new Rect(tankLeft, tankTop, tankWidth, tankHeight);

            // Metallic Wall Gradient
            var wallBrush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 0)
            };
            if (isDark)
            {
                wallBrush.GradientStops.Add(new GradientStop(Color.FromRgb(30, 41, 59), 0.0));
                wallBrush.GradientStops.Add(new GradientStop(Color.FromRgb(71, 85, 105), 0.45));
                wallBrush.GradientStops.Add(new GradientStop(Color.FromRgb(15, 23, 42), 1.0));
            }
            else
            {
                wallBrush.GradientStops.Add(new GradientStop(Color.FromRgb(226, 232, 240), 0.0));
                wallBrush.GradientStops.Add(new GradientStop(Color.FromRgb(255, 255, 255), 0.45));
                wallBrush.GradientStops.Add(new GradientStop(Color.FromRgb(148, 163, 184), 1.0));
            }
            wallBrush.Freeze();

            // Draw Vessel Body Cylinder
            dc.DrawRectangle(wallBrush, borderPen, new Rect(tankLeft, tankTop + ellipseHeight * 0.5, tankWidth, tankHeight - ellipseHeight));

            // Top Cap Ellipse
            dc.DrawEllipse(wallBrush, borderPen, new Point(tankLeft + tankWidth * 0.5, tankTop + ellipseHeight * 0.5), tankWidth * 0.5, ellipseHeight * 0.5);

            // Bottom Dish Ellipse
            dc.DrawEllipse(wallBrush, borderPen, new Point(tankLeft + tankWidth * 0.5, tankBottom - ellipseHeight * 0.5), tankWidth * 0.5, ellipseHeight * 0.5);

            // 3. Fluid Volume
            double maxFluidHeight = tankHeight - ellipseHeight;
            double fluidHeight = maxFluidHeight * (LevelPercent / 100.0);
            double fluidTop = tankBottom - ellipseHeight * 0.5 - fluidHeight;

            if (fluidHeight > 0)
            {
                var fluidBrush = new LinearGradientBrush(MediumColor, Color.FromArgb(180, MediumColor.R, MediumColor.G, MediumColor.B), 90.0);
                fluidBrush.Freeze();

                // Fluid Rectangle
                dc.DrawRectangle(fluidBrush, null, new Rect(tankLeft + 2, fluidTop, tankWidth - 4, fluidHeight));

                // Fluid Surface Ellipse (With slight wave pulse)
                double waveOffset = Math.Sin(_wavePhase) * 1.5;
                var surfaceBrush = new SolidColorBrush(Color.FromArgb(220, MediumColor.R, MediumColor.G, MediumColor.B));
                surfaceBrush.Freeze();
                dc.DrawEllipse(surfaceBrush, null, new Point(tankLeft + tankWidth * 0.5, fluidTop + waveOffset), (tankWidth - 4) * 0.5, ellipseHeight * 0.45);
            }

            // 4. Sight Glass Level Gauge (Right side of vessel)
            double glassX = tankRight + 8;
            double glassTop = tankTop + ellipseHeight * 0.5;
            double glassH = tankHeight - ellipseHeight;

            dc.DrawRectangle(isDark ? Freeze(new SolidColorBrush(Color.FromRgb(15, 23, 42))) : Freeze(new SolidColorBrush(Color.FromRgb(241, 245, 249))), borderPen, new Rect(glassX, glassTop, 8, glassH));
            double fluidGlassH = glassH * (LevelPercent / 100.0);
            dc.DrawRectangle(new SolidColorBrush(MediumColor), null, new Rect(glassX + 1, glassTop + (glassH - fluidGlassH), 6, fluidGlassH));

            // 5. Telemetry Readouts (Right column)
            double rx = w * 0.76;
            var lvlText = new FormattedText($"{LevelPercent:0.0}%", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, SegoeBold, 13, textBrush
            #if !NETFRAMEWORK
            , dpi
            #endif
            );
            dc.DrawText(lvlText, new Point(rx - lvlText.Width / 2, tankTop + 8));

            double currentVol = CapacityLiters * (LevelPercent / 100.0);
            var volText = new FormattedText($"{currentVol:0} L", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, SegoeNormal, 10, textBrush
            #if !NETFRAMEWORK
            , dpi
            #endif
            );
            dc.DrawText(volText, new Point(rx - volText.Width / 2, tankTop + 28));

            var tempText = new FormattedText($"{TemperatureC:0.0}°C", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, SegoeNormal, 10, textBrush
            #if !NETFRAMEWORK
            , dpi
            #endif
            );
            dc.DrawText(tempText, new Point(rx - tempText.Width / 2, tankTop + 48));

            var pText = new FormattedText($"{PressureBar:0.00} bar", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, SegoeNormal, 10, textBrush
            #if !NETFRAMEWORK
            , dpi
            #endif
            );
            dc.DrawText(pText, new Point(rx - pText.Width / 2, tankTop + 68));
        }
    
    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        ZeroWpfTheme.ThemeChanged += OnThemeChanged;
    }

    private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        ZeroWpfTheme.ThemeChanged -= OnThemeChanged;
    }
    private void OnThemeChanged() => InvalidateVisual();
}

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZTank3D"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("Tank3D is deprecated and will be removed in 5 release cycles. Please migrate to ZTank3D instead.")]
    public class Tank3D : ZTank3D { }

    /// <summary>
    /// Legacy alias for <see cref="ZTank3D"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroTank3D is deprecated and will be removed in 5 release cycles. Please migrate to ZTank3D instead.")]
    public class ZeroTank3D : ZTank3D { }

    #endregion

}
