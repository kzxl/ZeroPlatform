using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace ZeroUI.WinForms.Industrial
{    public class SoeEvent { public DateTime Timestamp { get; set; } public string Source { get; set; } = string.Empty; public string Description { get; set; } = string.Empty; public int Priority { get; set; } }
    /// <summary>
    /// ZEventChronicle WinForms control.
    /// </summary>
    public class ZEventChronicle : Control
    {        public System.Collections.Generic.List<SoeEvent> Events { get; set; } = new System.Collections.Generic.List<SoeEvent>();
        public TimeSpan TimeRange { get; set; }
        public bool ShowFilter { get; set; }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
        }
    }

    [Obsolete("ZeroEventChronicle is deprecated.")]
    [ToolboxItem(false)]
    public class ZeroEventChronicle : ZEventChronicle { }

    [Obsolete("EventChronicle is deprecated.")]
    [ToolboxItem(false)]
    public class EventChronicle : ZEventChronicle { }
}
