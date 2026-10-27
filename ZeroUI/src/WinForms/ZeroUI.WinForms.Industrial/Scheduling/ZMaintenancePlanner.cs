using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using ZeroUI.Core.Theme;
using ZeroUI.WinForms.Theme;
using ZeroUI.WinForms.Base;


namespace ZeroUI.WinForms.Industrial.Scheduling
{
    /// <summary>
    /// ZMaintenancePlanner control for Scheduling.
    /// </summary>
    public class ZMaintenancePlanner : ControlBase
    {
        public System.Collections.Generic.List<ZeroUI.Core.Scheduling.MaintenanceTask> Tasks { get; set; } public DateTime CurrentDate { get; set; }

        public ZMaintenancePlanner()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.Clear(ZeroTheme.Colors.Background);
            using (var brush = new SolidBrush(ZeroTheme.Colors.TextPrimary))
            {
                e.Graphics.DrawString("ZMaintenancePlanner rendering", System.Drawing.SystemFonts.DefaultFont, brush, 10, 10);
            }
        }

        protected override void OnThemeChanged(ZeroSkin skin)
        {
            base.OnThemeChanged(skin);
            Invalidate();
        }
    }

    [Obsolete("Use ZMaintenancePlanner instead.")]
    public class ZeroMaintenancePlanner : ZMaintenancePlanner { }
}
