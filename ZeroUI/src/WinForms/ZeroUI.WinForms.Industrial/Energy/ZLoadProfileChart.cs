using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace ZeroUI.WinForms.Industrial
{    /// <summary>
    /// ZLoadProfileChart WinForms control.
    /// </summary>
    public class ZLoadProfileChart : Control
    {        public double[] DemandData { get; set; } = new double[0];
        public double CapacityLimit { get; set; }
        public bool PeakHighlight { get; set; }
        public System.Collections.Generic.List<string> TimeLabels { get; set; } = new System.Collections.Generic.List<string>();
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
        }
    }

    [Obsolete("ZeroLoadProfileChart is deprecated.")]
    [ToolboxItem(false)]
    public class ZeroLoadProfileChart : ZLoadProfileChart { }

    [Obsolete("LoadProfileChart is deprecated.")]
    [ToolboxItem(false)]
    public class LoadProfileChart : ZLoadProfileChart { }
}
