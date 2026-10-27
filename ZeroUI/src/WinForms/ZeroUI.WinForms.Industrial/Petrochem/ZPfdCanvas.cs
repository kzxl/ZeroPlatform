using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace ZeroUI.WinForms.Industrial
{    public class PfdNode { public double X { get; set; } public double Y { get; set; } public string Type { get; set; } = string.Empty; public string Label { get; set; } = string.Empty; }
    public class PfdConnection { public PfdNode Source { get; set; } public PfdNode Target { get; set; } }
    /// <summary>
    /// ZPfdCanvas WinForms control.
    /// </summary>
    public class ZPfdCanvas : Control
    {        public System.Collections.Generic.List<PfdNode> Nodes { get; set; } = new System.Collections.Generic.List<PfdNode>();
        public System.Collections.Generic.List<PfdConnection> Connections { get; set; } = new System.Collections.Generic.List<PfdConnection>();
        public bool ShowGrid { get; set; }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
        }
    }

    [Obsolete("ZeroPfdCanvas is deprecated.")]
    [ToolboxItem(false)]
    public class ZeroPfdCanvas : ZPfdCanvas { }

    [Obsolete("PfdCanvas is deprecated.")]
    [ToolboxItem(false)]
    public class PfdCanvas : ZPfdCanvas { }
}
