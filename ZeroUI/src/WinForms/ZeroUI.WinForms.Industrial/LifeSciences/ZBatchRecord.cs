using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace ZeroUI.WinForms.Industrial
{    public class BatchRecordEntry { public string Step { get; set; } = string.Empty; public string Parameter { get; set; } = string.Empty; public string Value { get; set; } = string.Empty; public DateTime Timestamp { get; set; } public string Operator { get; set; } = string.Empty; public string Signature { get; set; } = string.Empty; }
    /// <summary>
    /// ZBatchRecord WinForms control.
    /// </summary>
    public class ZBatchRecord : Control
    {        public System.Collections.Generic.List<BatchRecordEntry> Records { get; set; } = new System.Collections.Generic.List<BatchRecordEntry>();
        public bool ShowSignatures { get; set; }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
        }
    }

    [Obsolete("ZeroBatchRecord is deprecated.")]
    [ToolboxItem(false)]
    public class ZeroBatchRecord : ZBatchRecord { }

    [Obsolete("BatchRecord is deprecated.")]
    [ToolboxItem(false)]
    public class BatchRecord : ZBatchRecord { }
}
