using System;
using System.Windows;
using System.Windows.Media;

namespace ZeroUI.Wpf.Industrial
{    public class SoeEvent { public DateTime Timestamp { get; set; } public string Source { get; set; } = string.Empty; public string Description { get; set; } = string.Empty; public int Priority { get; set; } }
    /// <summary>
    /// ZEventChronicle WPF control.
    /// </summary>
    public class ZEventChronicle : FrameworkElement
    {        public static readonly DependencyProperty EventsProperty = DependencyProperty.Register(nameof(Events), typeof(System.Collections.Generic.List<SoeEvent>), typeof(ZEventChronicle));
        public System.Collections.Generic.List<SoeEvent> Events { get => (System.Collections.Generic.List<SoeEvent>)GetValue(EventsProperty); set => SetValue(EventsProperty, value); }
        public static readonly DependencyProperty TimeRangeProperty = DependencyProperty.Register(nameof(TimeRange), typeof(TimeSpan), typeof(ZEventChronicle));
        public TimeSpan TimeRange { get => (TimeSpan)GetValue(TimeRangeProperty); set => SetValue(TimeRangeProperty, value); }
        public static readonly DependencyProperty ShowFilterProperty = DependencyProperty.Register(nameof(ShowFilter), typeof(bool), typeof(ZEventChronicle));
        public bool ShowFilter { get => (bool)GetValue(ShowFilterProperty); set => SetValue(ShowFilterProperty, value); }
        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
        }
    }

    [Obsolete("ZeroEventChronicle is deprecated.")]
    public class ZeroEventChronicle : ZEventChronicle { }

    [Obsolete("EventChronicle is deprecated.")]
    public class EventChronicle : ZEventChronicle { }
}
