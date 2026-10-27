using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace ZeroUI.WinForms.Industrial
{    public class TrendPen { public string Name { get; set; } = string.Empty; public System.Drawing.Color Color { get; set; } }
    /// <summary>
    /// ZHistoricalTrend WinForms control.
    /// </summary>
    public class ZHistoricalTrend : Control
    {        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public System.Collections.Generic.List<TrendPen> Pens { get; set; } = new System.Collections.Generic.List<TrendPen>();
        public event EventHandler TimeRangeChanged;
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
        }
    }

    [Obsolete("ZeroHistoricalTrend is deprecated.")]
    [ToolboxItem(false)]
    public class ZeroHistoricalTrend : ZHistoricalTrend { }

    [Obsolete("HistoricalTrend is deprecated.")]
    [ToolboxItem(false)]
    public class HistoricalTrend : ZHistoricalTrend { }
}
