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
    public enum MotorState
    {
        Stopped,
        Running,
        Trip
    }

    /// <summary>
    /// Industrial electric drive motor component with cooling fin vector geometry,
    /// dynamic shaft rotation, and telemetry readouts for ZeroUI WPF.
    /// </summary>
    public class ZIndustrialMotor : FrameworkElement, IScadaBindable
    {
        public static readonly DependencyProperty StateProperty =
            DependencyProperty.Register(nameof(State), typeof(MotorState), typeof(ZIndustrialMotor),
                new FrameworkPropertyMetadata(MotorState.Running, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty StatusFlagsProperty =
            DependencyProperty.Register(nameof(StatusFlags), typeof(DeviceStatusFlags), typeof(ZIndustrialMotor),
                new FrameworkPropertyMetadata(DeviceStatusFlags.None, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty SpeedRpmProperty =
            DependencyProperty.Register(nameof(SpeedRpm), typeof(double), typeof(ZIndustrialMotor),
                new FrameworkPropertyMetadata(1450.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty TorqueNmProperty =
            DependencyProperty.Register(nameof(TorqueNm), typeof(double), typeof(ZIndustrialMotor),
                new FrameworkPropertyMetadata(122.5, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty TagLabelProperty =
            DependencyProperty.Register(nameof(TagLabel), typeof(string), typeof(ZIndustrialMotor),
                new FrameworkPropertyMetadata("MTR-101", FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty BoundTagPathProperty =
            DependencyProperty.Register(nameof(BoundTagPath), typeof(string), typeof(ZIndustrialMotor),
                new FrameworkPropertyMetadata(null));

        public MotorState State
        {
            get => (MotorState)GetValue(StateProperty);
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

        public double TorqueNm
        {
            get => (double)GetValue(TorqueNmProperty);
            set => SetValue(TorqueNmProperty, Math.Max(0, value));
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

        private float _shaftAngle;
        private IDisposable? _clockToken;

        private static readonly Typeface SegoeBold = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        private static readonly Typeface SegoeNormal = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

        private static readonly Brush DarkText = Freeze(new SolidColorBrush(Color.FromRgb(248, 250, 252)));
        private static readonly Brush LightText = Freeze(new SolidColorBrush(Color.FromRgb(15, 23, 42)));
        private static readonly Brush RunningBlue = Freeze(new SolidColorBrush(Color.FromRgb(59, 130, 246)));
        private static readonly Brush TripRed = Freeze(new SolidColorBrush(Color.FromRgb(239, 68, 68)));
        private static readonly Brush StoppedGray = Freeze(new SolidColorBrush(Color.FromRgb(148, 163, 184)));
        private static readonly Pen DarkBorderPen = Freeze(new Pen(new SolidColorBrush(Color.FromRgb(71, 85, 105)), 1.5));
        private static readonly Pen LightBorderPen = Freeze(new Pen(new SolidColorBrush(Color.FromRgb(203, 213, 225)), 1.5));
        private static readonly Pen FinPen = Freeze(new Pen(new SolidColorBrush(Color.FromArgb(160, 255, 255, 255)), 1.5));

        private static T Freeze<T>(T freezable) where T : Freezable
        {
            if (freezable.CanFreeze) freezable.Freeze();
            return freezable;
        }

        public ZIndustrialMotor()
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
            if (State != MotorState.Running || SpeedRpm <= 0) return;
            float step = (float)(SpeedRpm / 60.0 * 360.0 * deltaSeconds);
            _shaftAngle = (_shaftAngle + step) % 360f;
            InvalidateVisual();
        }

        public void OnTagValueChanged(IScadaTag tag)
        {
            if (tag?.Value == null) return;
            if (double.TryParse(tag.Value.ToString(), out var rpm))
            {
                SpeedRpm = rpm;
                State = rpm > 10 ? MotorState.Running : MotorState.Stopped;
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            return new Size(180, 130);
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
                MotorState.Running => RunningBlue,
                MotorState.Trip => TripRed,
                _ => StoppedGray
            };

            Brush textBrush = isDark ? DarkText : LightText;
            Pen borderPen = isDark ? DarkBorderPen : LightBorderPen;

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

            // 2. Geometry Layout
            double bodyLeft = 14;
            double bodyTop = 26;
            double bodyWidth = Math.Min(w * 0.48, 90.0);
            double bodyHeight = Math.Min(h * 0.48, 55.0);
            var bodyRect = new Rect(bodyLeft, bodyTop, bodyWidth, bodyHeight);

            // Terminal Box (Top of motor body)
            dc.DrawRectangle(bodyBrush, borderPen, new Rect(bodyLeft + 14, bodyTop - 10, 24, 10));

            // Main Stator Housing
            dc.DrawRoundedRectangle(bodyBrush, borderPen, bodyRect, 6, 6);

            // Cooling Fins (Vertical ribs across body)
            int finCount = 5;
            double finSpacing = bodyWidth / (finCount + 1);
            for (int i = 1; i <= finCount; i++)
            {
                double fx = bodyLeft + i * finSpacing;
                dc.DrawLine(FinPen, new Point(fx, bodyTop + 4), new Point(fx, bodyTop + bodyHeight - 4));
            }

            // Output Drive Shaft (Extends right)
            double shaftX = bodyRect.Right;
            double shaftY = bodyTop + bodyHeight * 0.5 - 6;
            double shaftW = 20;
            double shaftH = 12;
            dc.DrawRectangle(borderPen.Brush, borderPen, new Rect(shaftX, shaftY, shaftW, shaftH));

            // Shaft End Keyway (Rotating circle indicator)
            double shaftCx = shaftX + shaftW + 12;
            double shaftCy = bodyTop + bodyHeight * 0.5;
            dc.DrawEllipse(borderPen.Brush, borderPen, new Point(shaftCx, shaftCy), 10, 10);

            dc.PushTransform(new RotateTransform(_shaftAngle, shaftCx, shaftCy));
            dc.DrawLine(FinPen, new Point(shaftCx - 8, shaftCy), new Point(shaftCx + 8, shaftCy));
            dc.Pop();

            // 3. Telemetry Readouts
            double rx = w * 0.76;
            var rpmText = new FormattedText($"{SpeedRpm:0} RPM", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, SegoeBold, 11, textBrush
            #if !NETFRAMEWORK
            , dpi
            #endif
            );
            dc.DrawText(rpmText, new Point(rx - rpmText.Width / 2, bodyTop + 4));

            var tqText = new FormattedText($"{TorqueNm:0.0} N·m", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, SegoeNormal, 10, textBrush
            #if !NETFRAMEWORK
            , dpi
            #endif
            );
            dc.DrawText(tqText, new Point(rx - tqText.Width / 2, bodyTop + 24));

            // Status string at bottom
            string stateStr = State.ToString().ToUpperInvariant();
            var stateText = new FormattedText(stateStr, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, SegoeBold, 9.5, bodyBrush
            #if !NETFRAMEWORK
            , dpi
            #endif
            );
            dc.DrawText(stateText, new Point(8, h - 18));
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
    /// Legacy alias for <see cref="ZIndustrialMotor"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("IndustrialMotor is deprecated and will be removed in 5 release cycles. Please migrate to ZIndustrialMotor instead.")]
    public class IndustrialMotor : ZIndustrialMotor { }

    /// <summary>
    /// Legacy alias for <see cref="ZIndustrialMotor"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroIndustrialMotor is deprecated and will be removed in 5 release cycles. Please migrate to ZIndustrialMotor instead.")]
    public class ZeroIndustrialMotor : ZIndustrialMotor { }

    #endregion

}
