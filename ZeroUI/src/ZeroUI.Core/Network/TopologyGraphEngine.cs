using System;
using System.Collections.Generic;

namespace ZeroUI.Core.Network
{
    public enum NetworkDeviceType
    {
        Router,
        Firewall,
        CoreSwitch,
        AccessSwitch,
        Server,
        EdgeDevice,
        Plc,
        Workstation
    }

    public enum NetworkDeviceStatus
    {
        Online,
        Warning,
        Critical,
        Offline
    }

    public enum NetworkLinkType
    {
        Fiber,
        CopperTwistedPair,
        IndustrialBus,
        Wireless
    }

    public enum NetworkLinkStatus
    {
        Up,
        Degraded,
        Down
    }

    public class TopologyNode
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "Node";
        public NetworkDeviceType DeviceType { get; set; } = NetworkDeviceType.AccessSwitch;
        public string IpAddress { get; set; } = "192.168.1.1";
        public NetworkDeviceStatus Status { get; set; } = NetworkDeviceStatus.Online;
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; } = 110f;
        public float Height { get; set; } = 64f;

        public float CenterX => X + Width * 0.5f;
        public float CenterY => Y + Height * 0.5f;

        public bool Contains(float px, float py)
        {
            return px >= X && px <= X + Width && py >= Y && py <= Y + Height;
        }
    }

    public class TopologyLink
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string SourceNodeId { get; set; } = string.Empty;
        public string TargetNodeId { get; set; } = string.Empty;
        public NetworkLinkType LinkType { get; set; } = NetworkLinkType.CopperTwistedPair;
        public double BandwidthGbps { get; set; } = 1.0;
        public double UtilizationPercent { get; set; } = 25.0;
        public NetworkLinkStatus Status { get; set; } = NetworkLinkStatus.Up;
        public double PacketRatePps { get; set; } = 1500.0;
    }

    /// <summary>
    /// High-performance graph engine for network topologies and industrial OT device interconnection.
    /// Handles node/link lookups, coordinate spatial queries, animated packet pulse interpolation, and automatic layouts.
    /// </summary>
    public class TopologyGraphEngine
    {
        private readonly List<TopologyNode> _nodes = new List<TopologyNode>();
        private readonly List<TopologyLink> _links = new List<TopologyLink>();
        private readonly Dictionary<string, TopologyNode> _nodeIndex = new Dictionary<string, TopologyNode>();

        public IReadOnlyList<TopologyNode> Nodes => _nodes;
        public IReadOnlyList<TopologyLink> Links => _links;

        public void AddNode(TopologyNode node)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (!_nodeIndex.ContainsKey(node.Id))
            {
                _nodes.Add(node);
                _nodeIndex[node.Id] = node;
            }
        }

        public bool RemoveNode(string nodeId)
        {
            if (_nodeIndex.TryGetValue(nodeId, out var node))
            {
                _nodeIndex.Remove(nodeId);
                _nodes.Remove(node);
                _links.RemoveAll(l => l.SourceNodeId == nodeId || l.TargetNodeId == nodeId);
                return true;
            }
            return false;
        }

        public TopologyNode? FindNode(string nodeId)
        {
            _nodeIndex.TryGetValue(nodeId, out var node);
            return node;
        }

        public void AddLink(TopologyLink link)
        {
            if (link == null) throw new ArgumentNullException(nameof(link));
            _links.Add(link);
        }

        public bool RemoveLink(string linkId)
        {
            int idx = _links.FindIndex(l => l.Id == linkId);
            if (idx >= 0)
            {
                _links.RemoveAt(idx);
                return true;
            }
            return false;
        }

        public void Clear()
        {
            _nodes.Clear();
            _links.Clear();
            _nodeIndex.Clear();
        }

        public TopologyNode? HitTestNode(float px, float py)
        {
            for (int i = _nodes.Count - 1; i >= 0; i--)
            {
                if (_nodes[i].Contains(px, py))
                {
                    return _nodes[i];
                }
            }
            return null;
        }

        /// <summary>
        /// Computes interpolated 2D coordinate of an animated packet pulse along a link vector.
        /// Zero allocation on hot rendering loop.
        /// </summary>
        public bool ComputePulseCoordinate(TopologyLink link, float phase01, out float px, out float py)
        {
            px = 0;
            py = 0;
            if (!_nodeIndex.TryGetValue(link.SourceNodeId, out var src) ||
                !_nodeIndex.TryGetValue(link.TargetNodeId, out var dst))
            {
                return false;
            }

            float t = phase01 % 1.0f;
            px = src.CenterX + (dst.CenterX - src.CenterX) * t;
            py = src.CenterY + (dst.CenterY - src.CenterY) * t;
            return true;
        }

        /// <summary>
        /// Calculates bounding box for all nodes in the topology graph.
        /// </summary>
        public void CalculateBounds(out float minX, out float minY, out float maxX, out float maxY)
        {
            if (_nodes.Count == 0)
            {
                minX = minY = maxX = maxY = 0;
                return;
            }

            minX = float.MaxValue;
            minY = float.MaxValue;
            maxX = float.MinValue;
            maxY = float.MinValue;

            for (int i = 0; i < _nodes.Count; i++)
            {
                var n = _nodes[i];
                if (n.X < minX) minX = n.X;
                if (n.Y < minY) minY = n.Y;
                if (n.X + n.Width > maxX) maxX = n.X + n.Width;
                if (n.Y + n.Height > maxY) maxY = n.Y + n.Height;
            }
        }

        /// <summary>
        /// Applies an automatic multi-tiered hierarchical layout (Core -> Distribution -> Access -> Edge).
        /// </summary>
        public void ApplyHierarchicalLayout(float canvasWidth, float canvasHeight)
        {
            var tiers = new Dictionary<int, List<TopologyNode>>();
            for (int i = 0; i <= 3; i++) tiers[i] = new List<TopologyNode>();

            for (int i = 0; i < _nodes.Count; i++)
            {
                var n = _nodes[i];
                int tier = n.DeviceType switch
                {
                    NetworkDeviceType.Router or NetworkDeviceType.Firewall => 0,
                    NetworkDeviceType.CoreSwitch => 1,
                    NetworkDeviceType.AccessSwitch or NetworkDeviceType.Server => 2,
                    _ => 3
                };
                tiers[tier].Add(n);
            }

            float rowHeight = canvasHeight / 4.5f;
            for (int tier = 0; tier <= 3; tier++)
            {
                var list = tiers[tier];
                if (list.Count == 0) continue;

                float rowY = 40f + tier * rowHeight;
                float colWidth = canvasWidth / (list.Count + 1);

                for (int col = 0; col < list.Count; col++)
                {
                    var node = list[col];
                    node.X = (col + 1) * colWidth - node.Width * 0.5f;
                    node.Y = rowY;
                }
            }
        }

        /// <summary>
        /// Applies circular / ring layout (ideal for industrial redundant rings like Profinet MRP / EtherNet/IP DLR).
        /// </summary>
        public void ApplyCircularLayout(float centerX, float centerY, float radius)
        {
            if (_nodes.Count == 0) return;
            double step = (Math.PI * 2.0) / _nodes.Count;

            for (int i = 0; i < _nodes.Count; i++)
            {
                double angle = i * step - Math.PI * 0.5;
                _nodes[i].X = (float)(centerX + Math.Cos(angle) * radius - _nodes[i].Width * 0.5f);
                _nodes[i].Y = (float)(centerY + Math.Sin(angle) * radius - _nodes[i].Height * 0.5f);
            }
        }
    }
}
