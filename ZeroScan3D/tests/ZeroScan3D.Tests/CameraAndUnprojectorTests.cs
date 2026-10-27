using System;
using Xunit;
using ZeroGeometry.Core.Spatial;
using ZeroScan3D.Core.Camera;

namespace ZeroScan3D.Tests
{
    public class CameraAndUnprojectorTests
    {
        [Fact]
        public void CameraIntrinsics_Project_Unproject_Cycle_Matches()
        {
            var cam = CameraIntrinsics.SyntheticTest(100, 100, 60.0);
            double originalU = 35.0;
            double originalV = 65.0;
            double depthMeters = 2.5;

            var point3D = cam.Unproject(originalU, originalV, depthMeters);
            Assert.True(point3D.Z > 0);

            bool projected = cam.Project(point3D, out double projU, out double projV);
            Assert.True(projected);
            Assert.InRange(projU, originalU - 1e-4, originalU + 1e-4);
            Assert.InRange(projV, originalV - 1e-4, originalV + 1e-4);
        }

        [Fact]
        public void DepthUnprojector_ProducesValidPointCloud()
        {
            var cam = CameraIntrinsics.SyntheticTest(10, 10);
            float[] depth = new float[100];
            for (int i = 0; i < 100; i++) depth[i] = 1.0f; // 1 meter flat wall

            var frame = new RgbdFrame(10, 10, depth);
            var cloud = DepthUnprojector.Unproject(frame, cam);

            Assert.Equal(100, cloud.Count);
            for (int i = 0; i < cloud.Count; i++)
            {
                Assert.InRange(cloud[i].Z, 0.999, 1.001);
            }
        }

        [Fact]
        public void DepthUnprojector_Colored_ExtractsColorChannels()
        {
            var cam = CameraIntrinsics.SyntheticTest(4, 4);
            float[] depth = new float[16];
            byte[] rgb = new byte[16 * 3];

            for (int i = 0; i < 16; i++)
            {
                depth[i] = 1.5f;
                rgb[i * 3 + 0] = 255; // Red
                rgb[i * 3 + 1] = 128; // Green
                rgb[i * 3 + 2] = 0;   // Blue
            }

            var frame = new RgbdFrame(4, 4, depth, rgb);
            var points = DepthUnprojector.UnprojectColored(frame, cam);

            Assert.Equal(16, points.Count);
            Assert.Equal(255, points[0].Color.R);
            Assert.Equal(128, points[0].Color.G);
            Assert.Equal(0, points[0].Color.B);
        }

        [Fact]
        public void DepthUnprojector_ClipsMinAndMaxDepth()
        {
            var cam = CameraIntrinsics.SyntheticTest(4, 4);
            float[] depth = new float[16];
            depth[0] = 0.05f; // < min 0.2
            depth[1] = 0.5f;  // valid
            depth[2] = 6.0f;  // > max 5.0
            depth[3] = 1.0f;  // valid

            for (int i = 4; i < 16; i++) depth[i] = 0f;

            var frame = new RgbdFrame(4, 4, depth);
            var cloud = DepthUnprojector.Unproject(frame, cam, minDepthMeters: 0.2, maxDepthMeters: 5.0);

            Assert.Equal(2, cloud.Count);
        }
    }
}
