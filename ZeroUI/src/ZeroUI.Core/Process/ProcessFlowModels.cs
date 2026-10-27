using System;
using System.Collections.Generic;
using System.Linq;

namespace ZeroUI.Core.Process
{
    /// <summary>
    /// Visual shape type for a node in a business process map.
    /// </summary>
    public enum ProcessNodeShape
    {
        /// <summary>
        /// Standard rectangular process task card with header, title, and description.
        /// </summary>
        TaskCard,

        /// <summary>
        /// Diamond decision node representing conditional branch logic.
        /// </summary>
        DecisionDiamond,

        /// <summary>
        /// Pill-shaped start terminal node.
        /// </summary>
        StartTerminal,

        /// <summary>
        /// Pill-shaped end/completion terminal node.
        /// </summary>
        EndTerminal
    }

    /// <summary>
    /// Runtime operational status of a process step.
    /// </summary>
    public enum ProcessNodeStatus
    {
        Normal,
        InProgress,
        Completed,
        Warning,
        Disabled
    }

    /// <summary>
    /// Cardinal port position on a process node for wire attachments.
    /// </summary>
    public enum ProcessPortPosition
    {
        Left,
        Right,
        Top,
        Bottom,
        Center
    }

    /// <summary>
    /// Wire routing style between process nodes.
    /// </summary>
    public enum ProcessRoutingMode
    {
        /// <summary>
        /// Standard orthogonal stepped 90-degree lines with arrows.
        /// </summary>
        Orthogonal,

        /// <summary>
        /// Direct point-to-point straight line.
        /// </summary>
        Straight,

        /// <summary>
        /// Smooth cubic Bezier spline.
        /// </summary>
        Bezier
    }

    /// <summary>
    /// Represents a single actionable step or decision node in a business process diagram.
    /// </summary>
    public class ProcessFlowNode
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Title { get; set; } = "Step Title";
        public string Subtitle { get; set; } = string.Empty;
        public ProcessNodeShape Shape { get; set; } = ProcessNodeShape.TaskCard;
        public ProcessNodeStatus Status { get; set; } = ProcessNodeStatus.Normal;

        public double X { get; set; } = 0;
        public double Y { get; set; } = 0;
        public double Width { get; set; } = 220;
        public double Height { get; set; } = 80;

        /// <summary>
        /// Semantic or hex header background color (e.g. #3B82F6, #8B5CF6, #10B981, #0284C7).
        /// </summary>
        public string HeaderColorHex { get; set; } = "#3B82F6";

        /// <summary>
        /// Semantic or hex border color.
        /// </summary>
        public string BorderColorHex { get; set; } = "#CBD5E1";

        /// <summary>
        /// Icon symbol or glyph (e.g. 📄, 🔍, 📦, 🛒, 🏢, 📥).
        /// </summary>
        public string IconGlyph { get; set; } = "📄";

        /// <summary>
        /// Target action key registered in <see cref="ProcessActionRegistry"/> to execute when clicked.
        /// </summary>
        public string ActionKey { get; set; } = string.Empty;

        /// <summary>
        /// Optional identifier of the parent swimlane containing this node.
        /// </summary>
        public string? LaneId { get; set; }

        /// <summary>
        /// Optional runtime metadata or data transfer object.
        /// </summary>
        public object? Tag { get; set; }

        public ProcessFlowNode()
        {
        }

