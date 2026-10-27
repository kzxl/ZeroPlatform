using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using ZeroUI.Core.Theme;
using ZeroUI.WinForms.Theme;
using ZeroUI.WinForms.Base;


using ZeroUI.WinForms.Theme;
namespace ZeroUI.WinForms.Charts.AiMl
{
    /// <summary>
    /// ZFeatureImportance control for AiMl.
    /// </summary>
    public class ZFeatureImportance : ZChart
    {
        public System.Collections.Generic.List<ZeroUI.Core.AiMl.FeatureScore> Features { get; set; } public bool ShowValues { get; set; } public System.Drawing.Color BarColor { get; set; } public bool SortDescending { get; set; }

        public ZFeatureImportance()
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
                e.Graphics.DrawString("ZFeatureImportance rendering", System.Drawing.SystemFonts.DefaultFont, brush, 10, 10);
            }
        }
    }

    [Obsolete("Use ZFeatureImportance instead.")]
    public class ZeroFeatureImportance : ZFeatureImportance { }
}

