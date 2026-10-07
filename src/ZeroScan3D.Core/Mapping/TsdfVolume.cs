using System;
using System.Threading.Tasks;
using ZeroGeometry.Core.Spatial;
using ZeroScan3D.Core.Camera;
using ZeroScan3D.Core.Tracking;

namespace ZeroScan3D.Core.Mapping
{
    /// <summary>
    /// Dense Truncated Signed Distance Field (TSDF) volumetric grid for continuous 3D surface reconstruction.
    /// </summary>
    public sealed class TsdfVolume
    {
        private readonly float[] _tsdf;
        private readonly float[] _weights;

        public int DimX { get; }
        public int DimY { get; }
        public int DimZ { get; }
        public double VoxelSize { get; }
        public double TruncationDistance { get; }
        public Point3D Origin { get; }
        public float MaxWeight { get; set; } = 100.0f;

        public TsdfVolume(
            int dimX,
            int dimY,
            int dimZ,
            double voxelSize,
            double truncationDistance,
            Point3D origin)
        {
            if (dimX <= 0) throw new ArgumentOutOfRangeException(nameof(dimX));
            if (dimY <= 0) throw new ArgumentOutOfRangeException(nameof(dimY));
            if (dimZ <= 0) throw new ArgumentOutOfRangeException(nameof(dimZ));
            if (voxelSize <= 1e-6) throw new ArgumentOutOfRangeException(nameof(voxelSize));
            if (truncationDistance <= 1e-6) throw new ArgumentOutOfRangeException(nameof(truncationDistance));

            DimX = dimX;
            DimY = dimY;
            DimZ = dimZ;
            VoxelSize = voxelSize;
            TruncationDistance = truncationDistance;
            Origin = origin;

            int total = dimX * dimY * dimZ;
            _tsdf = new float[total];
            _weights = new float[total];

            // Initialize TSDF to 1.0 (free space)
            for (int i = 0; i < total; i++)
            {
                _tsdf[i] = 1.0f;
                _weights[i] = 0.0f;
            }
        }

        private int GetIndex(int x, int y, int z) => (z * DimY + y) * DimX + x;

        public Point3D GetVoxelWorldPos(int x, int y, int z)
        {
            return new Point3D(
                Origin.X + (x + 0.5) * VoxelSize,
                Origin.Y + (y + 0.5) * VoxelSize,
                Origin.Z + (z + 0.5) * VoxelSize);
        }

        public bool GetVoxel(int x, int y, int z, out float tsdf, out float weight)
        {
            if (x < 0 || x >= DimX || y < 0 || y >= DimY || z < 0 || z >= DimZ)
            {
                tsdf = 1.0f;
                weight = 0.0f;
                return false;
            }

            int idx = GetIndex(x, y, z);
            tsdf = _tsdf[idx];
            weight = _weights[idx];
            return true;
        }

        public void SetVoxel(int x, int y, int z, float tsdf, float weight)
        {
            if (x >= 0 && x < DimX && y >= 0 && y < DimY && z >= 0 && z < DimZ)
            {
                int idx = GetIndex(x, y, z);
                _tsdf[idx] = tsdf;
                _weights[idx] = weight;
            }
        }

        /// <summary>
        /// Integrates an RGB-D depth frame into the TSDF volume using projective SDF integration.
        /// </summary>
        public void Integrate(RgbdFrame frame, CameraIntrinsics intrinsics, CameraPose pose)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            if (intrinsics == null) throw new ArgumentNullException(nameof(intrinsics));
            if (pose == null) throw new ArgumentNullException(nameof(pose));

            float truncDist = (float)TruncationDistance;
            float invTrunc = 1.0f / truncDist;
            double depthScale = intrinsics.DepthScale;

            // Parallelize across Z slices for high throughput
            Parallel.For(0, DimZ, z =>
            {
                for (int y = 0; y < DimY; y++)
                {
                    for (int x = 0; x < DimX; x++)
                    {
                        var pWorld = GetVoxelWorldPos(x, y, z);
                        var pCam = pose.InverseTransformPoint(pWorld);

                        if (pCam.Z <= 0.05)
                            continue; // Behind camera or closer than near clipping

                        if (!intrinsics.Project(pCam, out double u, out double v))
                            continue;

                        int iu = (int)Math.Round(u);
                        int iv = (int)Math.Round(v);

                        if (iu < 0 || iu >= frame.Width || iv < 0 || iv >= frame.Height)
                            continue;

                        float rawDepth = frame.GetDepth(iu, iv);
                        if (rawDepth <= 0f)
                            continue;

                        double dMeters = rawDepth * depthScale;
                        double sdf = dMeters - pCam.Z;

                        if (sdf >= -TruncationDistance)
                        {
                            float tsdfVal = (float)Math.Min(1.0, sdf * invTrunc);
                            int idx = GetIndex(x, y, z);

                            float oldWeight = _weights[idx];
                            float oldTsdf = _tsdf[idx];

                            float newWeight = Math.Min(oldWeight + 1.0f, MaxWeight);
                            float newTsdf = (oldTsdf * oldWeight + tsdfVal) / (oldWeight + 1.0f);

                            _tsdf[idx] = newTsdf;
                            _weights[idx] = newWeight;
                        }
                    }
                }
            });
        }
    }
}
