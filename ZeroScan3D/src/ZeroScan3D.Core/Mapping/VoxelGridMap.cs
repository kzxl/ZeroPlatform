using System;
using System.Collections.Generic;
using ZeroGeometry.Core.PointCloud;
using ZeroGeometry.Core.Spatial;
using ZeroScan3D.Core.Camera;
using ZeroScan3D.Core.Tracking;

namespace ZeroScan3D.Core.Mapping
{
    public readonly struct VoxelIndex : IEquatable<VoxelIndex>
    {
        public int X { get; }
        public int Y { get; }
        public int Z { get; }

        public VoxelIndex(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public bool Equals(VoxelIndex other) => X == other.X && Y == other.Y && Z == other.Z;
        public override bool Equals(object? obj) => obj is VoxelIndex other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = X * 73856093;
                hash ^= Y * 19349663;
                hash ^= Z * 83492791;
                return hash;
            }
        }

        public override string ToString() => $"[{X}, {Y}, {Z}]";
    }

    /// <summary>
    /// Sparse volumetric voxel hash grid representing an incremental, fused 3D spatial map.
    /// </summary>
    public sealed class VoxelGridMap
    {
        private readonly Dictionary<VoxelIndex, SpatialVoxel> _voxels;
        private readonly double _voxelSize;
        private readonly double _invVoxelSize;

        public double VoxelSize => _voxelSize;
        public int VoxelCount => _voxels.Count;

        public VoxelGridMap(double voxelSize = 0.02)
        {
            if (voxelSize <= 1e-6)
                throw new ArgumentOutOfRangeException(nameof(voxelSize), "Voxel size must be > 0.000001 meters.");

            _voxelSize = voxelSize;
            _invVoxelSize = 1.0 / voxelSize;
            _voxels = new Dictionary<VoxelIndex, SpatialVoxel>(4096);
        }

        public VoxelIndex GetVoxelIndex(Point3D p)
        {
            int vx = (int)Math.Floor(p.X * _invVoxelSize);
            int vy = (int)Math.Floor(p.Y * _invVoxelSize);
            int vz = (int)Math.Floor(p.Z * _invVoxelSize);
            return new VoxelIndex(vx, vy, vz);
        }

        /// <summary>
        /// Integrates a camera-frame point cloud transformed into world space by pose.
        /// </summary>
        public void Integrate(PointCloud3D cloud, CameraPose pose)
        {
            if (cloud == null) throw new ArgumentNullException(nameof(cloud));
            if (pose == null) throw new ArgumentNullException(nameof(pose));

            for (int i = 0; i < cloud.Count; i++)
            {
                var worldPoint = pose.TransformPoint(cloud[i]);
                var index = GetVoxelIndex(worldPoint);

                if (_voxels.TryGetValue(index, out var existing))
                {
                    existing.Fuse(worldPoint, RgbColor.White);
                }
                else
                {
                    _voxels[index] = new SpatialVoxel(worldPoint, RgbColor.White, 1);
                }
            }
        }

        /// <summary>
        /// Integrates colored camera-frame points transformed into world space.
        /// </summary>
        public void IntegrateColored(IReadOnlyList<ColoredPoint3D> points, CameraPose pose)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (pose == null) throw new ArgumentNullException(nameof(pose));

            for (int i = 0; i < points.Count; i++)
            {
                var p = points[i];
                var worldPoint = pose.TransformPoint(p.Position);
                var index = GetVoxelIndex(worldPoint);

                if (_voxels.TryGetValue(index, out var existing))
                {
                    existing.Fuse(worldPoint, p.Color);
                }
                else
                {
                    _voxels[index] = new SpatialVoxel(worldPoint, p.Color, 1);
                }
            }
        }

        /// <summary>
        /// Exports all fused voxel centroids as a PointCloud3D.
        /// </summary>
        public PointCloud3D ToPointCloud(int minWeight = 1)
        {
            var points = new List<Point3D>(_voxels.Count);
            foreach (var kvp in _voxels)
            {
                if (kvp.Value.Weight >= minWeight)
                {
                    points.Add(kvp.Value.Centroid);
                }
            }
            return new PointCloud3D(points);
        }

        /// <summary>
        /// Exports all fused voxel centroids and colors.
        /// </summary>
        public List<ColoredPoint3D> ToColoredPoints(int minWeight = 1)
        {
            var result = new List<ColoredPoint3D>(_voxels.Count);
            foreach (var kvp in _voxels)
            {
                if (kvp.Value.Weight >= minWeight)
                {
                    result.Add(new ColoredPoint3D(kvp.Value.Centroid, kvp.Value.Color));
                }
            }
            return result;
        }

        /// <summary>
        /// Computes minimum and maximum spatial bounds across all active voxels.
        /// </summary>
        public bool GetBoundingBox(out Point3D minBound, out Point3D maxBound)
        {
            if (_voxels.Count == 0)
            {
                minBound = default;
                maxBound = default;
                return false;
            }

            double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;

            foreach (var voxel in _voxels.Values)
            {
                var p = voxel.Centroid;
                if (p.X < minX) minX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.Z < minZ) minZ = p.Z;
                if (p.X > maxX) maxX = p.X;
                if (p.Y > maxY) maxY = p.Y;
                if (p.Z > maxZ) maxZ = p.Z;
            }

            minBound = new Point3D(minX, minY, minZ);
            maxBound = new Point3D(maxX, maxY, maxZ);
            return true;
        }

        public void Clear()
        {
            _voxels.Clear();
        }
    }
}
