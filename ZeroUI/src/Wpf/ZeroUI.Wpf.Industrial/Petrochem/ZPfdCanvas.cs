using System;
using System.Windows;
using System.Windows.Media;

namespace ZeroUI.Wpf.Industrial
{    public class PfdNode { public double X { get; set; } public double Y { get; set; } public string Type { get; set; } = string.Empty; public string Label { get; set; } = string.Empty; }
    public class PfdConnection { public PfdNode Source { get; set; } public PfdNode Target { get; set; } }
    /// <summary>
    /// ZPfdCanvas WPF control.
    /// </summary>
    public class ZPfdCanvas : FrameworkElement
    {        public static readonly DependencyProperty NodesProperty = DependencyProperty.Register(nameof(Nodes), typeof(System.Collections.Generic.List<PfdNode>), typeof(ZPfdCanvas));
        public System.Collections.Generic.List<PfdNode> Nodes { get => (System.Collections.Generic.List<PfdNode>)GetValue(NodesProperty); set => SetValue(NodesProperty, value); }
        public static readonly DependencyProperty ConnectionsProperty = DependencyProperty.Register(nameof(Connections), typeof(System.Collections.Generic.List<PfdConnection>), typeof(ZPfdCanvas));
        public System.Collections.Generic.List<PfdConnection> Connections { get => (System.Collections.Generic.List<PfdConnection>)GetValue(ConnectionsProperty); set => SetValue(ConnectionsProperty, value); }
        public static readonly DependencyProperty ShowGridProperty = DependencyProperty.Register(nameof(ShowGrid), typeof(bool), typeof(ZPfdCanvas));
        public bool ShowGrid { get => (bool)GetValue(ShowGridProperty); set => SetValue(ShowGridProperty, value); }
        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
        }
    }

    [Obsolete("ZeroPfdCanvas is deprecated.")]
    public class ZeroPfdCanvas : ZPfdCanvas { }

    [Obsolete("PfdCanvas is deprecated.")]
    public class PfdCanvas : ZPfdCanvas { }
}
