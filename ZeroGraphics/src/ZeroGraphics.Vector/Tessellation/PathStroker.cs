using System;
using System.Collections.Generic;
using ZeroGraphics.Vector.Geometry;

namespace ZeroGraphics.Vector.Tessellation
{
    /// <summary>
    /// GPU Stroke Expansion Engine.
    /// Expands 1D vector paths into 2D triangle meshes based on stroke width, line caps, and corner joins.
    /// </summary>
    public static class PathStroker
    {
        /// <summary>
        /// Strokes a Path2D, expanding all subpaths into triangulated geometry in the specified VectorMesh.
        /// </summary>
        public static void StrokePath(
            Path2D path,
            StrokeStyle stroke,
            uint color,
            VectorMesh destinationMesh,
            float tolerance = 0.5f)
        {
            if (path == null || destinationMesh == null) return;

            var subpaths = AdaptiveFlattening.Flatten(path, tolerance);
            float halfWidth = stroke.Width * 0.5f;

            for (int i = 0; i < subpaths.Count; i++)
            {
                var points = subpaths[i];
                if (points.Count < 2) continue;

                StrokePolyline(points, stroke, halfWidth, color, destinationMesh);
            }
        }

        private static void StrokePolyline(
            List<VectorPoint> rawPoints,
            StrokeStyle stroke,
            float halfWidth,
            uint color,
            VectorMesh mesh)
        {
            var points = new List<VectorPoint>(rawPoints.Count);
            for (int i = 0; i < rawPoints.Count; i++)
            {
                if (points.Count == 0 || (rawPoints[i] - points[points.Count - 1]).LengthSquared >= 1e-6f)
                {
                    points.Add(rawPoints[i]);
                }
            }
            if (points.Count < 2) return;

            int n = points.Count;
            bool isClosed = (points[n - 1] - points[0]).LengthSquared < 1e-4f;

            // Start cap
            VectorPoint d0 = (points[1] - points[0]).Normalized();
            VectorPoint n0 = new VectorPoint(-d0.Y, d0.X);

            if (!isClosed)
            {
                ApplyStartCap(points[0], d0, n0, stroke.Cap, halfWidth, color, mesh);
            }

            uint prevLeft = mesh.AddVertex(points[0].X + n0.X * halfWidth, points[0].Y + n0.Y * halfWidth, color);
            uint prevRight = mesh.AddVertex(points[0].X - n0.X * halfWidth, points[0].Y - n0.Y * halfWidth, color);

            for (int i = 1; i < n; i++)
            {
                VectorPoint pPrev = points[i - 1];
                VectorPoint pCurr = points[i];

                VectorPoint d = (pCurr - pPrev).Normalized();
                VectorPoint norm = new VectorPoint(-d.Y, d.X);

                uint currLeft = mesh.AddVertex(pCurr.X + norm.X * halfWidth, pCurr.Y + norm.Y * halfWidth, color);
                uint currRight = mesh.AddVertex(pCurr.X - norm.X * halfWidth, pCurr.Y - norm.Y * halfWidth, color);

                // Segment quad
                mesh.AddQuad(prevLeft, prevRight, currRight, currLeft);

                // Join with next segment if not at end
                if (i < n - 1)
                {
                    VectorPoint pNext = points[i + 1];
                    VectorPoint dNext = (pNext - pCurr).Normalized();
                    VectorPoint normNext = new VectorPoint(-dNext.Y, dNext.X);

                    uint nextLeft = mesh.AddVertex(pCurr.X + normNext.X * halfWidth, pCurr.Y + normNext.Y * halfWidth, color);
                    uint nextRight = mesh.AddVertex(pCurr.X - normNext.X * halfWidth, pCurr.Y - normNext.Y * halfWidth, color);

                    ApplyJoin(pCurr, d, norm, dNext, normNext, stroke.Join, stroke.MiterLimit, halfWidth, color, mesh, currLeft, currRight, nextLeft, nextRight);

                    prevLeft = nextLeft;
                    prevRight = nextRight;
                }
                else
                {
                    prevLeft = currLeft;
                    prevRight = currRight;
                }
            }

            // End cap
            if (!isClosed)
            {
                VectorPoint dLast = (points[n - 1] - points[n - 2]).Normalized();
                VectorPoint nLast = new VectorPoint(-dLast.Y, dLast.X);
                ApplyEndCap(points[n - 1], dLast, nLast, stroke.Cap, halfWidth, color, mesh);
            }
        }

