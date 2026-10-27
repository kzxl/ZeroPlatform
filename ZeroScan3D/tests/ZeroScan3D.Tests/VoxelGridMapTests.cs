using System.Collections.Generic;
using Xunit;
using ZeroGeometry.Core.PointCloud;
using ZeroGeometry.Core.Spatial;
using ZeroScan3D.Core.Camera;
using ZeroScan3D.Core.Mapping;
using ZeroScan3D.Core.Tracking;

namespace ZeroScan3D.Tests
{
    public class VoxelGridMapTests
    {
        [Fact]
        public void VoxelGridMap_IntegratesAndAveragesPoints()
        {
            var map = new VoxelGridMap(voxelSize: 0.1); // 10cm voxels

            // Insert two points that fall into the same voxel [0, 0, 0]
            var points = new List<Point3D>
            {
                new Point3D(0.02, 0.02, 0.02),
                new Point3D(0.08, 0.08, 0.08)
            };
            var cloud = new PointCloud3D(points);

            map.Integrate(cloud, CameraPose.Identity);

            Assert.Equal(1, map.VoxelCount);
            var mapCloud = map.ToPointCloud();
            Assert.Equal(1, mapCloud.Count);

            // Centroid should be average: (0.05, 0.05, 0.05)
            Assert.InRange(mapCloud[0].X, 0.049, 0.051);
            Assert.InRange(mapCloud[0].Y, 0.049, 0.051);
            Assert.InRange(mapCloud[0].Z, 0.049, 0.051);
        }

        [Fact]
        public void VoxelGridMap_IntegratesWithCameraPoseTransform()
        {
            var map = new VoxelGridMap(voxelSize: 0.05);

            var cloud = new PointCloud3D(new[] { new Point3D(0, 0, 1.0) });
            // Camera is translated by +2.0m along X
            var pose = new CameraPose(new Point3D(2.0, 0.0, 0.0));

            map.Integrate(cloud, pose);

            var mapCloud = map.ToPointCloud();
            Assert.Equal(1, mapCloud.Count);
            Assert.InRange(mapCloud[0].X, 1.99, 2.01);
            Assert.InRange(mapCloud[0].Z, 0.99, 1.01);
        }

        [Fact]
        public void VoxelGridMap_BoundingBox_ComputesCorrectExtents()
        {
            var map = new VoxelGridMap(voxelSize: 0.1);
            var cloud = new PointCloud3D(new[]
            {
                new Point3D(-1.0, -0.5, 0.0),
                new Point3D(3.0, 2.0, 5.0)
            });

            map.Integrate(cloud, CameraPose.Identity);

            bool hasBounds = map.GetBoundingBox(out var min, out var max);
            Assert.True(hasBounds);
            Assert.InRange(min.X, -1.05, -0.95);
            Assert.InRange(max.X, 2.95, 3.05);
            Assert.InRange(min.Y, -0.55, -0.45);
            Assert.InRange(max.Y, 1.95, 2.05);
        }

        [Fact]
        public void VoxelGridMap_FiltersLowWeightNoise()
        {
            var map = new VoxelGridMap(voxelSize: 0.1);

            // Single hit point (noise)
            map.Integrate(new PointCloud3D(new[] { new Point3D(0.05, 0.05, 0.05) }), CameraPose.Identity);

            // Repeated hit point (stable surface)
            var pStable = new PointCloud3D(new[] { new Point3D(1.05, 1.05, 1.05) });
            map.Integrate(pStable, CameraPose.Identity);
            map.Integrate(pStable, CameraPose.Identity);

            var filteredCloud = map.ToPointCloud(minWeight: 2);
            Assert.Equal(1, filteredCloud.Count);
            Assert.InRange(filteredCloud[0].X, 1.0, 1.1);
        }
    }
}
