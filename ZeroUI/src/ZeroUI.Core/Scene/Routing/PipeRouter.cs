using System;
using System.Collections.Generic;

namespace ZeroUI.Core.Scene.Routing
{
    public enum PortDirection
    {
        Auto,
        Right,
        Left,
        Top,
        Bottom
    }

    /// <summary>
    /// Industrial Manhattan (Orthogonal) pipe routing algorithm for P&ID mimic diagrams.
    /// Calculates optimal orthogonal waypoints between equipment ports, ensuring clean 90-degree elbows
    /// with port takeoff stubs and collinear segment reduction.
    /// </summary>
    public static class PipeRouter
    {
        private const float DefaultStubLength = 20f;

        /// <summary>
        /// Calculates an orthogonal path consisting of 90-degree pipe bends between source and target points.
        /// </summary>
        public static List<ScenePoint> RouteManhattan(
            ScenePoint source,
            ScenePoint target,
            PortDirection sourceDir = PortDirection.Auto,
            PortDirection targetDir = PortDirection.Auto,
            float stubLength = DefaultStubLength)
        {
            var rawPoints = new List<ScenePoint> { source };

            // Determine effective port directions
            if (sourceDir == PortDirection.Auto)
            {
                sourceDir = Math.Abs(target.X - source.X) >= Math.Abs(target.Y - source.Y)
                    ? (target.X >= source.X ? PortDirection.Right : PortDirection.Left)
                    : (target.Y >= source.Y ? PortDirection.Bottom : PortDirection.Top);
            }

            if (targetDir == PortDirection.Auto)
            {
                targetDir = Math.Abs(target.X - source.X) >= Math.Abs(target.Y - source.Y)
                    ? (target.X >= source.X ? PortDirection.Left : PortDirection.Right)
                    : (target.Y >= source.Y ? PortDirection.Top : PortDirection.Bottom);
            }

            // Calculate initial stubs extending away from ports
            ScenePoint srcStub = GetOffsetPoint(source, sourceDir, stubLength);
            ScenePoint tgtStub = GetOffsetPoint(target, targetDir, stubLength);

            rawPoints.Add(srcStub);

            // Connect srcStub to tgtStub orthogonally
            bool srcHorizontal = sourceDir == PortDirection.Left || sourceDir == PortDirection.Right;
            bool tgtHorizontal = targetDir == PortDirection.Left || targetDir == PortDirection.Right;

            if (srcHorizontal && tgtHorizontal)
            {
                // Both ports are horizontal
                if ((sourceDir == PortDirection.Right && srcStub.X < tgtStub.X) ||
                    (sourceDir == PortDirection.Left && srcStub.X > tgtStub.X))
                {
                    // Standard Z-shape: bend in the middle
                    float midX = (srcStub.X + tgtStub.X) * 0.5f;
                    rawPoints.Add(new ScenePoint(midX, srcStub.Y));
                    rawPoints.Add(new ScenePoint(midX, tgtStub.Y));
                }
                else
                {
                    // Loop-around C-shape
                    float detourY = Math.Min(srcStub.Y, tgtStub.Y) - stubLength * 1.5f;
                    rawPoints.Add(new ScenePoint(srcStub.X, detourY));
                    rawPoints.Add(new ScenePoint(tgtStub.X, detourY));
                }
            }
            else if (!srcHorizontal && !tgtHorizontal)
            {
                // Both ports are vertical
                if ((sourceDir == PortDirection.Bottom && srcStub.Y < tgtStub.Y) ||
                    (sourceDir == PortDirection.Top && srcStub.Y > tgtStub.Y))
                {
                    // Standard Z-shape: bend vertically in the middle
                    float midY = (srcStub.Y + tgtStub.Y) * 0.5f;
                    rawPoints.Add(new ScenePoint(srcStub.X, midY));
                    rawPoints.Add(new ScenePoint(tgtStub.X, midY));
                }
                else
                {
                    // Loop-around C-shape
                    float detourX = Math.Max(srcStub.X, tgtStub.X) + stubLength * 1.5f;
                    rawPoints.Add(new ScenePoint(detourX, srcStub.Y));
                    rawPoints.Add(new ScenePoint(detourX, tgtStub.Y));
                }
            }
            else
            {
                // One horizontal, one vertical -> L-shape transition
                if (srcHorizontal)
                {
                    rawPoints.Add(new ScenePoint(tgtStub.X, srcStub.Y));
                }
                else
                {
                    rawPoints.Add(new ScenePoint(srcStub.X, tgtStub.Y));
                }
            }

            rawPoints.Add(tgtStub);
            rawPoints.Add(target);

            // Clean up and optimize: remove redundant collinear points
            return SimplifyOrthogonalPath(rawPoints);
        }

        private static ScenePoint GetOffsetPoint(ScenePoint pt, PortDirection dir, float distance)
        {
            return dir switch
            {
                PortDirection.Right => new ScenePoint(pt.X + distance, pt.Y),
                PortDirection.Left => new ScenePoint(pt.X - distance, pt.Y),
                PortDirection.Bottom => new ScenePoint(pt.X, pt.Y + distance),
                PortDirection.Top => new ScenePoint(pt.X, pt.Y - distance),
                _ => pt
            };
        }

        /// <summary>
        /// Collapses consecutive collinear segments into single direct lines.
        /// </summary>
        public static List<ScenePoint> SimplifyOrthogonalPath(List<ScenePoint> points)
        {
            if (points == null || points.Count <= 2) return points ?? new List<ScenePoint>();

            var result = new List<ScenePoint> { points[0] };

            for (int i = 1; i < points.Count - 1; i++)
            {
                var prev = result[result.Count - 1];
                var curr = points[i];
                var next = points[i + 1];

                // If identical to previous, skip
                if (prev == curr) continue;

                // Check collinearity
                bool horizontal = Math.Abs(prev.Y - curr.Y) < 1e-4f && Math.Abs(curr.Y - next.Y) < 1e-4f;
                bool vertical = Math.Abs(prev.X - curr.X) < 1e-4f && Math.Abs(curr.X - next.X) < 1e-4f;

                if (!horizontal && !vertical)
                {
                    result.Add(curr);
                }
            }

            var last = points[points.Count - 1];
            if (result[result.Count - 1] != last)
            {
                result.Add(last);
            }

            return result;
        }
    }
}
