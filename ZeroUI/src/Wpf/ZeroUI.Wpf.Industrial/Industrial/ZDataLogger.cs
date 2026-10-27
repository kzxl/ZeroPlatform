using System;
using System.Windows;
using System.Windows.Media;

namespace ZeroUI.Wpf.Industrial
{    /// <summary>
    /// ZDataLogger WPF control.
    /// </summary>
    public class ZDataLogger : FrameworkElement
    {        public static readonly DependencyProperty ColumnsProperty = DependencyProperty.Register(nameof(Columns), typeof(System.Collections.Generic.List<string>), typeof(ZDataLogger));
        public System.Collections.Generic.List<string> Columns { get => (System.Collections.Generic.List<string>)GetValue(ColumnsProperty); set => SetValue(ColumnsProperty, value); }
        public static readonly DependencyProperty MaxRowsProperty = DependencyProperty.Register(nameof(MaxRows), typeof(int), typeof(ZDataLogger));
        public int MaxRows { get => (int)GetValue(MaxRowsProperty); set => SetValue(MaxRowsProperty, value); }
        public static readonly DependencyProperty AutoScrollProperty = DependencyProperty.Register(nameof(AutoScroll), typeof(bool), typeof(ZDataLogger));
        public bool AutoScroll { get => (bool)GetValue(AutoScrollProperty); set => SetValue(AutoScrollProperty, value); }
        public static readonly RoutedEvent RowAddedEvent = EventManager.RegisterRoutedEvent(nameof(RowAdded), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ZDataLogger));
        public event RoutedEventHandler RowAdded { add { AddHandler(RowAddedEvent, value); } remove { RemoveHandler(RowAddedEvent, value); } }
        public void AddRow(object[] data) {}
        public void Clear() {}
        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
        }
    }

    [Obsolete("ZeroDataLogger is deprecated.")]
    public class ZeroDataLogger : ZDataLogger { }

    [Obsolete("DataLogger is deprecated.")]
    public class DataLogger : ZDataLogger { }
}
