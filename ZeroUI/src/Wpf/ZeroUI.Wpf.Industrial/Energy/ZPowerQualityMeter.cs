using System;
using System.Windows;
using System.Windows.Media;

namespace ZeroUI.Wpf.Industrial
{    /// <summary>
    /// ZPowerQualityMeter WPF control.
    /// </summary>
    public class ZPowerQualityMeter : FrameworkElement
    {        public static readonly DependencyProperty VoltageProperty = DependencyProperty.Register(nameof(Voltage), typeof(double), typeof(ZPowerQualityMeter));
        public double Voltage { get => (double)GetValue(VoltageProperty); set => SetValue(VoltageProperty, value); }
        public static readonly DependencyProperty CurrentProperty = DependencyProperty.Register(nameof(Current), typeof(double), typeof(ZPowerQualityMeter));
        public double Current { get => (double)GetValue(CurrentProperty); set => SetValue(CurrentProperty, value); }
        public static readonly DependencyProperty PowerFactorProperty = DependencyProperty.Register(nameof(PowerFactor), typeof(double), typeof(ZPowerQualityMeter));
        public double PowerFactor { get => (double)GetValue(PowerFactorProperty); set => SetValue(PowerFactorProperty, value); }
        public static readonly DependencyProperty THDProperty = DependencyProperty.Register(nameof(THD), typeof(double), typeof(ZPowerQualityMeter));
        public double THD { get => (double)GetValue(THDProperty); set => SetValue(THDProperty, value); }
        public static readonly DependencyProperty FrequencyProperty = DependencyProperty.Register(nameof(Frequency), typeof(double), typeof(ZPowerQualityMeter));
        public double Frequency { get => (double)GetValue(FrequencyProperty); set => SetValue(FrequencyProperty, value); }
        public static readonly DependencyProperty ShowWaveformProperty = DependencyProperty.Register(nameof(ShowWaveform), typeof(bool), typeof(ZPowerQualityMeter));
        public bool ShowWaveform { get => (bool)GetValue(ShowWaveformProperty); set => SetValue(ShowWaveformProperty, value); }
        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
        }
    }

    [Obsolete("ZeroPowerQualityMeter is deprecated.")]
    public class ZeroPowerQualityMeter : ZPowerQualityMeter { }

    [Obsolete("PowerQualityMeter is deprecated.")]
    public class PowerQualityMeter : ZPowerQualityMeter { }
}
