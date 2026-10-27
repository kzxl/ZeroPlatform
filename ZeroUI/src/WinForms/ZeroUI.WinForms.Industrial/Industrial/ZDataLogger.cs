using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace ZeroUI.WinForms.Industrial
{    /// <summary>
    /// ZDataLogger WinForms control.
    /// </summary>
    public class ZDataLogger : Control
    {        public System.Collections.Generic.List<string> Columns { get; set; } = new System.Collections.Generic.List<string>();
        public int MaxRows { get; set; }
        public bool AutoScroll { get; set; }
        public event EventHandler RowAdded;
        public void AddRow(object[] data) {}
        public void Clear() {}
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
        }
    }

    [Obsolete("ZeroDataLogger is deprecated.")]
    [ToolboxItem(false)]
    public class ZeroDataLogger : ZDataLogger { }

    [Obsolete("DataLogger is deprecated.")]
    [ToolboxItem(false)]
    public class DataLogger : ZDataLogger { }
}
