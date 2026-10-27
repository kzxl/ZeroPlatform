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
    [DefaultEvent("NodeClicked")]
    public class ZOrgChart : Control
    {
        public OrgNode? RootNode { get; set; }
        public OrgChartOrientation Orientation { get; set; }
        public int NodeSpacing { get; set; }

        public event EventHandler? NodeClicked;

        public ZOrgChart()
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

    [Obsolete("OrgChart is deprecated and will be removed in 5 release cycles. Please migrate to ZOrgChart instead.")]
    [ToolboxItem(false)]
    public class OrgChart : ZOrgChart { }

    [Obsolete("ZeroOrgChart is deprecated. Please use ZOrgChart instead.")]
    [ToolboxItem(false)]
    public class ZeroOrgChart : ZOrgChart { }
}
