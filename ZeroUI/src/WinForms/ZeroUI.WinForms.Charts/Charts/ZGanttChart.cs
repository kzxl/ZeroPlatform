using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using ZeroUI.Core.Theme;
using ZeroUI.WinForms.Theme;
using ZeroUI.WinForms.Base;


using ZeroUI.WinForms.Theme;
namespace ZeroUI.WinForms.Charts.Charts
{
    /// <summary>
    /// ZGanttChart control for Charts.
    /// </summary>
    public class ZGanttChart : ZChart
    {
        public System.Collections.Generic.List<ZeroUI.Core.Charts.GanttTask> Tasks { get; set; } public bool ShowProgress { get; set; } public string TimeScale { get; set; } public bool ShowDependencies { get; set; }

        public ZGanttChart()
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
                e.Graphics.DrawString("ZGanttChart rendering", System.Drawing.SystemFonts.DefaultFont, brush, 10, 10);
            }
        }
    }

    [Obsolete("Use ZGanttChart instead.")]
    public class ZeroGanttChart : ZGanttChart { }
}

