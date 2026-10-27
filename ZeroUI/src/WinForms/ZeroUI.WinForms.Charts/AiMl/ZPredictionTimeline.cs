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
    /// ZPredictionTimeline control for AiMl.
    /// </summary>
    public class ZPredictionTimeline : ZChart
    {
        public System.Collections.Generic.List<ZeroUI.Core.AiMl.TimeValue> Actuals { get; set; } public System.Collections.Generic.List<ZeroUI.Core.AiMl.TimeValue> Predictions { get; set; } public bool ShowConfidenceBand { get; set; } public double ConfidenceLevel { get; set; }

        public ZPredictionTimeline()
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
                e.Graphics.DrawString("ZPredictionTimeline rendering", System.Drawing.SystemFonts.DefaultFont, brush, 10, 10);
            }
        }
    }

    [Obsolete("Use ZPredictionTimeline instead.")]
    public class ZeroPredictionTimeline : ZPredictionTimeline { }
}

