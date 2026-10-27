using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Network;
using ZeroUI.Core.Rendering;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Network
{
    /// <summary>
    /// 2D IPAM Subnet Allocation & Health Heatmap Matrix.
    /// Visualizes complete IPv4 /24 subnet blocks (16x16 grid of 256 hosts) with
    /// state color-coding, conflict alerts, and live ping RTT latency sparklines.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Network & Infrastructure")]
    [DefaultEvent("SelectedHostChanged")]
    [Description("2D IPAM IPv4 Subnet Allocation Matrix with ping latency sparklines")]
    public class ZIpMatrix : Control
    {
        private readonly IpSubnetEngine _engine = new IpSubnetEngine();
        private IpHostEntry? _selectedHost;
        private int _hoveredOctet = -1;
        private IDisposable? _animSub;

        public event EventHandler? SelectedHostChanged;

        [Category("IPAM")]
        [Description("Access to the underlying IP subnet computation engine")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IpSubnetEngine Engine => _engine;

        [Category("IPAM")]
        public string SubnetPrefix
        {
            get => _engine.SubnetPrefix;
            set
            {
                if (_engine.SubnetPrefix != value)
                {
                    _engine.SubnetPrefix = value;
                    _engine.InitializeSubnet();
                    _selectedHost = null;
                    Invalidate();
                }
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IpHostEntry? SelectedHost
        {
            get => _selectedHost;
            set
            {
                if (_selectedHost != value)
                {
                    _selectedHost = value;
                    SelectedHostChanged?.Invoke(this, EventArgs.Empty);
                    Invalidate();
                }
            }
        }

        public ZIpMatrix()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);

            Size = new Size(580, 560);
            BackColor = Color.Transparent;
            Font = new Font("Segoe UI", 8.25f);

            _engine.PopulateDemoData();
            _selectedHost = _engine.GetHost(1);

            ZeroTheme.ThemeChanged += OnThemeChanged;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (!DesignMode)
            {
                _animSub = ZeroAnimationClock.Subscribe((delta, frame) =>
                {
                    if (IsHandleCreated && Visible)
                    {
                        Invalidate();
                    }
                });
            }
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            _animSub?.Dispose();
            _animSub = null;
            base.OnHandleDestroyed(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
            ZeroTheme.ThemeChanged -= OnThemeChanged;
                _animSub?.Dispose();
                _animSub = null;
            }
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var bounds = ClientRectangle;
            if (bounds.Width < 250 || bounds.Height < 300) return;

            // 1. Frame Background
            using (var bgBrush = new SolidBrush(Color.FromArgb(20, 24, 33)))
            using (var borderPen = new Pen(Color.FromArgb(45, 55, 72), 1.5f))
            {
                using var path = CreateRoundedRect(new Rectangle(0, 0, bounds.Width - 1, bounds.Height - 1), 6);
                g.FillPath(bgBrush, path);
                g.DrawPath(borderPen, path);
            }

            // 2. Header & Legend Strip
            int topH = 54;
            DrawHeaderAndLegend(g, new Rectangle(10, 8, bounds.Width - 20, topH));

            // 3. 16x16 Subnet Grid Area
            int gridTop = topH + 14;
            int bottomHudH = 74;
            int gridH = bounds.Height - gridTop - bottomHudH - 12;
            int gridW = bounds.Width - 20;

            DrawSubnetGrid(g, new Rectangle(10, gridTop, gridW, gridH));

            // 4. Selected Host Inspection HUD (Bottom)
            int hudTop = bounds.Height - bottomHudH - 8;
            DrawHostInspectionHud(g, new Rectangle(10, hudTop, bounds.Width - 20, bottomHudH));
        }

        private void DrawHeaderAndLegend(Graphics g, Rectangle r)
        {
            using var titleFont = new Font(Font.FontFamily, 9f, FontStyle.Bold);
            using var subFont = new Font(Font.FontFamily, 7.5f, FontStyle.Regular);
            using var textBrush = new SolidBrush(Color.FromArgb(241, 245, 249));
            using var subBrush = new SolidBrush(Color.FromArgb(148, 163, 184));

            g.DrawString($"IPAM Subnet Matrix: {_engine.SubnetPrefix}.0/{_engine.CidrMask}", titleFont, textBrush, r.Left, r.Top + 2);

            int free = _engine.CountByState(IpHostState.Free);
            int dhcp = _engine.CountByState(IpHostState.DhcpLeased);
            int stat = _engine.CountByState(IpHostState.StaticReserved);
            int conflict = _engine.CountByState(IpHostState.Conflict);
            g.DrawString($"Free: {free} | DHCP: {dhcp} | Static: {stat} | Conflicts: {conflict}", subFont, subBrush, r.Left, r.Top + 18);

            // Legend dots row
            int lx = r.Left;
            int ly = r.Top + 36;
            DrawLegendItem(g, "Free", Color.FromArgb(40, 48, 64), ref lx, ly);
            DrawLegendItem(g, "DHCP", Color.FromArgb(56, 189, 248), ref lx, ly);
            DrawLegendItem(g, "Static", Color.FromArgb(168, 85, 247), ref lx, ly);
            DrawLegendItem(g, "Gateway", Color.FromArgb(34, 197, 94), ref lx, ly);
            DrawLegendItem(g, "Conflict", Color.FromArgb(239, 68, 68), ref lx, ly);
            DrawLegendItem(g, "Rogue", Color.FromArgb(245, 158, 11), ref lx, ly);
        }

        private void DrawLegendItem(Graphics g, string label, Color color, ref int x, int y)
        {
            using (var brush = new SolidBrush(color))
            {
                g.FillEllipse(brush, x, y + 2, 7, 7);
            }
            using var font = new Font(Font.FontFamily, 6.75f, FontStyle.Regular);
            using var textBrush = new SolidBrush(Color.FromArgb(148, 163, 184));
            g.DrawString(label, font, textBrush, x + 10, y);
            x += (int)g.MeasureString(label, font).Width + 22;
        }

        private void DrawSubnetGrid(Graphics g, Rectangle r)
        {
            int headerOffset = 24;
            float cellW = (float)(r.Width - headerOffset) / 16f;
            float cellH = (float)(r.Height - headerOffset) / 16f;

            using var hdrFont = new Font(Font.FontFamily, 6.5f, FontStyle.Regular);
            using var hdrBrush = new SolidBrush(Color.FromArgb(100, 116, 139));
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

            // Column numbers (0..15)
            for (int col = 0; col < 16; col++)
            {
                float cx = r.Left + headerOffset + (col * cellW);
                g.DrawString($"+{col}", hdrFont, hdrBrush, new RectangleF(cx, r.Top, cellW, headerOffset), sf);
            }

            // Row numbers (.0, .16, .32...)
            for (int row = 0; row < 16; row++)
            {
                float ry = r.Top + headerOffset + (row * cellH);
                g.DrawString($".{row * 16}", hdrFont, hdrBrush, new RectangleF(r.Left, ry, headerOffset, cellH), sf);
            }

            // Draw 256 cells
            using var cellFont = new Font(Font.FontFamily, 6.5f, FontStyle.Bold);

            for (int octet = 0; octet < 256; octet++)
            {
                IpSubnetEngine.OctetToGrid(octet, out int row, out int col);
                float x = r.Left + headerOffset + (col * cellW);
                float y = r.Top + headerOffset + (row * cellH);
                var cellRect = new Rectangle((int)x + 1, (int)y + 1, (int)cellW - 2, (int)cellH - 2);

                var host = _engine.GetHost(octet);
                DrawCell(g, host, cellRect, cellFont, sf);
            }
        }

        private void DrawCell(Graphics g, IpHostEntry host, Rectangle r, Font font, StringFormat sf)
        {
            bool isSelected = host == _selectedHost;
            bool isHovered = host.HostOctet == _hoveredOctet;

            Color fillColor = host.State switch
            {
                IpHostState.DhcpLeased => Color.FromArgb(14, 116, 144),
                IpHostState.StaticReserved => Color.FromArgb(109, 40, 217),
                IpHostState.Gateway => Color.FromArgb(21, 128, 61),
                IpHostState.Conflict => ZeroAnimationClock.BlinkFast ? Color.FromArgb(220, 38, 38) : Color.FromArgb(127, 29, 29),
                IpHostState.Rogue => Color.FromArgb(180, 83, 9),
                IpHostState.Offline => Color.FromArgb(51, 65, 85),
                _ => Color.FromArgb(26, 32, 44) // Free
            };

            using (var fillBrush = new SolidBrush(fillColor))
            using (var borderPen = new Pen(isSelected ? Color.White : (isHovered ? Color.FromArgb(56, 189, 248) : Color.FromArgb(38, 46, 62)), isSelected ? 2f : 1f))
            {
                g.FillRectangle(fillBrush, r);
                g.DrawRectangle(borderPen, r);
            }

            // Octet number text
            Color textColor = host.State == IpHostState.Free ? Color.FromArgb(100, 116, 139) : Color.White;
            using (var textBrush = new SolidBrush(textColor))
            {
                g.DrawString(host.HostOctet.ToString(), font, textBrush, r, sf);
            }
        }

        private void DrawHostInspectionHud(Graphics g, Rectangle r)
        {
            using (var hudBrush = new SolidBrush(Color.FromArgb(28, 33, 46)))
            using (var hudPen = new Pen(Color.FromArgb(45, 55, 72), 1f))
            {
                using var path = CreateRoundedRect(r, 4);
                g.FillPath(hudBrush, path);
                g.DrawPath(hudPen, path);
            }

            if (_selectedHost == null) return;

            var h = _selectedHost;
            using var titleFont = new Font(Font.FontFamily, 8.5f, FontStyle.Bold);
            using var font = new Font(Font.FontFamily, 7.5f, FontStyle.Regular);
            using var boldFont = new Font(Font.FontFamily, 7.5f, FontStyle.Bold);
            using var textBrush = new SolidBrush(Color.FromArgb(241, 245, 249));
            using var subBrush = new SolidBrush(Color.FromArgb(148, 163, 184));

            // Left details
            g.DrawString($"IP: {h.IpAddress} ({h.State})", titleFont, textBrush, r.Left + 10, r.Top + 8);
            g.DrawString($"Host: {(string.IsNullOrEmpty(h.Hostname) ? "Unresolved" : h.Hostname)} | MAC: {(string.IsNullOrEmpty(h.MacAddress) ? "N/A" : h.MacAddress)}", font, subBrush, r.Left + 10, r.Top + 28);
            g.DrawString($"Vendor: {(string.IsNullOrEmpty(h.Vendor) ? "Unknown" : h.Vendor)} | Current RTT: {h.PingRttMs:F1} ms", font, subBrush, r.Left + 10, r.Top + 46);

            // Right Sparkline of Ping History
            var sparkRect = new Rectangle(r.Right - 150, r.Top + 14, 136, r.Height - 28);
            DrawPingSparkline(g, h, sparkRect);
        }

        private void DrawPingSparkline(Graphics g, IpHostEntry host, Rectangle r)
        {
            using (var bgBrush = new SolidBrush(Color.FromArgb(18, 22, 30)))
            using (var bgPen = new Pen(Color.FromArgb(40, 48, 64), 1f))
            {
                g.FillRectangle(bgBrush, r);
                g.DrawRectangle(bgPen, r);
            }

            var hist = host.PingHistory;
            if (hist.Count < 2)
            {
                using var font = new Font(Font.FontFamily, 6.5f, FontStyle.Regular);
                using var brush = new SolidBrush(Color.FromArgb(100, 116, 139));
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("No Ping History", font, brush, r, sf);
                return;
            }

            float maxVal = 10.0f;
            for (int i = 0; i < hist.Count; i++)
            {
                if (hist[i] > maxVal) maxVal = hist[i];
            }

            var pts = new PointF[hist.Count];
            float step = (float)r.Width / (hist.Count - 1);

            for (int i = 0; i < hist.Count; i++)
            {
                float px = r.Left + (i * step);
                float py = r.Bottom - (hist[i] / maxVal * (r.Height - 6)) - 3;
                pts[i] = new PointF(px, py);
            }

            using var sparkPen = new Pen(Color.FromArgb(56, 189, 248), 1.5f);
            g.DrawLines(sparkPen, pts);

            // Latency label
            using var lFont = new Font(Font.FontFamily, 6.25f, FontStyle.Bold);
            using var lBrush = new SolidBrush(Color.FromArgb(148, 163, 184));
            g.DrawString($"Ping {hist[hist.Count - 1]:F1}ms", lFont, lBrush, r.Left + 4, r.Top + 2);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            int octet = HitTestOctet(e.Location);
            if (octet >= 0 && octet < 256)
            {
                SelectedHost = _engine.GetHost(octet);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int octet = HitTestOctet(e.Location);
            if (octet != _hoveredOctet)
            {
                _hoveredOctet = octet;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoveredOctet != -1)
            {
                _hoveredOctet = -1;
                Invalidate();
            }
        }

        private int HitTestOctet(Point pt)
        {
            int topH = 54;
            int gridTop = topH + 14;
            int bottomHudH = 74;
            int gridH = Height - gridTop - bottomHudH - 12;
            int gridW = Width - 20;
            int headerOffset = 24;

            int gridLeft = 10 + headerOffset;
            int gridActualTop = gridTop + headerOffset;
            int gridActualW = gridW - headerOffset;
            int gridActualH = gridH - headerOffset;

            if (pt.X < gridLeft || pt.X > gridLeft + gridActualW ||
                pt.Y < gridActualTop || pt.Y > gridActualTop + gridActualH)
            {
                return -1;
            }

            float cellW = (float)gridActualW / 16f;
            float cellH = (float)gridActualH / 16f;

            int col = (int)((pt.X - gridLeft) / cellW);
            int row = (int)((pt.Y - gridActualTop) / cellH);

            if (col >= 0 && col < 16 && row >= 0 && row < 16)
            {
                return IpSubnetEngine.GridToOctet(row, col);
            }

            return -1;
        }

        private static GraphicsPath CreateRoundedRect(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    
    private void OnThemeChanged(object? sender, EventArgs e) => Invalidate();

}

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZIpMatrix"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("IpMatrix is deprecated and will be removed in 5 release cycles. Please migrate to ZIpMatrix instead.")]
    [ToolboxItem(false)]
    public class IpMatrix : ZIpMatrix
    {
    }

    #endregion
}
