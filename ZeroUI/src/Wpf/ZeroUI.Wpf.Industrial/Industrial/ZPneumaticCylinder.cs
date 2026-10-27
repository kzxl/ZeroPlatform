using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using ZeroUI.Core.Rendering;
using ZeroUI.Core.Scada;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Industrial
{
    public enum CylinderState
    {
        Retracted,
        Extended,
        Moving,
        Fault
    }

    /// <summary>
    /// Industrial pneumatic cylinder component with animated piston rod and magnetic limit sensors for ZeroUI WPF.
    /// Supports direct telemetry binding via <see cref="IScadaBindable"/>.
    /// </summary>
    public class ZPneumaticCylinder : FrameworkElement, IScadaBindable
    {
        public static readonly DependencyProperty StateProperty =
            DependencyProperty.Register(nameof(State), typeof(CylinderState), typeof(ZPneumaticCylinder),
                new FrameworkPropertyMetadata(CylinderState.Extended, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ExtensionPercentProperty =
            DependencyProperty.Register(nameof(ExtensionPercent), typeof(double), typeof(ZPneumaticCylinder),
                new FrameworkPropertyMetadata(100.0, FrameworkPropertyMetadataOptions.AffectsRender, OnExtensionChanged));

        public static readonly DependencyProperty TagLabelProperty =
            DependencyProperty.Register(nameof(TagLabel), typeof(string), typeof(ZPneumaticCylinder),
                new FrameworkPropertyMetadata("CYL-501", FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty BoundTagPathProperty =
            DependencyProperty.Register(nameof(BoundTagPath), typeof(string), typeof(ZPneumaticCylinder),
                new FrameworkPropertyMetadata(null));

        public CylinderState State
        {
            get => (CylinderState)GetValue(StateProperty);
            set => SetValue(StateProperty, value);
        }

        public double ExtensionPercent
        {
            get => (double)GetValue(ExtensionPercentProperty);
            set => SetValue(ExtensionPercentProperty, Math.Max(0.0, Math.Min(100.0, value)));
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

        private static void OnExtensionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZPneumaticCylinder cyl)
            {
                cyl._targetExtension = (double)e.NewValue;
                cyl.UpdateState();
            }
        }

        private double _targetExtension = 100.0;
        private IDisposable? _clockToken;

        private static readonly Typeface SegoeBold = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        private static readonly Typeface SegoeNormal = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

        private static readonly Brush DarkText = Freeze(new SolidColorBrush(Color.FromRgb(248, 250, 252)));
        private static readonly Brush LightText = Freeze(new SolidColorBrush(Color.FromRgb(15, 23, 42)));
        private static readonly Brush SensorOnGreen = Freeze(new SolidColorBrush(Color.FromRgb(34, 197, 94)));
        private static readonly Brush SensorOffDark = Freeze(new SolidColorBrush(Color.FromRgb(71, 85, 105)));
        private static readonly Brush SensorOffLight = Freeze(new SolidColorBrush(Color.FromRgb(203, 213, 225)));
        private static readonly Brush RodDark = Freeze(new SolidColorBrush(Color.FromRgb(203, 213, 225)));
        private static readonly Brush RodLight = Freeze(new SolidColorBrush(Color.FromRgb(148, 163, 184)));
        private static readonly Brush BarrelDark = Freeze(new SolidColorBrush(Color.FromRgb(30, 41, 59)));
        private static readonly Brush BarrelLight = Freeze(new SolidColorBrush(Color.FromRgb(226, 232, 240)));
        private static readonly Pen DarkBorderPen = Freeze(new Pen(new SolidColorBrush(Color.FromRgb(71, 85, 105)), 1.2));
        private static readonly Pen LightBorderPen = Freeze(new Pen(new SolidColorBrush(Color.FromRgb(203, 213, 225)), 1.2));

        private static T Freeze<T>(T freezable) where T : Freezable
        {
            if (freezable.CanFreeze) freezable.Freeze();
            return freezable;
        }

        public ZPneumaticCylinder()
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
            if (Math.Abs(ExtensionPercent - _targetExtension) > 0.5)
            {
                double step = 80.0 * deltaSeconds;
                if (ExtensionPercent < _targetExtension)
                {
                    ExtensionPercent = Math.Min(_targetExtension, ExtensionPercent + step);
                }
                else
                {
                    ExtensionPercent = Math.Max(_targetExtension, ExtensionPercent - step);
                }
                UpdateState();
                InvalidateVisual();
            }
        }

        private void UpdateState()
        {
            if (Math.Abs(ExtensionPercent - _targetExtension) > 1.0) State = CylinderState.Moving;
            else if (ExtensionPercent <= 2.0) State = CylinderState.Retracted;
            else if (ExtensionPercent >= 98.0) State = CylinderState.Extended;
            else State = CylinderState.Moving;
        }

        public void OnTagValueChanged(IScadaTag tag)
        {
            if (tag?.Value == null) return;
            if (double.TryParse(tag.Value.ToString(), out var pos))
            {
                _targetExtension = Math.Max(0.0, Math.Min(100.0, pos));
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            return new Size(200, 80);
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            bool isDark = ZeroWpfTheme.IsDark;
            Brush barrelBrush = isDark ? BarrelDark : BarrelLight;
            Brush rodBrush = isDark ? RodDark : RodLight;
            Brush sensorOffBrush = isDark ? SensorOffDark : SensorOffLight;
            Brush textBrush = isDark ? DarkText : LightText;
            Pen borderPen = isDark ? DarkBorderPen : LightBorderPen;

            #if !NETFRAMEWORK
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            #endif

            // 1. Tag Header
            var tagText = new FormattedText(TagLabel, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, SegoeBold, 11, textBrush
            #if !NETFRAMEWORK
            , dpi
            #endif
            );
            dc.DrawText(tagText, new Point(8, 4));

            string stateText = State.ToString().ToUpperInvariant();
            var stFormatted = new FormattedText(stateText, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, SegoeBold, 10,
                State == CylinderState.Fault ? Freeze(new SolidColorBrush(Color.FromRgb(239, 68, 68))) : SensorOnGreen
            #if !NETFRAMEWORK
            , dpi
            #endif
            );
            dc.DrawText(stFormatted, new Point(w - stFormatted.Width - 8, 4));

            // 2. Geometry: Cylinder Barrel
            double barrelLeft = 8;
            double barrelTop = 24;
            double barrelWidth = w * 0.55;
            double barrelHeight = 32;
            var barrelRect = new Rect(barrelLeft, barrelTop, barrelWidth, barrelHeight);

            // 3. Piston Rod
            double maxStroke = w - barrelRect.Right - 24;
            double currentStroke = maxStroke * (ExtensionPercent / 100.0);
            double rodTop = barrelTop + barrelHeight * 0.5 - 5;
            double rodHeight = 10;
            var rodRect = new Rect(barrelRect.Right - 4, rodTop, currentStroke + 8, rodHeight);

            // Draw Rod
            dc.DrawRectangle(rodBrush, borderPen, rodRect);

            // Rod Clevis / Eye End
            var clevisRect = new Rect(rodRect.Right, rodTop - 3, 14, rodHeight + 6);
            dc.DrawRoundedRectangle(rodBrush, borderPen, clevisRect, 4, 4);
            dc.DrawEllipse(Brushes.Black, null, new Point(clevisRect.X + 7, clevisRect.Y + clevisRect.Height * 0.5), 3, 3);

            // Draw Barrel
            dc.DrawRectangle(barrelBrush, borderPen, barrelRect);

            // 4. End-of-Stroke Magnetic Reed Sensors
            bool isRetractedSensor = ExtensionPercent <= 5.0;
            bool isExtendedSensor = ExtensionPercent >= 95.0;

            dc.DrawRectangle(isRetractedSensor ? SensorOnGreen : sensorOffBrush, null, new Rect(barrelLeft + 6, barrelTop - 4, 10, 4));
            dc.DrawRectangle(isExtendedSensor ? SensorOnGreen : sensorOffBrush, null, new Rect(barrelRect.Right - 16, barrelTop - 4, 10, 4));

            // 5. Position Readout
            string info = $"Stroke: {ExtensionPercent:0}%";
            var infoText = new FormattedText(info, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, SegoeNormal, 9.5, textBrush
            #if !NETFRAMEWORK
            , dpi
            #endif
            );
            dc.DrawText(infoText, new Point(8, h - 18));
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
    /// Legacy alias for <see cref="ZPneumaticCylinder"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("PneumaticCylinder is deprecated and will be removed in 5 release cycles. Please migrate to ZPneumaticCylinder instead.")]
    public class PneumaticCylinder : ZPneumaticCylinder { }

    /// <summary>
    /// Legacy alias for <see cref="ZPneumaticCylinder"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroPneumaticCylinder is deprecated and will be removed in 5 release cycles. Please migrate to ZPneumaticCylinder instead.")]
    public class ZeroPneumaticCylinder : ZPneumaticCylinder { }

    #endregion

}
