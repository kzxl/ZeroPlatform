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
    /// ZConfusionMatrix control for AiMl.
    /// </summary>
    public class ZConfusionMatrix : ZChart
    {
        public string[] Labels { get; set; } public int[,] Matrix { get; set; } public bool ShowPercentages { get; set; } public string ColorScale { get; set; } public string Title { get; set; }

        public ZConfusionMatrix()
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
                e.Graphics.DrawString("ZConfusionMatrix rendering", System.Drawing.SystemFonts.DefaultFont, brush, 10, 10);
            }
        }
    }

    [Obsolete("Use ZConfusionMatrix instead.")]
    public class ZeroConfusionMatrix : ZConfusionMatrix { }
}

