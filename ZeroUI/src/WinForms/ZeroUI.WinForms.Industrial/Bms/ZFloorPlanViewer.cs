using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace ZeroUI.WinForms.Industrial
{    public class SensorPin { public double X { get; set; } public double Y { get; set; } public double Value { get; set; } public string Label { get; set; } = string.Empty; }
    /// <summary>
    /// ZFloorPlanViewer WinForms control.
    /// </summary>
    public class ZFloorPlanViewer : Control
    {        public object FloorPlanImage { get; set; }
        public System.Collections.Generic.List<SensorPin> Sensors { get; set; } = new System.Collections.Generic.List<SensorPin>();
        public bool ShowSensorValues { get; set; }
        public event EventHandler SensorClicked;
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
        }
    }

    [Obsolete("ZeroFloorPlanViewer is deprecated.")]
    [ToolboxItem(false)]
    public class ZeroFloorPlanViewer : ZFloorPlanViewer { }

    [Obsolete("FloorPlanViewer is deprecated.")]
    [ToolboxItem(false)]
    public class FloorPlanViewer : ZFloorPlanViewer { }
}
