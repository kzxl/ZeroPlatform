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
    /// ZGeoFence control for Gis.
    /// </summary>
    public class ZGeoFence : ControlBase
    {
        public System.Collections.Generic.List<ZeroUI.Core.Gis.GeoPoint> Polygon { get; set; } public bool EditMode { get; set; } public System.Drawing.Color FillColor { get; set; } public System.Drawing.Color BorderColor { get; set; } public event EventHandler PolygonChanged;

        public ZGeoFence()
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
                e.Graphics.DrawString("ZGeoFence rendering", System.Drawing.SystemFonts.DefaultFont, brush, 10, 10);
            }
        }

        protected override void OnThemeChanged(ZeroSkin skin)
        {
            base.OnThemeChanged(skin);
            Invalidate();
        }
    }

    [Obsolete("Use ZGeoFence instead.")]
    public class ZeroGeoFence : ZGeoFence { }
}
