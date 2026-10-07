using System;
using System.Collections.Generic;
using ZeroGeometry.Core.Spatial;
using ZeroScan3D.Core.Camera;
using ZeroScan3D.Core.Mapping;

namespace ZeroScan3D.Core.Meshing
{
    /// <summary>
    /// Pure C# Marching Cubes isosurface extractor converting TSDF volumes into 3D triangular meshes.
    /// </summary>
    public static class MarchingCubesExtractor
    {
        /// <summary>
        /// Extracts an isosurface mesh at the specified iso-level (typically 0 for zero-crossing).
        /// </summary>
        public static Mesh3D ExtractMesh(
            TsdfVolume volume,
            float isoLevel = 0.0f,
            float minWeight = 0.5f)
        {
            if (volume == null) throw new ArgumentNullException(nameof(volume));

            var vertices = new List<Point3D>();
            var indices = new List<int>();
            var normals = new List<Point3D>();
            var colors = new List<RgbColor>();

            int dimX = volume.DimX;
            int dimY = volume.DimY;
            int dimZ = volume.DimZ;
            double voxelSize = volume.VoxelSize;

            Point3D[] cornerPos = new Point3D[8];
            float[] cornerVal = new float[8];
            float[] cornerWeight = new float[8];
            Point3D[] edgeVertex = new Point3D[12];

            for (int z = 0; z < dimZ - 1; z++)
            {
                for (int y = 0; y < dimY - 1; y++)
                {
                    for (int x = 0; x < dimX - 1; x++)
                    {
                        // 1. Gather 8 corner values
                        bool allValid = true;
                        for (int i = 0; i < 8; i++)
                        {
                            int cx = x + MarchingCubesTables.VertexOffset[i, 0];
                            int cy = y + MarchingCubesTables.VertexOffset[i, 1];
                            int cz = z + MarchingCubesTables.VertexOffset[i, 2];

                            cornerPos[i] = volume.GetVoxelWorldPos(cx, cy, cz);
                            volume.GetVoxel(cx, cy, cz, out cornerVal[i], out cornerWeight[i]);

                            if (cornerWeight[i] < minWeight)
                            {
                                allValid = false;
                                break;
                            }
                        }

                        if (!allValid)
                            continue;

                        // 2. Classify cube index
                        int cubeIndex = 0;
                        for (int i = 0; i < 8; i++)
                        {
                            if (cornerVal[i] < isoLevel)
                            {
                                cubeIndex |= (1 << i);
                            }
                        }

                        int edgeFlags = MarchingCubesTables.EdgeTable[cubeIndex];
                        if (edgeFlags == 0)
                            continue; // Completely inside or outside

                        // 3. Interpolate vertices along intersecting edges
                        for (int e = 0; e < 12; e++)
                        {
                            if ((edgeFlags & (1 << e)) != 0)
                            {
                                int v0 = MarchingCubesTables.EdgeConnection[e, 0];
                                int v1 = MarchingCubesTables.EdgeConnection[e, 1];

                                float val0 = cornerVal[v0];
                                float val1 = cornerVal[v1];
                                Point3D p0 = cornerPos[v0];
                                Point3D p1 = cornerPos[v1];

                                double t;
                                float diff = val1 - val0;
                                if (Math.Abs(diff) < 1e-9)
                                {
                                    t = 0.5;
                                }
                                else
                                {
                                    t = (isoLevel - val0) / diff;
                                    if (t < 0.0) t = 0.0;
                                    else if (t > 1.0) t = 1.0;
                                }

                                edgeVertex[e] = new Point3D(
                                    p0.X + t * (p1.X - p0.X),
                                    p0.Y + t * (p1.Y - p0.Y),
                                    p0.Z + t * (p1.Z - p0.Z));
                            }
                        }

                        // 4. Generate triangles from table
                        for (int i = 0; i < 16; i += 3)
                        {
                            int e0 = MarchingCubesTables.TriTable[cubeIndex, i];
                            if (e0 == -1) break;

                            int e1 = MarchingCubesTables.TriTable[cubeIndex, i + 1];
                            int e2 = MarchingCubesTables.TriTable[cubeIndex, i + 2];

                            Point3D vert0 = edgeVertex[e0];
                            Point3D vert1 = edgeVertex[e1];
                            Point3D vert2 = edgeVertex[e2];

                            // Compute face normal: (v1 - v0) x (v2 - v0)
                            Point3D edgeA = vert1 - vert0;
                            Point3D edgeB = vert2 - vert0;
                            Point3D normal = edgeA.Cross(edgeB).Normalized();

                            int baseIndex = vertices.Count;
                            vertices.Add(vert0);
                            vertices.Add(vert1);
                            vertices.Add(vert2);

                            normals.Add(normal);
                            normals.Add(normal);
                            normals.Add(normal);

                            colors.Add(RgbColor.White);
                            colors.Add(RgbColor.White);
                            colors.Add(RgbColor.White);

                            indices.Add(baseIndex);
                            indices.Add(baseIndex + 1);
                            indices.Add(baseIndex + 2);
                        }
                    }
                }
            }

            return new Mesh3D(vertices, indices, normals, colors);
        }
    }
}
