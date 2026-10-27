using ZeroScan3D.Core.Tracking;

namespace ZeroScan3D.Core.Reconstruction
{
    /// <summary>
    /// Real-time telemetry and statistics from active 3D scanning sessions.
    /// </summary>
    public sealed class ScanStatistics
    {
        public int FramesProcessed { get; }
        public long TotalPointsProcessed { get; }
        public int VoxelCount { get; }
        public CameraPose CurrentPose { get; }
        public double LastTrackingRms { get; }
        public bool IsTrackingValid { get; }
        public double LastFrameDurationMs { get; }

        public ScanStatistics(
            int framesProcessed,
            long totalPointsProcessed,
            int voxelCount,
            CameraPose currentPose,
            double lastTrackingRms,
            bool isTrackingValid,
            double lastFrameDurationMs)
        {
            FramesProcessed = framesProcessed;
            TotalPointsProcessed = totalPointsProcessed;
            VoxelCount = voxelCount;
            CurrentPose = currentPose;
            LastTrackingRms = lastTrackingRms;
            IsTrackingValid = isTrackingValid;
            LastFrameDurationMs = lastFrameDurationMs;
        }

        public override string ToString() =>
            $"Frames: {FramesProcessed} | Voxels: {VoxelCount} | Pose: {CurrentPose.Translation} | Valid: {IsTrackingValid} ({LastFrameDurationMs:F1}ms)";
    }
}