        public ProcessFlowNode(string id, string title, string subtitle, double x, double y, double width = 220, double height = 80)
        {
            Id = id;
            Title = title;
            Subtitle = subtitle;
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }
    }

    /// <summary>
    /// Represents a directed connector or branch between two process nodes.
    /// </summary>
    public class ProcessFlowConnection
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string SourceNodeId { get; set; } = string.Empty;
        public string TargetNodeId { get; set; } = string.Empty;

        public ProcessPortPosition SourcePort { get; set; } = ProcessPortPosition.Bottom;
        public ProcessPortPosition TargetPort { get; set; } = ProcessPortPosition.Top;

        /// <summary>
        /// Descriptive label displayed along the transition connector (e.g. "CÒN HÀNG SẴN", "HẾT HÀNG -> ĐỀ XUẤT MUA").
        /// </summary>
        public string Label { get; set; } = string.Empty;

        public string StrokeColorHex { get; set; } = "#0EA5E9";
        public double StrokeThickness { get; set; } = 2.0;
        public bool IsDashed { get; set; } = false;
        public ProcessRoutingMode RoutingMode { get; set; } = ProcessRoutingMode.Orthogonal;

        public ProcessFlowConnection()
        {
        }

        public ProcessFlowConnection(string sourceNodeId, string targetNodeId, string label = "", string strokeColorHex = "#0EA5E9")
        {
            SourceNodeId = sourceNodeId;
            TargetNodeId = targetNodeId;
            Label = label;
            StrokeColorHex = strokeColorHex;
        }
    }

    /// <summary>
    /// Cardinal and center alignment options for aligning multiple process nodes.
    /// </summary>
    public enum ProcessNodeAlignment
    {
        Left,
        Center,
        Right,
        Top,
        Middle,
        Bottom
    }

    /// <summary>
    /// Represents a horizontal or vertical swimlane boundary grouping steps by operational department.
    /// </summary>
    public class ProcessFlowLane
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Title { get; set; } = "Lane";
        public double X { get; set; } = 0;
        public double Y { get; set; } = 0;
        public double Width { get; set; } = 1200;
        public double Height { get; set; } = 400;
        public string HeaderColorHex { get; set; } = "#64748B";
        public string BackgroundColorHex { get; set; } = "#F8FAFC";
        public double HeaderHeight { get; set; } = 34;

        /// <summary>
        /// Curated visual color theme presets for swimlanes / group frames.
        /// </summary>
        public static readonly SwimlaneColorPreset[] DefaultPresets = new[]
        {
            new SwimlaneColorPreset("Slate Gray (Default)",  "#64748B", "#F8FAFC"),
            new SwimlaneColorPreset("Indigo / Blue",         "#6366F1", "#EEF2FF"),
            new SwimlaneColorPreset("Emerald Green",         "#10B981", "#ECFDF5"),
            new SwimlaneColorPreset("Amber Orange",          "#F59E0B", "#FFFBEB"),
            new SwimlaneColorPreset("Rose Red",              "#F43F5E", "#FFF1F2"),
            new SwimlaneColorPreset("Purple / Violet",       "#8B5CF6", "#F5F3FF"),
            new SwimlaneColorPreset("Sky Blue / Cyan",       "#0284C7", "#F0F9FF"),
            new SwimlaneColorPreset("Teal / Mint",           "#0D9488", "#F0FDFA"),
            new SwimlaneColorPreset("Dark Steel / Charcoal", "#334155", "#F1F5F9")
        };

        public ProcessFlowLane()
        {
        }

        public ProcessFlowLane(string id, string title, double x, double y, double width, double height)
        {
            Id = id;
            Title = title;
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public bool Contains(double px, double py)
        {
            return px >= X && px <= X + Width && py >= Y && py <= Y + Height;
        }

        public bool Contains(ProcessFlowNode node)
        {
            if (node == null) return false;
            return node.X >= X && (node.X + node.Width) <= (X + Width) &&
                   node.Y >= Y && (node.Y + node.Height) <= (Y + Height);
        }

        public bool Intersects(ProcessFlowNode node)
        {
            if (node == null) return false;
            return !(node.X + node.Width < X || node.X > X + Width ||
                     node.Y + node.Height < Y || node.Y > Y + Height);
        }
    }

    /// <summary>
    /// Visual color preset for swimlanes combining complementary header and background colors.
    /// </summary>
    public class SwimlaneColorPreset
    {
        public string Name { get; set; } = string.Empty;
        public string HeaderHex { get; set; } = "#64748B";
        public string BgHex { get; set; } = "#F8FAFC";

        public SwimlaneColorPreset()
        {
        }

        public SwimlaneColorPreset(string name, string headerHex, string bgHex)
        {
            Name = name;
            HeaderHex = headerHex;
            BgHex = bgHex;
        }

        public override string ToString() => Name;
    }

    /// <summary>
    /// Root data model encapsulating a complete customizable business process workflow graph.
    /// </summary>
    public class ProcessFlowDefinition
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Code { get; set; } = "PROC_DEFAULT";
        public string Title { get; set; } = "Business Process Flow";
        public string Description { get; set; } = string.Empty;
        public int Version { get; set; } = 1;

        public List<ProcessFlowLane> Lanes { get; } = new List<ProcessFlowLane>();
        public List<ProcessFlowNode> Nodes { get; } = new List<ProcessFlowNode>();
        public List<ProcessFlowConnection> Connections { get; } = new List<ProcessFlowConnection>();

        /// <summary>
        /// Creates and adds a new <see cref="ProcessFlowLane"/> that encompasses the specified nodes with appropriate padding.
        /// </summary>
        public ProcessFlowLane CreateLaneFromSelection(IEnumerable<ProcessFlowNode> nodes, string title = "NEW PHASE", string headerColorHex = "#0EA5E9", string? bgColorHex = null)
        {
            var list = nodes?.ToList() ?? new List<ProcessFlowNode>();
            if (list.Count == 0)
            {
                var emptyLane = new ProcessFlowLane("lane_" + Guid.NewGuid().ToString("N").Substring(0, 8), title, 40, 40, 600, 300)
                {
                    HeaderColorHex = headerColorHex,
                    BackgroundColorHex = bgColorHex ?? "#F0F9FF"
                };
                Lanes.Add(emptyLane);
                return emptyLane;
            }

            double minX = double.MaxValue;
            double minY = double.MaxValue;
            double maxX = double.MinValue;
            double maxY = double.MinValue;

            foreach (var n in list)
            {
                if (n.X < minX) minX = n.X;
                if (n.Y < minY) minY = n.Y;
                if (n.X + n.Width > maxX) maxX = n.X + n.Width;
                if (n.Y + n.Height > maxY) maxY = n.Y + n.Height;
            }

            double padLeft = 36;
            double padRight = 36;
            double padTop = 48;
            double padBottom = 36;

            var lane = new ProcessFlowLane(
                "lane_" + Guid.NewGuid().ToString("N").Substring(0, 8),
                title,
                Math.Max(10, minX - padLeft),
                Math.Max(10, minY - padTop),
                (maxX - minX) + padLeft + padRight,
                (maxY - minY) + padTop + padBottom)
            {
                HeaderColorHex = headerColorHex,
                BackgroundColorHex = bgColorHex ?? "#F0F9FF"
            };

            foreach (var n in list)
            {
                n.LaneId = lane.Id;
            }

            Lanes.Add(lane);
            return lane;
        }

        /// <summary>
        /// Adjusts every lane's bounds to snugly enclose all its member nodes.
        /// </summary>
        public void FitLanesToNodes(double padding = 36, double headerPadding = 48)
        {
            foreach (var lane in Lanes)
            {
                var memberNodes = Nodes.Where(n => n.LaneId == lane.Id || lane.Contains(n.X + n.Width / 2, n.Y + n.Height / 2)).ToList();
                if (memberNodes.Count == 0) continue;

                double minX = memberNodes.Min(n => n.X);
                double minY = memberNodes.Min(n => n.Y);
                double maxX = memberNodes.Max(n => n.X + n.Width);
                double maxY = memberNodes.Max(n => n.Y + n.Height);

                lane.X = Math.Max(10, minX - padding);
                lane.Y = Math.Max(10, minY - headerPadding);
                lane.Width = (maxX - minX) + (padding * 2);
                lane.Height = (maxY - minY) + headerPadding + padding;
            }
        }

        /// <summary>
        /// Automatically organizes nodes in a clear, layered DAG layout and adjusts lane bounds.
        /// </summary>
        public void AutoArrangeLayout(bool horizontal = true, double nodeSpacingX = 80, double nodeSpacingY = 50)
        {
            if (Nodes.Count == 0) return;

            var laneGroups = Nodes.GroupBy(n => n.LaneId ?? "").ToList();
            double currentLaneOffsetY = 30;
            double currentLaneOffsetX = 30;

            foreach (var group in laneGroups)
            {
                var groupNodes = group.ToList();
                var nodeIds = new HashSet<string>(groupNodes.Select(n => n.Id));
                var groupConns = Connections.Where(c => nodeIds.Contains(c.SourceNodeId) && nodeIds.Contains(c.TargetNodeId)).ToList();

                var inDegrees = new Dictionary<string, int>();
                foreach (var n in groupNodes) inDegrees[n.Id] = 0;
                foreach (var c in groupConns)
                {
                    if (inDegrees.ContainsKey(c.TargetNodeId))
                        inDegrees[c.TargetNodeId]++;
                }

                var layers = new List<List<ProcessFlowNode>>();
                var assigned = new HashSet<string>();

                var currentLayer = groupNodes.Where(n => inDegrees[n.Id] == 0).ToList();
                if (currentLayer.Count == 0 && groupNodes.Count > 0)
                {
                    currentLayer.Add(groupNodes[0]);
                }

                while (currentLayer.Count > 0)
                {
                    layers.Add(currentLayer);
                    foreach (var n in currentLayer) assigned.Add(n.Id);

                    var nextLayer = new List<ProcessFlowNode>();
                    foreach (var n in currentLayer)
                    {
                        var targets = groupConns.Where(c => c.SourceNodeId == n.Id)
                                                .Select(c => groupNodes.FirstOrDefault(gn => gn.Id == c.TargetNodeId))
                                                .Where(gn => gn != null && !assigned.Contains(gn.Id) && !nextLayer.Contains(gn!))
                                                .ToList();
                        foreach (var t in targets)
                        {
                            if (t != null) nextLayer.Add(t);
                        }
                    }

                    if (nextLayer.Count == 0 && assigned.Count < groupNodes.Count)
                    {
                        var unassigned = groupNodes.FirstOrDefault(gn => !assigned.Contains(gn.Id));
                        if (unassigned != null) nextLayer.Add(unassigned);
                    }

                    currentLayer = nextLayer;
                }

                double startX = horizontal ? 60 : currentLaneOffsetX;
                double startY = horizontal ? currentLaneOffsetY + 50 : 60;

                double maxGroupX = startX;
                double maxGroupY = startY;

                if (horizontal)
                {
                    double curX = startX;
                    foreach (var layer in layers)
                    {
                        double layerMaxW = layer.Max(n => n.Width);
                        double curY = startY;

                        foreach (var n in layer)
                        {
                            n.X = curX;
                            n.Y = curY;
                            curY += n.Height + nodeSpacingY;
                            if (n.X + n.Width > maxGroupX) maxGroupX = n.X + n.Width;
                            if (n.Y + n.Height > maxGroupY) maxGroupY = n.Y + n.Height;
                        }
                        curX += layerMaxW + nodeSpacingX;
                    }
                }
                else
                {
                    double curY = startY;
                    foreach (var layer in layers)
                    {
                        double layerMaxH = layer.Max(n => n.Height);
                        double curX = startX;

                        foreach (var n in layer)
                        {
                            n.X = curX;
                            n.Y = curY;
                            curX += n.Width + nodeSpacingX;
                            if (n.X + n.Width > maxGroupX) maxGroupX = n.X + n.Width;
                            if (n.Y + n.Height > maxGroupY) maxGroupY = n.Y + n.Height;
                        }
                        curY += layerMaxH + nodeSpacingY;
                    }
                }

                var matchedLane = Lanes.FirstOrDefault(l => l.Id == group.Key);
                if (matchedLane != null)
                {
                    matchedLane.X = Math.Max(20, startX - 40);
                    matchedLane.Y = Math.Max(20, currentLaneOffsetY);
                    matchedLane.Width = Math.Max(400, (maxGroupX - startX) + 80);
                    matchedLane.Height = Math.Max(200, (maxGroupY - currentLaneOffsetY) + 40);

                    currentLaneOffsetY = matchedLane.Y + matchedLane.Height + 40;
                }
                else
                {
                    currentLaneOffsetY = maxGroupY + 60;
                }
            }
        }

        /// <summary>
        /// Aligns the specified nodes along a common edge or center axis.
        /// </summary>
        public static void AlignNodes(IEnumerable<ProcessFlowNode> nodes, ProcessNodeAlignment alignment)
        {
            var list = nodes?.ToList();
            if (list == null || list.Count < 2) return;

            switch (alignment)
            {
                case ProcessNodeAlignment.Left:
                    double minX = list.Min(n => n.X);
                    foreach (var n in list) n.X = minX;
                    break;
                case ProcessNodeAlignment.Center:
                    double avgCenterX = list.Average(n => n.X + n.Width / 2);
                    foreach (var n in list) n.X = avgCenterX - n.Width / 2;
                    break;
                case ProcessNodeAlignment.Right:
                    double maxRight = list.Max(n => n.X + n.Width);
                    foreach (var n in list) n.X = maxRight - n.Width;
                    break;
                case ProcessNodeAlignment.Top:
                    double minY = list.Min(n => n.Y);
                    foreach (var n in list) n.Y = minY;
                    break;
                case ProcessNodeAlignment.Middle:
                    double avgCenterY = list.Average(n => n.Y + n.Height / 2);
                    foreach (var n in list) n.Y = avgCenterY - n.Height / 2;
                    break;
                case ProcessNodeAlignment.Bottom:
                    double maxBottom = list.Max(n => n.Y + n.Height);
                    foreach (var n in list) n.Y = maxBottom - n.Height;
                    break;
            }
        }

        /// <summary>
        /// Evenly distributes the specified nodes horizontally or vertically between the two outermost nodes.
        /// </summary>
        public static void DistributeNodes(IEnumerable<ProcessFlowNode> nodes, bool horizontally)
        {
            var list = nodes?.ToList();
            if (list == null || list.Count < 3) return;

            if (horizontally)
            {
                var sorted = list.OrderBy(n => n.X).ToList();
                double totalSpan = (sorted.Last().X + sorted.Last().Width) - sorted.First().X;
                double totalNodesWidth = sorted.Sum(n => n.Width);
                double gap = (totalSpan - totalNodesWidth) / (sorted.Count - 1);

                double currentX = sorted.First().X;
                foreach (var n in sorted)
                {
                    n.X = currentX;
                    currentX += n.Width + gap;
                }
            }
            else
            {
                var sorted = list.OrderBy(n => n.Y).ToList();
                double totalSpan = (sorted.Last().Y + sorted.Last().Height) - sorted.First().Y;
                double totalNodesHeight = sorted.Sum(n => n.Height);
                double gap = (totalSpan - totalNodesHeight) / (sorted.Count - 1);

                double currentY = sorted.First().Y;
                foreach (var n in sorted)
                {
                    n.Y = currentY;
                    currentY += n.Height + gap;
                }
            }
        }

        /// <summary>
        /// Creates a factory template matching the standard IT Inventory business workflow
        /// with requisition, stock-check decision, PO purchase approval, receipt, and warehouse inward.
        /// </summary>
        public static ProcessFlowDefinition CreateDefaultWarehouseFlow()
        {
            var def = new ProcessFlowDefinition
            {
                Code = "IT_INVENTORY_WORKFLOW",
                Title = "IT EQUIPMENT INVENTORY & FULFILLMENT WORKFLOW",
                Description = "Click process cards to navigate to corresponding operations"
            };

            // 1. Swimlanes
            def.Lanes.Add(new ProcessFlowLane("lane_request", "REQUISITION & PROCUREMENT", 20, 20, 960, 520)
            {
                HeaderColorHex = "#64748B",
                BackgroundColorHex = "#F8FAFC"
            });
            def.Lanes.Add(new ProcessFlowLane("lane_warehouse", "RECEIVING & INVENTORY INWARD", 20, 560, 960, 320)
            {
                HeaderColorHex = "#0EA5E9",
                BackgroundColorHex = "#F0F9FF"
            });

            // 2. Nodes
            // Step 1: IT Requisition
            def.Nodes.Add(new ProcessFlowNode("node_req", "1. REQUISITION TICKET", "User submits equipment requisition ticket", 370, 60, 220, 70)
            {
                Shape = ProcessNodeShape.TaskCard,
                HeaderColorHex = "#475569",
                IconGlyph = "📄",
                ActionKey = "it.ticket.request",
                LaneId = "lane_request"
            });

            // Step 2: Inventory Stock Check (Decision Diamond)
            def.Nodes.Add(new ProcessFlowNode("node_check", "2. INVENTORY AUDIT", "(Verify stock in warehouse pool)", 390, 180, 180, 80)
            {
                Shape = ProcessNodeShape.DecisionDiamond,
                HeaderColorHex = "#0284C7",
                BorderColorHex = "#38BDF8",
                IconGlyph = "🔍",
                ActionKey = "it.inventory.lookup",
                LaneId = "lane_request"
            });

            // Step 3: Purchase Requisition
            def.Nodes.Add(new ProcessFlowNode("node_pr", "3. PURCHASE REQUISITION", "• Create PR form\n• Submit for approval hierarchy", 640, 290, 220, 75)
            {
                Shape = ProcessNodeShape.TaskCard,
                HeaderColorHex = "#7C3AED",
                BorderColorHex = "#DDD6FE",
                IconGlyph = "🛒",
                ActionKey = "purchase.pr.create",
                LaneId = "lane_request"
            });

            // Step 4: Purchase Order (PO)
            def.Nodes.Add(new ProcessFlowNode("node_po", "4. PURCHASE ORDER (PO)", "• Procurement team creates PO\n• Transmit to vendor", 640, 390, 220, 75)
            {
                Shape = ProcessNodeShape.TaskCard,
                HeaderColorHex = "#7C3AED",
                BorderColorHex = "#DDD6FE",
                IconGlyph = "🏢",
                ActionKey = "purchase.po.create",
                LaneId = "lane_request"
            });

            // Step 5: Goods Receipt Note
            def.Nodes.Add(new ProcessFlowNode("node_grn", "5. GOODS RECEIPT (GRN)", "• Warehouse manager verifies shipment\n• Sign receiving note", 640, 490, 220, 75)
            {
                Shape = ProcessNodeShape.TaskCard,
                HeaderColorHex = "#0284C7",
                BorderColorHex = "#BAE6FD",
                IconGlyph = "📦",
                ActionKey = "inventory.receipt.note",
                LaneId = "lane_request"
            });

            // Step 6: Warehouse Stock-In
            def.Nodes.Add(new ProcessFlowNode("node_inward", "6. WAREHOUSE INWARD", "• Record stock-in entry\n• Generate Asset Tags", 640, 590, 220, 75)
            {
                Shape = ProcessNodeShape.TaskCard,
                HeaderColorHex = "#16A34A",
                BorderColorHex = "#BBF7D0",
                IconGlyph = "📥",
                ActionKey = "it.inventory.stockin",
                LaneId = "lane_warehouse"
            });

            // Target Step: Warehouse Stock Pool
            def.Nodes.Add(new ProcessFlowNode("node_stock", "AVAILABLE STOCK", "Hardware ready for deployment", 260, 680, 220, 70)
            {
                Shape = ProcessNodeShape.TaskCard,
                HeaderColorHex = "#0D9488",
                BorderColorHex = "#99F6E4",
                IconGlyph = "🖥️",
                ActionKey = "it.inventory.pool",
                LaneId = "lane_warehouse"
            });

            // 3. Connections
            // node_req -> node_check
            def.Connections.Add(new ProcessFlowConnection("node_req", "node_check")
            {
                SourcePort = ProcessPortPosition.Bottom,
                TargetPort = ProcessPortPosition.Top,
                StrokeColorHex = "#94A3B8"
            });

            // node_check -> node_pr (Out of Stock branch)
            def.Connections.Add(new ProcessFlowConnection("node_check", "node_pr", "OUT OF STOCK -> REORDER", "#EF4444")
            {
                SourcePort = ProcessPortPosition.Right,
                TargetPort = ProcessPortPosition.Top,
                StrokeColorHex = "#EF4444"
            });

            // node_check -> node_stock (In Stock branch)
            def.Connections.Add(new ProcessFlowConnection("node_check", "node_stock", "IN STOCK AVAILABLE", "#10B981")
            {
                SourcePort = ProcessPortPosition.Left,
                TargetPort = ProcessPortPosition.Top,
                StrokeColorHex = "#10B981"
            });

            // node_pr -> node_po
            def.Connections.Add(new ProcessFlowConnection("node_pr", "node_po")
            {
                SourcePort = ProcessPortPosition.Bottom,
                TargetPort = ProcessPortPosition.Top,
                StrokeColorHex = "#94A3B8"
            });

            // node_po -> node_grn
            def.Connections.Add(new ProcessFlowConnection("node_po", "node_grn")
            {
                SourcePort = ProcessPortPosition.Bottom,
                TargetPort = ProcessPortPosition.Top,
                StrokeColorHex = "#94A3B8"
            });

            // node_grn -> node_inward
            def.Connections.Add(new ProcessFlowConnection("node_grn", "node_inward")
            {
                SourcePort = ProcessPortPosition.Bottom,
                TargetPort = ProcessPortPosition.Top,
                StrokeColorHex = "#94A3B8"
            });

            // node_inward -> node_stock
            def.Connections.Add(new ProcessFlowConnection("node_inward", "node_stock")
            {
                SourcePort = ProcessPortPosition.Left,
                TargetPort = ProcessPortPosition.Right,
                StrokeColorHex = "#10B981"
            });

            return def;
        }
    }
}
