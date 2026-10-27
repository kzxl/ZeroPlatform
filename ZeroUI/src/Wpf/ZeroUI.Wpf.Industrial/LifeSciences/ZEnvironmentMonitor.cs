using System;
using System.Windows;
using System.Windows.Media;

namespace ZeroUI.Wpf.Industrial
{    /// <summary>
    /// ZEnvironmentMonitor WPF control.
    /// </summary>
    public class ZEnvironmentMonitor : FrameworkElement
    {        public static readonly DependencyProperty TemperatureProperty = DependencyProperty.Register(nameof(Temperature), typeof(double), typeof(ZEnvironmentMonitor));
        public double Temperature { get => (double)GetValue(TemperatureProperty); set => SetValue(TemperatureProperty, value); }
        public static readonly DependencyProperty HumidityProperty = DependencyProperty.Register(nameof(Humidity), typeof(double), typeof(ZEnvironmentMonitor));
        public double Humidity { get => (double)GetValue(HumidityProperty); set => SetValue(HumidityProperty, value); }
        public static readonly DependencyProperty ParticleCountProperty = DependencyProperty.Register(nameof(ParticleCount), typeof(double), typeof(ZEnvironmentMonitor));
        public double ParticleCount { get => (double)GetValue(ParticleCountProperty); set => SetValue(ParticleCountProperty, value); }
        public static readonly DependencyProperty DifferentialPressureProperty = DependencyProperty.Register(nameof(DifferentialPressure), typeof(double), typeof(ZEnvironmentMonitor));
        public double DifferentialPressure { get => (double)GetValue(DifferentialPressureProperty); set => SetValue(DifferentialPressureProperty, value); }
        public static readonly DependencyProperty ShowTrendProperty = DependencyProperty.Register(nameof(ShowTrend), typeof(bool), typeof(ZEnvironmentMonitor));
        public bool ShowTrend { get => (bool)GetValue(ShowTrendProperty); set => SetValue(ShowTrendProperty, value); }
        public static readonly DependencyProperty AlertThresholdsProperty = DependencyProperty.Register(nameof(AlertThresholds), typeof(double), typeof(ZEnvironmentMonitor));
        public double AlertThresholds { get => (double)GetValue(AlertThresholdsProperty); set => SetValue(AlertThresholdsProperty, value); }
        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
        }
    }

    [Obsolete("ZeroEnvironmentMonitor is deprecated.")]
    public class ZeroEnvironmentMonitor : ZEnvironmentMonitor { }

    [Obsolete("EnvironmentMonitor is deprecated.")]
    public class EnvironmentMonitor : ZEnvironmentMonitor { }
}
