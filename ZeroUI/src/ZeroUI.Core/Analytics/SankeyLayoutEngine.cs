using System;
using System.Collections.Generic;
using System.Linq;

namespace ZeroUI.Core.Analytics
{
    /// <summary>
    /// Platform-agnostic node definition in a Sankey process flow network.
    /// </summary>
    public class SankeyNode
    {
        public string Name { get; set; } = string.Empty;
        public int Column { get; set; }
        public uint? ColorRgba { get; set; }
        public object? Tag { get; set; }

        public SankeyNode() { }

        public SankeyNode(string name, int column = 0, uint? colorRgba = null)
        {
            Name = name ?? string.Empty;
            Column = column;
            ColorRgba = colorRgba;
        }
    }

    /// <summary>
    /// Platform-agnostic directed connection link between two Sankey nodes.
    /// </summary>
    public class SankeyLink
    {
        public string Source { get; set; } = string.Empty;
        public string Target { get; set; } = string.Empty;
        public double Value { get; set; }
        public uint? ColorRgba { get; set; }
        public object? Tag { get; set; }

        public SankeyLink() { }

        public SankeyLink(string source, string target, double value, uint? colorRgba = null)
        {
            Source = source ?? string.Empty;
            Target = target ?? string.Empty;
            Value = value;
            ColorRgba = colorRgba;
        }
    }

    /// <summary>
    /// Calculated spatial metrics for a node normalized to [0..1] coordinate space.
    /// </summary>
    public class SankeyComputedNode
    {
        public SankeyNode Node { get; set; } = null!;
        public double InValue { get; set; }
        public double OutValue { get; set; }
        public double EffectiveValue { get; set; }

        /// <summary>
        /// Normalized horizontal center [0..1]
        /// </summary>
        public double NormalizedX { get; set; }

        /// <summary>
        /// Normalized top coordinate [0..1]
        /// </summary>
        public double NormalizedY { get; set; }

        /// <summary>
        /// Normalized height [0..1]
        /// </summary>
        public double NormalizedHeight { get; set; }
    }

    /// <summary>
    /// Calculated ribbon flow segment linking source and target nodes with exact vertical attachment points.
    /// </summary>
    public class SankeyComputedRibbon
    {
        public SankeyLink Link { get; set; } = null!;
        public SankeyComputedNode SourceNode { get; set; } = null!;
        public SankeyComputedNode TargetNode { get; set; } = null!;

        public double SourceY0 { get; set; }
        public double SourceY1 { get; set; }
        public double TargetY0 { get; set; }
        public double TargetY1 { get; set; }
    }

    /// <summary>
    /// Layout result containing normalized geometric arrangement for entire Sankey diagram.
    /// </summary>
    public class SankeyLayoutResult
    {
        public IReadOnlyList<SankeyComputedNode> Nodes { get; }
        public IReadOnlyList<SankeyComputedRibbon> Ribbons { get; }
        public int ColumnCount { get; }

        public SankeyLayoutResult(IReadOnlyList<SankeyComputedNode> nodes, IReadOnlyList<SankeyComputedRibbon> ribbons, int columnCount)
        {
            Nodes = nodes;
            Ribbons = ribbons;
            ColumnCount = columnCount;
        }
    }

