using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using ZeroGeometry.Core.Spatial;

namespace ZeroGeometry.Tests
{
    public class KdTreeTests
    {
        [Fact]
        public void KdTree_NearestNeighbor_MatchesBruteForce()
        {
            var points = new List<Point3D>();
            var rng = new Random(42);

            for (int i = 0; i < 200; i++)
            {
                points.Add(new Point3D(rng.NextDouble() * 100.0, rng.NextDouble() * 100.0, rng.NextDouble() * 100.0));
            }

            var tree = new KdTree3D(points);

            // Query 10 random targets
            for (int q = 0; q < 10; q++)
            {
                var query = new Point3D(rng.NextDouble() * 100.0, rng.NextDouble() * 100.0, rng.NextDouble() * 100.0);

                // Brute-force linear scan
                double bruteBestDistSq = double.MaxValue;
                Point3D bruteBestPoint = default;
                for (int i = 0; i < points.Count; i++)
                {
                    double dSq = points[i].DistanceSquaredTo(query);
                    if (dSq < bruteBestDistSq)
                    {
                        bruteBestDistSq = dSq;
                        bruteBestPoint = points[i];
                    }
                }

                // KdTree search
                bool found = tree.NearestNeighbor(query, out var treeNearest, out double treeDistSq, out _);
                Assert.True(found);
                Assert.InRange(treeDistSq, bruteBestDistSq - 1e-9, bruteBestDistSq + 1e-9);
                Assert.Equal(bruteBestPoint, treeNearest);
            }
        }

        [Fact]
        public void KdTree_KNearestNeighbors_ReturnsKClosestPoints()
        {
            var points = new List<Point3D>();
            for (int i = 0; i < 50; i++)
            {
                points.Add(new Point3D(i, 0, 0));
            }

            var tree = new KdTree3D(points);
            var knn = tree.KNearestNeighbors(new Point3D(10.2, 0, 0), k: 3);

            Assert.Equal(3, knn.Count);
            // Closest integers to 10.2 are 10, 11, 9
            Assert.Equal(10, knn[0].point.X);
            Assert.Equal(11, knn[1].point.X);
            Assert.Equal(9, knn[2].point.X);
        }
    }
}
