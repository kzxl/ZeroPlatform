using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace ZeroUI.WinForms.Industrial
{    /// <summary>
    /// ZBandwidthChart WinForms control.
    /// </summary>
    public class ZBandwidthChart : Control
    {        public double[] InboundData { get; set; } = new double[0];
        public double[] OutboundData { get; set; } = new double[0];
        public double MaxBandwidth { get; set; }
        public TimeSpan TimeWindow { get; set; }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
        }
    }

    [Obsolete("ZeroBandwidthChart is deprecated.")]
    [ToolboxItem(false)]
    public class ZeroBandwidthChart : ZBandwidthChart { }

    [Obsolete("BandwidthChart is deprecated.")]
    [ToolboxItem(false)]
    public class BandwidthChart : ZBandwidthChart { }
}
