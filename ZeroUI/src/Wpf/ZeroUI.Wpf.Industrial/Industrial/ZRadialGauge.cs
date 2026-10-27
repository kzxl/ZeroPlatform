using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Threading;
using ZeroUI.Core.Scada;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Industrial
{
    /// <summary>
    /// High-precision circular industrial instrumentation dial gauge for SCADA and MES monitoring in WPF.
    /// Supports 180° / 270° sweep scales, multi-zone colored threshold bands, major/minor tick marks,
    /// inertial needle damping, and direct binding to SCADA telemetry tags via <see cref="IScadaBindable"/>.
    /// </summary>
    public partial class ZRadialGauge : FrameworkElement, IScadaBindable
    {
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(
                nameof(Value),
                typeof(double),
                typeof(ZRadialGauge),
                new FrameworkPropertyMetadata(65.0, FrameworkPropertyMetadataOptions.AffectsRender, OnValueChanged));

        public static readonly DependencyProperty MinimumProperty =
            DependencyProperty.Register(
                nameof(Minimum),
                typeof(double),
                typeof(ZRadialGauge),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register(
                nameof(Maximum),
                typeof(double),
                typeof(ZRadialGauge),
                new FrameworkPropertyMetadata(100.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(
                nameof(Title),
                typeof(string),
                typeof(ZRadialGauge),
                new FrameworkPropertyMetadata("Pressure", FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty UnitProperty =
            DependencyProperty.Register(
                nameof(Unit),
                typeof(string),
                typeof(ZRadialGauge),
                new FrameworkPropertyMetadata("bar", FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ValueFormatProperty =
            DependencyProperty.Register(
                nameof(ValueFormat),
                typeof(string),
                typeof(ZRadialGauge),
                new FrameworkPropertyMetadata("0.#", FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty UseTabularReadoutProperty =
            DependencyProperty.Register(
                nameof(UseTabularReadout),
                typeof(bool),
                typeof(ZRadialGauge),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        private double _indicatedValue = 65.0;
        private double _targetValue = 65.0;
        private float _startAngle = 135f;
        private float _sweepAngle = 270f;
        private int _majorTicks = 10;
        private int _minorTicks = 4;
        private bool _enableDamping = true;
        private double _dampingFactor = 0.25;
        private readonly DispatcherTimer? _animationTimer;
        private readonly List<GaugeThresholdRange> _thresholds = new List<GaugeThresholdRange>();

        public event EventHandler? ValueChanged;

        #region Properties

        public double Value
        {
            get => (double)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public double Minimum
        {
            get => (double)GetValue(MinimumProperty);
            set => SetValue(MinimumProperty, value);
        }

        public double Maximum
        {
            get => (double)GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, value);
        }

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public string Unit
        {
            get => (string)GetValue(UnitProperty);
            set => SetValue(UnitProperty, value);
        }

        /// <summary>
        /// Gets or sets the custom format string for the digital readout.
        /// Defaults to "0.#". Callers can specify fixed-precision formats such as "0.0" or "0.00" to avoid jitter.
        /// </summary>
        public string ValueFormat
        {
            get => (string)GetValue(ValueFormatProperty);
            set => SetValue(ValueFormatProperty, value);
        }

        /// <summary>
        /// Gets or sets whether to use tabular character slot pitch to eliminate horizontal jumping/jitter in variable-width fonts.
        /// Defaults to true.
        /// </summary>
        public bool UseTabularReadout
        {
            get => (bool)GetValue(UseTabularReadoutProperty);
            set => SetValue(UseTabularReadoutProperty, value);
        }

        public float StartAngle
        {
            get => _startAngle;
            set { _startAngle = value; InvalidateVisual(); }
        }

        public float SweepAngle
        {
            get => _sweepAngle;
            set { _sweepAngle = Math.Max(30f, Math.Min(360f, value)); InvalidateVisual(); }
        }

        public int MajorTicks
        {
            get => _majorTicks;
            set { _majorTicks = Math.Max(2, value); InvalidateVisual(); }
        }

        public int MinorTicks
        {
            get => _minorTicks;
            set { _minorTicks = Math.Max(0, value); InvalidateVisual(); }
        }

        public bool EnableDamping
        {
            get => _enableDamping;
            set => _enableDamping = value;
        }

        public double DampingFactor
        {
            get => _dampingFactor;
            set => _dampingFactor = Math.Max(0.01, Math.Min(1.0, value));
        }

        public List<GaugeThresholdRange> Thresholds => _thresholds;

        public double WarningThreshold
        {
            get => _thresholds.Count > 1 ? _thresholds[1].From : 70.0;
            set
            {
                if (_thresholds.Count >= 2)
                {
                    _thresholds[0].To = value;
                    _thresholds[1].From = value;
                    InvalidateVisual();
                }
            }
        }

        public double DangerThreshold
        {
            get => _thresholds.Count > 2 ? _thresholds[2].From : 85.0;
            set
            {
                if (_thresholds.Count >= 3)
                {
                    _thresholds[1].To = value;
                    _thresholds[2].From = value;
                    InvalidateVisual();
                }
            }
        }

        #region IScadaBindable Implementation

        public string? BoundTagPath { get; set; }

        public void OnTagValueChanged(IScadaTag tag)
        {
            if (tag != null)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    Value = tag.GetValue<double>();
                }));
            }
        }

        #endregion

        #endregion

        public ZRadialGauge()
        {
            ClipToBounds = true;

            // Default industrial thresholds: 0-70 Normal (Green), 70-85 Warning (Amber), 85-100 Danger (Red)
            _thresholds.Add(new GaugeThresholdRange(0, 70, GaugeSeverity.Normal, 0xFF10B981u, "Normal"));
            _thresholds.Add(new GaugeThresholdRange(70, 85, GaugeSeverity.Warning, 0xFFF59E0Bu, "Warning"));
            _thresholds.Add(new GaugeThresholdRange(85, 100, GaugeSeverity.Critical, 0xFFEF4444u, "Danger"));

            _animationTimer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            _animationTimer.Tick += OnAnimationTick;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZRadialGauge gauge)
            {
                double newVal = (double)e.NewValue;
                gauge.HandleNewValue(newVal);
            }
        }

        private void HandleNewValue(double val)
        {
            double clamped = Math.Max(Minimum, Math.Min(Maximum, val));
            if (_enableDamping)
            {
                _targetValue = clamped;
                if (_animationTimer != null && !_animationTimer.IsEnabled)
                {
                    _animationTimer.Start();
                }
            }
            else
            {
                _indicatedValue = clamped;
                _targetValue = clamped;
                InvalidateVisual();
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void OnAnimationTick(object? sender, EventArgs e)
        {
            double next = GaugeMath.ApplyDamping(_indicatedValue, _targetValue, _dampingFactor);
            if (Math.Abs(next - _indicatedValue) > 1e-4)
            {
                _indicatedValue = next;
                InvalidateVisual();
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                _indicatedValue = _targetValue;
                _animationTimer?.Stop();
                InvalidateVisual();
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
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

    /// <summary>
    /// Backward-compatibility alias for <see cref="RadialGauge"/>.
    /// </summary>
    [Obsolete("ZeroGauge is deprecated. Use RadialGauge instead.")]
    public class ZeroGauge : RadialGauge
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZRadialGauge"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("RadialGauge is deprecated and will be removed in 5 release cycles. Please migrate to ZRadialGauge instead.")]
    public class RadialGauge : ZRadialGauge
    {
        internal new string FormatValue(double val) => base.FormatValue(val);
    }

    /// <summary>
    /// Legacy alias for <see cref="ZRadialGauge"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroRadialGauge is deprecated and will be removed in 5 release cycles. Please migrate to ZRadialGauge instead.")]
    public class ZeroRadialGauge : ZRadialGauge { }

    #endregion

}
