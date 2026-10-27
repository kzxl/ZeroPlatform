using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using ZeroUI.Core.Theme;
using ZeroUI.WinForms.Theme;
using ZeroUI.WinForms.Base;


namespace ZeroUI.WinForms.Industrial.Industrial
{
    /// <summary>
    /// ZHexInspector control for Industrial.
    /// </summary>
    public class ZHexInspector : ControlBase
    {
        public byte[] Data { get; set; } public int BytesPerRow { get; set; } = 16; public bool ShowAscii { get; set; } public System.Collections.Generic.List<ZeroUI.Core.Industrial.ByteRange> HighlightRanges { get; set; } public int SelectedOffset { get; set; }

        public ZHexInspector()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.Clear(ZeroTheme.Colors.Background);
            using (var brush = new SolidBrush(ZeroTheme.Colors.TextPrimary))
            {
                e.Graphics.DrawString("ZHexInspector rendering", System.Drawing.SystemFonts.DefaultFont, brush, 10, 10);
            }
        }

        protected override void OnThemeChanged(ZeroSkin skin)
        {
            base.OnThemeChanged(skin);
            Invalidate();
        }
    }

    [Obsolete("Use ZHexInspector instead.")]
    public class ZeroHexInspector : ZHexInspector { }
}
