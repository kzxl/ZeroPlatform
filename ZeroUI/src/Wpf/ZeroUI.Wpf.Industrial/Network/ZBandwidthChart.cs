using System;
using System.Windows;
using System.Windows.Media;

namespace ZeroUI.Wpf.Industrial
{    /// <summary>
    /// ZBandwidthChart WPF control.
    /// </summary>
    public class ZBandwidthChart : FrameworkElement
    {        public static readonly DependencyProperty InboundDataProperty = DependencyProperty.Register(nameof(InboundData), typeof(double[]), typeof(ZBandwidthChart));
        public double[] InboundData { get => (double[])GetValue(InboundDataProperty); set => SetValue(InboundDataProperty, value); }
        public static readonly DependencyProperty OutboundDataProperty = DependencyProperty.Register(nameof(OutboundData), typeof(double[]), typeof(ZBandwidthChart));
        public double[] OutboundData { get => (double[])GetValue(OutboundDataProperty); set => SetValue(OutboundDataProperty, value); }
        public static readonly DependencyProperty MaxBandwidthProperty = DependencyProperty.Register(nameof(MaxBandwidth), typeof(double), typeof(ZBandwidthChart));
        public double MaxBandwidth { get => (double)GetValue(MaxBandwidthProperty); set => SetValue(MaxBandwidthProperty, value); }
        public static readonly DependencyProperty TimeWindowProperty = DependencyProperty.Register(nameof(TimeWindow), typeof(TimeSpan), typeof(ZBandwidthChart));
        public TimeSpan TimeWindow { get => (TimeSpan)GetValue(TimeWindowProperty); set => SetValue(TimeWindowProperty, value); }
        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
        }
    }

    [Obsolete("ZeroBandwidthChart is deprecated.")]
    public class ZeroBandwidthChart : ZBandwidthChart { }

    [Obsolete("BandwidthChart is deprecated.")]
    public class BandwidthChart : ZBandwidthChart { }
}
