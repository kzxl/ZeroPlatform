using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace ZeroUI.WinForms.Industrial
{    public class SequenceStep { public string Name { get; set; } = string.Empty; public string State { get; set; } = string.Empty; public TimeSpan Duration { get; set; } }
    /// <summary>
    /// ZBatchSequenceViewer WinForms control.
    /// </summary>
    public class ZBatchSequenceViewer : Control
    {        public System.Collections.Generic.List<SequenceStep> Steps { get; set; } = new System.Collections.Generic.List<SequenceStep>();
        public SequenceStep CurrentStep { get; set; }
        public bool ShowTimeline { get; set; }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
        }
    }

    [Obsolete("ZeroBatchSequenceViewer is deprecated.")]
    [ToolboxItem(false)]
    public class ZeroBatchSequenceViewer : ZBatchSequenceViewer { }

    [Obsolete("BatchSequenceViewer is deprecated.")]
    [ToolboxItem(false)]
    public class BatchSequenceViewer : ZBatchSequenceViewer { }
}
