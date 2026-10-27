using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace ZeroUI.WinForms.Industrial
{    /// <summary>
    /// ZProtocolDecoder WinForms control.
    /// </summary>
    public class ZProtocolDecoder : Control
    {        public byte[] RawBytes { get; set; } = new byte[0];
        public string Protocol { get; set; } = string.Empty;
        public System.Collections.Generic.Dictionary<string, string> DecodedFields { get; set; } = new System.Collections.Generic.Dictionary<string, string>();
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
        }
    }

    [Obsolete("ZeroProtocolDecoder is deprecated.")]
    [ToolboxItem(false)]
    public class ZeroProtocolDecoder : ZProtocolDecoder { }

    [Obsolete("ProtocolDecoder is deprecated.")]
    [ToolboxItem(false)]
    public class ProtocolDecoder : ZProtocolDecoder { }
}
