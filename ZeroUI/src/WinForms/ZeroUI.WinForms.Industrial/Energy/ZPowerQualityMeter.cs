using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace ZeroUI.WinForms.Industrial
{    /// <summary>
    /// ZPowerQualityMeter WinForms control.
    /// </summary>
    public class ZPowerQualityMeter : Control
    {        public double Voltage { get; set; }
        public double Current { get; set; }
        public double PowerFactor { get; set; }
        public double THD { get; set; }
        public double Frequency { get; set; }
        public bool ShowWaveform { get; set; }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
        }
    }

    [Obsolete("ZeroPowerQualityMeter is deprecated.")]
    [ToolboxItem(false)]
    public class ZeroPowerQualityMeter : ZPowerQualityMeter { }

    [Obsolete("PowerQualityMeter is deprecated.")]
    [ToolboxItem(false)]
    public class PowerQualityMeter : ZPowerQualityMeter { }
}
