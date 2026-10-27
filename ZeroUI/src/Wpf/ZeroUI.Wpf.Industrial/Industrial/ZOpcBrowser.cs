using System;
using System.Windows;
using System.Windows.Media;

namespace ZeroUI.Wpf.Industrial
{    public class OpcNode { public string Name { get; set; } = string.Empty; public System.Collections.Generic.List<OpcNode> Children { get; set; } = new System.Collections.Generic.List<OpcNode>(); public string NodeType { get; set; } = string.Empty; }
    /// <summary>
    /// ZOpcBrowser WPF control.
    /// </summary>
    public class ZOpcBrowser : FrameworkElement
    {        public static readonly DependencyProperty RootNodesProperty = DependencyProperty.Register(nameof(RootNodes), typeof(System.Collections.Generic.List<OpcNode>), typeof(ZOpcBrowser));
        public System.Collections.Generic.List<OpcNode> RootNodes { get => (System.Collections.Generic.List<OpcNode>)GetValue(RootNodesProperty); set => SetValue(RootNodesProperty, value); }
        public static readonly DependencyProperty SelectedTagProperty = DependencyProperty.Register(nameof(SelectedTag), typeof(OpcNode), typeof(ZOpcBrowser));
        public OpcNode SelectedTag { get => (OpcNode)GetValue(SelectedTagProperty); set => SetValue(SelectedTagProperty, value); }
        public static readonly DependencyProperty ShowNodeTypeProperty = DependencyProperty.Register(nameof(ShowNodeType), typeof(bool), typeof(ZOpcBrowser));
        public bool ShowNodeType { get => (bool)GetValue(ShowNodeTypeProperty); set => SetValue(ShowNodeTypeProperty, value); }
        public static readonly RoutedEvent TagSelectedEvent = EventManager.RegisterRoutedEvent(nameof(TagSelected), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ZOpcBrowser));
        public event RoutedEventHandler TagSelected { add { AddHandler(TagSelectedEvent, value); } remove { RemoveHandler(TagSelectedEvent, value); } }
        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
        }
    }

    [Obsolete("ZeroOpcBrowser is deprecated.")]
    public class ZeroOpcBrowser : ZOpcBrowser { }

    [Obsolete("OpcBrowser is deprecated.")]
    public class OpcBrowser : ZOpcBrowser { }
}
