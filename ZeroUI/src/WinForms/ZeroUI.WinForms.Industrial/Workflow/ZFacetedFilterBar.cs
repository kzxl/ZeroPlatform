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
    /// ZFacetedFilterBar control for Workflow.
    /// </summary>
    public class ZFacetedFilterBar : ControlBase
    {
        public System.Collections.Generic.List<ZeroUI.Core.Workflow.Facet> Facets { get; set; } public event EventHandler FilterChanged;

        public ZFacetedFilterBar()
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
                e.Graphics.DrawString("ZFacetedFilterBar rendering", System.Drawing.SystemFonts.DefaultFont, brush, 10, 10);
            }
        }

        protected override void OnThemeChanged(ZeroSkin skin)
        {
            base.OnThemeChanged(skin);
            Invalidate();
        }
    }

    [Obsolete("Use ZFacetedFilterBar instead.")]
    public class ZeroFacetedFilterBar : ZFacetedFilterBar { }
}
