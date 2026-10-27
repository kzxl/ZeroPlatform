using System;
using System.Collections.Generic;
using ZeroGeometry.Core.Spatial;

namespace ZeroGeometry.Core.Polygons
{
    /// <summary>
    /// Represents a 2D closed planar polygon with geometric properties and point classification.
    /// </summary>
    public sealed class Polygon2D
    {
        private readonly List<Point2D> _vertices;

        public IReadOnlyList<Point2D> Vertices => _vertices;
        public int VertexCount => _vertices.Count;

        public Polygon2D(IEnumerable<Point2D> vertices)
        {
            if (vertices == null) throw new ArgumentNullException(nameof(vertices));
            _vertices = new List<Point2D>(vertices);
        }

        public Point2D this[int index] => _vertices[index];

        /// <summary>
        /// Calculates signed area via the Shoelace formula.
        /// Positive for Counter-Clockwise (CCW), negative for Clockwise (CW).
        /// </summary>
        public double SignedArea()
        {
            int n = _vertices.Count;
            if (n < 3) return 0.0;

            double area = 0.0;
            for (int i = 0; i < n; i++)
            {
                var p1 = _vertices[i];
                var p2 = _vertices[(i + 1) % n];
                area += (p1.X * p2.Y - p2.X * p1.Y);
            }
            return area * 0.5;
        }

        public double Area() => Math.Abs(SignedArea());

        public bool IsClockwise() => SignedArea() < 0.0;

        public double Perimeter()
        {
            int n = _vertices.Count;
            if (n < 2) return 0.0;

            double perim = 0.0;
            for (int i = 0; i < n; i++)
            {
                perim += _vertices[i].DistanceTo(_vertices[(i + 1) % n]);
            }
            return perim;
        }

        public Point2D Centroid()
        {
            int n = _vertices.Count;
            if (n < 3) return n > 0 ? _vertices[0] : default;

            double cx = 0.0, cy = 0.0;
            double signedArea = SignedArea();
            if (Math.Abs(signedArea) < 1e-12) return _vertices[0];

            for (int i = 0; i < n; i++)
            {
                var p1 = _vertices[i];
                var p2 = _vertices[(i + 1) % n];
                double cross = (p1.X * p2.Y - p2.X * p1.Y);
                cx += (p1.X + p2.X) * cross;
                cy += (p1.Y + p2.Y) * cross;
            }

            double factor = 1.0 / (6.0 * signedArea);
            return new Point2D(cx * factor, cy * factor);
        }

        /// <summary>
        /// Tests whether a point lies strictly inside the polygon using ray-casting algorithm.
        /// </summary>
        public bool ContainsPoint(Point2D pt)
        {
            int n = _vertices.Count;
            if (n < 3) return false;

            bool inside = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                var pi = _vertices[i];
                var pj = _vertices[j];

                if (((pi.Y > pt.Y) != (pj.Y > pt.Y)) &&
                    (pt.X < (pj.X - pi.X) * (pt.Y - pi.Y) / (pj.Y - pi.Y + 1e-15) + pi.X))
                {
                    inside = !inside;
                }
            }

