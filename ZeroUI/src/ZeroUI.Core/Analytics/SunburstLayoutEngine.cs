using System;
using System.Collections.Generic;
using System.Linq;

namespace ZeroUI.Core.Analytics
{
    /// <summary>
    /// Hierarchical node item representing a branch or leaf in a Sunburst partition diagram.
    /// </summary>
    public class SunburstNode
    {
        public string Name { get; set; } = string.Empty;
        public double Value { get; set; }
        public List<SunburstNode> Children { get; set; } = new List<SunburstNode>();
        public uint? ColorRgba { get; set; }
        public object? Tag { get; set; }

        public SunburstNode() { }

        public SunburstNode(string name, double value = 0.0, uint? colorRgba = null)
        {
            Name = name ?? string.Empty;
            Value = value;
            ColorRgba = colorRgba;
        }

        /// <summary>
        /// Gets the effective aggregated value across this node and its recursive subtrees.
        /// </summary>
        public double GetEffectiveValue()
        {
            if (Children != null && Children.Count > 0)
            {
                double sum = 0.0;
                foreach (var child in Children)
                {
                    if (child != null) sum += child.GetEffectiveValue();
                }
                return sum > 0 ? sum : Value;
            }
            return Math.Max(0.0, Value);
        }
    }

    /// <summary>
    /// Geometric radial sector computed by SunburstLayoutEngine ready for direct canvas rendering.
    /// </summary>
    public class SunburstSector
    {
        public SunburstNode Node { get; }
        public int Depth { get; }
        public double StartAngle { get; } // in degrees [0, 360)
        public double SweepAngle { get; } // in degrees
        public double InnerRadius { get; }
        public double OuterRadius { get; }

        public double EndAngle => StartAngle + SweepAngle;
        public double MidAngle => StartAngle + (SweepAngle / 2.0);
        public double MidRadius => (InnerRadius + OuterRadius) / 2.0;

        public SunburstSector(SunburstNode node, int depth, double startAngle, double sweepAngle, double innerRadius, double outerRadius)
        {
            Node = node;
            Depth = depth;
            StartAngle = startAngle;
            SweepAngle = sweepAngle;
            InnerRadius = innerRadius;
            OuterRadius = outerRadius;
        }

        /// <summary>
        /// Fast polar coordinate hit-test testing if a 2D cartesian point lies inside this radial ring sector.
        /// </summary>
        public bool Contains(double px, double py, double centerX, double centerY)
        {
            double dx = px - centerX;
            double dy = py - centerY;
            double distSq = (dx * dx) + (dy * dy);

            if (distSq < (InnerRadius * InnerRadius) || distSq > (OuterRadius * OuterRadius))
            {
                return false;
            }

            // Math.Atan2 returns angle in radians [-PI, PI] relative to X axis
            double angleRad = Math.Atan2(dy, dx);
            double angleDeg = angleRad * (180.0 / Math.PI);
            if (angleDeg < 0) angleDeg += 360.0;

            double start = StartAngle % 360.0;
            if (start < 0) start += 360.0;
            double end = start + SweepAngle;

            if (end <= 360.0)
            {
                return angleDeg >= start && angleDeg <= end;
            }
            else
            {
                // Wraps around 0/360 degrees boundary
                return angleDeg >= start || angleDeg <= (end - 360.0);
            }
        }
    }

    /// <summary>
    /// Core layout engine computing multi-tier radial partitions (angles and radii) for Sunburst diagrams.
    /// </summary>
    public static class SunburstLayoutEngine
    {
        /// <summary>
        /// Computes the complete hierarchical radial layout for all visible levels of the Sunburst chart.
        /// </summary>
        /// <param name="root">The root hierarchical node.</param>
        /// <param name="maxRadius">Maximum outer radius of the outermost ring.</param>
        /// <param name="innerHoleRatio">Ratio [0.0 - 0.8] of the center donut hole compared to maxRadius.</param>
        /// <param name="startAngleOffset">Initial angular orientation in degrees (default -90 for 12 o'clock top start).</param>
        public static IReadOnlyList<SunburstSector> ComputeLayout(
            SunburstNode root,
            double maxRadius,
            double innerHoleRatio = 0.25,
            double startAngleOffset = -90.0)
        {
            if (root == null || maxRadius <= 0)
            {
                return Array.Empty<SunburstSector>();
            }

            int maxDepth = GetMaxDepth(root);
            if (maxDepth <= 0)
            {
                return Array.Empty<SunburstSector>();
            }

            double holeRadius = maxRadius * Math.Max(0.05, Math.Min(0.75, innerHoleRatio));
            double availableRadialSpan = maxRadius - holeRadius;
            double ringThickness = availableRadialSpan / maxDepth;

            var sectors = new List<SunburstSector>();
            double totalValue = root.GetEffectiveValue();
            if (totalValue <= 1e-9)
            {
                return Array.Empty<SunburstSector>();
            }

            // Standardize start angle offset into [0, 360)
            double baseStart = startAngleOffset % 360.0;
            if (baseStart < 0) baseStart += 360.0;

            // Compute children partitions recursively
            if (root.Children != null && root.Children.Count > 0)
            {
                TraverseAndPartition(root.Children, 1, baseStart, 360.0, holeRadius, ringThickness, sectors);
            }
            else
            {
                // Single root sector covering full 360 degrees
                sectors.Add(new SunburstSector(root, 1, baseStart, 360.0, holeRadius, maxRadius));
            }

            return sectors;
        }

        private static void TraverseAndPartition(
            IReadOnlyList<SunburstNode> nodes,
            int depth,
            double startAngle,
            double totalSweepAngle,
            double holeRadius,
            double ringThickness,
            List<SunburstSector> result)
        {
            if (nodes == null || nodes.Count == 0 || totalSweepAngle <= 0.01) return;

            double parentTotal = 0.0;
            foreach (var n in nodes)
            {
                if (n != null) parentTotal += n.GetEffectiveValue();
            }

            if (parentTotal <= 1e-9) return;

            double currentAngle = startAngle;
            double innerR = holeRadius + (depth - 1) * ringThickness;
            double outerR = innerR + ringThickness;

            foreach (var node in nodes)
            {
                if (node == null) continue;
                double nodeVal = node.GetEffectiveValue();
                if (nodeVal <= 0) continue;

                double sweep = (nodeVal / parentTotal) * totalSweepAngle;
                result.Add(new SunburstSector(node, depth, currentAngle, sweep, innerR, outerR));

                if (node.Children != null && node.Children.Count > 0)
                {
                    TraverseAndPartition(node.Children, depth + 1, currentAngle, sweep, holeRadius, ringThickness, result);
                }

                currentAngle += sweep;
            }
        }

        private static int GetMaxDepth(SunburstNode node)
        {
            if (node == null) return 0;
            if (node.Children == null || node.Children.Count == 0) return 1;

            int maxChildDepth = 0;
            foreach (var c in node.Children)
            {
                int d = GetMaxDepth(c);
                if (d > maxChildDepth) maxChildDepth = d;
            }

            return 1 + maxChildDepth;
        }
    }
}
