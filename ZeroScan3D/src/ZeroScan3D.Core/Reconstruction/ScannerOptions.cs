using ZeroGeometry.Core.Spatial;

namespace ZeroScan3D.Core.Reconstruction
{
    /// <summary>
    /// Configuration parameters for SpatialScanner 3D mapping and SLAM.
    /// </summary>
    public sealed class ScannerOptions
    {
        /// <summary>
        /// Voxel resolution for the sparse spatial map in meters (default 0.02 = 2cm).
        /// </summary>
        public double VoxelSize { get; set; } = 0.02;

        /// <summary>
        /// Minimum valid sensor depth in meters (default 0.2m).
        /// </summary>
        public double MinDepth { get; set; } = 0.2;

        /// <summary>
        /// Maximum valid sensor depth in meters (default 5.0m).
        /// </summary>
        public double MaxDepth { get; set; } = 5.0;

        /// <summary>
        /// Pixel stride when unprojecting depth frame (1 = full resolution, 2 = 1/4 points for high speed).
        /// </summary>
        public int UnprojectStride { get; set; } = 2;

        /// <summary>
        /// Downsampling leaf size used for ICP visual odometry registration (meters).
        /// </summary>
        public double TrackingVoxelLeaf { get; set; } = 0.04;

        /// <summary>
        /// Enable continuous TSDF volume reconstruction for surface meshing.
        /// </summary>
        public bool EnableTsdf { get; set; } = false;

        public int TsdfDimX { get; set; } = 128;
        public int TsdfDimY { get; set; } = 128;
        public int TsdfDimZ { get; set; } = 128;
        public double TsdfVoxelSize { get; set; } = 0.02;
        public double TsdfTruncationDistance { get; set; } = 0.04;
        public Point3D TsdfOrigin { get; set; } = new Point3D(-1.28, -1.28, 0.0);
    }
}