    /// <summary>
    /// Core mathematical layout engine for multi-stage Sankey process & energy distribution flows.
    /// Computes column stratification, proportional bandwidth distribution, and cubic Bézier attachment anchors.
    /// </summary>
    public static class SankeyLayoutEngine
    {
        public static SankeyLayoutResult Compute(
            IEnumerable<SankeyNode> nodes,
            IEnumerable<SankeyLink> links,
            double nodeGapFraction = 0.05)
        {
            var nodeList = nodes?.Where(n => n != null).ToList() ?? new List<SankeyNode>();
            var linkList = links?.Where(l => l != null && l.Value > 0).ToList() ?? new List<SankeyLink>();

            if (nodeList.Count == 0)
            {
                return new SankeyLayoutResult(Array.Empty<SankeyComputedNode>(), Array.Empty<SankeyComputedRibbon>(), 0);
            }

            var nodeMap = new Dictionary<string, SankeyComputedNode>(StringComparer.OrdinalIgnoreCase);
            foreach (var n in nodeList)
            {
                nodeMap[n.Name] = new SankeyComputedNode { Node = n };
            }

            // Sum incoming and outgoing flows
            foreach (var l in linkList)
            {
                if (nodeMap.TryGetValue(l.Source, out var srcNode))
                {
                    srcNode.OutValue += l.Value;
                }
                if (nodeMap.TryGetValue(l.Target, out var dstNode))
                {
                    dstNode.InValue += l.Value;
                }
            }

            foreach (var cn in nodeMap.Values)
            {
                cn.EffectiveValue = Math.Max(0.001, Math.Max(cn.InValue, cn.OutValue));
            }

            // Group by Column
            var columns = nodeMap.Values.GroupBy(x => x.Node.Column).OrderBy(g => g.Key).ToList();
            int colCount = columns.Count;

            // Maximum column capacity determines vertical unit scale
            double maxColTotal = 0;
            foreach (var col in columns)
            {
                double sum = col.Sum(n => n.EffectiveValue);
                if (sum > maxColTotal) maxColTotal = sum;
            }
            if (maxColTotal <= 0) maxColTotal = 1.0;

            for (int c = 0; c < colCount; c++)
            {
                var colGroup = columns[c].ToList();
                double colX = colCount > 1 ? (double)c / (colCount - 1) : 0.5;

                int nodeCount = colGroup.Count;
                double totalGaps = (nodeCount - 1) * nodeGapFraction;
                double availableH = Math.Max(0.1, 1.0 - totalGaps);

                double totalVal = colGroup.Sum(n => n.EffectiveValue);
                double scale = totalVal > 0 ? (availableH * (totalVal / maxColTotal)) / totalVal : 1.0;

                // Center column vertically if total value is smaller than max column
                double usedHeight = (totalVal * scale) + totalGaps;
                double currentY = Math.Max(0, (1.0 - usedHeight) / 2.0);

                foreach (var node in colGroup)
                {
                    double nh = Math.Max(0.02, node.EffectiveValue * scale);
                    node.NormalizedX = colX;
                    node.NormalizedY = currentY;
                    node.NormalizedHeight = nh;
                    currentY += nh + nodeGapFraction;
                }
            }

            // Calculate ribbon attachments
            var ribbons = new List<SankeyComputedRibbon>(linkList.Count);
            var srcOffsets = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            var dstOffsets = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

            foreach (var link in linkList)
            {
                if (nodeMap.TryGetValue(link.Source, out var src) && nodeMap.TryGetValue(link.Target, out var dst))
                {
                    srcOffsets.TryGetValue(src.Node.Name, out double curSrcOff);
                    dstOffsets.TryGetValue(dst.Node.Name, out double curDstOff);

                    double srcFraction = src.OutValue > 0 ? (link.Value / src.OutValue) : 0;
                    double dstFraction = dst.InValue > 0 ? (link.Value / dst.InValue) : 0;

                    double srcH = src.NormalizedHeight * srcFraction;
                    double dstH = dst.NormalizedHeight * dstFraction;

                    double s0 = src.NormalizedY + curSrcOff;
                    double s1 = s0 + srcH;
                    double t0 = dst.NormalizedY + curDstOff;
                    double t1 = t0 + dstH;

                    ribbons.Add(new SankeyComputedRibbon
                    {
                        Link = link,
                        SourceNode = src,
                        TargetNode = dst,
                        SourceY0 = s0,
                        SourceY1 = s1,
                        TargetY0 = t0,
                        TargetY1 = t1
                    });

                    srcOffsets[src.Node.Name] = curSrcOff + srcH;
                    dstOffsets[dst.Node.Name] = curDstOff + dstH;
                }
            }

            return new SankeyLayoutResult(nodeMap.Values.ToList(), ribbons, colCount);
        }
    }
}
