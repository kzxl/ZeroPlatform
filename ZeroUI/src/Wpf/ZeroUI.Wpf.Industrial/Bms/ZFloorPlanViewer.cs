using System;
using System.Windows;
using System.Windows.Media;

namespace ZeroUI.Wpf.Industrial
{    public class SensorPin { public double X { get; set; } public double Y { get; set; } public double Value { get; set; } public string Label { get; set; } = string.Empty; }
    /// <summary>
    /// ZFloorPlanViewer WPF control.
    /// </summary>
    public class ZFloorPlanViewer : FrameworkElement
    {        public static readonly DependencyProperty FloorPlanImageProperty = DependencyProperty.Register(nameof(FloorPlanImage), typeof(object), typeof(ZFloorPlanViewer));
        public object FloorPlanImage { get => (object)GetValue(FloorPlanImageProperty); set => SetValue(FloorPlanImageProperty, value); }
        public static readonly DependencyProperty SensorsProperty = DependencyProperty.Register(nameof(Sensors), typeof(System.Collections.Generic.List<SensorPin>), typeof(ZFloorPlanViewer));
        public System.Collections.Generic.List<SensorPin> Sensors { get => (System.Collections.Generic.List<SensorPin>)GetValue(SensorsProperty); set => SetValue(SensorsProperty, value); }
        public static readonly DependencyProperty ShowSensorValuesProperty = DependencyProperty.Register(nameof(ShowSensorValues), typeof(bool), typeof(ZFloorPlanViewer));
        public bool ShowSensorValues { get => (bool)GetValue(ShowSensorValuesProperty); set => SetValue(ShowSensorValuesProperty, value); }
        public static readonly RoutedEvent SensorClickedEvent = EventManager.RegisterRoutedEvent(nameof(SensorClicked), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ZFloorPlanViewer));
        public event RoutedEventHandler SensorClicked { add { AddHandler(SensorClickedEvent, value); } remove { RemoveHandler(SensorClickedEvent, value); } }
        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
        }
    }

    [Obsolete("ZeroFloorPlanViewer is deprecated.")]
    public class ZeroFloorPlanViewer : ZFloorPlanViewer { }

    [Obsolete("FloorPlanViewer is deprecated.")]
    public class FloorPlanViewer : ZFloorPlanViewer { }
}
