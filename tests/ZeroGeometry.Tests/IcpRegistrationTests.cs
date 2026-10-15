using System;
using System.Collections.Generic;
using Xunit;
using ZeroGeometry.Core.PointCloud;
using ZeroGeometry.Core.Spatial;

namespace ZeroGeometry.Tests
{
    public class IcpRegistrationTests
    {
        [Fact]
        public void IcpRegistration_AlignsRigidBodyTransform()
        {
            // Target cloud: Asymmetric 3D grid points (distinct axes spacings to break symmetry)
            var targetPoints = new List<Point3D>();
            for (int x = -2; x <= 2; x++)
                for (int y = -3; y <= 3; y++)
                    for (int z = -1; z <= 1; z++)
                        targetPoints.Add(new Point3D(x * 2.5, y * 3.5, z * 4.5));
            var targetCloud = new PointCloud3D(targetPoints);

            // Ground truth rigid transformation:
            // Rotate around Z axis by theta = 5 degrees
            double theta = 5.0 * Math.PI / 180.0;
            double cosT = Math.Cos(theta);
            double sinT = Math.Sin(theta);
            double[,] R_true = new double[3, 3]
            {
                { cosT, -sinT, 0 },
                { sinT,  cosT, 0 },
                {    0,     0, 1 }
            };
            var t_true = new Point3D(0.2, -0.1, 0.1);

            // Create source cloud by applying true transformation
            var sourceCloud = targetCloud.Transform(R_true, t_true);

            // Run ICP to align source back to target
            var result = IcpRegistration.Align(sourceCloud, targetCloud, maxIterations: 40, tolerance: 1e-4);

            Assert.True(result.Converged);
            Assert.True(result.FitnessRms < 0.05, $"ICP alignment RMS error too high: {result.FitnessRms}");

            // Verify each aligned source point matches target cloud within tolerance
            var targetTree = new KdTree3D(targetCloud.Points);
            for (int i = 0; i < result.AlignedCloud.Count; i++)
            {
                var p = result.AlignedCloud[i];
                targetTree.NearestNeighbor(p, out _, out double distSq, out _);
                Assert.True(Math.Sqrt(distSq) < 0.08, $"Aligned point {p} too far from target cloud.");
            }
        }

        [Fact]
        public void RegisterPointToPlane_ConvergesRapidlyOnCurved3DSurfaceWithVector3Spans()
        {
            // Construct 3D paraboloid surface z = 0.15 * x^2 + 0.25 * y^2 with analytical surface normals
            var targetVecs = new List<System.Numerics.Vector3>();
            var normalVecs = new List<System.Numerics.Vector3>();

            for (int ix = -4; ix <= 4; ix++)
            {
                for (int iy = -4; iy <= 4; iy++)
                {
                    float x = ix * 0.5f;
                    float y = iy * 0.5f;
                    float z = 0.15f * x * x + 0.25f * y * y;
                    targetVecs.Add(new System.Numerics.Vector3(x, y, z));

                    var n = System.Numerics.Vector3.Normalize(new System.Numerics.Vector3(-0.30f * x, -0.50f * y, 1.0f));
                    normalVecs.Add(n);
                }
            }

            // Apply known rigid perturbation to create source span
            float angle = 6.0f * (float)Math.PI / 180.0f;
            float c = (float)Math.Cos(angle), s = (float)Math.Sin(angle);
            var sourceVecs = new System.Numerics.Vector3[targetVecs.Count];
            for (int i = 0; i < targetVecs.Count; i++)
            {
                var p = targetVecs[i];
                float rx = c * p.X - s * p.Y + 0.12f;
                float ry = s * p.X + c * p.Y - 0.08f;
                float rz = p.Z + 0.05f;
                sourceVecs[i] = new System.Numerics.Vector3(rx, ry, rz);
            }

            var result = IcpRegistration.RegisterPointToPlane(
                sourceVecs,
                targetVecs.ToArray(),
                normalVecs.ToArray(),
                maxIterations: 35,
                tolerance: 1e-5,
                huberDelta: 0.2);

            Assert.True(result.Converged);
            Assert.True(result.FitnessRms < 0.02, $"Point-to-Plane ICP RMS error too high: {result.FitnessRms}");
        }
    }
}
