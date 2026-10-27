using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace ZeroUI.WinForms.Industrial
{    /// <summary>
    /// ZEnergyDashboard WinForms control.
    /// </summary>
    public class ZEnergyDashboard : Control
    {        public double CurrentKw { get; set; }
        public double DailyKwh { get; set; }
        public double MonthlyKwh { get; set; }
        public double[] Trend { get; set; } = new double[0];
        public double TargetKwh { get; set; }
        public bool ShowTrend { get; set; }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
        }
    }

    [Obsolete("ZeroEnergyDashboard is deprecated.")]
    [ToolboxItem(false)]
    public class ZeroEnergyDashboard : ZEnergyDashboard { }

    [Obsolete("EnergyDashboard is deprecated.")]
    [ToolboxItem(false)]
    public class EnergyDashboard : ZEnergyDashboard { }
}
