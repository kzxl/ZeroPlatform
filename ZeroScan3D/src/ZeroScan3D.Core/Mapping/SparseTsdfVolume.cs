using System;
using System.Collections.Generic;
using ZeroGeometry.Core.PointCloud;
using ZeroGeometry.Core.Spatial;
using ZeroScan3D.Core.Camera;
using ZeroScan3D.Core.Tracking;

namespace ZeroScan3D.Core.Mapping
{
    /// <summary>
    /// 3D spatial block coordinate indexing an 8x8x8 VoxelBlock chunk in world space.
    /// </summary>
    public readonly struct BlockCoord3D : IEquatable<BlockCoord3D>
    {
        public int Bx { get; }
        public int By { get; }
        public int Bz { get; }

        public BlockCoord3D(int bx, int by, int bz)
        {
            Bx = bx;
            By = by;
            Bz = bz;
        }

        public bool Equals(BlockCoord3D other) => Bx == other.Bx && By == other.By && Bz == other.Bz;
        public override bool Equals(object? obj) => obj is BlockCoord3D other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Bx * 73856093;
                hash ^= By * 19349663;
                hash ^= Bz * 83492791;
                return hash;
            }
        }

        public override string ToString() => $"Block({Bx}, {By}, {Bz})";
    }

    /// <summary>
    /// Contiguous 8x8x8 = 512 voxel chunk storing truncated signed distances and integration weights.
    /// </summary>
    public sealed class VoxelBlock
    {
        public const int BlockSize = 8;
        public const int VoxelsPerBlock = BlockSize * BlockSize * BlockSize; // 512

        public BlockCoord3D Coord { get; }
        public float[] Tsdf { get; }
        public float[] Weights { get; }

        public VoxelBlock(BlockCoord3D coord)
        {
            Coord = coord;
            Tsdf = new float[VoxelsPerBlock];
            Weights = new float[VoxelsPerBlock];

            for (int i = 0; i < VoxelsPerBlock; i++)
            {
                Tsdf[i] = 1.0f;
            }
        }

        public static int GetLocalIndex(int lx, int ly, int lz) => (lz << 6) | (ly << 3) | lx;
    }

    /// <summary>
    /// Sparse Spatial-Hashed TSDF Volume (Niessner et al. VoxelHashing architecture).
    /// Partitions unbounded 3D space into 8x8x8 VoxelBlock chunks and only allocates blocks
    /// within the truncation band [-mu, +mu] along observed depth rays, reducing memory complexity
    /// from dense O(N^3) to sparse O(surface area).
    /// </summary>
    public sealed class SparseTsdfVolume
    {
        private readonly Dictionary<BlockCoord3D, VoxelBlock> _blocks;
        private readonly double _invVoxelSize;

        public double VoxelSize { get; }
        public double TruncationDistance { get; }
        public float MaxWeight { get; set; } = 100.0f;

        public int AllocatedBlockCount => _blocks.Count;
        public int TotalAllocatedVoxels => _blocks.Count * VoxelBlock.VoxelsPerBlock;
        public IReadOnlyDictionary<BlockCoord3D, VoxelBlock> Blocks => _blocks;

        public SparseTsdfVolume(double voxelSize = 0.02, double truncationDistance = 0.08, int initialBlockCapacity = 256)
        {
            if (voxelSize <= 1e-6)
                throw new ArgumentOutOfRangeException(nameof(voxelSize), "VoxelSize must be > 1e-6 meters.");
            if (truncationDistance <= voxelSize)
                throw new ArgumentOutOfRangeException(nameof(truncationDistance), "TruncationDistance must be greater than VoxelSize.");

            VoxelSize = voxelSize;
            _invVoxelSize = 1.0 / voxelSize;
            TruncationDistance = truncationDistance;
            _blocks = new Dictionary<BlockCoord3D, VoxelBlock>(Math.Max(16, initialBlockCapacity));
        }

        public void WorldToGlobalVoxel(Point3D worldPos, out int vx, out int vy, out int vz)
        {
            vx = (int)Math.Floor(worldPos.X * _invVoxelSize);
            vy = (int)Math.Floor(worldPos.Y * _invVoxelSize);
            vz = (int)Math.Floor(worldPos.Z * _invVoxelSize);
        }

        public static void GlobalVoxelToBlockAndLocal(
            int vx,
            int vy,
            int vz,
            out BlockCoord3D blockCoord,
            out int lx,
            out int ly,
            out int lz)
        {
            // Arithmetic right shift >> 3 is exact floor division by 8 for all two's complement signed ints
            blockCoord = new BlockCoord3D(vx >> 3, vy >> 3, vz >> 3);
            lx = vx & 7;
            ly = vy & 7;
            lz = vz & 7;
        }

        public Point3D GetVoxelWorldPos(int vx, int vy, int vz)
        {
            return new Point3D(
                (vx + 0.5) * VoxelSize,
                (vy + 0.5) * VoxelSize,
                (vz + 0.5) * VoxelSize);
        }

        public bool GetVoxel(int vx, int vy, int vz, out float tsdf, out float weight)
        {
            GlobalVoxelToBlockAndLocal(vx, vy, vz, out var blockCoord, out int lx, out int ly, out int lz);
            if (_blocks.TryGetValue(blockCoord, out var block))
            {
                int idx = VoxelBlock.GetLocalIndex(lx, ly, lz);
                tsdf = block.Tsdf[idx];
                weight = block.Weights[idx];
                return true;
            }

            tsdf = 1.0f;
            weight = 0.0f;
            return false;
        }

        public void SetVoxel(int vx, int vy, int vz, float tsdf, float weight)
        {
            GlobalVoxelToBlockAndLocal(vx, vy, vz, out var blockCoord, out int lx, out int ly, out int lz);
            if (!_blocks.TryGetValue(blockCoord, out var block))
            {
                block = new VoxelBlock(blockCoord);
                _blocks[blockCoord] = block;
            }

            int idx = VoxelBlock.GetLocalIndex(lx, ly, lz);
            block.Tsdf[idx] = tsdf;
            block.Weights[idx] = weight;
        }

        /// <summary>
        /// Allocates 8x8x8 VoxelBlocks strictly within the truncation band [-mu, +mu] along depth rays
        /// and integrates projective signed distances into those active blocks.
        /// </summary>
        public int Integrate(RgbdFrame frame, CameraIntrinsics intrinsics, CameraPose pose)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            if (intrinsics == null) throw new ArgumentNullException(nameof(intrinsics));
            if (pose == null) throw new ArgumentNullException(nameof(pose));

            var touchedBlocks = new HashSet<BlockCoord3D>();
            double mu = TruncationDistance;
            double stepSize = Math.Max(VoxelSize * 0.75, mu * 0.25);
            var camOriginWorld = pose.Translation;

            // Phase 1: Discover and allocate 8x8x8 blocks along depth rays within [-mu, +mu]
            for (int v = 0; v < frame.Height; v++)
            {
                for (int u = 0; u < frame.Width; u++)
                {
                    float rawDepth = frame.GetDepth(u, v);
                    if (rawDepth <= 0f)
                        continue;

                    var pCamSurface = intrinsics.Unproject(u, v, rawDepth);
                    if (pCamSurface.Z <= 0.05)
                        continue;

                    var pWorldSurface = pose.TransformPoint(pCamSurface);
                    var rayVec = pWorldSurface - camOriginWorld;
                    double rayLen = rayVec.Length;
                    if (rayLen <= 1e-6)
                        continue;

                    var rayDir = rayVec / rayLen;
                    double tStart = Math.Max(0.05, rayLen - mu);
                    double tEnd = rayLen + mu;

                    for (double t = tStart; t <= tEnd + 1e-9; t += stepSize)
                    {
                        var samplePos = camOriginWorld + rayDir * Math.Min(t, tEnd);
                        WorldToGlobalVoxel(samplePos, out int vx, out int vy, out int vz);
                        var blockCoord = new BlockCoord3D(vx >> 3, vy >> 3, vz >> 3);

                        if (touchedBlocks.Add(blockCoord) && !_blocks.ContainsKey(blockCoord))
                        {
                            _blocks[blockCoord] = new VoxelBlock(blockCoord);
                        }
                    }
                }
            }

            // Phase 2: Projective TSDF sweep only over touched 8x8x8 blocks
            float invTrunc = (float)(1.0 / TruncationDistance);
            double depthScale = intrinsics.DepthScale;

            foreach (var blockCoord in touchedBlocks)
            {
                var block = _blocks[blockCoord];
                int baseVx = blockCoord.Bx << 3;
                int baseVy = blockCoord.By << 3;
                int baseVz = blockCoord.Bz << 3;

                for (int lz = 0; lz < VoxelBlock.BlockSize; lz++)
                {
                    int vz = baseVz + lz;
                    for (int ly = 0; ly < VoxelBlock.BlockSize; ly++)
                    {
                        int vy = baseVy + ly;
                        for (int lx = 0; lx < VoxelBlock.BlockSize; lx++)
                        {
                            int vx = baseVx + lx;
                            var pWorld = GetVoxelWorldPos(vx, vy, vz);
                            var pCam = pose.InverseTransformPoint(pWorld);

                            if (pCam.Z <= 0.05)
                                continue;

                            if (!intrinsics.Project(pCam, out double uProj, out double vProj))
                                continue;

                            int iu = (int)Math.Round(uProj);
                            int iv = (int)Math.Round(vProj);
                            if (iu < 0 || iu >= frame.Width || iv < 0 || iv >= frame.Height)
                                continue;

                            float rawDepth = frame.GetDepth(iu, iv);
                            if (rawDepth <= 0f)
                                continue;

                            double dMeters = rawDepth * depthScale;
                            double sdf = dMeters - pCam.Z;

                            if (sdf >= -TruncationDistance && sdf <= TruncationDistance)
                            {
                                float tsdfVal = (float)Math.Max(-1.0, Math.Min(1.0, sdf * invTrunc));
                                int localIdx = VoxelBlock.GetLocalIndex(lx, ly, lz);

                                float oldWeight = block.Weights[localIdx];
                                float oldTsdf = block.Tsdf[localIdx];

                                float newWeight = Math.Min(oldWeight + 1.0f, MaxWeight);
                                float newTsdf = (oldTsdf * oldWeight + tsdfVal) / (oldWeight + 1.0f);

                                block.Tsdf[localIdx] = newTsdf;
                                block.Weights[localIdx] = newWeight;
                            }
                        }
                    }
                }
            }

            return touchedBlocks.Count;
        }

        /// <summary>
        /// Extracts sub-voxel zero-crossing surface points from all allocated blocks.
        /// </summary>
        public PointCloud3D ExtractPointCloud(float minWeight = 1.0f)
        {
            var normals = ExtractSurfaceNormals(minWeight);
            var points = new List<Point3D>(normals.Count);
            for (int i = 0; i < normals.Count; i++)
            {
                points.Add(normals[i].Point);
            }
            return new PointCloud3D(points);
        }

        /// <summary>
        /// Extracts sub-voxel zero-crossing surface points paired with analytical TSDF gradient surface normals.
        /// </summary>
        public List<PointNormal3D> ExtractSurfaceNormals(float minWeight = 1.0f)
        {
            var results = new List<PointNormal3D>();

            foreach (var kvp in _blocks)
            {
                var block = kvp.Value;
                int baseVx = block.Coord.Bx << 3;
                int baseVy = block.Coord.By << 3;
                int baseVz = block.Coord.Bz << 3;

                for (int lz = 0; lz < VoxelBlock.BlockSize; lz++)
                {
                    int vz = baseVz + lz;
                    for (int ly = 0; ly < VoxelBlock.BlockSize; ly++)
                    {
                        int vy = baseVy + ly;
                        for (int lx = 0; lx < VoxelBlock.BlockSize; lx++)
                        {
                            int vx = baseVx + lx;
                            int idx = VoxelBlock.GetLocalIndex(lx, ly, lz);

                            float w0 = block.Weights[idx];
                            if (w0 < minWeight)
                                continue;

                            float f0 = block.Tsdf[idx];
                            var p0 = GetVoxelWorldPos(vx, vy, vz);

                            // Check zero-crossings along +X, +Y, +Z edges
                            TryAddZeroCrossing(vx, vy, vz, p0, f0, 1, 0, 0, minWeight, results);
                            TryAddZeroCrossing(vx, vy, vz, p0, f0, 0, 1, 0, minWeight, results);
                            TryAddZeroCrossing(vx, vy, vz, p0, f0, 0, 0, 1, minWeight, results);
                        }
                    }
                }
            }

            return results;
        }

        private void TryAddZeroCrossing(
            int vx,
            int vy,
            int vz,
            Point3D p0,
            float f0,
            int dx,
            int dy,
            int dz,
            float minWeight,
            List<PointNormal3D> results)
        {
            if (!GetVoxel(vx + dx, vy + dy, vz + dz, out float f1, out float w1) || w1 < minWeight)
                return;

            // Zero crossing occurs when f0 and f1 have opposite signs (or one hits 0 with the other non-zero)
            if ((f0 <= 0.0f && f1 > 0.0f) || (f0 >= 0.0f && f1 < 0.0f))
            {
                float denom = Math.Abs(f0) + Math.Abs(f1);
                double alpha = denom > 1e-7f ? Math.Abs(f0) / denom : 0.5;
                var p1 = GetVoxelWorldPos(vx + dx, vy + dy, vz + dz);
                var pZero = p0 + (p1 - p0) * alpha;

                var normal = ComputeGradientNormal(vx, vy, vz, f0);
                results.Add(new PointNormal3D(pZero, normal, 0.0));
            }
        }

        private Point3D ComputeGradientNormal(int vx, int vy, int vz, float centerTsdf)
        {
            double gx = SampleTsdfOrDefault(vx + 1, vy, vz, centerTsdf) - SampleTsdfOrDefault(vx - 1, vy, vz, centerTsdf);
            double gy = SampleTsdfOrDefault(vx, vy + 1, vz, centerTsdf) - SampleTsdfOrDefault(vx, vy - 1, vz, centerTsdf);
            double gz = SampleTsdfOrDefault(vx, vy, vz + 1, centerTsdf) - SampleTsdfOrDefault(vx, vy, vz - 1, centerTsdf);

            // Since TSDF is positive in front of the surface (towards sensor) and negative inside surface,
            // -grad(TSDF) points from outside towards inside, whereas +grad(TSDF) points outward towards the sensor.
            var grad = new Point3D(gx, gy, gz);
            return grad.LengthSquared > 1e-12 ? grad.Normalized() : new Point3D(0, 0, -1);
        }

        private float SampleTsdfOrDefault(int vx, int vy, int vz, float fallback)
        {
            return GetVoxel(vx, vy, vz, out float tsdf, out float w) && w > 0.0f ? tsdf : fallback;
        }

        public void Clear()
        {
            _blocks.Clear();
        }
    }
}
