using System;
using ZeroGeometry.Core.PointCloud;
using ZeroGeometry.Core.Spatial;

namespace ZeroScan3D.Core.Tracking
{
    public sealed class TrackingResult
    {
        public bool Success { get; }
        public CameraPose Pose { get; }
        public double RmsFitness { get; }
        public int Iterations { get; }
        public bool IsKeyframe { get; }
        public string? FailureReason { get; }

        public TrackingResult(
            bool success,
            CameraPose pose,
            double rmsFitness,
            int iterations,
            bool isKeyframe = false,
            string? failureReason = null)
        {
            Success = success;
            Pose = pose;
            RmsFitness = rmsFitness;
            Iterations = iterations;
            IsKeyframe = isKeyframe;
            FailureReason = failureReason;
        }

        public static TrackingResult Failed(CameraPose lastKnownPose, string reason) =>
            new TrackingResult(false, lastKnownPose, double.MaxValue, 0, false, reason);
    }

    /// <summary>
    /// Real-time 6-DOF Visual Odometry tracker estimating incremental camera motion using scan-to-scan ICP.
    /// </summary>
    public sealed class VisualOdometryTracker
    {
        private PointCloud3D? _referenceCloud;
        private CameraPose _currentPose = CameraPose.Identity;
        private int _frameCount;

        public CameraPose CurrentPose => _currentPose;
        public bool IsInitialized => _referenceCloud != null;
        public int FrameCount => _frameCount;

        public double VoxelLeafSize { get; set; } = 0.03; // 3cm
        public double MaxCorrespondenceDistance { get; set; } = 0.3; // 30cm
        public double MaxAllowedRms { get; set; } = 0.15; // 15cm
        public int MaxIterations { get; set; } = 30;

        public VisualOdometryTracker(CameraPose? initialPose = null)
        {
            if (initialPose != null)
                _currentPose = initialPose;
        }

        /// <summary>
        /// Resets the tracker to initial state.
        /// </summary>
        public void Reset(CameraPose? initialPose = null)
        {
            _currentPose = initialPose ?? CameraPose.Identity;
            _referenceCloud = null;
            _frameCount = 0;
        }

        /// <summary>
        /// Tracks camera movement from incoming frame point cloud.
        /// </summary>
        public TrackingResult Track(PointCloud3D rawCloud)
        {
            if (rawCloud == null) throw new ArgumentNullException(nameof(rawCloud));
            if (rawCloud.Count < 20)
                return TrackingResult.Failed(_currentPose, "Insufficient points in frame (< 20 points).");

            var downsampled = VoxelLeafSize > 1e-4
                ? VoxelGridFilter.Downsample(rawCloud, VoxelLeafSize)
                : rawCloud;

            if (downsampled.Count < 10)
                return TrackingResult.Failed(_currentPose, "Insufficient downsampled points (< 10 points).");

            _frameCount++;

            // First frame initialization
            if (_referenceCloud == null)
            {
                _referenceCloud = downsampled;
                return new TrackingResult(true, _currentPose, 0.0, 0, isKeyframe: true);
            }

            // Align current frame to reference frame
            var icp = IcpRegistration.Align(
                source: downsampled,
                target: _referenceCloud,
                maxIterations: MaxIterations,
                tolerance: 1e-4,
                maxCorrespondenceDistance: MaxCorrespondenceDistance);

            if (icp.FitnessRms > MaxAllowedRms && !icp.Converged)
            {
                // Tracking lost
                return TrackingResult.Failed(_currentPose, $"Tracking lost: ICP RMS ({icp.FitnessRms:F3}m) exceeded threshold ({MaxAllowedRms:F3}m).");
            }

            // Relative transformation: T_step
            var relativePose = new CameraPose(icp.Rotation, icp.Translation);

            // Update global pose: T_world = T_world * T_step
            _currentPose = _currentPose.Compose(relativePose);

            // Update reference cloud
            _referenceCloud = downsampled;

            return new TrackingResult(true, _currentPose, icp.FitnessRms, icp.Iterations);
        }
    }
}
