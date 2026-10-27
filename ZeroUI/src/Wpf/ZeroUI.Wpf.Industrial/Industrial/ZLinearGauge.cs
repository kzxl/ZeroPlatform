using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Threading;
using ZeroUI.Core.Scada;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Industrial
{
    /// <summary>
    /// Industrial Linear Level & Pressure Gauge for SCADA / MES telemetry in WPF.
    /// Features multi-zone scale thresholds (Normal, Warning, Critical), graduations with tick marks,
    /// inertial damping, and direct binding to SCADA telemetry tags via <see cref="IScadaBindable"/>.
    /// </summary>
    public partial class ZLinearGauge : FrameworkElement, IScadaBindable
    {
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(
                nameof(Value),
                typeof(double),
                typeof(ZLinearGauge),
                new FrameworkPropertyMetadata(65.0, FrameworkPropertyMetadataOptions.AffectsRender, OnValueChanged));

        public static readonly DependencyProperty MinimumProperty =
            DependencyProperty.Register(
                nameof(Minimum),
                typeof(double),
                typeof(ZLinearGauge),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register(
                nameof(Maximum),
                typeof(double),
                typeof(ZLinearGauge),
                new FrameworkPropertyMetadata(100.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(
                nameof(Title),
                typeof(string),
                typeof(ZLinearGauge),
                new FrameworkPropertyMetadata("Pressure", FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty UnitProperty =
            DependencyProperty.Register(
                nameof(Unit),
                typeof(string),
                typeof(ZLinearGauge),
                new FrameworkPropertyMetadata("PSI", FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty IsHorizontalProperty =
            DependencyProperty.Register(
                nameof(IsHorizontal),
                typeof(bool),
                typeof(ZLinearGauge),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ValueFormatProperty =
            DependencyProperty.Register(
                nameof(ValueFormat),
                typeof(string),
                typeof(ZLinearGauge),
                new FrameworkPropertyMetadata("0.#", FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty UseTabularReadoutProperty =
            DependencyProperty.Register(
                nameof(UseTabularReadout),
                typeof(bool),
                typeof(ZLinearGauge),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        private double _indicatedValue = 65.0;
        private double _targetValue = 65.0;
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

        public bool IsHorizontal
        {
            get => (bool)GetValue(IsHorizontalProperty);
            set => SetValue(IsHorizontalProperty, value);
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
        /// Gets or sets whether to use tabular character slot pitch to eliminate horizontal jumping/jitter.
        /// Defaults to true.
        /// </summary>
        public bool UseTabularReadout
        {
            get => (bool)GetValue(UseTabularReadoutProperty);
            set => SetValue(UseTabularReadoutProperty, value);
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
            get
            {
                var r = _thresholds.Find(t => t.Severity == GaugeSeverity.Warning);
                return r?.From ?? (Maximum * 0.75);
            }
            set
            {
                _thresholds.RemoveAll(t => t.Severity == GaugeSeverity.Warning);
                _thresholds.Add(new GaugeThresholdRange(value, CriticalThreshold, GaugeSeverity.Warning, 0xFFFFAA00, "Warning"));
                InvalidateVisual();
            }
        }

        public double CriticalThreshold
        {
            get
            {
                var r = _thresholds.Find(t => t.Severity == GaugeSeverity.Critical);
                return r?.From ?? (Maximum * 0.90);
            }
            set
            {
                _thresholds.RemoveAll(t => t.Severity == GaugeSeverity.Critical);
                _thresholds.Add(new GaugeThresholdRange(value, Maximum, GaugeSeverity.Critical, 0xFFFF3333, "Critical"));
                InvalidateVisual();
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

        public ZLinearGauge()
        {
            ClipToBounds = true;

            // Default industrial thresholds: 0-75 Normal (Green), 75-90 Warning (Amber), 90-100 Danger (Red)
            _thresholds.Add(new GaugeThresholdRange(0, 75, GaugeSeverity.Normal, 0xFF10B981u, "Normal"));
            _thresholds.Add(new GaugeThresholdRange(75, 90, GaugeSeverity.Warning, 0xFFF59E0Bu, "Warning"));
            _thresholds.Add(new GaugeThresholdRange(90, 100, GaugeSeverity.Critical, 0xFFEF4444u, "Danger"));

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
            if (d is ZLinearGauge gauge)
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
    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZLinearGauge"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("LinearGauge is deprecated and will be removed in 5 release cycles. Please migrate to ZLinearGauge instead.")]
    public class LinearGauge : ZLinearGauge
    {
        internal new string FormatValue(double val) => base.FormatValue(val);
    }

    /// <summary>
    /// Legacy alias for <see cref="ZLinearGauge"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroLinearGauge is deprecated and will be removed in 5 release cycles. Please migrate to ZLinearGauge instead.")]
    public class ZeroLinearGauge : ZLinearGauge { }

    #endregion

}
