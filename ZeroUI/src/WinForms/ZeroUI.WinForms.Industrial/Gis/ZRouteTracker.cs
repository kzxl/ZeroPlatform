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
    /// ZRouteTracker control for Gis.
    /// </summary>
    public class ZRouteTracker : ControlBase
    {
        public System.Collections.Generic.List<ZeroUI.Core.Gis.GeoPoint> RoutePoints { get; set; } public ZeroUI.Core.Gis.GeoPoint CurrentPosition { get; set; } public bool ShowTrail { get; set; } public System.Drawing.Color TrailColor { get; set; } public string VehicleGlyph { get; set; } public event EventHandler PositionUpdated;

        public ZRouteTracker()
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
                e.Graphics.DrawString("ZRouteTracker rendering", System.Drawing.SystemFonts.DefaultFont, brush, 10, 10);
            }
        }

        protected override void OnThemeChanged(ZeroSkin skin)
        {
            base.OnThemeChanged(skin);
            Invalidate();
        }
    }

    [Obsolete("Use ZRouteTracker instead.")]
    public class ZeroRouteTracker : ZRouteTracker { }
}
