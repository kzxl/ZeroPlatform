using System;
using System.Collections.Generic;
using ZeroGeometry.Core.Spatial;

namespace ZeroGeometry.Core.PointCloud
{
    /// <summary>
    /// Represents a 3D point cloud with spatial bounds and rigid body transformation support.
    /// </summary>
    public sealed class PointCloud3D
    {
        private readonly List<Point3D> _points;

        public IReadOnlyList<Point3D> Points => _points;
        public int Count => _points.Count;

        public Point3D MinBound { get; private set; }
        public Point3D MaxBound { get; private set; }

        public PointCloud3D(IEnumerable<Point3D> points)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            _points = new List<Point3D>(points);
            ComputeBounds();
        }

        public Point3D this[int index] => _points[index];

        private void ComputeBounds()
        {
            if (_points.Count == 0)
            {
                MinBound = default;
                MaxBound = default;
                return;
            }

            double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;

            for (int i = 0; i < _points.Count; i++)
            {
                var p = _points[i];
                if (p.X < minX) minX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.Z < minZ) minZ = p.Z;
                if (p.X > maxX) maxX = p.X;
                if (p.Y > maxY) maxY = p.Y;
                if (p.Z > maxZ) maxZ = p.Z;
            }

            MinBound = new Point3D(minX, minY, minZ);
            MaxBound = new Point3D(maxX, maxY, maxZ);
        }

        /// <summary>
        /// Transforms point cloud by 3x3 rotation matrix and 3D translation vector.
        /// </summary>
        public PointCloud3D Transform(double[,] R, Point3D t)
        {
            var transformed = new Point3D[_points.Count];
            for (int i = 0; i < _points.Count; i++)
            {
                var p = _points[i];
                double x = R[0, 0] * p.X + R[0, 1] * p.Y + R[0, 2] * p.Z + t.X;
                double y = R[1, 0] * p.X + R[1, 1] * p.Y + R[1, 2] * p.Z + t.Y;
                double z = R[2, 0] * p.X + R[2, 1] * p.Y + R[2, 2] * p.Z + t.Z;
                transformed[i] = new Point3D(x, y, z);
            }
            return new PointCloud3D(transformed);
        }

        public PointCloud3D Clone() => new PointCloud3D(_points);
    }

    /// <summary>
    /// Voxel Grid filter for spatial downsampling of large 3D point clouds to uniform point density.
    /// </summary>
    public static class VoxelGridFilter
    {
        private readonly struct VoxelCoord : IEquatable<VoxelCoord>
        {
            public int X { get; }
            public int Y { get; }
            public int Z { get; }

            public VoxelCoord(int x, int y, int z)
            {
                X = x;
                Y = y;
                Z = z;
            }

            public bool Equals(VoxelCoord other) => X == other.X && Y == other.Y && Z == other.Z;
            public override bool Equals(object? obj) => obj is VoxelCoord other && Equals(other);
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
        }

        private sealed class VoxelAccumulator
        {
            public double SumX;
            public double SumY;
            public double SumZ;
            public int Count;
        }

        public static PointCloud3D Downsample(PointCloud3D cloud, double leafSize)
        {
            if (cloud == null) throw new ArgumentNullException(nameof(cloud));
            if (leafSize <= 1e-9) throw new ArgumentOutOfRangeException(nameof(leafSize), "Leaf size must be positive.");
            if (cloud.Count == 0) return new PointCloud3D(Array.Empty<Point3D>());

            double invLeaf = 1.0 / leafSize;
            var voxels = new Dictionary<VoxelCoord, VoxelAccumulator>();

            for (int i = 0; i < cloud.Count; i++)
            {
                var p = cloud[i];
                int vx = (int)Math.Floor(p.X * invLeaf);
                int vy = (int)Math.Floor(p.Y * invLeaf);
                int vz = (int)Math.Floor(p.Z * invLeaf);

                var coord = new VoxelCoord(vx, vy, vz);
                if (!voxels.TryGetValue(coord, out var accum))
                {
                    accum = new VoxelAccumulator();
                    voxels[coord] = accum;
                }

                accum.SumX += p.X;
                accum.SumY += p.Y;
                accum.SumZ += p.Z;
                accum.Count++;
            }

            var downsampled = new List<Point3D>(voxels.Count);
            foreach (var kvp in voxels)
            {
                var a = kvp.Value;
                downsampled.Add(new Point3D(a.SumX / a.Count, a.SumY / a.Count, a.SumZ / a.Count));
            }

            return new PointCloud3D(downsampled);
        }
    }

    /// <summary>
    /// Statistical Outlier Removal (SOR) filter for eliminating sensor noise and isolated scatter points.
    /// </summary>
    public static class StatisticalOutlierRemoval
    {
        public static PointCloud3D Filter(PointCloud3D cloud, int kNeighbors = 10, double stdDevMultiplier = 1.0)
        {
            if (cloud == null) throw new ArgumentNullException(nameof(cloud));
            if (cloud.Count < kNeighbors + 1) return cloud.Clone();

            var tree = new KdTree3D(cloud.Points);
            double[] meanDistances = new double[cloud.Count];

            double totalMean = 0.0;
            for (int i = 0; i < cloud.Count; i++)
            {
                var knn = tree.KNearestNeighbors(cloud[i], kNeighbors + 1); // +1 because self is included
                double sumDist = 0.0;
                int count = 0;
                for (int k = 0; k < knn.Count; k++)
                {
                    if (knn[k].index != i)
                    {
                        sumDist += Math.Sqrt(knn[k].distSq);
                        count++;
                    }
                }
                double avg = count > 0 ? sumDist / count : 0.0;
                meanDistances[i] = avg;
                totalMean += avg;
            }

            double globalMean = totalMean / cloud.Count;

            double sumVariance = 0.0;
            for (int i = 0; i < cloud.Count; i++)
            {
                double diff = meanDistances[i] - globalMean;
                sumVariance += diff * diff;
            }
            double globalStd = Math.Sqrt(sumVariance / cloud.Count);

            double threshold = globalMean + stdDevMultiplier * globalStd;

            var inliers = new List<Point3D>();
            for (int i = 0; i < cloud.Count; i++)
            {
                if (meanDistances[i] <= threshold)
                {
                    inliers.Add(cloud[i]);
                }
            }

            return new PointCloud3D(inliers);
        }
    }
}