            return inside;
        }
    }

    /// <summary>
    /// Boolean polygon clipping algorithms (Sutherland-Hodgman & AABB window clipping).
    /// Essential for AOI inspection zones, ROI masking, and robot boundary keep-out zones.
    /// </summary>
    public static class PolygonClipper
    {
        /// <summary>
        /// Computes the intersection of a subject polygon with a convex clip polygon via Sutherland-Hodgman.
        /// </summary>
        public static Polygon2D Intersect(Polygon2D subject, Polygon2D convexClip)
        {
            if (subject == null) throw new ArgumentNullException(nameof(subject));
            if (convexClip == null) throw new ArgumentNullException(nameof(convexClip));
            if (subject.VertexCount < 3 || convexClip.VertexCount < 3)
                return new Polygon2D(Array.Empty<Point2D>());

            var outputList = new List<Point2D>(subject.Vertices);
            int clipCount = convexClip.VertexCount;

            for (int c = 0; c < clipCount; c++)
            {
                var cp1 = convexClip[c];
                var cp2 = convexClip[(c + 1) % clipCount];

                var inputList = outputList;
                outputList = new List<Point2D>();

                if (inputList.Count == 0) break;

                var s = inputList[inputList.Count - 1];

                for (int i = 0; i < inputList.Count; i++)
                {
                    var p = inputList[i];

                    if (IsInside(p, cp1, cp2))
                    {
                        if (IsInside(s, cp1, cp2))
                        {
                            outputList.Add(p);
                        }
                        else
                        {
                            outputList.Add(Intersection(s, p, cp1, cp2));
                            outputList.Add(p);
                        }
                    }
                    else if (IsInside(s, cp1, cp2))
                    {
                        outputList.Add(Intersection(s, p, cp1, cp2));
                    }

                    s = p;
                }
            }

            return new Polygon2D(outputList);
        }

        private static bool IsInside(Point2D p, Point2D cp1, Point2D cp2)
        {
            // Point is inside (to the left of edge cp1 -> cp2)
            return (cp2.X - cp1.X) * (p.Y - cp1.Y) - (cp2.Y - cp1.Y) * (p.X - cp1.X) >= -1e-12;
        }

        private static Point2D Intersection(Point2D s, Point2D p, Point2D cp1, Point2D cp2)
        {
            double a1 = p.Y - s.Y;
            double b1 = s.X - p.X;
            double c1 = a1 * s.X + b1 * s.Y;

            double a2 = cp2.Y - cp1.Y;
            double b2 = cp1.X - cp2.X;
            double c2 = a2 * cp1.X + b2 * cp1.Y;

            double det = a1 * b2 - a2 * b1;
            if (Math.Abs(det) < 1e-14) return s;

            double x = (b2 * c1 - b1 * c2) / det;
            double y = (a1 * c2 - a2 * c1) / det;
            return new Point2D(x, y);
        }
    }

    /// <summary>
    /// Polygon offsetting (dilation and erosion / buffering) for tolerance search bands.
    /// </summary>
    public static class PolygonOffsetter
    {
        /// <summary>
        /// Offsets polygon outward (delta > 0) or inward (delta < 0) with miter corner limits.
        /// </summary>
        public static Polygon2D Offset(Polygon2D polygon, double delta, double miterLimit = 2.0)
        {
            if (polygon == null) throw new ArgumentNullException(nameof(polygon));
            int n = polygon.VertexCount;
            if (n < 3 || Math.Abs(delta) < 1e-12) return polygon;

            // Ensure CCW orientation
            var verts = new List<Point2D>(polygon.Vertices);
            if (polygon.IsClockwise())
            {
                verts.Reverse();
            }

            var offsetLines = new (Point2D p1, Point2D p2)[n];

            // Offset each edge outward by normal * delta
            for (int i = 0; i < n; i++)
            {
                var p1 = verts[i];
                var p2 = verts[(i + 1) % n];

                double dx = p2.X - p1.X;
                double dy = p2.Y - p1.Y;
                double len = Math.Sqrt(dx * dx + dy * dy);
                if (len < 1e-12) len = 1e-12;

                // Outward normal for CCW edge (dy, -dx)
                double nx = dy / len;
                double ny = -dx / len;

                offsetLines[i] = (
                    new Point2D(p1.X + nx * delta, p1.Y + ny * delta),
                    new Point2D(p2.X + nx * delta, p2.Y + ny * delta));
            }

            // Intersect adjacent offset edges
            var resultVerts = new List<Point2D>(n);
            for (int i = 0; i < n; i++)
            {
                int prev = (i - 1 + n) % n;
                var line1 = offsetLines[prev];
                var line2 = offsetLines[i];

                var pt = IntersectLines(line1.p1, line1.p2, line2.p1, line2.p2);
                resultVerts.Add(pt);
            }

            return new Polygon2D(resultVerts);
        }

        private static Point2D IntersectLines(Point2D p1, Point2D p2, Point2D p3, Point2D p4)
        {
            double a1 = p2.Y - p1.Y;
            double b1 = p1.X - p2.X;
            double c1 = a1 * p1.X + b1 * p1.Y;

            double a2 = p4.Y - p3.Y;
            double b2 = p3.X - p4.X;
            double c2 = a2 * p3.X + b2 * p3.Y;

            double det = a1 * b2 - a2 * b1;
            if (Math.Abs(det) < 1e-14) return p2;

            double x = (b2 * c1 - b1 * c2) / det;
            double y = (a1 * c2 - a2 * c1) / det;
            return new Point2D(x, y);
        }
    }
}
