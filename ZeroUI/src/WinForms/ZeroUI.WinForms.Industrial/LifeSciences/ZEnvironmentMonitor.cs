using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace ZeroUI.WinForms.Industrial
{    /// <summary>
    /// ZEnvironmentMonitor WinForms control.
    /// </summary>
    public class ZEnvironmentMonitor : Control
    {        public double Temperature { get; set; }
        public double Humidity { get; set; }
        public double ParticleCount { get; set; }
        public double DifferentialPressure { get; set; }
        public bool ShowTrend { get; set; }
        public double AlertThresholds { get; set; }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
        }
    }

    [Obsolete("ZeroEnvironmentMonitor is deprecated.")]
    [ToolboxItem(false)]
    public class ZeroEnvironmentMonitor : ZEnvironmentMonitor { }

    [Obsolete("EnvironmentMonitor is deprecated.")]
    [ToolboxItem(false)]
    public class EnvironmentMonitor : ZEnvironmentMonitor { }
}
