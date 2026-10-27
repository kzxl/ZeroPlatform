using System;
using System.Windows;
using System.Windows.Media;

namespace ZeroUI.Wpf.Industrial
{    /// <summary>
    /// ZWaterQualityDashboard WPF control.
    /// </summary>
    public class ZWaterQualityDashboard : FrameworkElement
    {        public static readonly DependencyProperty PhProperty = DependencyProperty.Register(nameof(Ph), typeof(double), typeof(ZWaterQualityDashboard));
        public double Ph { get => (double)GetValue(PhProperty); set => SetValue(PhProperty, value); }
        public static readonly DependencyProperty TurbidityProperty = DependencyProperty.Register(nameof(Turbidity), typeof(double), typeof(ZWaterQualityDashboard));
        public double Turbidity { get => (double)GetValue(TurbidityProperty); set => SetValue(TurbidityProperty, value); }
        public static readonly DependencyProperty ChlorineProperty = DependencyProperty.Register(nameof(Chlorine), typeof(double), typeof(ZWaterQualityDashboard));
        public double Chlorine { get => (double)GetValue(ChlorineProperty); set => SetValue(ChlorineProperty, value); }
        public static readonly DependencyProperty ConductivityProperty = DependencyProperty.Register(nameof(Conductivity), typeof(double), typeof(ZWaterQualityDashboard));
        public double Conductivity { get => (double)GetValue(ConductivityProperty); set => SetValue(ConductivityProperty, value); }
        public static readonly DependencyProperty ShowTrendProperty = DependencyProperty.Register(nameof(ShowTrend), typeof(bool), typeof(ZWaterQualityDashboard));
        public bool ShowTrend { get => (bool)GetValue(ShowTrendProperty); set => SetValue(ShowTrendProperty, value); }
        public static readonly DependencyProperty AlertLimitsProperty = DependencyProperty.Register(nameof(AlertLimits), typeof(double), typeof(ZWaterQualityDashboard));
        public double AlertLimits { get => (double)GetValue(AlertLimitsProperty); set => SetValue(AlertLimitsProperty, value); }
        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
        }
    }

    [Obsolete("ZeroWaterQualityDashboard is deprecated.")]
    public class ZeroWaterQualityDashboard : ZWaterQualityDashboard { }

    [Obsolete("WaterQualityDashboard is deprecated.")]
    public class WaterQualityDashboard : ZWaterQualityDashboard { }
}
