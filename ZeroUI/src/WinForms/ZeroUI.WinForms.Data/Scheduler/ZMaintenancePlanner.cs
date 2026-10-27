using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using ZeroUI.WinForms.Theme;
using ZeroUI.Core.Scheduler;

namespace ZeroUI.WinForms.Scheduler
{
    [ToolboxItem(true)]
    [Category("ZeroUI")]
    [DefaultEvent("WorkOrderClicked")]
    public class ZMaintenancePlanner : Control
    {
        public List<WorkOrder> WorkOrders { get; set; }
        public WorkOrderViewMode ViewMode { get; set; }

        public event EventHandler? WorkOrderClicked;

        public ZMaintenancePlanner()
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

    [Obsolete("MaintenancePlanner is deprecated and will be removed in 5 release cycles. Please migrate to ZMaintenancePlanner instead.")]
    [ToolboxItem(false)]
    public class MaintenancePlanner : ZMaintenancePlanner { }

    [Obsolete("ZeroMaintenancePlanner is deprecated. Please use ZMaintenancePlanner instead.")]
    [ToolboxItem(false)]
    public class ZeroMaintenancePlanner : ZMaintenancePlanner { }
}
