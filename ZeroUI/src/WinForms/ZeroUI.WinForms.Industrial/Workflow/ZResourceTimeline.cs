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
    [DefaultEvent("AllocationClicked")]
    public class ZResourceTimeline : Control
    {
        public List<Resource> Resources { get; set; }
        public TimeSpan TimeRange { get; set; }

        public event EventHandler? AllocationClicked;

        public ZResourceTimeline()
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

    [Obsolete("ResourceTimeline is deprecated and will be removed in 5 release cycles. Please migrate to ZResourceTimeline instead.")]
    [ToolboxItem(false)]
    public class ResourceTimeline : ZResourceTimeline { }

    [Obsolete("ZeroResourceTimeline is deprecated. Please use ZResourceTimeline instead.")]
    [ToolboxItem(false)]
    public class ZeroResourceTimeline : ZResourceTimeline { }
}
