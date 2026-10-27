using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using ZeroUI.WinForms.Theme;
using ZeroUI.Core.Workflow;

namespace ZeroUI.WinForms.Workflow
{
    [ToolboxItem(true)]
    [Category("ZeroUI")]
    [DefaultEvent("SelectionChanged")]
    public class ZFlowDesigner : Control
    {
        public List<FlowNode> Nodes { get; set; }
        public List<FlowEdge> Edges { get; set; }
        public bool AllowEdit { get; set; }

        public event EventHandler? NodeAdded;
        public event EventHandler? EdgeAdded;
        public event EventHandler? SelectionChanged;

        public ZFlowDesigner()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            ZeroTheme.ThemeChanged += OnThemeChanged;
        }

        private void OnThemeChanged(object sender, EventArgs e) => Invalidate();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                ZeroTheme.ThemeChanged -= OnThemeChanged;
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.Clear(ZeroTheme.Colors.Surface);
        }

        
    }

    [Obsolete("FlowDesigner is deprecated and will be removed in 5 release cycles. Please migrate to ZFlowDesigner instead.")]
    [ToolboxItem(false)]
    public class FlowDesigner : ZFlowDesigner { }

    [Obsolete("ZeroFlowDesigner is deprecated. Please use ZFlowDesigner instead.")]
    [ToolboxItem(false)]
    public class ZeroFlowDesigner : ZFlowDesigner { }
}
