using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using ZeroUI.Core.Rendering;
using ZeroUI.Core.Scada;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Industrial
{
    public enum HeaterState
    {
        Off,
        Heating,
        Warning,
        OverheatTrip
    }

    /// <summary>
    /// Industrial electric heating element component with thermal glow pulse and temperature telemetry for ZeroUI WPF.
    /// Supports direct telemetry binding via <see cref="IScadaBindable"/>.
    /// </summary>
    public class ZIndustrialHeater : FrameworkElement, IScadaBindable
    {
        public static readonly DependencyProperty StateProperty =
            DependencyProperty.Register(nameof(State), typeof(HeaterState), typeof(ZIndustrialHeater),
                new FrameworkPropertyMetadata(HeaterState.Heating, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty TemperatureCProperty =
            DependencyProperty.Register(nameof(TemperatureC), typeof(double), typeof(ZIndustrialHeater),
                new FrameworkPropertyMetadata(185.4, FrameworkPropertyMetadataOptions.AffectsRender, OnTemperatureChanged));

        public static readonly DependencyProperty SetpointCProperty =
            DependencyProperty.Register(nameof(SetpointC), typeof(double), typeof(ZIndustrialHeater),
                new FrameworkPropertyMetadata(200.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty HighAlarmCProperty =
            DependencyProperty.Register(nameof(HighAlarmC), typeof(double), typeof(ZIndustrialHeater),
                new FrameworkPropertyMetadata(230.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty TagLabelProperty =
            DependencyProperty.Register(nameof(TagLabel), typeof(string), typeof(ZIndustrialHeater),
                new FrameworkPropertyMetadata("HTR-301", FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty BoundTagPathProperty =
            DependencyProperty.Register(nameof(BoundTagPath), typeof(string), typeof(ZIndustrialHeater),
                new FrameworkPropertyMetadata(null));

        public HeaterState State
        {
            get => (HeaterState)GetValue(StateProperty);
            set => SetValue(StateProperty, value);
        }

        public double TemperatureC
        {
            get => (double)GetValue(TemperatureCProperty);
            set => SetValue(TemperatureCProperty, value);
        }

        public double SetpointC
        {
            get => (double)GetValue(SetpointCProperty);
            set => SetValue(SetpointCProperty, value);
        }

        public double HighAlarmC
        {
            get => (double)GetValue(HighAlarmCProperty);
            set => SetValue(HighAlarmCProperty, value);
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

        private static void OnTemperatureChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZIndustrialHeater heater)
            {
                heater.CheckAlarmThresholds();
            }
        }

        private void CheckAlarmThresholds()
        {
            if (TemperatureC >= HighAlarmC) State = HeaterState.OverheatTrip;
            else if (TemperatureC >= SetpointC + 15.0) State = HeaterState.Warning;
            else if (TemperatureC > 30.0) State = HeaterState.Heating;
            else State = HeaterState.Off;
        }

        private float _glowPhase;
        private IDisposable? _clockToken;

        private static readonly Typeface SegoeBold = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        private static readonly Typeface SegoeNormal = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

        private static readonly Brush DarkText = Freeze(new SolidColorBrush(Color.FromRgb(248, 250, 252)));
        private static readonly Brush LightText = Freeze(new SolidColorBrush(Color.FromRgb(15, 23, 42)));
        private static readonly Brush WarningRed = Freeze(new SolidColorBrush(Color.FromRgb(239, 68, 68)));
        private static readonly Brush OffGray = Freeze(new SolidColorBrush(Color.FromRgb(148, 163, 184)));

        private static T Freeze<T>(T freezable) where T : Freezable
        {
            if (freezable.CanFreeze) freezable.Freeze();
            return freezable;
        }

        public ZIndustrialHeater()
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
            if (State != HeaterState.Heating && State != HeaterState.Warning) return;
            _glowPhase += (float)(deltaSeconds * 4.0);
            InvalidateVisual();
        }

        public void OnTagValueChanged(IScadaTag tag)
        {
            if (tag?.Value == null) return;
            if (double.TryParse(tag.Value.ToString(), out var temp))
            {
                TemperatureC = temp;
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            return new Size(150, 110);
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            bool isDark = ZeroWpfTheme.IsDark;
            float pulse = (float)(Math.Sin(_glowPhase) * 0.15 + 0.85);

            Color elementColor;
            switch (State)
            {
                case HeaterState.Heating:
                    byte r = (byte)Math.Min(255, 249 * pulse);
                    byte gr = (byte)Math.Min(255, 115 * pulse);
                    elementColor = Color.FromRgb(r, gr, 22);
                    break;
                case HeaterState.Warning:
                case HeaterState.OverheatTrip:
                    elementColor = Color.FromRgb(239, 68, 68);
                    break;
                default:
                    elementColor = isDark ? Color.FromRgb(100, 116, 139) : Color.FromRgb(148, 163, 184);
                    break;
            }

            var coilBrush = new SolidColorBrush(elementColor);
            coilBrush.Freeze();
            var coilPen = new Pen(coilBrush, 3.0) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            coilPen.Freeze();

            Brush textBrush = isDark ? DarkText : LightText;
            Brush boxBg = isDark ? Freeze(new SolidColorBrush(Color.FromRgb(45, 25, 20))) : Freeze(new SolidColorBrush(Color.FromRgb(254, 242, 242)));
            Pen boxPen = new Pen(coilBrush, 1.2);

            #if !NETFRAMEWORK
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            #endif

            // 1. Tag Label Header
            var tagText = new FormattedText(TagLabel, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, SegoeBold, 11, textBrush
            #if !NETFRAMEWORK
            , dpi
            #endif
            );
            dc.DrawText(tagText, new Point(8, 6));

            // 2. Housing Box
            var boxRect = new Rect(8, 24, w - 16, h - 48);
            dc.DrawRectangle(boxBg, boxPen, boxRect);

            // 3. Serpentine Heating Ribbon
            double coilLeft = boxRect.Left + 12;
            double coilRight = boxRect.Right - 12;
            double coilTop = boxRect.Top + 8;
            double coilBottom = boxRect.Bottom - 8;
            int loops = 5;
            double step = (coilRight - coilLeft) / loops;

            var path = new StreamGeometry();
            using (var ctx = path.Open())
            {
                ctx.BeginFigure(new Point(coilLeft, boxRect.Top), false, false);
                ctx.LineTo(new Point(coilLeft, coilBottom), true, false);

                for (int i = 0; i < loops; i++)
                {
                    double x1 = coilLeft + i * step;
                    double x2 = x1 + step;
                    double yPeak = (i % 2 == 0) ? coilTop : coilBottom;
                    ctx.LineTo(new Point(x2, yPeak), true, false);
                }
                ctx.LineTo(new Point(coilRight, boxRect.Top), true, false);
            }
            path.Freeze();
            dc.DrawGeometry(null, coilPen, path);

            // 4. Temperature Telemetry Readout
            string tempStr = $"{TemperatureC:0.0}°C";
            var tempText = new FormattedText(tempStr, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, SegoeBold, 10, coilBrush
            #if !NETFRAMEWORK
            , dpi
            #endif
            );
            dc.DrawText(tempText, new Point(8, h - 18));

            string spStr = $"SP: {SetpointC:0.0}°C";
            var spText = new FormattedText(spStr, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, SegoeNormal, 9.5, textBrush
            #if !NETFRAMEWORK
            , dpi
            #endif
            );
            dc.DrawText(spText, new Point(w - spText.Width - 8, h - 18));
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
    /// Legacy alias for <see cref="ZIndustrialHeater"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("IndustrialHeater is deprecated and will be removed in 5 release cycles. Please migrate to ZIndustrialHeater instead.")]
    public class IndustrialHeater : ZIndustrialHeater { }

    /// <summary>
    /// Legacy alias for <see cref="ZIndustrialHeater"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroIndustrialHeater is deprecated and will be removed in 5 release cycles. Please migrate to ZIndustrialHeater instead.")]
    public class ZeroIndustrialHeater : ZIndustrialHeater { }

    #endregion

}
