using System;
using System.Collections.Generic;
using Xunit;
using ZeroGeometry.Core.PointCloud;
using ZeroGeometry.Core.Spatial;
using ZeroScan3D.Core.Tracking;

namespace ZeroScan3D.Tests
{
    public class CameraPoseAndTrackingTests
    {
        [Fact]
        public void CameraPose_Transform_And_InverseTransform_AreExactInverses()
        {
            var pose = CameraPose.FromEuler(0.1, 0.2, 0.3, new Point3D(1.5, -2.0, 3.5));
            var original = new Point3D(0.5, 1.2, -0.8);

            var world = pose.TransformPoint(original);
            var recovered = pose.InverseTransformPoint(world);

            Assert.InRange(recovered.X, original.X - 1e-6, original.X + 1e-6);
            Assert.InRange(recovered.Y, original.Y - 1e-6, original.Y + 1e-6);
            Assert.InRange(recovered.Z, original.Z - 1e-6, original.Z + 1e-6);
        }

        [Fact]
        public void CameraPose_Composition_MatchesChainedTransform()
        {
            var pose1 = new CameraPose(new Point3D(1.0, 0.0, 0.0));
            var pose2 = new CameraPose(new Point3D(0.0, 2.0, 0.0));

            var combined = pose1.Compose(pose2);
            var p = new Point3D(0, 0, 0);

            var res = combined.TransformPoint(p);
            Assert.Equal(1.0, res.X);
            Assert.Equal(2.0, res.Y);
            Assert.Equal(0.0, res.Z);
        }

        [Fact]
        public void VisualOdometryTracker_TracksStaticFrame()
        {
            var tracker = new VisualOdometryTracker { VoxelLeafSize = 0.02 };

            // Create a 3D box of points
            var points = new List<Point3D>();
            for (double x = -0.5; x <= 0.5; x += 0.1)
                for (double y = -0.5; y <= 0.5; y += 0.1)
                    for (double z = 1.0; z <= 2.0; z += 0.2)
                        points.Add(new Point3D(x, y, z));

            var cloud = new PointCloud3D(points);

            // Frame 1
            var r1 = tracker.Track(cloud);
            Assert.True(r1.Success);
            Assert.Equal(1, tracker.FrameCount);

            // Frame 2 (identical point cloud)
            var r2 = tracker.Track(cloud);
            Assert.True(r2.Success);
            Assert.InRange(r2.RmsFitness, 0.0, 0.01);
            Assert.InRange(tracker.CurrentPose.Translation.Length, 0.0, 0.02);
        }

        [Fact]
        public void VisualOdometryTracker_EstimatesCameraTranslation()
        {
            var tracker = new VisualOdometryTracker
            {
                VoxelLeafSize = 0.02,
                MaxCorrespondenceDistance = 0.3
            };

            // Synthetic 3D environment points to ensure non-degenerate 3D geometry
            var basePoints = new List<Point3D>();
            for (double x = -1.0; x <= 1.0; x += 0.2)
                for (double y = -1.0; y <= 1.0; y += 0.2)
                    for (double z = 1.0; z <= 2.5; z += 0.3)
                        basePoints.Add(new Point3D(x, y, z));

            var cloudFrame1 = new PointCloud3D(basePoints);

            // Frame 2: Camera shifted slightly by +0.05m along X, so points appear shifted by -0.05m in camera frame
            var frame2Points = new List<Point3D>();
            for (int i = 0; i < basePoints.Count; i++)
            {
                frame2Points.Add(new Point3D(basePoints[i].X - 0.05, basePoints[i].Y, basePoints[i].Z));
            }
            var cloudFrame2 = new PointCloud3D(frame2Points);

            var r1 = tracker.Track(cloudFrame1);
            Assert.True(r1.Success);

            var r2 = tracker.Track(cloudFrame2);
            Assert.True(r2.Success);

            // Estimated translation should recover the ~0.05m movement
            Assert.InRange(tracker.CurrentPose.Translation.X, 0.03, 0.07);
        }
    }
}
