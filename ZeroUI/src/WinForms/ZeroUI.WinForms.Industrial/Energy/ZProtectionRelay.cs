using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace ZeroUI.WinForms.Industrial
{    /// <summary>
    /// ZProtectionRelay WinForms control.
    /// </summary>
    public class ZProtectionRelay : Control
    {        public string RelayState { get; set; } = string.Empty;
        public string TripCode { get; set; } = string.Empty;
        public bool ResetEnabled { get; set; }
        public event EventHandler ResetClicked;
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
        }
    }

    [Obsolete("ZeroProtectionRelay is deprecated.")]
    [ToolboxItem(false)]
    public class ZeroProtectionRelay : ZProtectionRelay { }

    [Obsolete("ProtectionRelay is deprecated.")]
    [ToolboxItem(false)]
    public class ProtectionRelay : ZProtectionRelay { }
}
