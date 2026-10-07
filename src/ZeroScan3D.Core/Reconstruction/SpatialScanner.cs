using System;
using System.Collections.Generic;
using System.Diagnostics;
using ZeroGeometry.Core.PointCloud;
using ZeroScan3D.Core.Camera;
using ZeroScan3D.Core.Mapping;
using ZeroScan3D.Core.Meshing;
using ZeroScan3D.Core.Tracking;

namespace ZeroScan3D.Core.Reconstruction
{
    public sealed class ScanFrameResult
    {
        public bool Success { get; }
        public CameraPose Pose { get; }
        public int FramePoints { get; }
        public double TrackingRms { get; }
        public string? ErrorMessage { get; }

        public ScanFrameResult(
            bool success,
            CameraPose pose,
            int framePoints,
            double trackingRms,
            string? errorMessage = null)
        {
            Success = success;
            Pose = pose;
            FramePoints = framePoints;
            TrackingRms = trackingRms;
            ErrorMessage = errorMessage;
        }
    }

    /// <summary>
    /// Sovereign pure C# 3D spatial scanner orchestrator integrating camera frames, visual odometry,
    /// incremental voxel grid fusion, and optional continuous TSDF Marching Cubes mesh reconstruction.
    /// </summary>
    public sealed class SpatialScanner
    {
        private readonly CameraIntrinsics _intrinsics;
        private readonly ScannerOptions _options;
        private readonly VisualOdometryTracker _tracker;
        private readonly VoxelGridMap _voxelMap;
        private readonly TsdfVolume? _tsdfVolume;

        private int _framesProcessed;
        private long _totalPointsProcessed;
        private double _lastDurationMs;
        private double _lastRms;
        private bool _lastTrackingValid;

        public CameraIntrinsics Intrinsics => _intrinsics;
        public ScannerOptions Options => _options;
        public VisualOdometryTracker Tracker => _tracker;
        public VoxelGridMap VoxelMap => _voxelMap;
        public TsdfVolume? TsdfVolume => _tsdfVolume;

        public SpatialScanner(CameraIntrinsics intrinsics, ScannerOptions? options = null)
        {
            _intrinsics = intrinsics ?? throw new ArgumentNullException(nameof(intrinsics));
            _options = options ?? new ScannerOptions();

            _tracker = new VisualOdometryTracker
            {
                VoxelLeafSize = _options.TrackingVoxelLeaf
            };

            _voxelMap = new VoxelGridMap(_options.VoxelSize);

            if (_options.EnableTsdf)
            {
                _tsdfVolume = new TsdfVolume(
                    _options.TsdfDimX,
                    _options.TsdfDimY,
                    _options.TsdfDimZ,
                    _options.TsdfVoxelSize,
                    _options.TsdfTruncationDistance,
                    _options.TsdfOrigin);
            }
        }

        /// <summary>
        /// Processes an incoming synchronized RGB-D frame: unprojects 3D points, tracks camera pose, and integrates into the 3D map.
        /// </summary>
        public ScanFrameResult ProcessFrame(RgbdFrame frame)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));

            var sw = Stopwatch.StartNew();

            // 1. Unproject depth frame
            var frameCloud = DepthUnprojector.Unproject(
                frame,
                _intrinsics,
                _options.MinDepth,
                _options.MaxDepth,
                _options.UnprojectStride);

            if (frameCloud.Count == 0)
            {
                sw.Stop();
                _lastDurationMs = sw.Elapsed.TotalMilliseconds;
                _lastTrackingValid = false;
                return new ScanFrameResult(false, _tracker.CurrentPose, 0, double.MaxValue, "No valid depth points in sensor frame.");
            }

            // 2. Track motion (Visual Odometry)
            var trackResult = _tracker.Track(frameCloud);
            _lastRms = trackResult.RmsFitness;
            _lastTrackingValid = trackResult.Success;

            if (!trackResult.Success)
            {
                sw.Stop();
                _lastDurationMs = sw.Elapsed.TotalMilliseconds;
                return new ScanFrameResult(false, _tracker.CurrentPose, frameCloud.Count, trackResult.RmsFitness, trackResult.FailureReason);
            }

            var currentPose = trackResult.Pose;

            // 3. Integrate into Voxel Grid Map
            if (frame.HasColor)
            {
                var coloredPoints = DepthUnprojector.UnprojectColored(
                    frame,
                    _intrinsics,
                    _options.MinDepth,
                    _options.MaxDepth,
                    _options.UnprojectStride);
                _voxelMap.IntegrateColored(coloredPoints, currentPose);
            }
            else
            {
                _voxelMap.Integrate(frameCloud, currentPose);
            }

            // 4. Integrate into continuous TSDF Volume if enabled
            if (_tsdfVolume != null)
            {
                _tsdfVolume.Integrate(frame, _intrinsics, currentPose);
            }

            // 5. Update Telemetry
            _framesProcessed++;
            _totalPointsProcessed += frameCloud.Count;
            sw.Stop();
            _lastDurationMs = sw.Elapsed.TotalMilliseconds;

            return new ScanFrameResult(true, currentPose, frameCloud.Count, trackResult.RmsFitness);
        }

        /// <summary>
        /// Retrieves the current fused world-space 3D point cloud map.
        /// </summary>
        public PointCloud3D GetMapPointCloud(int minVoxelWeight = 1) => _voxelMap.ToPointCloud(minVoxelWeight);

        /// <summary>
        /// Retrieves the current fused colored world-space 3D points.
        /// </summary>
        public List<ColoredPoint3D> GetColoredMapPoints(int minVoxelWeight = 1) => _voxelMap.ToColoredPoints(minVoxelWeight);

        /// <summary>
        /// Extracts a smooth continuous 3D triangular surface mesh from the TSDF volume using Marching Cubes.
        /// </summary>
        public Mesh3D ExtractMesh(float isoLevel = 0.0f, float minWeight = 0.5f)
        {
            if (_tsdfVolume == null)
                throw new InvalidOperationException("TSDF reconstruction was not enabled in ScannerOptions.");

            return MarchingCubesExtractor.ExtractMesh(_tsdfVolume, isoLevel, minWeight);
        }

        /// <summary>
        /// Resets the scanner map, tracker, and TSDF volume.
        /// </summary>
        public void Reset(CameraPose? initialPose = null)
        {
            _tracker.Reset(initialPose);
            _voxelMap.Clear();
            _framesProcessed = 0;
            _totalPointsProcessed = 0;
        }

        /// <summary>
        /// Retrieves current real-time scanner statistics.
        /// </summary>
        public ScanStatistics GetStatistics()
        {
            return new ScanStatistics(
                _framesProcessed,
                _totalPointsProcessed,
                _voxelMap.VoxelCount,
                _tracker.CurrentPose,
                _lastRms,
                _lastTrackingValid,
                _lastDurationMs);
        }
    }
}
