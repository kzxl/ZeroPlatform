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
    /// Single-HWND Interactive Vector Network Topology Canvas.
    /// Visualizes enterprise IT and industrial OT interconnected devices (Switches, Firewalls,
    /// Servers, PLCs) with real-time animated packet pulse wave flows.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Network & Infrastructure")]
    [DefaultEvent("SelectedNodeChanged")]
    [Description("Interactive Vector Network Topology Canvas with animated packet pulse flows")]
    public class ZNetworkTopology : Control
    {
        private readonly TopologyGraphEngine _engine = new TopologyGraphEngine();
        private TopologyNode? _selectedNode;
        private TopologyLink? _selectedLink;
        private TopologyNode? _hoveredNode;

        private float _zoom = 1.0f;
        private PointF _pan = new PointF(0, 0);
        private bool _isPanning;
        private Point _lastMousePos;
        private IDisposable? _animSub;

        public event EventHandler? SelectedNodeChanged;
        public event EventHandler? SelectedLinkChanged;

        [Category("Topology")]
        [Description("Access to the underlying graph engine and node/link collection")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public TopologyGraphEngine Engine => _engine;

        [Category("Topology")]
        [DefaultValue(1.0f)]
        public float ZoomLevel
        {
            get => _zoom;
            set
            {
                _zoom = Math.Max(0.2f, Math.Min(3.0f, value));
                Invalidate();
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public TopologyNode? SelectedNode
        {
            get => _selectedNode;
            set
            {
                if (_selectedNode != value)
                {
                    _selectedNode = value;
                    SelectedNodeChanged?.Invoke(this, EventArgs.Empty);
                    Invalidate();
                }
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public TopologyLink? SelectedLink
        {
            get => _selectedLink;
            set
            {
                if (_selectedLink != value)
                {
                    _selectedLink = value;
                    SelectedLinkChanged?.Invoke(this, EventArgs.Empty);
                    Invalidate();
                }
            }
        }

        public ZNetworkTopology()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);

            Size = new Size(720, 480);
            BackColor = Color.FromArgb(15, 18, 26);
            Font = new Font("Segoe UI", 8.25f);

            InitializeDemoTopology();
            ZeroTheme.ThemeChanged += OnThemeChanged;
        }

        private void InitializeDemoTopology()
        {
            _engine.Clear();

            // Core Tier
            var r1 = new TopologyNode { Id = "R1", Name = "Core-Router-01", DeviceType = NetworkDeviceType.Router, IpAddress = "10.0.0.1", X = 280, Y = 30 };
            var fw = new TopologyNode { Id = "FW1", Name = "Edge-Firewall-01", DeviceType = NetworkDeviceType.Firewall, IpAddress = "10.0.0.254", X = 460, Y = 30 };

            // Distribution Tier
            var swCore1 = new TopologyNode { Id = "SW1", Name = "Core-Switch-A", DeviceType = NetworkDeviceType.CoreSwitch, IpAddress = "10.0.1.1", X = 160, Y = 150 };
            var swCore2 = new TopologyNode { Id = "SW2", Name = "Core-Switch-B", DeviceType = NetworkDeviceType.CoreSwitch, IpAddress = "10.0.1.2", X = 400, Y = 150 };

            // Access Tier & OT Field
            var swAcc1 = new TopologyNode { Id = "ACC1", Name = "Access-Plant-Line1", DeviceType = NetworkDeviceType.AccessSwitch, IpAddress = "10.0.2.10", X = 80, Y = 280 };
            var srv1 = new TopologyNode { Id = "SRV1", Name = "MES-Historian-01", DeviceType = NetworkDeviceType.Server, IpAddress = "10.0.5.20", X = 250, Y = 280 };
            var plc1 = new TopologyNode { Id = "PLC1", Name = "PLC-Line1-Spindle", DeviceType = NetworkDeviceType.Plc, IpAddress = "192.168.0.10", X = 440, Y = 280 };
            var plc2 = new TopologyNode { Id = "PLC2", Name = "PLC-Line1-Packaging", DeviceType = NetworkDeviceType.Plc, IpAddress = "192.168.0.20", X = 600, Y = 280 };

            _engine.AddNode(r1);
            _engine.AddNode(fw);
            _engine.AddNode(swCore1);
            _engine.AddNode(swCore2);
            _engine.AddNode(swAcc1);
            _engine.AddNode(srv1);
            _engine.AddNode(plc1);
            _engine.AddNode(plc2);

            _engine.AddLink(new TopologyLink { Id = "L1", SourceNodeId = "R1", TargetNodeId = "FW1", LinkType = NetworkLinkType.Fiber, BandwidthGbps = 10 });
            _engine.AddLink(new TopologyLink { Id = "L2", SourceNodeId = "R1", TargetNodeId = "SW1", LinkType = NetworkLinkType.Fiber, BandwidthGbps = 10 });
            _engine.AddLink(new TopologyLink { Id = "L3", SourceNodeId = "FW1", TargetNodeId = "SW2", LinkType = NetworkLinkType.Fiber, BandwidthGbps = 10 });
            _engine.AddLink(new TopologyLink { Id = "L4", SourceNodeId = "SW1", TargetNodeId = "SW2", LinkType = NetworkLinkType.Fiber, BandwidthGbps = 40 }); // Core trunk
            _engine.AddLink(new TopologyLink { Id = "L5", SourceNodeId = "SW1", TargetNodeId = "ACC1", LinkType = NetworkLinkType.CopperTwistedPair, BandwidthGbps = 1 });
            _engine.AddLink(new TopologyLink { Id = "L6", SourceNodeId = "SW2", TargetNodeId = "SRV1", LinkType = NetworkLinkType.CopperTwistedPair, BandwidthGbps = 10 });
            _engine.AddLink(new TopologyLink { Id = "L7", SourceNodeId = "SW2", TargetNodeId = "PLC1", LinkType = NetworkLinkType.IndustrialBus, BandwidthGbps = 0.1 });
            _engine.AddLink(new TopologyLink { Id = "L8", SourceNodeId = "PLC1", TargetNodeId = "PLC2", LinkType = NetworkLinkType.IndustrialBus, BandwidthGbps = 0.1 });
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

            // 1. Grid Background
            DrawGridDots(g);

            // Apply Pan & Zoom Transform
            var state = g.Save();
            g.TranslateTransform(_pan.X, _pan.Y);
            g.ScaleTransform(_zoom, _zoom);

            // 2. Render Links & Animated Packet Pulses
            DrawLinks(g);

            // 3. Render Nodes
            DrawNodes(g);

            g.Restore(state);

            // 4. On-Canvas HUD (Zoom & Controls)
            DrawCanvasHud(g);
        }

        private void DrawGridDots(Graphics g)
        {
            using var dotBrush = new SolidBrush(Color.FromArgb(28, 34, 48));
            int step = 28;
            for (int x = 0; x < Width; x += step)
            {
                for (int y = 0; y < Height; y += step)
                {
                    g.FillRectangle(dotBrush, x, y, 2, 2);
                }
            }
        }

        private void DrawLinks(Graphics g)
        {
            float animTime = (float)ZeroAnimationClock.TotalElapsedTime;

            for (int i = 0; i < _engine.Links.Count; i++)
            {
                var link = _engine.Links[i];
                var src = _engine.FindNode(link.SourceNodeId);
                var dst = _engine.FindNode(link.TargetNodeId);
                if (src == null || dst == null) continue;

                bool isSelected = link == _selectedLink;

                Color lineColor = link.LinkType switch
                {
                    NetworkLinkType.Fiber => Color.FromArgb(56, 189, 248),
                    NetworkLinkType.CopperTwistedPair => Color.FromArgb(99, 102, 241),
                    NetworkLinkType.IndustrialBus => Color.FromArgb(245, 158, 11),
                    _ => Color.FromArgb(168, 85, 247)
                };

                using (var linkPen = new Pen(isSelected ? Color.White : lineColor, isSelected ? 2.5f : 1.5f))
                {
                    if (link.LinkType == NetworkLinkType.Wireless)
                    {
                        linkPen.DashStyle = DashStyle.Dash;
                    }
                    g.DrawLine(linkPen, src.CenterX, src.CenterY, dst.CenterX, dst.CenterY);
                }

                // Render animated packet pulse dot
                if (link.Status == NetworkLinkStatus.Up)
                {
                    float phase = (animTime * 0.75f + (i * 0.25f)) % 1.0f;
                    if (_engine.ComputePulseCoordinate(link, phase, out float px, out float py))
                    {
                        using var pulseBrush = new SolidBrush(Color.FromArgb(255, 255, 255));
                        using var glowBrush = new SolidBrush(Color.FromArgb(100, lineColor.R, lineColor.G, lineColor.B));
                        g.FillEllipse(glowBrush, px - 6, py - 6, 12, 12);
                        g.FillEllipse(pulseBrush, px - 3, py - 3, 6, 6);
                    }
                }
            }
        }

        private void DrawNodes(Graphics g)
        {
            for (int i = 0; i < _engine.Nodes.Count; i++)
            {
                var n = _engine.Nodes[i];
                bool isSelected = n == _selectedNode;
                bool isHovered = n == _hoveredNode;

                var rect = new RectangleF(n.X, n.Y, n.Width, n.Height);

                // Card background
                using (var bgBrush = new SolidBrush(Color.FromArgb(26, 31, 44)))
                using (var borderPen = new Pen(isSelected ? Color.FromArgb(56, 189, 248) : (isHovered ? Color.FromArgb(96, 165, 250) : Color.FromArgb(48, 56, 76)), isSelected ? 2f : 1f))
                {
                    using var path = CreateRoundedRect(rect, 4);
                    g.FillPath(bgBrush, path);
                    g.DrawPath(borderPen, path);
                }

                // Status Indicator Ring
                Color statusColor = n.Status switch
                {
                    NetworkDeviceStatus.Online => Color.FromArgb(34, 197, 94),
                    NetworkDeviceStatus.Warning => Color.FromArgb(245, 158, 11),
                    NetworkDeviceStatus.Critical => Color.FromArgb(239, 68, 68),
                    _ => Color.FromArgb(100, 116, 139)
                };

                using (var statusBrush = new SolidBrush(statusColor))
                {
                    g.FillEllipse(statusBrush, n.X + 8, n.Y + 10, 8, 8);
                }

                // Device Type Icon / Glyph Badge
                DrawDeviceIcon(g, n.DeviceType, new RectangleF(n.X + 22, n.Y + 8, 16, 16));

                // Labels: Device Name & IP
                using var nameFont = new Font(Font.FontFamily, 7.5f, FontStyle.Bold);
                using var ipFont = new Font(Font.FontFamily, 6.75f, FontStyle.Regular);
                using var nameBrush = new SolidBrush(Color.FromArgb(241, 245, 249));
                using var ipBrush = new SolidBrush(Color.FromArgb(148, 163, 184));

                var nameRect = new RectangleF(n.X + 6, n.Y + 28, n.Width - 12, 16);
                var ipRect = new RectangleF(n.X + 6, n.Y + 44, n.Width - 12, 14);
                var sf = new StringFormat { Alignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };

                g.DrawString(n.Name, nameFont, nameBrush, nameRect, sf);
                g.DrawString(n.IpAddress, ipFont, ipBrush, ipRect, sf);
            }
        }

        private void DrawDeviceIcon(Graphics g, NetworkDeviceType type, RectangleF r)
        {
            using var pen = new Pen(Color.FromArgb(148, 163, 184), 1.2f);
            switch (type)
            {
                case NetworkDeviceType.Router:
                    g.DrawEllipse(pen, r.X, r.Y, r.Width, r.Height);
                    g.DrawLine(pen, r.X + 3, r.Y + r.Height / 2, r.Right - 3, r.Y + r.Height / 2);
                    break;
                case NetworkDeviceType.Firewall:
                    g.DrawRectangle(pen, r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2);
                    g.DrawLine(pen, r.X + 1, r.Y + r.Height / 2, r.Right - 1, r.Y + r.Height / 2);
                    break;
                case NetworkDeviceType.Plc:
                    g.DrawRectangle(pen, r.X + 2, r.Y + 2, r.Width - 4, r.Height - 4);
                    using (var dot = new SolidBrush(Color.FromArgb(56, 189, 248)))
                    {
                        g.FillRectangle(dot, r.X + 4, r.Y + 4, 3, 3);
                        g.FillRectangle(dot, r.X + 9, r.Y + 4, 3, 3);
                    }
                    break;
                default:
                    g.DrawRectangle(pen, r.X + 1, r.Y + 3, r.Width - 2, r.Height - 6);
                    break;
            }
        }

        private void DrawCanvasHud(Graphics g)
        {
            // Bottom-left info strip
            using var hudBrush = new SolidBrush(Color.FromArgb(200, 20, 24, 33));
            using var hudPen = new Pen(Color.FromArgb(45, 55, 72), 1f);
            var hudRect = new Rectangle(12, Height - 36, 260, 24);
            using var path = CreateRoundedRect(hudRect, 4);
            g.FillPath(hudBrush, path);
            g.DrawPath(hudPen, path);

            using var font = new Font(Font.FontFamily, 7.5f, FontStyle.Regular);
            using var brush = new SolidBrush(Color.FromArgb(203, 213, 225));
            g.DrawString($"Nodes: {_engine.Nodes.Count} | Links: {_engine.Links.Count} | Zoom: {_zoom:P0}", font, brush, 20, Height - 31);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                var worldPt = ScreenToWorld(e.Location);
                var hitNode = _engine.HitTestNode(worldPt.X, worldPt.Y);
                if (hitNode != null)
                {
                    SelectedNode = hitNode;
                }
                else
                {
                    _isPanning = true;
                    _lastMousePos = e.Location;
                }
            }
            else if (e.Button == MouseButtons.Middle)
            {
                _isPanning = true;
                _lastMousePos = e.Location;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_isPanning)
            {
                _pan.X += (e.X - _lastMousePos.X);
                _pan.Y += (e.Y - _lastMousePos.Y);
                _lastMousePos = e.Location;
                Invalidate();
            }
            else
            {
                var worldPt = ScreenToWorld(e.Location);
                var hit = _engine.HitTestNode(worldPt.X, worldPt.Y);
                if (hit != _hoveredNode)
                {
                    _hoveredNode = hit;
                    Invalidate();
                }
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _isPanning = false;
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            float zoomDelta = e.Delta > 0 ? 1.1f : 0.9f;
            ZoomLevel *= zoomDelta;
        }

        private PointF ScreenToWorld(Point pt)
        {
            return new PointF((pt.X - _pan.X) / _zoom, (pt.Y - _pan.Y) / _zoom);
        }

        private static GraphicsPath CreateRoundedRect(RectangleF r, float radius)
        {
            var path = new GraphicsPath();
            float d = radius * 2;
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
    /// Legacy alias for <see cref="ZNetworkTopology"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("NetworkTopology is deprecated and will be removed in 5 release cycles. Please migrate to ZNetworkTopology instead.")]
    [ToolboxItem(false)]
    public class NetworkTopology : ZNetworkTopology
    {
    }

    #endregion
}
