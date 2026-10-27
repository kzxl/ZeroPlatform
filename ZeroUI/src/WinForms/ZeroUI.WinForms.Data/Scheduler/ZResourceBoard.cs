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
    [DefaultEvent("SlotClicked")]
    public class ZResourceBoard : Control
    {
        public List<ResourceRow> Resources { get; set; }
        public int TimeSlotWidth { get; set; }
        public bool ShowConflicts { get; set; }

        public event EventHandler? SlotClicked;

        public ZResourceBoard()
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

    [Obsolete("ResourceBoard is deprecated and will be removed in 5 release cycles. Please migrate to ZResourceBoard instead.")]
    [ToolboxItem(false)]
    public class ResourceBoard : ZResourceBoard { }

    [Obsolete("ZeroResourceBoard is deprecated. Please use ZResourceBoard instead.")]
    [ToolboxItem(false)]
    public class ZeroResourceBoard : ZResourceBoard { }
}
