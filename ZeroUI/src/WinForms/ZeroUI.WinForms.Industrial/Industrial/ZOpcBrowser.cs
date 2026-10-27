using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace ZeroUI.WinForms.Industrial
{    public class OpcNode { public string Name { get; set; } = string.Empty; public System.Collections.Generic.List<OpcNode> Children { get; set; } = new System.Collections.Generic.List<OpcNode>(); public string NodeType { get; set; } = string.Empty; }
    /// <summary>
    /// ZOpcBrowser WinForms control.
    /// </summary>
    public class ZOpcBrowser : Control
    {        public System.Collections.Generic.List<OpcNode> RootNodes { get; set; } = new System.Collections.Generic.List<OpcNode>();
        public OpcNode SelectedTag { get; set; }
        public bool ShowNodeType { get; set; }
        public event EventHandler TagSelected;
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
        }
    }

    [Obsolete("ZeroOpcBrowser is deprecated.")]
    [ToolboxItem(false)]
    public class ZeroOpcBrowser : ZOpcBrowser { }

    [Obsolete("OpcBrowser is deprecated.")]
    [ToolboxItem(false)]
    public class OpcBrowser : ZOpcBrowser { }
}
