using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace ZeroUI.WinForms.Industrial
{    public class PacketRecord { public DateTime Timestamp { get; set; } public string Source { get; set; } = string.Empty; public string Dest { get; set; } = string.Empty; public string Protocol { get; set; } = string.Empty; public int Length { get; set; } public byte[] Data { get; set; } = new byte[0]; }
    /// <summary>
    /// ZPacketInspector WinForms control.
    /// </summary>
    public class ZPacketInspector : Control
    {        public System.Collections.Generic.List<PacketRecord> Packets { get; set; } = new System.Collections.Generic.List<PacketRecord>();
        public int MaxPackets { get; set; }
        public bool AutoScroll { get; set; }
        public string Filter { get; set; } = string.Empty;
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
        }
    }

    [Obsolete("ZeroPacketInspector is deprecated.")]
    [ToolboxItem(false)]
    public class ZeroPacketInspector : ZPacketInspector { }

    [Obsolete("PacketInspector is deprecated.")]
    [ToolboxItem(false)]
    public class PacketInspector : ZPacketInspector { }
}
