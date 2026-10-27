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
    /// ZAnomalyHighlighter control for AiMl.
    /// </summary>
    public class ZAnomalyHighlighter : ZChart
    {
        public System.Collections.Generic.List<ZeroUI.Core.AiMl.TimeValue> DataPoints { get; set; } public System.Collections.Generic.List<ZeroUI.Core.AiMl.AnomalyRange> Anomalies { get; set; } public double ThresholdLine { get; set; } public bool ShowScores { get; set; }

        public ZAnomalyHighlighter()
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
                e.Graphics.DrawString("ZAnomalyHighlighter rendering", System.Drawing.SystemFonts.DefaultFont, brush, 10, 10);
            }
        }
    }

    [Obsolete("Use ZAnomalyHighlighter instead.")]
    public class ZeroAnomalyHighlighter : ZAnomalyHighlighter { }
}

