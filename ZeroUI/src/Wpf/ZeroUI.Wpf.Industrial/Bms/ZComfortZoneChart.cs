using System;
using System.Windows;
using System.Windows.Media;

namespace ZeroUI.Wpf.Industrial
{    /// <summary>
    /// ZComfortZoneChart WPF control.
    /// </summary>
    public class ZComfortZoneChart : FrameworkElement
    {        public static readonly DependencyProperty TemperatureProperty = DependencyProperty.Register(nameof(Temperature), typeof(double), typeof(ZComfortZoneChart));
        public double Temperature { get => (double)GetValue(TemperatureProperty); set => SetValue(TemperatureProperty, value); }
        public static readonly DependencyProperty HumidityProperty = DependencyProperty.Register(nameof(Humidity), typeof(double), typeof(ZComfortZoneChart));
        public double Humidity { get => (double)GetValue(HumidityProperty); set => SetValue(HumidityProperty, value); }
        public static readonly DependencyProperty ComfortZoneProperty = DependencyProperty.Register(nameof(ComfortZone), typeof(System.Windows.Rect), typeof(ZComfortZoneChart));
        public System.Windows.Rect ComfortZone { get => (System.Windows.Rect)GetValue(ComfortZoneProperty); set => SetValue(ComfortZoneProperty, value); }
        public static readonly DependencyProperty ShowCurrentPointProperty = DependencyProperty.Register(nameof(ShowCurrentPoint), typeof(bool), typeof(ZComfortZoneChart));
        public bool ShowCurrentPoint { get => (bool)GetValue(ShowCurrentPointProperty); set => SetValue(ShowCurrentPointProperty, value); }
        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
        }
    }

    [Obsolete("ZeroComfortZoneChart is deprecated.")]
    public class ZeroComfortZoneChart : ZComfortZoneChart { }

    [Obsolete("ComfortZoneChart is deprecated.")]
    public class ComfortZoneChart : ZComfortZoneChart { }
}
