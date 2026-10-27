using Xunit;
using ZeroGeometry.Core.Spatial;
using ZeroScan3D.Core.Camera;
using ZeroScan3D.Core.Reconstruction;

namespace ZeroScan3D.Tests
{
    public class TrackingAndScannerTests
    {
        [Fact]
        public void SpatialScanner_ProcessesSequentialFrames_BuildsMap()
        {
            var cam = CameraIntrinsics.SyntheticTest(30, 30, 60.0);
            var options = new ScannerOptions
            {
                VoxelSize = 0.05,
                UnprojectStride = 1,
                EnableTsdf = true,
                TsdfDimX = 30,
                TsdfDimY = 30,
                TsdfDimZ = 30,
                TsdfVoxelSize = 0.05,
                TsdfTruncationDistance = 0.1,
                TsdfOrigin = new Point3D(-0.75, -0.75, 0.5)
            };

            var scanner = new SpatialScanner(cam, options);

            // Generate synthetic flat wall at Z = 1.0m
            float[] depth = new float[900];
            for (int i = 0; i < 900; i++) depth[i] = 1.0f;

            var frame1 = new RgbdFrame(30, 30, depth, frameId: 1);
            var res1 = scanner.ProcessFrame(frame1);

            Assert.True(res1.Success);
            Assert.True(res1.FramePoints > 0);

            // Frame 2
            var frame2 = new RgbdFrame(30, 30, depth, frameId: 2);
            var res2 = scanner.ProcessFrame(frame2);

            Assert.True(res2.Success);

            // Check map point cloud
            var mapCloud = scanner.GetMapPointCloud();
            Assert.True(mapCloud.Count > 0);
            Assert.Equal(scanner.VoxelMap.VoxelCount, mapCloud.Count);

            // Check telemetry
            var stats = scanner.GetStatistics();
            Assert.Equal(2, stats.FramesProcessed);
            Assert.True(stats.TotalPointsProcessed >= 1800);
            Assert.True(stats.IsTrackingValid);

            // Extract mesh from TSDF
            var mesh = scanner.ExtractMesh(isoLevel: 0.0f, minWeight: 0.5f);
            Assert.NotNull(mesh);
        }

        [Fact]
        public void SpatialScanner_Reset_ClearsInternalState()
        {
            var cam = CameraIntrinsics.SyntheticTest(10, 10);
            var scanner = new SpatialScanner(cam);

            float[] depth = new float[100];
            for (int i = 0; i < 100; i++) depth[i] = 1.5f;

            scanner.ProcessFrame(new RgbdFrame(10, 10, depth));
            Assert.True(scanner.VoxelMap.VoxelCount > 0);

            scanner.Reset();
            Assert.Equal(0, scanner.VoxelMap.VoxelCount);
            Assert.Equal(0, scanner.GetStatistics().FramesProcessed);
        }
    }
}
