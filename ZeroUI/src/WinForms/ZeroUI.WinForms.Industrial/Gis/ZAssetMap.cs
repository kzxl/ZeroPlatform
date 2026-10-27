using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using ZeroUI.Core.Theme;
using ZeroUI.WinForms.Theme;
using ZeroUI.WinForms.Base;


namespace ZeroUI.WinForms.Industrial.Gis
{
    /// <summary>
    /// ZAssetMap control for Gis.
    /// </summary>
    public class ZAssetMap : ControlBase
    {
        public System.Drawing.Image BackgroundImage { get; set; } public System.Collections.Generic.List<ZeroUI.Core.Gis.AssetPin> Assets { get; set; } public bool ShowLabels { get; set; } public bool ShowStatusColor { get; set; } public event EventHandler AssetClicked;

        public ZAssetMap()
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
                e.Graphics.DrawString("ZAssetMap rendering", System.Drawing.SystemFonts.DefaultFont, brush, 10, 10);
            }
        }

        protected override void OnThemeChanged(ZeroSkin skin)
        {
            base.OnThemeChanged(skin);
            Invalidate();
        }
    }

    [Obsolete("Use ZAssetMap instead.")]
    public class ZeroAssetMap : ZAssetMap { }
}
