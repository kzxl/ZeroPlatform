using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace ZeroUI.WinForms.Industrial
{    /// <summary>
    /// ZComfortZoneChart WinForms control.
    /// </summary>
    public class ZComfortZoneChart : Control
    {        public double Temperature { get; set; }
        public double Humidity { get; set; }
        public System.Drawing.RectangleF ComfortZone { get; set; }
        public bool ShowCurrentPoint { get; set; }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
        }
    }

    [Obsolete("ZeroComfortZoneChart is deprecated.")]
    [ToolboxItem(false)]
    public class ZeroComfortZoneChart : ZComfortZoneChart { }

    [Obsolete("ComfortZoneChart is deprecated.")]
    [ToolboxItem(false)]
    public class ComfortZoneChart : ZComfortZoneChart { }
}
