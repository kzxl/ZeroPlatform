using System;
using System.Collections.Generic;
using ZeroGeometry.Core.Spatial;

namespace ZeroGeometry.Core.Polygons
{
    /// <summary>
    /// Represents a 2D triangle defined by 3 vertices with precomputed circumcircle geometry.
    /// </summary>
    public readonly struct Triangle2D : IEquatable<Triangle2D>
    {
        public Point2D A { get; }
        public Point2D B { get; }
        public Point2D C { get; }
        public Point2D Circumcenter { get; }
        public double CircumradiusSquared { get; }

        public Triangle2D(Point2D a, Point2D b, Point2D c)
        {
            A = a;
            B = b;
            C = c;

            // Circumcircle calculation
            double d = 2.0 * (a.X * (b.Y - c.Y) + b.X * (c.Y - a.Y) + c.X * (a.Y - b.Y));
            if (Math.Abs(d) < 1e-14)
            {
                // Degenerate/collinear points
                Circumcenter = new Point2D((a.X + b.X + c.X) / 3.0, (a.Y + b.Y + c.Y) / 3.0);
                CircumradiusSquared = double.PositiveInfinity;
            }
            else
            {
                double aSq = a.X * a.X + a.Y * a.Y;
                double bSq = b.X * b.X + b.Y * b.Y;
                double cSq = c.X * c.X + c.Y * c.Y;

                double ux = (aSq * (b.Y - c.Y) + bSq * (c.Y - a.Y) + cSq * (a.Y - b.Y)) / d;
                double uy = (aSq * (c.X - b.X) + bSq * (a.X - c.X) + cSq * (b.X - a.X)) / d;

                Circumcenter = new Point2D(ux, uy);
                double dx = a.X - ux;
                double dy = a.Y - uy;
                CircumradiusSquared = dx * dx + dy * dy;
            }
        }

        public bool ContainsInCircumcircle(Point2D p)
        {
            double dx = p.X - Circumcenter.X;
            double dy = p.Y - Circumcenter.Y;
            double distSq = dx * dx + dy * dy;
            return distSq <= CircumradiusSquared + 1e-12;
        }

        public bool HasVertex(Point2D p) => A.Equals(p) || B.Equals(p) || C.Equals(p);

        public bool SharesAnyVertexWith(Point2D p1, Point2D p2, Point2D p3) =>
            HasVertex(p1) || HasVertex(p2) || HasVertex(p3);

        public bool Equals(Triangle2D other) => A.Equals(other.A) && B.Equals(other.B) && C.Equals(other.C);
        public override bool Equals(object? obj) => obj is Triangle2D other && Equals(other);
        public override int GetHashCode() => unchecked(A.GetHashCode() ^ (B.GetHashCode() * 397) ^ (C.GetHashCode() * 1013));
        public override string ToString() => $"Triangle: {A} -> {B} -> {C}";
    }

    /// <summary>
    /// Represents an undirected edge between two 2D points.
    /// </summary>
    internal readonly struct Edge2D : IEquatable<Edge2D>
    {
        public Point2D P1 { get; }
        public Point2D P2 { get; }

        public Edge2D(Point2D p1, Point2D p2)
        {
            P1 = p1;
            P2 = p2;
        }

        public bool Equals(Edge2D other) =>
            (P1.Equals(other.P1) && P2.Equals(other.P2)) || (P1.Equals(other.P2) && P2.Equals(other.P1));

        public override bool Equals(object? obj) => obj is Edge2D other && Equals(other);
        public override int GetHashCode() => unchecked(P1.GetHashCode() ^ P2.GetHashCode());
    }

    /// <summary>
    /// 2D Delaunay Triangulation and Voronoi diagram generator using the Bowyer-Watson algorithm.
    /// Pure C# with zero external dependencies.
    /// </summary>
    public static class DelaunayTriangulator2D
    {
        /// <summary>
        /// Computes 2D Delaunay triangulation for a set of points.
        /// </summary>
        public static List<Triangle2D> Triangulate(IReadOnlyList<Point2D> points)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            var triangulation = new List<Triangle2D>();
            if (points.Count < 3) return triangulation;

