using System;
using System.Windows;
using System.Windows.Media;

namespace ZeroUI.Wpf.Industrial
{    public class TrendPen { public string Name { get; set; } = string.Empty; public System.Windows.Media.Color Color { get; set; } }
    /// <summary>
    /// ZHistoricalTrend WPF control.
    /// </summary>
    public class ZHistoricalTrend : FrameworkElement
    {        public static readonly DependencyProperty StartTimeProperty = DependencyProperty.Register(nameof(StartTime), typeof(DateTime), typeof(ZHistoricalTrend));
        public DateTime StartTime { get => (DateTime)GetValue(StartTimeProperty); set => SetValue(StartTimeProperty, value); }
        public static readonly DependencyProperty EndTimeProperty = DependencyProperty.Register(nameof(EndTime), typeof(DateTime), typeof(ZHistoricalTrend));
        public DateTime EndTime { get => (DateTime)GetValue(EndTimeProperty); set => SetValue(EndTimeProperty, value); }
        public static readonly DependencyProperty PensProperty = DependencyProperty.Register(nameof(Pens), typeof(System.Collections.Generic.List<TrendPen>), typeof(ZHistoricalTrend));
        public System.Collections.Generic.List<TrendPen> Pens { get => (System.Collections.Generic.List<TrendPen>)GetValue(PensProperty); set => SetValue(PensProperty, value); }
        public static readonly RoutedEvent TimeRangeChangedEvent = EventManager.RegisterRoutedEvent(nameof(TimeRangeChanged), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ZHistoricalTrend));
        public event RoutedEventHandler TimeRangeChanged { add { AddHandler(TimeRangeChangedEvent, value); } remove { RemoveHandler(TimeRangeChangedEvent, value); } }
        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
        }
    }

    [Obsolete("ZeroHistoricalTrend is deprecated.")]
    public class ZeroHistoricalTrend : ZHistoricalTrend { }

    [Obsolete("HistoricalTrend is deprecated.")]
    public class HistoricalTrend : ZHistoricalTrend { }
}
