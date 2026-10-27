using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using ZeroUI.Core.Theme;
using ZeroUI.WinForms.Theme;
using ZeroUI.WinForms.Base;


namespace ZeroUI.WinForms.Industrial.Workflow
{
    /// <summary>
    /// ZPivotMatrix control for Workflow.
    /// </summary>
    public class ZPivotMatrix : ControlBase
    {
        public string[] RowHeaders { get; set; } public string[] ColHeaders { get; set; } public double[,] Values { get; set; } public object CellColorResolver { get; set; } public bool ShowTotals { get; set; }

        public ZPivotMatrix()
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
                e.Graphics.DrawString("ZPivotMatrix rendering", System.Drawing.SystemFonts.DefaultFont, brush, 10, 10);
            }
        }

        protected override void OnThemeChanged(ZeroSkin skin)
        {
            base.OnThemeChanged(skin);
            Invalidate();
        }
    }

    [Obsolete("Use ZPivotMatrix instead.")]
    public class ZeroPivotMatrix : ZPivotMatrix { }
}