            // 1. Calculate Bounding Box
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;

            for (int i = 0; i < points.Count; i++)
            {
                var p = points[i];
                if (p.X < minX) minX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.X > maxX) maxX = p.X;
                if (p.Y > maxY) maxY = p.Y;
            }

            double dx = maxX - minX;
            double dy = maxY - minY;
            double deltaMax = Math.Max(dx, dy);
            if (deltaMax < 1e-6) deltaMax = 1.0;

            double midX = (minX + maxX) * 0.5;
            double midY = (minY + maxY) * 0.5;

            // 2. Construct Super-Triangle enclosing all points
            Point2D stA = new Point2D(midX - 20.0 * deltaMax, midY - deltaMax);
            Point2D stB = new Point2D(midX, midY + 20.0 * deltaMax);
            Point2D stC = new Point2D(midX + 20.0 * deltaMax, midY - deltaMax);

            triangulation.Add(new Triangle2D(stA, stB, stC));

            // 3. Bowyer-Watson point insertion
            for (int i = 0; i < points.Count; i++)
            {
                var point = points[i];
                var badTriangles = new List<Triangle2D>();

                // Find all triangles whose circumcircle contains the point
                for (int t = 0; t < triangulation.Count; t++)
                {
                    if (triangulation[t].ContainsInCircumcircle(point))
                    {
                        badTriangles.Add(triangulation[t]);
                    }
                }

                // Find polygon boundary (edges that are not shared between bad triangles)
                var polygonEdges = new List<Edge2D>();
                for (int b = 0; b < badTriangles.Count; b++)
                {
                    var tri = badTriangles[b];
                    Edge2D[] edges = new Edge2D[]
                    {
                        new Edge2D(tri.A, tri.B),
                        new Edge2D(tri.B, tri.C),
                        new Edge2D(tri.C, tri.A)
                    };

                    for (int e = 0; e < 3; e++)
                    {
                        var edge = edges[e];
                        bool isShared = false;
                        for (int other = 0; other < badTriangles.Count; other++)
                        {
                            if (b == other) continue;
                            var oTri = badTriangles[other];
                            Edge2D[] oEdges = new Edge2D[]
                            {
                                new Edge2D(oTri.A, oTri.B),
                                new Edge2D(oTri.B, oTri.C),
                                new Edge2D(oTri.C, oTri.A)
                            };

                            if (edge.Equals(oEdges[0]) || edge.Equals(oEdges[1]) || edge.Equals(oEdges[2]))
                            {
                                isShared = true;
                                break;
                            }
                        }

                        if (!isShared)
                        {
                            polygonEdges.Add(edge);
                        }
                    }
                }

                // Remove bad triangles
                for (int b = 0; b < badTriangles.Count; b++)
                {
                    triangulation.Remove(badTriangles[b]);
                }

                // Retriangulate with new point
                for (int e = 0; e < polygonEdges.Count; e++)
                {
                    triangulation.Add(new Triangle2D(polygonEdges[e].P1, polygonEdges[e].P2, point));
                }
            }

            // 4. Remove all triangles that share any vertex with the super-triangle
            var finalTriangles = new List<Triangle2D>(triangulation.Count);
            for (int t = 0; t < triangulation.Count; t++)
            {
                var tri = triangulation[t];
                if (!tri.SharesAnyVertexWith(stA, stB, stC))
                {
                    finalTriangles.Add(tri);
                }
            }

            return finalTriangles;
        }

        /// <summary>
        /// Computes Voronoi dual vertices (circumcenters of Delaunay triangles).
        /// </summary>
        public static List<Point2D> ComputeVoronoiVertices(IEnumerable<Triangle2D> triangles)
        {
            if (triangles == null) throw new ArgumentNullException(nameof(triangles));

            var list = new List<Point2D>();
            foreach (var t in triangles)
            {
                if (!double.IsInfinity(t.CircumradiusSquared))
                {
                    list.Add(t.Circumcenter);
                }
            }
            return list;
        }
    }
}
