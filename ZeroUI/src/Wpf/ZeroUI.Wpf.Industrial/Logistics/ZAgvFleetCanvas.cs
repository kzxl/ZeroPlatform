using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Logistics;
using ZeroUI.Core.Rendering;
using ZeroUI.Wpf.Base;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Logistics
{
    /// <summary>
    /// 2D LiDAR SLAM floor map canvas for AGV / AMR robot fleets in WPF.
    /// Visualizes real-time robot footprints, orientation headings, safety zones,
    /// planned trajectory splines, battery SoC %, and automated dock stations.
    /// </summary>
    public class ZAgvFleetCanvas : ZeroWpfVisualBase
    {
        private readonly AgvFleetEngine _engine = new AgvFleetEngine();
        private AgvVehicle? _selectedVehicle;

        public event EventHandler? SelectedVehicleChanged;

        protected override bool AutoAnimate => true;

        public ZAgvFleetCanvas()
        {
            MouseDown += OnWpfMouseDown;
        }

        #region Properties

        public AgvFleetEngine Engine => _engine;

        public AgvVehicle? SelectedVehicle
        {
            get => _selectedVehicle;
            set
            {
                if (_selectedVehicle != value)
                {
                    _selectedVehicle = value;
                    SelectedVehicleChanged?.Invoke(this, EventArgs.Empty);
                    InvalidateVisual();
                }
            }
        }

        #endregion

        private void OnWpfMouseDown(object sender, MouseButtonEventArgs e)
        {
            Point pt = e.GetPosition(this);
            double mapLeft = 16;
            double mapTop = 44;

            AgvVehicle? hit = null;
            for (int i = 0; i < _engine.Vehicles.Count; i++)
            {
                var v = _engine.Vehicles[i];
                double vx = mapLeft + v.X;
                double vy = mapTop + v.Y;
                double dist = AgvFleetEngine.CalculateDistance(pt.X, pt.Y, vx, vy);
                if (dist <= 22.0)
                {
                    hit = v;
                    break;
                }
            }

            if (hit != _selectedVehicle)
            {
                _selectedVehicle = hit;
                SelectedVehicleChanged?.Invoke(this, EventArgs.Empty);
                InvalidateVisual();
            }
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double w = ActualWidth;
            double h = ActualHeight;
            if (w < 120 || h < 80)
                return;

            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            // Background
            dc.DrawRectangle(ZeroWpfTheme.BgPrimary, null, new Rect(0, 0, w, h));

            // Header
            var titleFt = CreateFormattedText("AGV / AMR Fleet LiDAR SLAM Map", ZeroWpfTheme.BoldTypeface, 11.5, ZeroWpfTheme.TextPrimary, dpi);
            dc.DrawText(titleFt, new Point(16, 12));

            double statsX = w - 300;
            if (statsX > 220)
            {
                string infoStr = $"Vehicles: {_engine.Vehicles.Count} | Avg SoC: {_engine.AverageBatterySocPct:F0}%";
                var infoFt = CreateFormattedText(infoStr, ZeroWpfTheme.RegularTypeface, 9.5, ZeroWpfTheme.TextSecondary, dpi);
                dc.DrawText(infoFt, new Point(statsX, 14));

                Rect badgeRect = new Rect(w - 90, 10, 76, 24);
                dc.DrawRectangle(ZeroWpfTheme.BgCard, new Pen(Brushes.MediumSeaGreen, 1.0), badgeRect);
                var badgeFt = CreateFormattedText("FLEET OK", ZeroWpfTheme.BoldTypeface, 9.0, Brushes.MediumSeaGreen, dpi);
                dc.DrawText(badgeFt, new Point(badgeRect.Left + 10, badgeRect.Top + 4));
            }

            // Floor Map
            double mapLeft = 16;
            double mapTop = 44;
            double mapW = w - 32;
            double mapH = h - mapTop - 14;

            if (mapW < 140 || mapH < 60)
                return;

            Rect mapRect = new Rect(mapLeft, mapTop, mapW, mapH);
            dc.DrawRectangle(ZeroWpfTheme.BgCard, ZeroWpfTheme.BorderPen, mapRect);

            // Grid lines
            Pen gridPen = new Pen(ZeroWpfTheme.BorderSubtle, 1.0);
            gridPen.Freeze();
            int step = 40;
            for (double x = mapLeft + step; x < mapRect.Right; x += step)
                dc.DrawLine(gridPen, new Point(x, mapTop), new Point(x, mapRect.Bottom));
            for (double y = mapTop + step; y < mapRect.Bottom; y += step)
                dc.DrawLine(gridPen, new Point(mapLeft, y), new Point(mapRect.Right, y));

            // Stations
            for (int i = 0; i < _engine.Stations.Count; i++)
            {
                var st = _engine.Stations[i];
                double sx = mapLeft + st.X;
                double sy = mapTop + st.Y;

                Brush stBrush = st.StationType switch
                {
                    AgvStationType.Charging => Brushes.Orange,
                    AgvStationType.Pickup => Brushes.SkyBlue,
                    _ => Brushes.MediumSeaGreen
                };

                Rect stRect = new Rect(sx - 14, sy - 12, 28, 24);
                dc.DrawRectangle(ZeroWpfTheme.BgHover, new Pen(stBrush, 1.5), stRect);

                var stFt = CreateFormattedText(st.Name, ZeroWpfTheme.RegularTypeface, 7.5, ZeroWpfTheme.TextSecondary, dpi);
                dc.DrawText(stFt, new Point(sx - stFt.Width / 2, sy + 14));
            }

            // Trajectory Splines
            Pen pathPen = new Pen(Brushes.SkyBlue, 1.2) { DashStyle = DashStyles.Dash };
            pathPen.Freeze();

            for (int v = 0; v < _engine.Vehicles.Count; v++)
            {
                var veh = _engine.Vehicles[v];
                if (veh.PlannedPath.Count < 2) continue;

                for (int p = 0; p < veh.PlannedPath.Count - 1; p++)
                {
                    Point p1 = new Point(mapLeft + veh.PlannedPath[p].X, mapTop + veh.PlannedPath[p].Y);
                    Point p2 = new Point(mapLeft + veh.PlannedPath[p + 1].X, mapTop + veh.PlannedPath[p + 1].Y);
                    dc.DrawLine(pathPen, p1, p2);
                    dc.DrawEllipse(Brushes.SkyBlue, null, p2, 2.5, 2.5);
                }
            }

            // Vehicles
            for (int i = 0; i < _engine.Vehicles.Count; i++)
            {
                var v = _engine.Vehicles[i];
                double vx = mapLeft + v.X;
                double vy = mapTop + v.Y;

                // LiDAR safety zone arc
                Brush warnBrush = new SolidColorBrush(Color.FromArgb(30, 34, 197, 94));
                warnBrush.Freeze();
                dc.DrawEllipse(warnBrush, new Pen(Brushes.MediumSeaGreen, 0.8), new Point(vx, vy), 22, 22);

                // Body rectangle rotated
                dc.PushTransform(new RotateTransform(v.HeadingAngleDeg, vx, vy));

                Rect vRect = new Rect(vx - 14, vy - 9, 28, 18);
                Brush bodyBrush = v == _selectedVehicle ? Brushes.SkyBlue : Brushes.SlateGray;
                dc.DrawRectangle(bodyBrush, new Pen(Brushes.White, 1.0), vRect);

                // Heading chevron
                Pen headPen = new Pen(Brushes.LimeGreen, 1.8);
                headPen.Freeze();
                dc.DrawLine(headPen, new Point(vx + 3, vy - 3), new Point(vx + 8, vy));
                dc.DrawLine(headPen, new Point(vx + 8, vy), new Point(vx + 3, vy + 3));

                dc.Pop();

                var nameFt = CreateFormattedText($"{v.Name} ({v.BatterySocPct:0}%)", ZeroWpfTheme.BoldTypeface, 7.5, ZeroWpfTheme.TextPrimary, dpi);
                dc.DrawText(nameFt, new Point(vx - nameFt.Width / 2, vy - 24));
            }

            // Selected Vehicle HUD Card
            if (_selectedVehicle != null)
            {
                Rect cardRect = new Rect(mapRect.Right - 180, mapTop + 12, 168, 76);
                dc.DrawRectangle(ZeroWpfTheme.BgPrimary, new Pen(Brushes.SkyBlue, 1.0), cardRect);

                var h1 = CreateFormattedText($"{_selectedVehicle.Name} ({_selectedVehicle.Type})", ZeroWpfTheme.BoldTypeface, 8.5, Brushes.White, dpi);
                dc.DrawText(h1, new Point(cardRect.Left + 8, cardRect.Top + 6));

                var h2 = CreateFormattedText($"Task: {_selectedVehicle.CurrentTask}", ZeroWpfTheme.RegularTypeface, 8.0, Brushes.SkyBlue, dpi);
                dc.DrawText(h2, new Point(cardRect.Left + 8, cardRect.Top + 22));

                var h3 = CreateFormattedText($"Speed: {_selectedVehicle.VelocityMps:F1}m/s | Head: {_selectedVehicle.HeadingAngleDeg:0}°", ZeroWpfTheme.RegularTypeface, 7.5, ZeroWpfTheme.TextSecondary, dpi);
                dc.DrawText(h3, new Point(cardRect.Left + 8, cardRect.Top + 38));

                var h4 = CreateFormattedText($"Battery: {_selectedVehicle.BatterySocPct:0}% ({_selectedVehicle.Status})", ZeroWpfTheme.BoldTypeface, 7.5, Brushes.LimeGreen, dpi);
                dc.DrawText(h4, new Point(cardRect.Left + 8, cardRect.Top + 54));
            }
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZAgvFleetCanvas"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("AgvFleetCanvas is deprecated and will be removed in 5 release cycles. Please migrate to ZAgvFleetCanvas instead.")]
    public class AgvFleetCanvas : ZAgvFleetCanvas { }

    /// <summary>
    /// Legacy alias for <see cref="ZAgvFleetCanvas"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroAgvFleetCanvas is deprecated and will be removed in 5 release cycles. Please migrate to ZAgvFleetCanvas instead.")]
    public class ZeroAgvFleetCanvas : ZAgvFleetCanvas { }

    #endregion

}
