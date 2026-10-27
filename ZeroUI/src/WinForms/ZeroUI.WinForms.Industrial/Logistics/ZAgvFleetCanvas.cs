using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Logistics;
using ZeroUI.Core.Rendering;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Logistics
{
    /// <summary>
    /// 2D LiDAR SLAM floor map canvas for AGV / AMR robot fleets.
    /// Visualizes real-time robot footprints, orientation headings, safety envelope zones,
    /// planned trajectory splines, battery SoC %, and automated dock stations.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Logistics & Warehouse")]
    [Description("2D LiDAR SLAM floor map canvas for AGV / AMR robot fleets with trajectory splines")]
    public class ZAgvFleetCanvas : VisualControlBase
    {
        private readonly AgvFleetEngine _engine = new AgvFleetEngine();
        private AgvVehicle? _selectedVehicle;

        public event EventHandler? SelectedVehicleChanged;

        protected override bool AutoAnimate => true;

        public ZAgvFleetCanvas()
        {
            Size = new Size(740, 360);
        }

        #region Public Properties

        [Category("Fleet")]
        [Description("Access to the underlying multi-vehicle fleet coordination engine")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public AgvFleetEngine Engine => _engine;

        [Browsable(false)]
        public AgvVehicle? SelectedVehicle
        {
            get => _selectedVehicle;
            set
            {
                if (_selectedVehicle != value)
                {
                    _selectedVehicle = value;
                    SelectedVehicleChanged?.Invoke(this, EventArgs.Empty);
                    Invalidate();
                }
            }
        }

        #endregion

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            AgvVehicle? hit = HitTestVehicle(e.Location);
            if (hit != _selectedVehicle)
            {
                _selectedVehicle = hit;
                SelectedVehicleChanged?.Invoke(this, EventArgs.Empty);
                Invalidate();
            }
        }

        private AgvVehicle? HitTestVehicle(Point pt)
        {
            for (int i = 0; i < _engine.Vehicles.Count; i++)
            {
                var v = _engine.Vehicles[i];
                double dist = AgvFleetEngine.CalculateDistance(pt.X, pt.Y, v.X, v.Y);
                if (dist <= 22.0)
                    return v;
            }
            return null;
        }

        protected override void OnDrawVisual(Graphics g, Rectangle bounds, ZeroThemePalette palette)
        {
            // Top Header: Fleet Title, Active count, Avg Battery SoC
            int headerH = DrawHeader(g, bounds, palette);

            // Floor Canvas Map
            int mapLeft = bounds.X + 20;
            int mapTop = bounds.Y + headerH + 6;
            int mapWidth = bounds.Width - 40;
            int mapHeight = bounds.Height - headerH - 18;

            if (mapWidth < 180 || mapHeight < 100)
                return;

            Rectangle mapRect = new Rectangle(mapLeft, mapTop, mapWidth, mapHeight);

            // Grid background
            DrawFloorGrid(g, mapRect, palette);

            // Stations (Charging docks, Pick/Drop zones)
            DrawStations(g, mapRect, palette);

            // Trajectory Splines
            DrawPlannedTrajectories(g, mapRect, palette);

            // Vehicles (Footprints, Orientation, Safety zones)
            DrawVehicles(g, mapRect, palette);

            // Selected Vehicle Telemetry HUD Card
            if (_selectedVehicle != null)
            {
                DrawSelectedVehicleHud(g, _selectedVehicle, mapRect, palette);
            }
        }

        private int DrawHeader(Graphics g, Rectangle bounds, ZeroThemePalette theme)
        {
            using (var fontTitle = new Font("Segoe UI", 10.5f, FontStyle.Bold))
            using (var fontSmall = new Font("Segoe UI", 8.5f))
            using (var fontBold = new Font("Segoe UI", 9f, FontStyle.Bold))
            {
                string title = "AGV / AMR Fleet LiDAR SLAM Map";
                string fleetInfo = $"Vehicles: {_engine.Vehicles.Count} | Avg SoC: {_engine.AverageBatterySocPct:F0}%";
                int badgeW = 76;
                int badgeH = 24;

                if (bounds.Width >= 540)
                {
                    TextRenderer.DrawText(g, title, fontTitle, new Point(bounds.X + 20, bounds.Y + 14), theme.TextPrimary);

                    int statsX = bounds.Right - 310;
                    TextRenderer.DrawText(g, fleetInfo, fontSmall, new Point(statsX, bounds.Y + 16), theme.TextSecondary);

                    Rectangle pillRect = new Rectangle(bounds.Right - badgeW - 20, bounds.Y + 12, badgeW, badgeH);
                    using (var pillBrush = new SolidBrush(Color.FromArgb(30, 34, 197, 94)))
                    using (var pillPen = new Pen(Color.FromArgb(34, 197, 94), 1f))
                    {
                        g.FillRectangle(pillBrush, pillRect);
                        g.DrawRectangle(pillPen, pillRect);
                    }
                    TextRenderer.DrawText(g, "FLEET OK", fontBold, pillRect, Color.FromArgb(34, 197, 94), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    return 48;
                }
                else
                {
                    Rectangle titleRect = new Rectangle(bounds.X + 20, bounds.Y + 8, bounds.Width - badgeW - 36, 22);
                    TextRenderer.DrawText(g, title, fontTitle, titleRect, theme.TextPrimary,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                    Rectangle pillRect = new Rectangle(bounds.Right - badgeW - 16, bounds.Y + 8, badgeW, badgeH);
                    using (var pillBrush = new SolidBrush(Color.FromArgb(30, 34, 197, 94)))
                    using (var pillPen = new Pen(Color.FromArgb(34, 197, 94), 1f))
                    {
                        g.FillRectangle(pillBrush, pillRect);
                        g.DrawRectangle(pillPen, pillRect);
                    }
                    TextRenderer.DrawText(g, "FLEET OK", fontBold, pillRect, Color.FromArgb(34, 197, 94), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

                    Rectangle statsRect = new Rectangle(bounds.X + 20, bounds.Y + 32, bounds.Width - 40, 18);
                    TextRenderer.DrawText(g, fleetInfo, fontSmall, statsRect, theme.TextSecondary,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                    return 54;
                }
            }
        }

        private void DrawFloorGrid(Graphics g, Rectangle rect, ZeroThemePalette theme)
        {
            using (var fillBrush = new SolidBrush(theme.Surface))
            using (var borderPen = new Pen(theme.Border, 1.5f))
            using (var gridPen = new Pen(Color.FromArgb(18, theme.TextSecondary), 1f))
            {
                g.FillRectangle(fillBrush, rect);
                g.DrawRectangle(borderPen, rect);

                int step = 40;
                for (int x = rect.Left + step; x < rect.Right; x += step)
                    g.DrawLine(gridPen, x, rect.Top, x, rect.Bottom);

                for (int y = rect.Top + step; y < rect.Bottom; y += step)
                    g.DrawLine(gridPen, rect.Left, y, rect.Right, y);
            }
        }

        private void DrawStations(Graphics g, Rectangle map, ZeroThemePalette theme)
        {
            using (var font = new Font("Segoe UI", 8f, FontStyle.Bold))
            {
                for (int i = 0; i < _engine.Stations.Count; i++)
                {
                    var st = _engine.Stations[i];
                    float sx = map.Left + (float)st.X;
                    float sy = map.Top + (float)st.Y;

                    Color stColor = st.StationType switch
                    {
                        AgvStationType.Charging => Color.FromArgb(245, 158, 11), // Amber
                        AgvStationType.Pickup => Color.FromArgb(56, 189, 248), // Sky Blue
                        AgvStationType.Dropoff => Color.FromArgb(34, 197, 94), // Emerald
                        _ => Color.FromArgb(148, 163, 184)
                    };

                    // Station Footprint
                    RectangleF stRect = new RectangleF(sx - 16, sy - 14, 32, 28);
                    using (var stBrush = new SolidBrush(Color.FromArgb(35, stColor)))
                    using (var stPen = new Pen(stColor, 1.5f))
                    {
                        g.FillRectangle(stBrush, stRect);
                        g.DrawRectangle(stPen, stRect.X, stRect.Y, stRect.Width, stRect.Height);
                    }

                    // Label
                    TextRenderer.DrawText(g, st.Name, font, new Point((int)sx - 24, (int)sy + 16), theme.TextSecondary);
                }
            }
        }

        private void DrawPlannedTrajectories(Graphics g, Rectangle map, ZeroThemePalette theme)
        {
            using (var pathPen = new Pen(Color.FromArgb(90, 56, 189, 248), 1.5f) { DashStyle = DashStyle.Dash })
            {
                for (int v = 0; v < _engine.Vehicles.Count; v++)
                {
                    var veh = _engine.Vehicles[v];
                    if (veh.PlannedPath.Count < 2) continue;

                    for (int p = 0; p < veh.PlannedPath.Count - 1; p++)
                    {
                        float x1 = map.Left + (float)veh.PlannedPath[p].X;
                        float y1 = map.Top + (float)veh.PlannedPath[p].Y;
                        float x2 = map.Left + (float)veh.PlannedPath[p + 1].X;
                        float y2 = map.Top + (float)veh.PlannedPath[p + 1].Y;

                        g.DrawLine(pathPen, x1, y1, x2, y2);
                        g.FillEllipse(Brushes.SkyBlue, x2 - 3, y2 - 3, 6, 6);
                    }
                }
            }
        }

        private void DrawVehicles(Graphics g, Rectangle map, ZeroThemePalette theme)
        {
            using (var font = new Font("Segoe UI", 7.5f, FontStyle.Bold))
            {
                for (int i = 0; i < _engine.Vehicles.Count; i++)
                {
                    var v = _engine.Vehicles[i];
                    float vx = map.Left + (float)v.X;
                    float vy = map.Top + (float)v.Y;

                    // 1. LiDAR Safety Arcs
                    float warnR = 24f;
                    using (var arcBrush = new SolidBrush(Color.FromArgb(20, 34, 197, 94)))
                    using (var arcPen = new Pen(Color.FromArgb(70, 34, 197, 94), 1f))
                    {
                        g.FillEllipse(arcBrush, vx - warnR, vy - warnR, warnR * 2, warnR * 2);
                        g.DrawEllipse(arcPen, vx - warnR, vy - warnR, warnR * 2, warnR * 2);
                    }

                    // 2. Robot Footprint Rectangle (Rotated by Heading)
                    GraphicsState state = g.Save();
                    g.TranslateTransform(vx, vy);
                    g.RotateTransform((float)v.HeadingAngleDeg);

                    float vW = 28f;
                    float vH = 18f;
                    RectangleF vRect = new RectangleF(-vW / 2, -vH / 2, vW, vH);

                    Color bodyColor = v == _selectedVehicle ? Color.FromArgb(56, 189, 248) : Color.FromArgb(30, 41, 59);
                    using (var vBrush = new SolidBrush(bodyColor))
                    using (var vPen = new Pen(v == _selectedVehicle ? Color.White : Color.FromArgb(56, 189, 248), 1.5f))
                    {
                        g.FillRectangle(vBrush, vRect);
                        g.DrawRectangle(vPen, vRect.X, vRect.Y, vRect.Width, vRect.Height);
                    }

                    // Heading forward chevron (pointed +X)
                    using (var headPen = new Pen(Color.FromArgb(34, 197, 94), 2f))
                    {
                        g.DrawLine(headPen, 4, -4, 10, 0);
                        g.DrawLine(headPen, 10, 0, 4, 4);
                    }

                    g.Restore(state);

                    // Name & Battery Label
                    string label = $"{v.Name} ({v.BatterySocPct:0}%)";
                    TextRenderer.DrawText(g, label, font, new Point((int)vx - 24, (int)vy - 28), theme.TextPrimary);
                }
            }
        }

        private void DrawSelectedVehicleHud(Graphics g, AgvVehicle v, Rectangle map, ZeroThemePalette theme)
        {
            Rectangle cardRect = new Rectangle(map.Right - 190, map.Top + 14, 176, 84);
            using (var cardBrush = new SolidBrush(Color.FromArgb(230, 17, 19, 31)))
            using (var cardPen = new Pen(Color.FromArgb(56, 189, 248), 1f))
            using (var fontBold = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            using (var fontSmall = new Font("Segoe UI", 8f))
            {
                g.FillRectangle(cardBrush, cardRect);
                g.DrawRectangle(cardPen, cardRect);

                TextRenderer.DrawText(g, $"{v.Name} ({v.Type})", fontBold, new Point(cardRect.Left + 8, cardRect.Top + 6), Color.White);
                TextRenderer.DrawText(g, $"Task: {v.CurrentTask}", fontSmall, new Point(cardRect.Left + 8, cardRect.Top + 24), Color.FromArgb(56, 189, 248));
                TextRenderer.DrawText(g, $"Speed: {v.VelocityMps:F1} m/s | Head: {v.HeadingAngleDeg:0}°", fontSmall, new Point(cardRect.Left + 8, cardRect.Top + 42), theme.TextSecondary);
                TextRenderer.DrawText(g, $"Battery: {v.BatterySocPct:0}% ({v.Status})", fontSmall, new Point(cardRect.Left + 8, cardRect.Top + 60),
                    v.BatterySocPct > 30 ? Color.FromArgb(34, 197, 94) : Color.FromArgb(239, 68, 68));
            }
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZAgvFleetCanvas"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("AgvFleetCanvas is deprecated and will be removed in 5 release cycles. Please migrate to ZAgvFleetCanvas instead.")]
    [ToolboxItem(false)]
    public class AgvFleetCanvas : ZAgvFleetCanvas
    {
    }

    #endregion
}
