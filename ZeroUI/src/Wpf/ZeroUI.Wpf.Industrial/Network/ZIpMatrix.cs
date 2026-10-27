using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Network;
using ZeroUI.Core.Rendering;
using ZeroUI.Wpf.Base;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Network
{
    /// <summary>
    /// 2D IPAM Subnet Allocation Matrix Control for WPF.
    /// Renders a 16x16 grid (256 hosts for a /24 IPv4 subnet) with color-coded host states,
    /// conflict alert pulsing via <see cref="ZeroAnimationClock"/>, and an embedded ping RTT latency sparkline HUD.
    /// </summary>
    public class ZIpMatrix : ZeroWpfVisualBase
    {
        private readonly IpSubnetEngine _engine = new IpSubnetEngine();
        private IpHostEntry? _selectedHost;
        private float _pulsePhase = 0f;

        public event EventHandler? SelectedHostChanged;

        #region Properties

        public IpSubnetEngine Engine => _engine;

        public IpHostEntry? SelectedHost
        {
            get => _selectedHost;
            set
            {
                if (_selectedHost != value)
                {
                    _selectedHost = value;
                    SelectedHostChanged?.Invoke(this, EventArgs.Empty);
                    InvalidateVisual();
                }
            }
        }

        #endregion

        protected override bool AutoAnimate => true;

        protected override void OnAnimationTick(double delta, long frame)
        {
            _pulsePhase = (float)((_pulsePhase + delta * 3.0) % (Math.PI * 2.0));
        }

        public ZIpMatrix()
        {
            _engine.PopulateDemoData();
            _selectedHost = _engine.GetHost(1);
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
            if (w < 200 || h < 200) return;

            #if NETFRAMEWORK
            double dpi = 1.0;
            #else
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            #endif

            // Outer Container Card
            var bg = new SolidColorBrush(Color.FromRgb(20, 24, 33));
            bg.Freeze();
            var borderPen = new Pen(new SolidColorBrush(Color.FromRgb(45, 55, 72)), 1.5);
            borderPen.Freeze();
            dc.DrawRoundedRectangle(bg, borderPen, new Rect(0.5, 0.5, w - 1, h - 1), 6, 6);

            // 1. Header & Summary Stats
            double headerH = 46.0;
            DrawHeader(dc, new Rect(10, 10, w - 20, headerH), dpi);

            // 2. 16x16 Host Matrix Grid
            double matrixTop = headerH + 18;
            double detailH = 68.0;
            double matrixH = Math.Max(100.0, h - matrixTop - detailH - 16);
            double matrixW = w - 20.0;
            DrawMatrixGrid(dc, new Rect(10, matrixTop, matrixW, matrixH), dpi);

            // 3. Selected Host Inspection HUD & Ping Sparkline
            double detailTop = h - detailH - 10;
            DrawDetailHud(dc, new Rect(10, detailTop, w - 20, detailH), dpi);
        }

        private void DrawHeader(DrawingContext dc, Rect r, double dpi)
        {
            var headerBg = ZeroWpfTheme.BgCard;
            var headerPen = ZeroWpfTheme.BorderPen;
            dc.DrawRoundedRectangle(headerBg, headerPen, r, 4, 4);

            var titleFt = CreateFormattedText($"IPAM Matrix - {_engine.SubnetPrefix}.0/24", ZeroWpfTheme.BoldTypeface, 11.0, ZeroWpfTheme.TextPrimary, dpi);
            int allocated = _engine.CountByState(IpHostState.DhcpLeased) + _engine.CountByState(IpHostState.StaticReserved) + _engine.CountByState(IpHostState.Gateway);
            int free = _engine.CountByState(IpHostState.Free);
            int conflicts = _engine.CountByState(IpHostState.Conflict);

            string stats = $"Used: {allocated} / 256 | Free: {free} | Conflicts: {conflicts}";
            var subFt = CreateFormattedText(stats, ZeroWpfTheme.RegularTypeface, 9.5, ZeroWpfTheme.TextSecondary, dpi);

            dc.DrawText(titleFt, new Point(r.Left + 8, r.Top + 6));
            dc.DrawText(subFt, new Point(r.Left + 8, r.Top + 24));
        }

        private void DrawMatrixGrid(DrawingContext dc, Rect r, double dpi)
        {
            double cellW = r.Width / 16.0;
            double cellH = r.Height / 16.0;

            for (int octet = 0; octet < 256; octet++)
            {
                int row = octet / 16;
                int col = octet % 16;

                double cx = r.Left + col * cellW;
                double cy = r.Top + row * cellH;
                Rect cellRect = new Rect(cx + 1, cy + 1, cellW - 2, cellH - 2);

                var host = _engine.GetHost(octet);
                bool isSelected = host == _selectedHost;

                Color cellColor = host.State switch
                {
                    IpHostState.Free => ZeroWpfTheme.BgInput.Color,
                    IpHostState.DhcpLeased => Color.FromRgb(59, 130, 246),     // Blue
                    IpHostState.StaticReserved => Color.FromRgb(16, 185, 129), // Green
                    IpHostState.Gateway => Color.FromRgb(168, 85, 247),        // Purple
                    IpHostState.Conflict => Color.FromRgb(239, 68, 68),        // Red
                    IpHostState.Rogue => Color.FromRgb(245, 158, 11),          // Amber
                    _ => ZeroWpfTheme.BgInput.Color
                };

                // Pulsing highlight on conflict
                if (host.State == IpHostState.Conflict)
                {
                    byte pulseAlpha = (byte)(160 + 95 * Math.Sin(_pulsePhase));
                    cellColor = Color.FromArgb(pulseAlpha, 239, 68, 68);
                }

                var cellBg = new SolidColorBrush(cellColor);
                cellBg.Freeze();
                Pen pen = isSelected
                    ? new Pen(ZeroWpfTheme.PrimaryAccent, 2.0)
                    : ZeroWpfTheme.GridLinePen;

                dc.DrawRectangle(cellBg, pen, cellRect);

                if (cellW >= 18 && cellH >= 14)
                {
                    Brush numBrush = host.State == IpHostState.Free ? ZeroWpfTheme.TextMuted : Brushes.White;
                    var numFt = CreateFormattedText(octet.ToString(), ZeroWpfTheme.RegularTypeface, 7.0, numBrush, dpi);
                    dc.DrawText(numFt, new Point(cellRect.Left + (cellRect.Width - numFt.Width) / 2.0, cellRect.Top + (cellRect.Height - numFt.Height) / 2.0));
                }
            }
        }

        private void DrawDetailHud(DrawingContext dc, Rect r, double dpi)
        {
            var hudBg = ZeroWpfTheme.BgCard;
            var hudPen = ZeroWpfTheme.BorderPen;
            dc.DrawRoundedRectangle(hudBg, hudPen, r, 4, 4);

            if (_selectedHost == null)
            {
                var noneFt = CreateFormattedText("Click an IP address cell to inspect host details.", ZeroWpfTheme.RegularTypeface, 10.0, ZeroWpfTheme.TextSecondary, dpi);
                dc.DrawText(noneFt, new Point(r.Left + 12, r.Top + (r.Height - noneFt.Height) / 2.0));
                return;
            }

            // Host Info
            string hostTitle = $"{_selectedHost.IpAddress} ({_selectedHost.State})";
            var titleFt = CreateFormattedText(hostTitle, ZeroWpfTheme.BoldTypeface, 10.5, ZeroWpfTheme.TextPrimary, dpi);
            dc.DrawText(titleFt, new Point(r.Left + 10, r.Top + 8));

            string meta = $"Host: {_selectedHost.Hostname} | MAC: {_selectedHost.MacAddress} | RTT: {_selectedHost.PingRttMs:F1} ms";
            var metaFt = CreateFormattedText(meta, ZeroWpfTheme.RegularTypeface, 9.0, ZeroWpfTheme.TextSecondary, dpi);
            dc.DrawText(metaFt, new Point(r.Left + 10, r.Top + 28));

            // Ping Sparkline on the right
            double sparkW = Math.Min(140.0, r.Width * 0.3);
            double sparkH = r.Height - 16;
            Rect sparkRect = new Rect(r.Right - sparkW - 10, r.Top + 8, sparkW, sparkH);
            DrawPingSparkline(dc, _selectedHost, sparkRect);
        }

        private void DrawPingSparkline(DrawingContext dc, IpHostEntry host, Rect r)
        {
            var sparkBg = new SolidColorBrush(Color.FromRgb(18, 22, 30));
            sparkBg.Freeze();
            var sparkPen = new Pen(new SolidColorBrush(Color.FromRgb(38, 46, 62)), 1.0);
            sparkPen.Freeze();
            dc.DrawRoundedRectangle(sparkBg, sparkPen, r, 3, 3);

            var rtt = host.PingHistory;
            if (rtt == null || rtt.Count == 0) return;

            var linePen = new Pen(ZeroWpfTheme.SuccessAccent, 1.5);
            linePen.Freeze();

            double maxRtt = 100.0;
            double dx = r.Width / Math.Max(1, rtt.Count - 1);

            Point prev = new Point(r.Left, r.Bottom - (Math.Min(rtt[0], maxRtt) / maxRtt) * r.Height);
            for (int i = 1; i < rtt.Count; i++)
            {
                double px = r.Left + i * dx;
                double py = r.Bottom - (Math.Min(rtt[i], maxRtt) / maxRtt) * r.Height;
                Point next = new Point(px, py);
                dc.DrawLine(linePen, prev, next);
                prev = next;
            }
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            Point p = e.GetPosition(this);

            double headerH = 46.0;
            double matrixTop = headerH + 18;
            double detailH = 68.0;
            double matrixH = Math.Max(100.0, ActualHeight - matrixTop - detailH - 16);
            double matrixW = ActualWidth - 20.0;

            Rect matrixRect = new Rect(10, matrixTop, matrixW, matrixH);
            if (matrixRect.Contains(p))
            {
                double cellW = matrixW / 16.0;
                double cellH = matrixH / 16.0;

                int col = (int)((p.X - 10) / cellW);
                int row = (int)((p.Y - matrixTop) / cellH);

                col = Math.Max(0, Math.Min(15, col));
                row = Math.Max(0, Math.Min(15, row));

                int octet = row * 16 + col;
                SelectedHost = _engine.GetHost(octet);
            }
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZIpMatrix"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("IpMatrix is deprecated and will be removed in 5 release cycles. Please migrate to ZIpMatrix instead.")]
    public class IpMatrix : ZIpMatrix { }

    /// <summary>
    /// Legacy alias for <see cref="ZIpMatrix"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroIpMatrix is deprecated and will be removed in 5 release cycles. Please migrate to ZIpMatrix instead.")]
    public class ZeroIpMatrix : ZIpMatrix { }

    #endregion

}
