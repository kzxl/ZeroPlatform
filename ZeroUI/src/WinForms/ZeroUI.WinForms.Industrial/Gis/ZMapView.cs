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
    /// ZMapView control for Gis.
    /// </summary>
    public class ZMapView : ControlBase
    {
        public double CenterLat { get; set; } public double CenterLon { get; set; } public double ZoomLevel { get; set; } public System.Collections.Generic.List<ZeroUI.Core.Gis.MapMarker> Markers { get; set; } public bool ShowGrid { get; set; } public event EventHandler MarkerClicked; public event EventHandler MapClicked;

        public ZMapView()
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
                e.Graphics.DrawString("ZMapView rendering", System.Drawing.SystemFonts.DefaultFont, brush, 10, 10);
            }
        }

        protected override void OnThemeChanged(ZeroSkin skin)
        {
            base.OnThemeChanged(skin);
            Invalidate();
        }
    }

    [Obsolete("Use ZMapView instead.")]
    public class ZeroMapView : ZMapView { }
}
