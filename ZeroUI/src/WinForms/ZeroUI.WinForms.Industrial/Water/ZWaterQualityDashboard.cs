using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace ZeroUI.WinForms.Industrial
{    /// <summary>
    /// ZWaterQualityDashboard WinForms control.
    /// </summary>
    public class ZWaterQualityDashboard : Control
    {        public double Ph { get; set; }
        public double Turbidity { get; set; }
        public double Chlorine { get; set; }
        public double Conductivity { get; set; }
        public bool ShowTrend { get; set; }
        public double AlertLimits { get; set; }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
        }
    }

    [Obsolete("ZeroWaterQualityDashboard is deprecated.")]
    [ToolboxItem(false)]
    public class ZeroWaterQualityDashboard : ZWaterQualityDashboard { }

    [Obsolete("WaterQualityDashboard is deprecated.")]
    [ToolboxItem(false)]
    public class WaterQualityDashboard : ZWaterQualityDashboard { }
}
