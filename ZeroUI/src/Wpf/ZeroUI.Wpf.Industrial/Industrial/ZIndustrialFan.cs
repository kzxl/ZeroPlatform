using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using ZeroUI.Core.Rendering;
using ZeroUI.Core.Scada;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Industrial
{
    public enum FanState
    {
        Stopped,
        Running,
        Fault
    }

    /// <summary>
    /// Industrial ventilation and exhaust fan component with dynamic vector blade rotation for ZeroUI WPF.
    /// Supports direct telemetry binding via <see cref="IScadaBindable"/>.
    /// </summary>
    public class ZIndustrialFan : FrameworkElement, IScadaBindable
    {
        public static readonly DependencyProperty StateProperty =
            DependencyProperty.Register(nameof(State), typeof(FanState), typeof(ZIndustrialFan),
                new FrameworkPropertyMetadata(FanState.Running, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty SpeedRpmProperty =
            DependencyProperty.Register(nameof(SpeedRpm), typeof(double), typeof(ZIndustrialFan),
                new FrameworkPropertyMetadata(1200.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty TagLabelProperty =
            DependencyProperty.Register(nameof(TagLabel), typeof(string), typeof(ZIndustrialFan),
                new FrameworkPropertyMetadata("FAN-201", FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty BoundTagPathProperty =
            DependencyProperty.Register(nameof(BoundTagPath), typeof(string), typeof(ZIndustrialFan),
                new FrameworkPropertyMetadata(null));

        public FanState State
        {
            get => (FanState)GetValue(StateProperty);
            set => SetValue(StateProperty, value);
        }

        public double SpeedRpm
        {
            get => (double)GetValue(SpeedRpmProperty);
            set => SetValue(SpeedRpmProperty, Math.Max(0, value));
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

        private float _bladeAngle;
        private IDisposable? _clockToken;

        private static readonly Typeface SegoeBold = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        private static readonly Typeface SegoeNormal = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

        private static readonly Brush DarkText = Freeze(new SolidColorBrush(Color.FromRgb(248, 250, 252)));
        private static readonly Brush LightText = Freeze(new SolidColorBrush(Color.FromRgb(15, 23, 42)));
        private static readonly Brush RunningCyan = Freeze(new SolidColorBrush(Color.FromRgb(56, 189, 248)));
        private static readonly Brush FaultRed = Freeze(new SolidColorBrush(Color.FromRgb(248, 113, 113)));
        private static readonly Brush StoppedGray = Freeze(new SolidColorBrush(Color.FromRgb(148, 163, 184)));
        private static readonly Brush FrameDark = Freeze(new SolidColorBrush(Color.FromRgb(30, 41, 59)));
        private static readonly Brush FrameLight = Freeze(new SolidColorBrush(Color.FromRgb(226, 232, 240)));
        private static readonly Pen WireDarkPen = Freeze(new Pen(new SolidColorBrush(Color.FromRgb(71, 85, 105)), 1.0));
        private static readonly Pen WireLightPen = Freeze(new Pen(new SolidColorBrush(Color.FromRgb(203, 213, 225)), 1.0));

        private static T Freeze<T>(T freezable) where T : Freezable
        {
            if (freezable.CanFreeze) freezable.Freeze();
            return freezable;
        }

        public ZIndustrialFan()
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
            if (State != FanState.Running || SpeedRpm <= 0) return;
            float step = (float)(SpeedRpm / 60.0 * 360.0 * deltaSeconds);
            _bladeAngle = (_bladeAngle + step) % 360f;
            InvalidateVisual();
        }

        public void OnTagValueChanged(IScadaTag tag)
        {
            if (tag?.Value == null) return;
            if (double.TryParse(tag.Value.ToString(), out var rpm))
            {
                SpeedRpm = rpm;
                State = rpm > 10 ? FanState.Running : FanState.Stopped;
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            return new Size(130, 140);
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            bool isDark = ZeroWpfTheme.IsDark;
            Brush bladeBrush = State switch
            {
                FanState.Running => RunningCyan,
                FanState.Fault => FaultRed,
                _ => StoppedGray
            };

            Brush frameBrush = isDark ? FrameDark : FrameLight;
            Brush textBrush = isDark ? DarkText : LightText;
            Pen wirePen = isDark ? WireDarkPen : WireLightPen;

            #if !NETFRAMEWORK
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            #endif

            // 1. Tag & Status Header
            var tagText = new FormattedText(TagLabel, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, SegoeBold, 11, textBrush
            #if !NETFRAMEWORK
            , dpi
            #endif
            );
            dc.DrawText(tagText, new Point(8, 6));

            // 2. Circular Outer Shroud
            double diameter = Math.Min(w - 20, h - 50);
            double cx = w * 0.5;
            double cy = 24 + diameter * 0.5;
            var shroudPen = new Pen(bladeBrush, 1.5);

            dc.DrawEllipse(frameBrush, shroudPen, new Point(cx, cy), diameter * 0.5, diameter * 0.5);

            // 3. Rotating Blades (4 aerodynamic blades)
            double bladeRadius = diameter * 0.42;
            dc.PushTransform(new RotateTransform(_bladeAngle, cx, cy));

            var bladeGeom = new StreamGeometry();
            using (var ctx = bladeGeom.Open())
            {
                ctx.BeginFigure(new Point(cx, cy - 6), true, true);
                ctx.BezierTo(new Point(cx + bladeRadius * 0.5, cy - 14), new Point(cx + bladeRadius * 0.8, cy - 18), new Point(cx + bladeRadius, cy), true, false);
                ctx.BezierTo(new Point(cx + bladeRadius * 0.7, cy + 6), new Point(cx + bladeRadius * 0.3, cy + 4), new Point(cx, cy + 6), true, false);
            }
            bladeGeom.Freeze();

            for (int i = 0; i < 4; i++)
            {
                dc.PushTransform(new RotateTransform(i * 90, cx, cy));
                dc.DrawGeometry(bladeBrush, null, bladeGeom);
                dc.Pop();
            }
            dc.Pop();

            // 4. Center Hub & Protective Wire Grille
            dc.DrawLine(wirePen, new Point(cx - diameter * 0.5, cy), new Point(cx + diameter * 0.5, cy));
            dc.DrawLine(wirePen, new Point(cx, cy - diameter * 0.5), new Point(cx, cy + diameter * 0.5));

            double hubRadius = diameter * 0.12;
            dc.DrawEllipse(frameBrush, shroudPen, new Point(cx, cy), hubRadius, hubRadius);

            // 5. Telemetry Footer
            string rpmStr = $"{SpeedRpm:0} RPM";
            var rpmText = new FormattedText(rpmStr, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, SegoeNormal, 10, textBrush
            #if !NETFRAMEWORK
            , dpi
            #endif
            );
            dc.DrawText(rpmText, new Point(cx - rpmText.Width / 2, h - 18));
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
    /// Legacy alias for <see cref="ZIndustrialFan"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("IndustrialFan is deprecated and will be removed in 5 release cycles. Please migrate to ZIndustrialFan instead.")]
    public class IndustrialFan : ZIndustrialFan { }

    /// <summary>
    /// Legacy alias for <see cref="ZIndustrialFan"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroIndustrialFan is deprecated and will be removed in 5 release cycles. Please migrate to ZIndustrialFan instead.")]
    public class ZeroIndustrialFan : ZIndustrialFan { }

    #endregion

}
