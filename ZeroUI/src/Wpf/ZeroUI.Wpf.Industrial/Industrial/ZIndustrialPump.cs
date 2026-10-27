using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using ZeroUI.Core.Rendering;
using ZeroUI.Core.Scada;
using ZeroUI.Core.Scada.Safety;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Industrial
{
    public enum PumpState
    {
        Stopped,
        Running,
        Trip
    }

    /// <summary>
    /// Industrial standard P&ID centrifugal pump component for ZeroUI WPF.
    /// Features smooth vector impeller rotation, status badges, and SCADA tag telemetry.
    /// </summary>
    public class ZIndustrialPump : FrameworkElement, IScadaBindable
    {
        public static readonly DependencyProperty StateProperty =
            DependencyProperty.Register(nameof(State), typeof(PumpState), typeof(ZIndustrialPump),
                new FrameworkPropertyMetadata(PumpState.Running, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty StatusFlagsProperty =
            DependencyProperty.Register(nameof(StatusFlags), typeof(DeviceStatusFlags), typeof(ZIndustrialPump),
                new FrameworkPropertyMetadata(DeviceStatusFlags.None, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty SpeedRpmProperty =
            DependencyProperty.Register(nameof(SpeedRpm), typeof(double), typeof(ZIndustrialPump),
                new FrameworkPropertyMetadata(2950.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty PowerKwProperty =
            DependencyProperty.Register(nameof(PowerKw), typeof(double), typeof(ZIndustrialPump),
                new FrameworkPropertyMetadata(18.5, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty TagLabelProperty =
            DependencyProperty.Register(nameof(TagLabel), typeof(string), typeof(ZIndustrialPump),
                new FrameworkPropertyMetadata("P-101A", FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty BoundTagPathProperty =
            DependencyProperty.Register(nameof(BoundTagPath), typeof(string), typeof(ZIndustrialPump),
                new FrameworkPropertyMetadata(null));

        public PumpState State
        {
            get => (PumpState)GetValue(StateProperty);
            set => SetValue(StateProperty, value);
        }

        public DeviceStatusFlags StatusFlags
        {
            get => (DeviceStatusFlags)GetValue(StatusFlagsProperty);
            set => SetValue(StatusFlagsProperty, value);
        }

        public double SpeedRpm
        {
            get => (double)GetValue(SpeedRpmProperty);
            set => SetValue(SpeedRpmProperty, Math.Max(0, value));
        }

        public double PowerKw
        {
            get => (double)GetValue(PowerKwProperty);
            set => SetValue(PowerKwProperty, Math.Max(0, value));
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

        private float _impellerAngle;
        private IDisposable? _clockToken;

        private static readonly Typeface SegoeBold = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        private static readonly Typeface SegoeNormal = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

        // Cached Frozen Brushes & Pens
        private static readonly Brush DarkCardBg = Freeze(new SolidColorBrush(Color.FromRgb(30, 41, 59)));
        private static readonly Brush LightCardBg = Freeze(new SolidColorBrush(Color.FromRgb(241, 245, 249)));
        private static readonly Brush DarkText = Freeze(new SolidColorBrush(Color.FromRgb(248, 250, 252)));
        private static readonly Brush LightText = Freeze(new SolidColorBrush(Color.FromRgb(15, 23, 42)));
        private static readonly Brush RunningGreen = Freeze(new SolidColorBrush(Color.FromRgb(34, 197, 94)));
        private static readonly Brush TripRed = Freeze(new SolidColorBrush(Color.FromRgb(239, 68, 68)));
        private static readonly Brush StoppedGray = Freeze(new SolidColorBrush(Color.FromRgb(148, 163, 184)));
        private static readonly Pen DarkBorderPen = Freeze(new Pen(new SolidColorBrush(Color.FromRgb(71, 85, 105)), 1.5));
        private static readonly Pen LightBorderPen = Freeze(new Pen(new SolidColorBrush(Color.FromRgb(203, 213, 225)), 1.5));

        private static T Freeze<T>(T freezable) where T : Freezable
        {
            if (freezable.CanFreeze) freezable.Freeze();
            return freezable;
        }

        public ZIndustrialPump()
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
            if (State != PumpState.Running || SpeedRpm <= 0) return;
            float step = (float)(SpeedRpm / 60.0 * 360.0 * deltaSeconds);
            _impellerAngle = (_impellerAngle + step) % 360f;
            InvalidateVisual();
        }

        public void OnTagValueChanged(IScadaTag tag)
        {
            if (tag?.Value == null) return;
            if (double.TryParse(tag.Value.ToString(), out var rpm))
            {
                SpeedRpm = rpm;
                State = rpm > 10 ? PumpState.Running : PumpState.Stopped;
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            return new Size(180, 140);
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            bool isDark = ZeroWpfTheme.IsDark;
            Brush bodyBrush = State switch
            {
                PumpState.Running => RunningGreen,
                PumpState.Trip => TripRed,
                _ => StoppedGray
            };

            Brush textBrush = isDark ? DarkText : LightText;
            Pen borderPen = isDark ? DarkBorderPen : LightBorderPen;

            #if !NETFRAMEWORK
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            #endif

            // 1. Tag & Status Header
            var tagText = new FormattedText(TagLabel, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, SegoeBold, 12, textBrush
            #if !NETFRAMEWORK
            , dpi
            #endif
            );
            dc.DrawText(tagText, new Point(10, 8));

            // 2. Volute Casing (Main Pump Circular Body)
            double cx = w * 0.42;
            double cy = h * 0.52;
            double radius = Math.Min(w * 0.28, h * 0.32);

            // Suction & Discharge Flanges
            dc.DrawRectangle(bodyBrush, borderPen, new Rect(cx - radius - 16, cy - 8, 16, 16));
            dc.DrawRectangle(bodyBrush, borderPen, new Rect(cx - 8, cy - radius - 16, 16, 16));

            // Volute Circle
            dc.DrawEllipse(bodyBrush, borderPen, new Point(cx, cy), radius, radius);

            // Impeller Hub
            dc.PushTransform(new RotateTransform(_impellerAngle, cx, cy));
            var hubBrush = isDark ? DarkCardBg : LightCardBg;
            dc.DrawEllipse(hubBrush, borderPen, new Point(cx, cy), radius * 0.35, radius * 0.35);

            // 4 Impeller Vanes
            for (int i = 0; i < 4; i++)
            {
                dc.PushTransform(new RotateTransform(i * 90, cx, cy));
                dc.DrawLine(borderPen, new Point(cx, cy), new Point(cx + radius * 0.85, cy));
                dc.Pop();
            }
            dc.Pop();

            // 3. Telemetry Readouts (Right Side)
            double rx = w * 0.72;
            var rpmText = new FormattedText($"{SpeedRpm:0} RPM", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, SegoeBold, 11, textBrush
            #if !NETFRAMEWORK
            , dpi
            #endif
            );
            dc.DrawText(rpmText, new Point(rx - rpmText.Width / 2, cy - 14));

            var kwText = new FormattedText($"{PowerKw:0.0} kW", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, SegoeNormal, 10, textBrush
            #if !NETFRAMEWORK
            , dpi
            #endif
            );
            dc.DrawText(kwText, new Point(rx - kwText.Width / 2, cy + 4));

            // Status Badge at Bottom
            string statusStr = State.ToString().ToUpperInvariant();
            var statusText = new FormattedText(statusStr, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, SegoeBold, 10, bodyBrush
            #if !NETFRAMEWORK
            , dpi
            #endif
            );
            dc.DrawText(statusText, new Point(10, h - 18));
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
    /// Legacy alias for <see cref="ZIndustrialPump"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("IndustrialPump is deprecated and will be removed in 5 release cycles. Please migrate to ZIndustrialPump instead.")]
    public class IndustrialPump : ZIndustrialPump { }

    /// <summary>
    /// Legacy alias for <see cref="ZIndustrialPump"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroIndustrialPump is deprecated and will be removed in 5 release cycles. Please migrate to ZIndustrialPump instead.")]
    public class ZeroIndustrialPump : ZIndustrialPump { }

    #endregion

}
