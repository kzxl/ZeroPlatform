using System;
using System.Collections.Generic;
using Xunit;
using ZeroGeometry.Core.PointCloud;
using ZeroGeometry.Core.Polygons;
using ZeroGeometry.Core.Spatial;

namespace ZeroGeometry.Tests
{
    public class AdvancedGeometryTests
    {
        [Fact]
        public void TestNormalEstimator_PlanarSurface()
        {
            var points = new List<Point3D>();
            // 10x10 planar grid on Z = 2.0
            for (int x = 0; x < 10; x++)
            {
                for (int y = 0; y < 10; y++)
                {
                    points.Add(new Point3D(x, y, 2.0));
                }
            }

            // Viewpoint above the plane
            var viewpoint = new Point3D(5, 5, 10);
            var normals = NormalEstimator3D.EstimateNormals(points, k: 8, viewpoint: viewpoint);

            Assert.Equal(points.Count, normals.Count);

            // Check interior points
            for (int i = 0; i < normals.Count; i++)
            {
                var n = normals[i];
                Assert.True(Math.Abs(n.Normal.X) < 1e-4);
                Assert.True(Math.Abs(n.Normal.Y) < 1e-4);
                Assert.True(n.Normal.Z > 0.99); // Oriented towards viewpoint (0, 0, 1)
                Assert.True(n.Curvature < 1e-4); // Flat plane has ~0 curvature
            }
        }

        [Fact]
        public void TestNormalEstimator_SphericalSurface()
        {
            var points = new List<Point3D>();
            double radius = 5.0;

            // Generate sphere surface points
            for (double theta = 0.2; theta < Math.PI - 0.2; theta += 0.3)
            {
                for (double phi = 0; phi < 2 * Math.PI; phi += 0.3)
                {
                    double x = radius * Math.Sin(theta) * Math.Cos(phi);
                    double y = radius * Math.Sin(theta) * Math.Sin(phi);
                    double z = radius * Math.Cos(theta);
                    points.Add(new Point3D(x, y, z));
                }
            }

            // Viewpoint at center (0, 0, 0)
            var normals = NormalEstimator3D.EstimateNormals(points, k: 12, viewpoint: new Point3D(0, 0, 0));
            Assert.Equal(points.Count, normals.Count);

            // For each point, normal oriented towards center should have negative dot product with point position
            for (int i = 0; i < normals.Count; i++)
            {
                var pn = normals[i];
                double dot = pn.Normal.X * pn.Point.X + pn.Normal.Y * pn.Point.Y + pn.Normal.Z * pn.Point.Z;
                Assert.True(dot < 0); // Pointing towards center (0,0,0)
                Assert.True(pn.Curvature > 0); // Non-zero curvature on curved surface
            }
        }

        [Fact]
        public void TestDelaunayTriangulation_Square()
        {
            var points = new Point2D[]
            {
                new Point2D(0, 0),
                new Point2D(10, 0),
                new Point2D(10, 10),
                new Point2D(0, 10)
            };

            var triangles = DelaunayTriangulator2D.Triangulate(points);
            Assert.Equal(2, triangles.Count);

            var voronoi = DelaunayTriangulator2D.ComputeVoronoiVertices(triangles);
            Assert.Equal(2, voronoi.Count);
        }

        [Fact]
        public void TestDelaunayTriangulation_CircumcircleProperty()
        {
            var points = new List<Point2D>();
            for (int x = 0; x < 5; x++)
            {
                for (int y = 0; y < 5; y++)
                {
                    points.Add(new Point2D(x * 2.0, y * 2.0));
                }
            }

            var triangles = DelaunayTriangulator2D.Triangulate(points);
            Assert.NotEmpty(triangles);

            // Verify Delaunay property: circumcircle of any triangle contains no other points strictly inside
            for (int t = 0; t < triangles.Count; t++)
            {
                var tri = triangles[t];
                for (int p = 0; p < points.Count; p++)
                {
                    var pt = points[p];
                    if (tri.HasVertex(pt)) continue;

                    double dx = pt.X - tri.Circumcenter.X;
                    double dy = pt.Y - tri.Circumcenter.Y;
                    double distSq = dx * dx + dy * dy;

                    // Point cannot be strictly inside circumcircle (outside or on boundary within numerical margin)
                    Assert.True(distSq >= tri.CircumradiusSquared - 1e-4);
                }
            }
        }
    }
}