        private static void ApplyJoin(
            VectorPoint p,
            VectorPoint d1, VectorPoint n1,
            VectorPoint d2, VectorPoint n2,
            LineJoin join, float miterLimit, float halfWidth, uint color,
            VectorMesh mesh,
            uint currLeft, uint currRight, uint nextLeft, uint nextRight)
        {
            float cross = d1.X * d2.Y - d1.Y * d2.X;
            if (Math.Abs(cross) < 1e-4f) return; // Collinear

            uint center = mesh.AddVertex(p.X, p.Y, color);

            if (cross > 0)
            {
                // Left turn: join on right side
                if (join == LineJoin.Bevel)
                {
                    mesh.AddTriangle(currRight, center, nextRight);
                }
                else if (join == LineJoin.Miter)
                {
                    VectorPoint miterDir = (n1 + n2).Normalized();
                    float dot = VectorPoint.Dot(miterDir, n1);
                    float miterLength = dot > 1e-4f ? halfWidth / dot : halfWidth;

                    if (miterLength <= miterLimit * halfWidth)
                    {
                        uint miterVertex = mesh.AddVertex(p.X - miterDir.X * miterLength, p.Y - miterDir.Y * miterLength, color);
                        mesh.AddTriangle(currRight, center, miterVertex);
                        mesh.AddTriangle(miterVertex, center, nextRight);
                    }
                    else
                    {
                        mesh.AddTriangle(currRight, center, nextRight);
                    }
                }
                else // Round
                {
                    mesh.AddTriangle(currRight, center, nextRight);
                }
            }
            else
            {
                // Right turn: join on left side
                if (join == LineJoin.Bevel)
                {
                    mesh.AddTriangle(currLeft, nextLeft, center);
                }
                else if (join == LineJoin.Miter)
                {
                    VectorPoint miterDir = (n1 + n2).Normalized();
                    float dot = VectorPoint.Dot(miterDir, n1);
                    float miterLength = dot > 1e-4f ? halfWidth / dot : halfWidth;

                    if (miterLength <= miterLimit * halfWidth)
                    {
                        uint miterVertex = mesh.AddVertex(p.X + miterDir.X * miterLength, p.Y + miterDir.Y * miterLength, color);
                        mesh.AddTriangle(currLeft, miterVertex, center);
                        mesh.AddTriangle(miterVertex, nextLeft, center);
                    }
                    else
                    {
                        mesh.AddTriangle(currLeft, nextLeft, center);
                    }
                }
                else // Round
                {
                    mesh.AddTriangle(currLeft, nextLeft, center);
                }
            }
        }

        private static void ApplyStartCap(
            VectorPoint p, VectorPoint dir, VectorPoint norm,
            LineCap cap, float halfWidth, uint color, VectorMesh mesh)
        {
            if (cap == LineCap.Square)
            {
                VectorPoint pExt = p - dir * halfWidth;
                uint i0 = mesh.AddVertex(pExt.X + norm.X * halfWidth, pExt.Y + norm.Y * halfWidth, color);
                uint i1 = mesh.AddVertex(pExt.X - norm.X * halfWidth, pExt.Y - norm.Y * halfWidth, color);
                uint i2 = mesh.AddVertex(p.X - norm.X * halfWidth, p.Y - norm.Y * halfWidth, color);
                uint i3 = mesh.AddVertex(p.X + norm.X * halfWidth, p.Y + norm.Y * halfWidth, color);
                mesh.AddQuad(i0, i1, i2, i3);
            }
            else if (cap == LineCap.Round)
            {
                uint center = mesh.AddVertex(p.X, p.Y, color);
                int steps = 6;
                uint prev = mesh.AddVertex(p.X - norm.X * halfWidth, p.Y - norm.Y * halfWidth, color);

                for (int i = 1; i <= steps; i++)
                {
                    double angle = -Math.PI / 2.0 + (Math.PI * i / steps);
                    float cx = (float)(-norm.X * Math.Cos(angle) - dir.X * Math.Sin(angle)) * halfWidth;
                    float cy = (float)(-norm.Y * Math.Cos(angle) - dir.Y * Math.Sin(angle)) * halfWidth;
                    uint curr = mesh.AddVertex(p.X + cx, p.Y + cy, color);
                    mesh.AddTriangle(center, prev, curr);
                    prev = curr;
                }
            }
        }

        private static void ApplyEndCap(
            VectorPoint p, VectorPoint dir, VectorPoint norm,
            LineCap cap, float halfWidth, uint color, VectorMesh mesh)
        {
            if (cap == LineCap.Square)
            {
                VectorPoint pExt = p + dir * halfWidth;
                uint i0 = mesh.AddVertex(p.X + norm.X * halfWidth, p.Y + norm.Y * halfWidth, color);
                uint i1 = mesh.AddVertex(p.X - norm.X * halfWidth, p.Y - norm.Y * halfWidth, color);
                uint i2 = mesh.AddVertex(pExt.X - norm.X * halfWidth, pExt.Y - norm.Y * halfWidth, color);
                uint i3 = mesh.AddVertex(pExt.X + norm.X * halfWidth, pExt.Y + norm.Y * halfWidth, color);
                mesh.AddQuad(i0, i1, i2, i3);
            }
            else if (cap == LineCap.Round)
            {
                uint center = mesh.AddVertex(p.X, p.Y, color);
                int steps = 6;
                uint prev = mesh.AddVertex(p.X + norm.X * halfWidth, p.Y + norm.Y * halfWidth, color);

                for (int i = 1; i <= steps; i++)
                {
                    double angle = Math.PI * i / steps;
                    float cx = (float)(norm.X * Math.Cos(angle) + dir.X * Math.Sin(angle)) * halfWidth;
                    float cy = (float)(norm.Y * Math.Cos(angle) + dir.Y * Math.Sin(angle)) * halfWidth;
                    uint curr = mesh.AddVertex(p.X + cx, p.Y + cy, color);
                    mesh.AddTriangle(center, prev, curr);
                    prev = curr;
                }
            }
        }
    }
}
