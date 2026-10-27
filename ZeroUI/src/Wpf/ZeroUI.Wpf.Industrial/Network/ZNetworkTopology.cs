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
    /// Vector Topology Graph Canvas Control for WPF.
    /// Supports canvas pan and zoom, device node dragging, multi-type links (Fiber, Copper, Bus, Wireless),
    /// and zero-allocation animated packet pulse wave flows driven by <see cref="ZeroAnimationClock"/>.
    /// </summary>
    public class ZNetworkTopology : ZeroWpfVisualBase
    {
        private readonly TopologyGraphEngine _engine = new TopologyGraphEngine();
        private double _zoom = 1.0;
        private Point _panOffset = new Point(0, 0);

        private TopologyNode? _selectedNode;
        private TopologyLink? _selectedLink;
        private TopologyNode? _draggedNode;
        private Point _lastMousePos;
        private bool _isPanning = false;

        private float _packetPulsePhase = 0f;

        public event EventHandler? SelectedNodeChanged;
        public event EventHandler? SelectedLinkChanged;

        #region Properties

        public TopologyGraphEngine Engine => _engine;

        public double Zoom
        {
            get => _zoom;
            set
            {
                _zoom = Math.Max(0.2, Math.Min(3.0, value));
                InvalidateVisual();
            }
        }

        public TopologyNode? SelectedNode
        {
            get => _selectedNode;
            set
            {
                if (_selectedNode != value)
                {
                    _selectedNode = value;
                    SelectedNodeChanged?.Invoke(this, EventArgs.Empty);
                    InvalidateVisual();
                }
            }
        }

        public TopologyLink? SelectedLink
        {
            get => _selectedLink;
            set
            {
                if (_selectedLink != value)
                {
                    _selectedLink = value;
                    SelectedLinkChanged?.Invoke(this, EventArgs.Empty);
                    InvalidateVisual();
                }
            }
        }

        #endregion

        protected override bool AutoAnimate => true;

        protected override void OnAnimationTick(double delta, long frame)
        {
            _packetPulsePhase = (float)((_packetPulsePhase + delta * 0.8) % 1.0);
        }

        public ZNetworkTopology()
        {
            InitializeDemoTopology();
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
            _engine.AddLink(new TopologyLink { Id = "L4", SourceNodeId = "SW1", TargetNodeId = "SW2", LinkType = NetworkLinkType.Fiber, BandwidthGbps = 40 });
            _engine.AddLink(new TopologyLink { Id = "L5", SourceNodeId = "SW1", TargetNodeId = "ACC1", LinkType = NetworkLinkType.CopperTwistedPair, BandwidthGbps = 1 });
            _engine.AddLink(new TopologyLink { Id = "L6", SourceNodeId = "SW2", TargetNodeId = "SRV1", LinkType = NetworkLinkType.CopperTwistedPair, BandwidthGbps = 10 });
            _engine.AddLink(new TopologyLink { Id = "L7", SourceNodeId = "SW2", TargetNodeId = "PLC1", LinkType = NetworkLinkType.IndustrialBus, BandwidthGbps = 0.1 });
            _engine.AddLink(new TopologyLink { Id = "L8", SourceNodeId = "PLC1", TargetNodeId = "PLC2", LinkType = NetworkLinkType.IndustrialBus, BandwidthGbps = 0.1 });
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
            if (w < 80 || h < 80) return;

            #if NETFRAMEWORK
            double dpi = 1.0;
            #else
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            #endif

            // Canvas Background & Subtle Grid
            var bg = new SolidColorBrush(Color.FromRgb(15, 18, 26));
            bg.Freeze();
            dc.DrawRectangle(bg, null, new Rect(0, 0, w, h));

            DrawGridDots(dc, w, h);

            // Apply Pan & Zoom Transform
            dc.PushTransform(new TransformGroup
            {
                Children = new TransformCollection
                {
                    new ScaleTransform(_zoom, _zoom),
                    new TranslateTransform(_panOffset.X, _panOffset.Y)
                }
            });

            // 1. Draw Links & Animated Packets
            for (int i = 0; i < _engine.Links.Count; i++)
            {
                var link = _engine.Links[i];
                var src = _engine.FindNode(link.SourceNodeId);
                var tgt = _engine.FindNode(link.TargetNodeId);
                if (src == null || tgt == null) continue;

                DrawLink(dc, link, src, tgt);
            }

            // 2. Draw Nodes
            for (int i = 0; i < _engine.Nodes.Count; i++)
            {
                var node = _engine.Nodes[i];
                DrawNode(dc, node, dpi);
            }

            dc.Pop(); // Pop transform

            // Overlay HUD
            DrawHud(dc, w, h, dpi);
        }

        private void DrawGridDots(DrawingContext dc, double w, double h)
        {
            var dotBrush = new SolidColorBrush(Color.FromRgb(30, 36, 50));
            dotBrush.Freeze();
            int spacing = 28;

            for (double x = spacing; x < w; x += spacing)
            {
                for (double y = spacing; y < h; y += spacing)
                {
                    dc.DrawEllipse(dotBrush, null, new Point(x, y), 1.0, 1.0);
                }
            }
        }

        private void DrawLink(DrawingContext dc, TopologyLink link, TopologyNode src, TopologyNode tgt)
        {
            Point p1 = new Point(src.X + src.Width / 2.0, src.Y + src.Height / 2.0);
            Point p2 = new Point(tgt.X + tgt.Width / 2.0, tgt.Y + tgt.Height / 2.0);

            Color linkColor = link.LinkType switch
            {
                NetworkLinkType.Fiber => Color.FromRgb(245, 158, 11),       // Amber
                NetworkLinkType.CopperTwistedPair => Color.FromRgb(56, 189, 248), // Sky Blue
                NetworkLinkType.IndustrialBus => Color.FromRgb(16, 185, 129),   // Emerald
                _ => Color.FromRgb(156, 163, 175)
            };

            bool isSelected = link == _selectedLink;
            var linkPen = new Pen(new SolidColorBrush(linkColor), isSelected ? 3.0 : 1.5);
            linkPen.Freeze();
            dc.DrawLine(linkPen, p1, p2);

            // Animated Packet Pulse traveling along link
            if (link.Status == NetworkLinkStatus.Up)
            {
                double px = p1.X + (p2.X - p1.X) * _packetPulsePhase;
                double py = p1.Y + (p2.Y - p1.Y) * _packetPulsePhase;

                var pulseBrush = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255));
                pulseBrush.Freeze();
                dc.DrawEllipse(pulseBrush, null, new Point(px, py), 3.0, 3.0);
            }
        }

        private void DrawNode(DrawingContext dc, TopologyNode node, double dpi)
        {
            bool isSelected = node == _selectedNode;
            Rect r = new Rect(node.X, node.Y, node.Width, node.Height);

            Color bg = ZeroWpfTheme.IsDark
                ? node.DeviceType switch
                {
                    NetworkDeviceType.Router => Color.FromRgb(30, 41, 59),
                    NetworkDeviceType.Firewall => Color.FromRgb(58, 28, 30),
                    NetworkDeviceType.CoreSwitch or NetworkDeviceType.AccessSwitch => Color.FromRgb(22, 47, 58),
                    NetworkDeviceType.Server => Color.FromRgb(40, 32, 56),
                    NetworkDeviceType.Plc => Color.FromRgb(25, 45, 35),
                    _ => Color.FromRgb(28, 33, 44)
                }
                : node.DeviceType switch
                {
                    NetworkDeviceType.Router => Color.FromRgb(224, 231, 255),
                    NetworkDeviceType.Firewall => Color.FromRgb(254, 226, 226),
                    NetworkDeviceType.CoreSwitch or NetworkDeviceType.AccessSwitch => Color.FromRgb(207, 250, 254),
                    NetworkDeviceType.Server => Color.FromRgb(243, 232, 255),
                    NetworkDeviceType.Plc => Color.FromRgb(220, 252, 231),
                    _ => Color.FromRgb(241, 245, 249)
                };

            var nodeBg = new SolidColorBrush(bg);
            nodeBg.Freeze();
            var nodePen = isSelected
                ? new Pen(ZeroWpfTheme.PrimaryAccent, 2.0)
                : ZeroWpfTheme.BorderPen;
            nodePen.Freeze();

            dc.DrawRoundedRectangle(nodeBg, nodePen, r, 4, 4);

            // Device Status LED
            Brush statusBrush = node.Status == NetworkDeviceStatus.Online ? ZeroWpfTheme.SuccessAccent : ZeroWpfTheme.DangerAccent;
            dc.DrawEllipse(statusBrush, null, new Point(r.Left + 8, r.Top + 10), 3.0, 3.0);

            // Device Name & IP
            var nameFt = CreateFormattedText(node.Name, ZeroWpfTheme.BoldTypeface, 9.5, ZeroWpfTheme.TextPrimary, dpi);
            var ipFt = CreateFormattedText(node.IpAddress, ZeroWpfTheme.RegularTypeface, 8.0, ZeroWpfTheme.TextSecondary, dpi);

            dc.DrawText(nameFt, new Point(r.Left + 18, r.Top + 4));
            dc.DrawText(ipFt, new Point(r.Left + 18, r.Top + 18));
        }

        private void DrawHud(DrawingContext dc, double w, double h, double dpi)
        {
            var hudBg = new SolidColorBrush(Color.FromArgb(220, ZeroWpfTheme.BgCard.Color.R, ZeroWpfTheme.BgCard.Color.G, ZeroWpfTheme.BgCard.Color.B));
            hudBg.Freeze();
            var hudPen = ZeroWpfTheme.BorderPen;

            Rect r = new Rect(10, 10, 260, 48);
            dc.DrawRoundedRectangle(hudBg, hudPen, r, 4, 4);

            var titleFt = CreateFormattedText("ZeroUI OT/IT Topology Graph", ZeroWpfTheme.BoldTypeface, 11.0, ZeroWpfTheme.TextPrimary, dpi);
            var subFt = CreateFormattedText($"Nodes: {_engine.Nodes.Count} | Links: {_engine.Links.Count} | Zoom: {_zoom * 100:0}%", ZeroWpfTheme.RegularTypeface, 9.5, ZeroWpfTheme.TextSecondary, dpi);

            dc.DrawText(titleFt, new Point(r.Left + 8, r.Top + 6));
            dc.DrawText(subFt, new Point(r.Left + 8, r.Top + 24));
        }

        #region Mouse Pan, Zoom, and Dragging

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            double factor = e.Delta > 0 ? 1.1 : 0.9;
            Zoom *= factor;
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            Point p = e.GetPosition(this);
            Point transformed = InverseTransform(p);

            // Check hit test node
            var hit = _engine.HitTestNode((float)transformed.X, (float)transformed.Y);
            if (hit != null)
            {
                SelectedNode = hit;
                _draggedNode = hit;
                _lastMousePos = p;
                CaptureMouse();
                return;
            }

            SelectedNode = null;
        }

        protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseRightButtonDown(e);
            _isPanning = true;
            _lastMousePos = e.GetPosition(this);
            CaptureMouse();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Point current = e.GetPosition(this);
            double dx = current.X - _lastMousePos.X;
            double dy = current.Y - _lastMousePos.Y;

            if (_draggedNode != null)
            {
                _draggedNode.X += (float)(dx / _zoom);
                _draggedNode.Y += (float)(dy / _zoom);
                _lastMousePos = current;
                InvalidateVisual();
            }
            else if (_isPanning)
            {
                _panOffset.X += dx / _zoom;
                _panOffset.Y += dy / _zoom;
                _lastMousePos = current;
                InvalidateVisual();
            }
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if (_draggedNode != null)
            {
                _draggedNode = null;
                ReleaseMouseCapture();
            }
        }

        protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseRightButtonUp(e);
            if (_isPanning)
            {
                _isPanning = false;
                ReleaseMouseCapture();
            }
        }

        private Point InverseTransform(Point p)
        {
            return new Point((p.X / _zoom) - _panOffset.X, (p.Y / _zoom) - _panOffset.Y);
        }

        #endregion
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZNetworkTopology"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("NetworkTopology is deprecated and will be removed in 5 release cycles. Please migrate to ZNetworkTopology instead.")]
    public class NetworkTopology : ZNetworkTopology { }

    /// <summary>
    /// Legacy alias for <see cref="ZNetworkTopology"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroNetworkTopology is deprecated and will be removed in 5 release cycles. Please migrate to ZNetworkTopology instead.")]
    public class ZeroNetworkTopology : ZNetworkTopology { }

    #endregion

}
