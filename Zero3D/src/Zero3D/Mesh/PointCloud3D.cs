using System;
using System.Collections.Generic;
using Zero3D.Materials;
using Zero3D.Math;

namespace Zero3D.Mesh
{
    /// <summary>
    /// Individual 3D spatial sample point with position, RGB color, normal vector, and reflectance intensity.
    /// Used in 3D Scanning, LiDAR, and Photogrammetry.
    /// </summary>
    public struct Point3D
    {
        public Vec3 Position;
        public ColorRgb Color;
        public Vec3 Normal;
        public float Intensity;

        public Point3D(Vec3 position)
        {
            Position = position;
            Color = ColorRgb.White;
            Normal = Vec3.UnitY;
            Intensity = 1.0f;
        }

        public Point3D(Vec3 position, ColorRgb color, Vec3 normal = default, float intensity = 1.0f)
        {
            Position = position;
            Color = color;
            Normal = normal.LengthSquared() > 1e-6f ? normal : Vec3.UnitY;
            Intensity = intensity;
        }

        public override string ToString() => $"Pos:{Position} Col:{Color}";
    }

    /// <summary>
    /// Container for dense 3D point cloud data without topology/mesh faces.
    /// </summary>
    public class PointCloud3D
    {
        public string Name { get; set; } = "PointCloud";
        public List<Point3D> Points { get; } = new List<Point3D>();
        public Aabb3D BoundingBox { get; private set; }

        public int Count => Points.Count;

        public void Add(Point3D point)
        {
            Points.Add(point);
        }

        public void Clear()
        {
            Points.Clear();
            BoundingBox = default;
        }

        public void ComputeBounds()
        {
            if (Points.Count == 0)
            {
                BoundingBox = default;
                return;
            }

            var min = new Vec3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vec3(float.MinValue, float.MinValue, float.MinValue);

            for (int i = 0; i < Points.Count; i++)
            {
                var p = Points[i].Position;
                if (p.X < min.X) min.X = p.X;
                if (p.Y < min.Y) min.Y = p.Y;
                if (p.Z < min.Z) min.Z = p.Z;

                if (p.X > max.X) max.X = p.X;
                if (p.Y > max.Y) max.Y = p.Y;
                if (p.Z > max.Z) max.Z = p.Z;
            }

            BoundingBox = new Aabb3D(min, max);
        }
    }

    /// <summary>
    /// Voxel Grid Downsampling filter to reduce point cloud density while preserving geometry.
    /// </summary>
    public static class VoxelGridFilter
    {
        private struct VoxelKey : IEquatable<VoxelKey>
        {
            public int X, Y, Z;
            public VoxelKey(int x, int y, int z) { X = x; Y = y; Z = z; }

            public bool Equals(VoxelKey other) => X == other.X && Y == other.Y && Z == other.Z;
            public override bool Equals(object? obj) => obj is VoxelKey k && Equals(k);
            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = hash * 31 + X;
                    hash = hash * 31 + Y;
                    hash = hash * 31 + Z;
                    return hash;
                }
            }
        }

        private class VoxelAccumulator
        {
            public float SumX, SumY, SumZ;
            public float SumR, SumG, SumB;
            public float SumNx, SumNy, SumNz;
            public float SumIntensity;
            public int Count;
        }

        /// <summary>
        /// Downsamples a point cloud using regular 3D grid cell averaging.
        /// </summary>
        public static PointCloud3D Downsample(PointCloud3D source, float voxelSize)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (voxelSize <= 0f) throw new ArgumentOutOfRangeException(nameof(voxelSize), "Voxel size must be positive.");

            var grid = new Dictionary<VoxelKey, VoxelAccumulator>();
            float invSize = 1f / voxelSize;

            for (int i = 0; i < source.Points.Count; i++)
            {
                var pt = source.Points[i];
                int vx = (int)System.Math.Floor(pt.Position.X * invSize);
                int vy = (int)System.Math.Floor(pt.Position.Y * invSize);
                int vz = (int)System.Math.Floor(pt.Position.Z * invSize);

                var key = new VoxelKey(vx, vy, vz);
                if (!grid.TryGetValue(key, out var acc))
                {
                    acc = new VoxelAccumulator();
                    grid[key] = acc;
                }

                acc.SumX += pt.Position.X;
                acc.SumY += pt.Position.Y;
                acc.SumZ += pt.Position.Z;

                acc.SumR += pt.Color.R;
                acc.SumG += pt.Color.G;
                acc.SumB += pt.Color.B;

                acc.SumNx += pt.Normal.X;
                acc.SumNy += pt.Normal.Y;
                acc.SumNz += pt.Normal.Z;

                acc.SumIntensity += pt.Intensity;
                acc.Count++;
            }

            var result = new PointCloud3D { Name = source.Name + "_Downsampled" };
            foreach (var kvp in grid)
            {
                var acc = kvp.Value;
                float invCount = 1f / acc.Count;

                var avgPos = new Vec3(acc.SumX * invCount, acc.SumY * invCount, acc.SumZ * invCount);
                var avgCol = new ColorRgb(acc.SumR * invCount, acc.SumG * invCount, acc.SumB * invCount);
                var avgNorm = new Vec3(acc.SumNx * invCount, acc.SumNy * invCount, acc.SumNz * invCount).Normalize();
                float avgIntensity = acc.SumIntensity * invCount;

                result.Add(new Point3D(avgPos, avgCol, avgNorm, avgIntensity));
            }

            result.ComputeBounds();
            return result;
        }
    }
}
