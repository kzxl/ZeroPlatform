using System;
using System.IO;
using Xunit;
using ZeroGeometry.Core.Spatial;
using ZeroScan3D.Core.Camera;
using ZeroScan3D.Core.Mapping;
using ZeroScan3D.Core.Meshing;
using ZeroScan3D.Core.Tracking;

namespace ZeroScan3D.Tests
{
    public class TsdfAndMarchingCubesTests
    {
        [Fact]
        public void TsdfVolume_IntegratesProjectiveDepth()
        {
            var volume = new TsdfVolume(
                dimX: 20,
                dimY: 20,
                dimZ: 20,
                voxelSize: 0.05,
                truncationDistance: 0.1,
                origin: new Point3D(-0.5, -0.5, 0.5));

            var cam = CameraIntrinsics.SyntheticTest(20, 20);
            float[] depth = new float[400];
            for (int i = 0; i < 400; i++) depth[i] = 1.0f; // 1.0m plane

            var frame = new RgbdFrame(20, 20, depth);
            volume.Integrate(frame, cam, CameraPose.Identity);

            // Voxel at center Z ~ 1.0m should have TSDF near 0
            // Origin.Z = 0.5, voxelSize = 0.05. At z = 10, Z_world = 0.5 + 10.5*0.05 = 1.025m
            volume.GetVoxel(10, 10, 10, out float tsdf, out float weight);

            Assert.True(weight > 0.0f);
            Assert.InRange(tsdf, -0.5f, 0.5f);
        }

        [Fact]
        public void MarchingCubes_ExtractsIsosurfaceMesh_FromSphere()
        {
            // Construct a 20x20x20 volume containing a sphere with radius R = 0.35m at center
            double voxelSize = 0.05;
            var volume = new TsdfVolume(
                dimX: 20,
                dimY: 20,
                dimZ: 20,
                voxelSize: voxelSize,
                truncationDistance: 0.1,
                origin: new Point3D(-0.5, -0.5, -0.5));

            var center = new Point3D(0, 0, 0);
            double radius = 0.3;

            for (int z = 0; z < volume.DimZ; z++)
            {
                for (int y = 0; y < volume.DimY; y++)
                {
                    for (int x = 0; x < volume.DimX; x++)
                    {
                        var pos = volume.GetVoxelWorldPos(x, y, z);
                        double dist = pos.DistanceTo(center) - radius;
                        float tsdf = (float)Math.Min(1.0, Math.Max(-1.0, dist / volume.TruncationDistance));
                        volume.SetVoxel(x, y, z, tsdf, 1.0f);
                    }
                }
            }

            var mesh = MarchingCubesExtractor.ExtractMesh(volume, isoLevel: 0.0f, minWeight: 0.5f);

            Assert.True(mesh.VertexCount > 50, $"Expected >50 vertices, got {mesh.VertexCount}");
            Assert.True(mesh.TriangleCount > 20, $"Expected >20 triangles, got {mesh.TriangleCount}");
            Assert.Equal(mesh.VertexCount, mesh.Normals?.Count);

            // Check that all vertices lie roughly on the sphere surface (radius ~ 0.3m)
            for (int i = 0; i < mesh.VertexCount; i++)
            {
                double dist = mesh.Vertices[i].DistanceTo(center);
                Assert.InRange(dist, 0.25, 0.35);
            }
        }

        [Fact]
        public void Mesh3D_ExportsToObjAndPlyFiles()
        {
            var volume = new TsdfVolume(10, 10, 10, 0.1, 0.1, new Point3D(-0.5, -0.5, -0.5));
            for (int z = 0; z < 10; z++)
                for (int y = 0; y < 10; y++)
                    for (int x = 0; x < 10; x++)
                    {
                        var pos = volume.GetVoxelWorldPos(x, y, z);
                        float val = (float)(pos.Length - 0.25);
                        volume.SetVoxel(x, y, z, val, 1.0f);
                    }

            var mesh = MarchingCubesExtractor.ExtractMesh(volume, 0.0f, 0.5f);
            Assert.True(mesh.VertexCount > 0);

            string tempDir = Path.Combine(Path.GetTempPath(), "zeroscan3d_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                string objPath = Path.Combine(tempDir, "model.obj");
                mesh.ExportObj(objPath);
                Assert.True(File.Exists(objPath));
                string objContent = File.ReadAllText(objPath);
                Assert.Contains("v ", objContent);
                Assert.Contains("f ", objContent);

                string plyPath = Path.Combine(tempDir, "model.ply");
                mesh.ExportPly(plyPath);
                Assert.True(File.Exists(plyPath));
                string plyContent = File.ReadAllText(plyPath);
                Assert.Contains("ply", plyContent);
                Assert.Contains("element vertex", plyContent);
                Assert.Contains("element face", plyContent);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void SparseTsdfVolume_AllocatesOnlySurfaceBandBlocksAndExtractsZeroCrossingCloud()
        {
            var sparse = new SparseTsdfVolume(voxelSize: 0.04, truncationDistance: 0.12);
            var cam = CameraIntrinsics.SyntheticTest(24, 24);

            // Synthetic planar wall at Z = 1.20m
            float[] depth = new float[24 * 24];
            for (int i = 0; i < depth.Length; i++) depth[i] = 1.20f;

            var frame = new RgbdFrame(24, 24, depth);
            int touchedBlocks = sparse.Integrate(frame, cam, CameraPose.Identity);

            Assert.True(touchedBlocks > 0);
            Assert.Equal(touchedBlocks, sparse.AllocatedBlockCount);
            // Sparse allocation should be orders of magnitude smaller than a dense 100^3 volume (1,000,000 voxels)
            Assert.True(sparse.TotalAllocatedVoxels < 50_000, $"Expected sparse voxel count < 50,000, got {sparse.TotalAllocatedVoxels}");

            var surfaceNormals = sparse.ExtractSurfaceNormals(minWeight: 1.0f);
            var cloud = sparse.ExtractPointCloud(minWeight: 1.0f);

            Assert.True(cloud.Count > 20, $"Expected > 20 zero-crossing surface points, got {cloud.Count}");
            Assert.Equal(cloud.Count, surfaceNormals.Count);

            // Verify extracted zero-crossing surface points lie accurately on the Z = 1.20m plane
            for (int i = 0; i < cloud.Count; i++)
            {
                Assert.InRange(cloud[i].Z, 1.15, 1.25);
                Assert.InRange(surfaceNormals[i].Normal.Length, 0.99, 1.01);
            }
        }
    }
}
