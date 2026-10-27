using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using ZeroUI.Core.Network;
using ZeroUI.Core.Rendering;
using ZeroUI.Wpf.Base;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Network
{
    /// <summary>
    /// Industrial Communication Line & Redundant Ring Monitor Control for WPF.
    /// Monitors industrial networks (Profinet MRP, EtherCAT, Modbus RTU), calculates cable break localization,
    /// and renders line jitter telemetry with alert flashing driven by <see cref="ZeroAnimationClock"/>.
    /// </summary>
    public class ZFieldbusMonitor : ZeroWpfVisualBase
    {
        private readonly FieldbusNetwork _network = new FieldbusNetwork();
        private bool _blinkState = false;

        #region Properties

        public FieldbusNetwork Network => _network;

        #endregion

        protected override bool AutoAnimate => true;

        protected override void OnAnimationTick(double delta, long frame)
        {
            if (frame % 30 == 0)
            {
                _blinkState = !_blinkState;
            }
        }

        public ZFieldbusMonitor()
        {
            _network.PopulateDemoIndustrialLine();
        }

        #if NETFRAMEWORK
        private static FormattedText CreateFormattedText(string text, Typeface typeface, double fontSize, Brush brush, double pixelsPerDip = 1.0)
        {
            return new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, fontSize, brush);
        }
        #else
        private static FormattedText CreateFormattedText(string text, Typeface typeface, double fontSize, Brush brush, double pixelsPerDip = 1.0)
        {
            return new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, fontSize, brush, pixelsPerDip);
        }
        #endif

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double w = ActualWidth;
            double h = ActualHeight;
            if (w < 200 || h < 120) return;

            #if NETFRAMEWORK
            double dpi = 1.0;
            #else
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            #endif

            // Outer Container Card
            var bg = ZeroWpfTheme.BgCard;
            var borderPen = ZeroWpfTheme.BorderPen;
            dc.DrawRoundedRectangle(bg, borderPen, new Rect(0.5, 0.5, w - 1, h - 1), 6, 6);

            // 1. Top Telemetry HUD
            double headerH = 46.0;
            DrawTelemetryHud(dc, new Rect(10, 10, w - 20, headerH), dpi);

            // 2. Alert Banner (if cable break detected)
            double lineAreaTop = headerH + 18;
            var breakSegment = _network.LocateCableBreak();
            if (breakSegment != null)
            {
                double alertH = 28.0;
                DrawBreakAlertBanner(dc, new Rect(10, lineAreaTop, w - 20, alertH), breakSegment, dpi);
                lineAreaTop += alertH + 8;
            }

            // 3. Fieldbus Physical Daisy-Chain Line & Stations
            double lineAreaH = Math.Max(50.0, h - lineAreaTop - 10);
            DrawFieldbusLine(dc, new Rect(10, lineAreaTop, w - 20, lineAreaH), breakSegment, dpi);
        }

        private void DrawTelemetryHud(DrawingContext dc, Rect r, double dpi)
        {
            var headerBg = ZeroWpfTheme.BgInput;
            var headerPen = ZeroWpfTheme.BorderPen;
            dc.DrawRoundedRectangle(headerBg, headerPen, r, 4, 4);

            var titleFt = CreateFormattedText($"{_network.Protocol} Fieldbus Line Monitor ({_network.Topology})", ZeroWpfTheme.BoldTypeface, 11.0, ZeroWpfTheme.TextPrimary, dpi);
            string stats = $"Cycle: {_network.MasterCycleTimeMs:F1} ms | Jitter: {_network.JitterMicroseconds:F1} μs | Stations: {_network.Stations.Count}";
            var subFt = CreateFormattedText(stats, ZeroWpfTheme.RegularTypeface, 9.5, ZeroWpfTheme.TextSecondary, dpi);

            dc.DrawText(titleFt, new Point(r.Left + 8, r.Top + 6));
            dc.DrawText(subFt, new Point(r.Left + 8, r.Top + 24));

            // Status Pill
            bool hasFault = _network.LocateCableBreak() != null;
            Brush statusBrush = hasFault ? ZeroWpfTheme.DangerAccent : ZeroWpfTheme.SuccessAccent;
            string statusStr = hasFault ? "FAULT" : "OPERATIONAL";

            double pillW = 100;
            double pillH = 24;
            Rect pillRect = new Rect(r.Right - pillW - 8, r.Top + (r.Height - pillH) / 2.0, pillW, pillH);
            var pillBg = ZeroWpfTheme.BgCard;
            var pillPen = new Pen(statusBrush, 1.0);
            pillPen.Freeze();

            dc.DrawRoundedRectangle(pillBg, pillPen, pillRect, 4, 4);
            var pillFt = CreateFormattedText(statusStr, ZeroWpfTheme.BoldTypeface, 9.5, statusBrush, dpi);
            dc.DrawText(pillFt, new Point(pillRect.Left + (pillRect.Width - pillFt.Width) / 2.0, pillRect.Top + (pillRect.Height - pillFt.Height) / 2.0));
        }

        private void DrawBreakAlertBanner(DrawingContext dc, Rect r, FieldbusSegment breakSeg, double dpi)
        {
            Color alertColor = _blinkState ? Color.FromRgb(239, 68, 68) : Color.FromRgb(185, 28, 28);
            var bg = new SolidColorBrush(alertColor);
            bg.Freeze();
            dc.DrawRoundedRectangle(bg, null, r, 4, 4);

            string alertMsg = $"⚡ CABLE SEVERED: Break isolated between Station {breakSeg.FromStationIndex} and Station {breakSeg.ToStationIndex}!";
            var ft = CreateFormattedText(alertMsg, ZeroWpfTheme.BoldTypeface, 10.0, Brushes.White, dpi);
            dc.DrawText(ft, new Point(r.Left + (r.Width - ft.Width) / 2.0, r.Top + (r.Height - ft.Height) / 2.0));
        }

        private void DrawFieldbusLine(DrawingContext dc, Rect r, FieldbusSegment? breakSegment, double dpi)
        {
            int stationCount = _network.Stations.Count;
            if (stationCount == 0) return;

            double cy = r.Top + r.Height * 0.45;
            double stationW = Math.Min(70.0, (r.Width - 60.0) / stationCount);
            double stationH = 46.0;
            double totalSpan = r.Width - 40.0;
            double stepX = totalSpan / Math.Max(1, stationCount - 1);

            // Draw Bus Cable Segments
            for (int i = 0; i < stationCount - 1; i++)
            {
                double x1 = r.Left + 20.0 + i * stepX;
                double x2 = r.Left + 20.0 + (i + 1) * stepX;

                bool isBroken = breakSegment != null &&
                                ((_network.Stations[i].StationIndex == breakSegment.FromStationIndex && _network.Stations[i + 1].StationIndex == breakSegment.ToStationIndex) ||
                                 (_network.Stations[i].StationIndex == breakSegment.ToStationIndex && _network.Stations[i + 1].StationIndex == breakSegment.FromStationIndex));

                Color segColor = isBroken ? Color.FromRgb(239, 68, 68) : Color.FromRgb(16, 185, 129);
                var segPen = new Pen(new SolidColorBrush(segColor), isBroken ? 3.0 : 2.0);
                if (isBroken && _blinkState)
                {
                    segPen.DashStyle = DashStyles.Dash;
                }
                segPen.Freeze();

                dc.DrawLine(segPen, new Point(x1, cy), new Point(x2, cy));
            }

            // Draw Stations (Master PLC + Drop I/O)
            for (int i = 0; i < stationCount; i++)
            {
                var station = _network.Stations[i];
                double sx = r.Left + 20.0 + i * stepX - stationW / 2.0;
                double sy = cy - stationH / 2.0;
                Rect sRect = new Rect(sx, sy, stationW, stationH);

                bool isMaster = station.StationIndex == 1;
                Brush fill = isMaster ? ZeroWpfTheme.BgInput : ZeroWpfTheme.BgCard;

                Brush led = station.Status == StationStatus.Normal ? ZeroWpfTheme.SuccessAccent : ZeroWpfTheme.DangerAccent;
                var pen = isMaster ? new Pen(ZeroWpfTheme.PrimaryAccent, 1.5) : ZeroWpfTheme.BorderPen;

                dc.DrawRoundedRectangle(fill, pen, sRect, 4, 4);

                // LED
                dc.DrawEllipse(led, null, new Point(sRect.Left + 8, sRect.Top + 8), 3.5, 3.5);

                // Title
                string name = isMaster ? "Master" : $"Stn {station.StationIndex}";
                var nameFt = CreateFormattedText(name, ZeroWpfTheme.BoldTypeface, 9.0, ZeroWpfTheme.TextPrimary, dpi);
                dc.DrawText(nameFt, new Point(sRect.Left + 16, sRect.Top + 3));

                // Device Type
                var typeFt = CreateFormattedText(station.DeviceType, ZeroWpfTheme.RegularTypeface, 7.5, ZeroWpfTheme.TextSecondary, dpi);
                dc.DrawText(typeFt, new Point(sRect.Left + 6, sRect.Top + 24));
            }
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZFieldbusMonitor"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("FieldbusMonitor is deprecated and will be removed in 5 release cycles. Please migrate to ZFieldbusMonitor instead.")]
    public class FieldbusMonitor : ZFieldbusMonitor { }

    /// <summary>
    /// Legacy alias for <see cref="ZFieldbusMonitor"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroFieldbusMonitor is deprecated and will be removed in 5 release cycles. Please migrate to ZFieldbusMonitor instead.")]
    public class ZeroFieldbusMonitor : ZFieldbusMonitor { }

    #endregion

}
