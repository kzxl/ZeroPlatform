using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ZeroUI.Wpf.Theme;
using ZeroUI.Core.Workflow;

namespace ZeroUI.Wpf.Workflow
{
    public class ZFlowDesigner : FrameworkElement
    {
        public List<FlowNode> Nodes { get; set; } = new List<FlowNode>();
        public List<FlowEdge> Edges { get; set; } = new List<FlowEdge>();
        public bool AllowEdit { get; set; }

        public event EventHandler? NodeAdded;
        public event EventHandler? EdgeAdded;
        public event EventHandler? SelectionChanged;

        public ZFlowDesigner()
        {
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ZeroWpfTheme.ThemeChanged += OnThemeChanged;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            ZeroWpfTheme.ThemeChanged -= OnThemeChanged;
        }

        private void OnThemeChanged() => InvalidateVisual();

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            // drawingContext.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight));
        }

        
    }

    [Obsolete("FlowDesigner is deprecated and will be removed in 5 release cycles. Please migrate to ZFlowDesigner instead.")]
    public class FlowDesigner : ZFlowDesigner { }

    [Obsolete("ZeroFlowDesigner is deprecated. Please use ZFlowDesigner instead.")]
    public class ZeroFlowDesigner : ZFlowDesigner { }
}
