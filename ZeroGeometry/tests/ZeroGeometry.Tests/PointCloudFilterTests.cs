using System;
using System.Collections.Generic;
using Xunit;
using ZeroGeometry.Core.PointCloud;
using ZeroGeometry.Core.Spatial;

namespace ZeroGeometry.Tests
{
    public class PointCloudFilterTests
    {
        [Fact]
        public void VoxelGridFilter_DownsamplesDensePoints()
        {
            var points = new List<Point3D>();
            var rng = new Random(100);

            // 1000 points randomly distributed inside [0, 2] x [0, 2] x [0, 2]
            for (int i = 0; i < 1000; i++)
            {
                points.Add(new Point3D(rng.NextDouble() * 2.0, rng.NextDouble() * 2.0, rng.NextDouble() * 2.0));
            }

            var cloud = new PointCloud3D(points);
            Assert.Equal(1000, cloud.Count);

            // Leaf size 0.5 -> each axis has ~4 voxels, max voxels ~64
            var downsampled = VoxelGridFilter.Downsample(cloud, leafSize: 0.5);

            Assert.True(downsampled.Count < 100, $"Expected < 100 voxels, got {downsampled.Count}");
            Assert.True(downsampled.Count > 30, $"Expected > 30 voxels, got {downsampled.Count}");

            // Center of mass should be conserved near (1.0, 1.0, 1.0)
            double meanX = 0, meanY = 0, meanZ = 0;
            for (int i = 0; i < downsampled.Count; i++)
            {
                meanX += downsampled[i].X;
                meanY += downsampled[i].Y;
                meanZ += downsampled[i].Z;
            }
            meanX /= downsampled.Count;
            meanY /= downsampled.Count;
            meanZ /= downsampled.Count;

            Assert.InRange(meanX, 0.8, 1.2);
            Assert.InRange(meanY, 0.8, 1.2);
            Assert.InRange(meanZ, 0.8, 1.2);
        }

        [Fact]
        public void StatisticalOutlierRemoval_RemovesOutliers()
        {
            var points = new List<Point3D>();
            var rng = new Random(200);

            // 100 inlier points in tight cluster around (0, 0, 0)
            for (int i = 0; i < 100; i++)
            {
                points.Add(new Point3D((rng.NextDouble() - 0.5) * 0.5, (rng.NextDouble() - 0.5) * 0.5, (rng.NextDouble() - 0.5) * 0.5));
            }

            // Add 3 far outliers
            points.Add(new Point3D(20.0, 20.0, 20.0));
            points.Add(new Point3D(-15.0, 30.0, 10.0));
            points.Add(new Point3D(5.0, -25.0, 40.0));

            var cloud = new PointCloud3D(points);
            Assert.Equal(103, cloud.Count);

            var filtered = StatisticalOutlierRemoval.Filter(cloud, kNeighbors: 8, stdDevMultiplier: 1.0);

            // Outliers should be eliminated
            Assert.Equal(100, filtered.Count);
            for (int i = 0; i < filtered.Count; i++)
            {
                Assert.True(filtered[i].Length < 2.0, $"Outlier leaked into filtered cloud: {filtered[i]}");
            }
        }
    }
}
