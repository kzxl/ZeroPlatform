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
    /// ZModelMetrics control for AiMl.
    /// </summary>
    public class ZModelMetrics : ZChart
    {
        public double Accuracy { get; set; } public double Precision { get; set; } public double Recall { get; set; } public double F1Score { get; set; } public double AucRoc { get; set; } public double LossValue { get; set; } public string ModelName { get; set; } public DateTime TrainDate { get; set; }

        public ZModelMetrics()
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
                e.Graphics.DrawString("ZModelMetrics rendering", System.Drawing.SystemFonts.DefaultFont, brush, 10, 10);
            }
        }
    }

    [Obsolete("Use ZModelMetrics instead.")]
    public class ZeroModelMetrics : ZModelMetrics { }
}

