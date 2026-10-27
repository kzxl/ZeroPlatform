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
    [DefaultEvent("DateClicked")]
    public class ZCalendarView : Control
    {
        public List<CalendarEvent> Events { get; set; }
        public DateTime CurrentMonth { get; set; }
        public bool ShowWeekNumbers { get; set; }

        public event EventHandler? DateClicked;
        public event EventHandler? EventClicked;

        public ZCalendarView()
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

    [Obsolete("CalendarView is deprecated and will be removed in 5 release cycles. Please migrate to ZCalendarView instead.")]
    [ToolboxItem(false)]
    public class CalendarView : ZCalendarView { }

    [Obsolete("ZeroCalendarView is deprecated. Please use ZCalendarView instead.")]
    [ToolboxItem(false)]
    public class ZeroCalendarView : ZCalendarView { }
}
