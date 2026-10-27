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
    [DefaultEvent("ShiftChanged")]
    public class ZShiftPlanner : Control
    {
        public List<Employee> Employees { get; set; }
        public List<Shift> Shifts { get; set; }
        public TimeSpan DateRange { get; set; }

        public event EventHandler? ShiftChanged;

        public ZShiftPlanner()
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

    [Obsolete("ShiftPlanner is deprecated and will be removed in 5 release cycles. Please migrate to ZShiftPlanner instead.")]
    [ToolboxItem(false)]
    public class ShiftPlanner : ZShiftPlanner { }

    [Obsolete("ZeroShiftPlanner is deprecated. Please use ZShiftPlanner instead.")]
    [ToolboxItem(false)]
    public class ZeroShiftPlanner : ZShiftPlanner { }
}
