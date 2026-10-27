using System;
using System.Windows;
using System.Windows.Media;

namespace ZeroUI.Wpf.Industrial
{    /// <summary>
    /// ZEnergyDashboard WPF control.
    /// </summary>
    public class ZEnergyDashboard : FrameworkElement
    {        public static readonly DependencyProperty CurrentKwProperty = DependencyProperty.Register(nameof(CurrentKw), typeof(double), typeof(ZEnergyDashboard));
        public double CurrentKw { get => (double)GetValue(CurrentKwProperty); set => SetValue(CurrentKwProperty, value); }
        public static readonly DependencyProperty DailyKwhProperty = DependencyProperty.Register(nameof(DailyKwh), typeof(double), typeof(ZEnergyDashboard));
        public double DailyKwh { get => (double)GetValue(DailyKwhProperty); set => SetValue(DailyKwhProperty, value); }
        public static readonly DependencyProperty MonthlyKwhProperty = DependencyProperty.Register(nameof(MonthlyKwh), typeof(double), typeof(ZEnergyDashboard));
        public double MonthlyKwh { get => (double)GetValue(MonthlyKwhProperty); set => SetValue(MonthlyKwhProperty, value); }
        public static readonly DependencyProperty TrendProperty = DependencyProperty.Register(nameof(Trend), typeof(double[]), typeof(ZEnergyDashboard));
        public double[] Trend { get => (double[])GetValue(TrendProperty); set => SetValue(TrendProperty, value); }
        public static readonly DependencyProperty TargetKwhProperty = DependencyProperty.Register(nameof(TargetKwh), typeof(double), typeof(ZEnergyDashboard));
        public double TargetKwh { get => (double)GetValue(TargetKwhProperty); set => SetValue(TargetKwhProperty, value); }
        public static readonly DependencyProperty ShowTrendProperty = DependencyProperty.Register(nameof(ShowTrend), typeof(bool), typeof(ZEnergyDashboard));
        public bool ShowTrend { get => (bool)GetValue(ShowTrendProperty); set => SetValue(ShowTrendProperty, value); }
        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
        }
    }

    [Obsolete("ZeroEnergyDashboard is deprecated.")]
    public class ZeroEnergyDashboard : ZEnergyDashboard { }

    [Obsolete("EnergyDashboard is deprecated.")]
    public class EnergyDashboard : ZEnergyDashboard { }
}
