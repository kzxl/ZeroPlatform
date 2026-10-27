using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace ZeroUI.WinForms.Industrial
{    /// <summary>
    /// ZFlareTower WinForms control.
    /// </summary>
    public class ZFlareTower : Control
    {        public double FlareIntensity { get; set; }
        public bool IsActive { get; set; }
        public System.Drawing.Color FlameColor { get; set; }
        public bool ShowSmoke { get; set; }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
        }
    }

    [Obsolete("ZeroFlareTower is deprecated.")]
    [ToolboxItem(false)]
    public class ZeroFlareTower : ZFlareTower { }

    [Obsolete("FlareTower is deprecated.")]
    [ToolboxItem(false)]
    public class FlareTower : ZFlareTower { }
}
