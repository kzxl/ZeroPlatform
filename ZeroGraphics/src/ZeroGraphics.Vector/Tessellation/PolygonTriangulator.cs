using System;
using System.Collections.Generic;
using ZeroGraphics.Vector.Geometry;

namespace ZeroGraphics.Vector.Tessellation
{
    /// <summary>
    /// Robust Ear-Clipping polygon triangulation engine.
    /// Decomposes arbitrary convex and concave 2D polygons into index buffers for GPU rasterization.
    /// </summary>
    public static class PolygonTriangulator
    {
        /// <summary>
        /// Triangulates a closed polygon boundary into the destination VectorMesh.
        /// </summary>
        public static void TriangulatePolygon(
            IReadOnlyList<VectorPoint> polygon,
            uint color,
            VectorMesh destinationMesh)
        {
            if (polygon == null || polygon.Count < 3 || destinationMesh == null) return;

            int n = polygon.Count;
            // Deduplicate closed end if present
            if ((polygon[n - 1] - polygon[0]).LengthSquared < 1e-4f)
            {
                n--;
            }
            if (n < 3) return;

            // Ensure non-zero area (exit early if collinear or degenerate)
            float area = ComputeSignedArea(polygon, n);
            if (Math.Abs(area) < 1e-5f) return; // Collinear or degenerate polygon

            // Base index offset in mesh
            uint baseVertex = (uint)destinationMesh.VertexCount;

            // Add all polygon vertices to mesh
            for (int i = 0; i < n; i++)
            {
                destinationMesh.AddVertex(polygon[i].X, polygon[i].Y, color);
            }

            // Quick path: Triangle
            if (n == 3)
            {
                destinationMesh.AddTriangle(baseVertex, baseVertex + 1, baseVertex + 2);
                return;
            }

            // Quick path: Convex polygon (fan triangulation)
            if (IsConvex(polygon, n))
            {
                for (uint i = 1; i < n - 1; i++)
                {
                    destinationMesh.AddTriangle(baseVertex, baseVertex + i, baseVertex + i + 1);
                }
                return;
            }

            // General concave path: Ear-clipping algorithm
            var indices = new List<int>(n);
            for (int i = 0; i < n; i++) indices.Add(i);

            // Ensure clockwise/counter-clockwise orientation
            if (area < 0)
            {
                indices.Reverse();
            }

            int count = n;
            int timeout = 3 * count;

            while (count > 2 && timeout-- > 0)
            {
                bool earFound = false;
                for (int i = 0; i < count; i++)
                {
                    int prevIdx = indices[(i - 1 + count) % count];
                    int currIdx = indices[i];
                    int nextIdx = indices[(i + 1) % count];

                    VectorPoint a = polygon[prevIdx];
                    VectorPoint b = polygon[currIdx];
                    VectorPoint c = polygon[nextIdx];

                    if (IsConvexCorner(a, b, c))
                    {
                        bool hasPointInside = false;
                        for (int j = 0; j < count; j++)
                        {
                            if (j == (i - 1 + count) % count || j == i || j == (i + 1) % count) continue;
                            VectorPoint p = polygon[indices[j]];
                            if (PointInTriangle(p, a, b, c))
                            {
                                hasPointInside = true;
                                break;
                            }
                        }

                        if (!hasPointInside)
                        {
                            destinationMesh.AddTriangle(
                                baseVertex + (uint)prevIdx,
                                baseVertex + (uint)currIdx,
                                baseVertex + (uint)nextIdx);

                            indices.RemoveAt(i);
                            count--;
                            earFound = true;
                            break;
                        }
                    }
                }

                if (!earFound)
                {
                    // Degenerate geometry fallback: Fan triangulate remaining indices
                    for (int i = 1; i < count - 1; i++)
                    {
                        destinationMesh.AddTriangle(
                            baseVertex + (uint)indices[0],
                            baseVertex + (uint)indices[i],
                            baseVertex + (uint)indices[i + 1]);
                    }
                    break;
                }
            }
        }

        private static bool IsConvex(IReadOnlyList<VectorPoint> points, int n)
        {
            bool hasPositive = false;
            bool hasNegative = false;

            for (int i = 0; i < n; i++)
            {
                VectorPoint a = points[i];
                VectorPoint b = points[(i + 1) % n];
                VectorPoint c = points[(i + 2) % n];

                float cross = (b.X - a.X) * (c.Y - b.Y) - (b.Y - a.Y) * (c.X - b.X);
                if (cross > 1e-4f) hasPositive = true;
                else if (cross < -1e-4f) hasNegative = true;

                if (hasPositive && hasNegative) return false;
            }

            return hasPositive || hasNegative;
        }

        private static float ComputeSignedArea(IReadOnlyList<VectorPoint> points, int n)
        {
            float area = 0.0f;
            for (int i = 0; i < n; i++)
            {
                VectorPoint a = points[i];
                VectorPoint b = points[(i + 1) % n];
                area += a.X * b.Y - b.X * a.Y;
            }
            return area * 0.5f;
        }

        private static bool IsConvexCorner(VectorPoint a, VectorPoint b, VectorPoint c)
        {
            return (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X) > 0.0f;
        }

        private static bool PointInTriangle(VectorPoint p, VectorPoint a, VectorPoint b, VectorPoint c)
        {
            float cross1 = (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);
            float cross2 = (c.X - b.X) * (p.Y - b.Y) - (c.Y - b.Y) * (p.X - b.X);
            float cross3 = (a.X - c.X) * (p.Y - c.Y) - (a.Y - c.Y) * (p.X - c.X);

            bool hasNeg = (cross1 < 0) || (cross2 < 0) || (cross3 < 0);
            bool hasPos = (cross1 > 0) || (cross2 > 0) || (cross3 > 0);

            return !(hasNeg && hasPos);
        }
    }
}
