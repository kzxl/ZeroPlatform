using System;
using System.Windows;
using System.Windows.Media;

namespace ZeroUI.Wpf.Industrial
{    public class MapStation { public double X { get; set; } public double Y { get; set; } public string Label { get; set; } = string.Empty; public string Status { get; set; } = string.Empty; }
    public class PipelineSegment { public MapStation Start { get; set; } public MapStation End { get; set; } }
    /// <summary>
    /// ZScadaMap WPF control.
    /// </summary>
    public class ZScadaMap : FrameworkElement
    {        public static readonly DependencyProperty BackgroundImageProperty = DependencyProperty.Register(nameof(BackgroundImage), typeof(object), typeof(ZScadaMap));
        public object BackgroundImage { get => (object)GetValue(BackgroundImageProperty); set => SetValue(BackgroundImageProperty, value); }
        public static readonly DependencyProperty StationsProperty = DependencyProperty.Register(nameof(Stations), typeof(System.Collections.Generic.List<MapStation>), typeof(ZScadaMap));
        public System.Collections.Generic.List<MapStation> Stations { get => (System.Collections.Generic.List<MapStation>)GetValue(StationsProperty); set => SetValue(StationsProperty, value); }
        public static readonly DependencyProperty PipelinesProperty = DependencyProperty.Register(nameof(Pipelines), typeof(System.Collections.Generic.List<PipelineSegment>), typeof(ZScadaMap));
        public System.Collections.Generic.List<PipelineSegment> Pipelines { get => (System.Collections.Generic.List<PipelineSegment>)GetValue(PipelinesProperty); set => SetValue(PipelinesProperty, value); }
        public static readonly RoutedEvent StationClickedEvent = EventManager.RegisterRoutedEvent(nameof(StationClicked), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ZScadaMap));
        public event RoutedEventHandler StationClicked { add { AddHandler(StationClickedEvent, value); } remove { RemoveHandler(StationClickedEvent, value); } }
        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
        }
    }

    [Obsolete("ZeroScadaMap is deprecated.")]
    public class ZeroScadaMap : ZScadaMap { }

    [Obsolete("ScadaMap is deprecated.")]
    public class ScadaMap : ZScadaMap { }
}
