using System;
using System.Collections.Generic;
using Xunit;
using ZeroGraphics.Imaging.Core;
using ZeroGraphics.Vision.Blob;
using ZeroGraphics.Vision.Matching;
using ZeroGraphics.Vision.Metrology;

namespace ZeroGraphics.Tests
{
    public class MetrologyExpansionTests
    {
        [Fact]
        public void RansacLineFitter_RejectsSevereOutliersAndRecoversTrueLine()
        {
            // Ground-truth line: y = 2x + 10  => 2x - y + 10 = 0
            var points = new List<VisionPoint2D>();
            var rng = new Random(12345);

            // 60 inliers along the line with minor Gaussian noise
            for (int i = 0; i < 60; i++)
            {
                double x = i * 2.0;
                double y = 2.0 * x + 10.0 + (rng.NextDouble() - 0.5) * 0.4;
                points.Add(new VisionPoint2D(x, y));
            }

            // 40 random outliers scattered far away
            for (int i = 0; i < 40; i++)
            {
                double x = rng.NextDouble() * 120.0;
                double y = rng.NextDouble() * 500.0 - 100.0;
                points.Add(new VisionPoint2D(x, y));
            }

            var result = RansacFitter.FitLineRansac(points, distanceThreshold: 2.0, maxIterations: 100, minInlierRatio: 0.5, seed: 42);

            Assert.True(result.InlierCount >= 58, $"Expected >= 58 inliers, got {result.InlierCount}");
            Assert.True(result.InlierRatio >= 0.58);

            // True line angle = atan(2) ~= 63.43 degrees
            double expectedAngle = Math.Atan(2.0) * (180.0 / Math.PI);
            Assert.InRange(result.Model.AngleDegrees, expectedAngle - 1.5, expectedAngle + 1.5);
            Assert.True(result.Model.RmsError < 0.5, $"Inlier RMS error should be tiny, got {result.Model.RmsError}");
        }

        [Fact]
        public void RansacCircleFitter_RejectsOutliersAndFindsTrueRadius()
        {
            double cx = 100.0, cy = 100.0, r = 50.0;
            var points = new List<VisionPoint2D>();
            var rng = new Random(54321);

            // 50 points on the circle
            for (int i = 0; i < 50; i++)
            {
                double angle = (2.0 * Math.PI * i) / 50;
                double noisyR = r + (rng.NextDouble() - 0.5) * 0.5;
                points.Add(new VisionPoint2D(cx + noisyR * Math.Cos(angle), cy + noisyR * Math.Sin(angle)));
            }

            // 20 outlier points
            for (int i = 0; i < 20; i++)
            {
                points.Add(new VisionPoint2D(rng.NextDouble() * 200, rng.NextDouble() * 200));
            }

            var result = RansacFitter.FitCircleRansac(points, distanceThreshold: 1.0, maxIterations: 150, minInlierRatio: 0.5, seed: 99);

            Assert.True(result.InlierCount >= 48);
            Assert.InRange(result.Model.CenterX, cx - 1.0, cx + 1.0);
            Assert.InRange(result.Model.CenterY, cy - 1.0, cy + 1.0);
            Assert.InRange(result.Model.Radius, r - 1.0, r + 1.0);
            Assert.True(result.Model.RmsError < 0.5);
        }

        [Fact]
        public void FitzgibbonEllipseFitter_RecoversAccurateAxesAndCenter()
        {
            double cx = 120.0, cy = 80.0;
            double a = 60.0; // semi-major
            double b = 25.0; // semi-minor
            double rotDeg = 30.0;
            double rotRad = rotDeg * (Math.PI / 180.0);

            var points = new List<VisionPoint2D>();
            for (int i = 0; i < 36; i++)
            {
                double t = (2.0 * Math.PI * i) / 36.0;
                double xLocal = a * Math.Cos(t);
                double yLocal = b * Math.Sin(t);

                // Rotate and translate
                double x = cx + xLocal * Math.Cos(rotRad) - yLocal * Math.Sin(rotRad);
                double y = cy + xLocal * Math.Sin(rotRad) + yLocal * Math.Cos(rotRad);
                points.Add(new VisionPoint2D(x, y));
            }

            var ellipse = GeometryFitters.FitEllipse(points);

            Assert.InRange(ellipse.CenterX, cx - 0.5, cx + 0.5);
            Assert.InRange(ellipse.CenterY, cy - 0.5, cy + 0.5);
            Assert.InRange(ellipse.SemiMajorAxis, a - 0.5, a + 0.5);
            Assert.InRange(ellipse.SemiMinorAxis, b - 0.5, b + 0.5);
            Assert.InRange(ellipse.AngleDegrees, rotDeg - 1.0, rotDeg + 1.0);
            Assert.True(ellipse.RmsError < 0.1);
        }

        [Fact]
        public void ConvexHullAndObb_ComputesMinimumAreaBoxAndOrientation()
        {
            // Create rotated rectangle points: Center=(50, 50), W=80, H=20, Angle=45 deg
            double cx = 50.0, cy = 50.0, w = 80.0, h = 20.0;
            double angleDeg = 45.0;
            var expectedRect = new RotatedRect2D(cx, cy, w, h, angleDeg);

            var vertices = expectedRect.GetVertices();
            var points = new List<VisionPoint2D>(vertices);

            // Add interior noisy points that shouldn't affect convex hull or OBB
            points.Add(new VisionPoint2D(50, 50));
            points.Add(new VisionPoint2D(52, 48));

            var hull = ConvexHull2D.ComputeConvexHull(points);
            Assert.Equal(4, hull.Length);

            var obb = ConvexHull2D.ComputeMinimumAreaBoundingBox(points);

            Assert.InRange(obb.CenterX, cx - 1.0, cx + 1.0);
            Assert.InRange(obb.CenterY, cy - 1.0, cy + 1.0);

            // Dimensions might be swapped depending on axis orientation
            double dim1 = Math.Max(obb.Width, obb.Height);
            double dim2 = Math.Min(obb.Width, obb.Height);
            Assert.InRange(dim1, w - 1.0, w + 1.0);
            Assert.InRange(dim2, h - 1.0, h + 1.0);
            Assert.InRange(obb.Area, (w * h) - 5.0, (w * h) + 5.0);

            // Contains check
            Assert.True(obb.Contains(50, 50));
            Assert.False(obb.Contains(150, 150));
        }

        [Fact]
        public void GdtEvaluator_CalculatesStraightnessAndIntersection()
        {
            // Line 1: y = 50 (horizontal line)
            var l1Points = new List<VisionPoint2D>
            {
                new VisionPoint2D(0, 50.2),
                new VisionPoint2D(50, 49.8),
                new VisionPoint2D(100, 50.1)
            };
            var line1 = GeometryFitters.FitLine(l1Points);
            double straightness = GdtEvaluator.CalculateStraightness(l1Points, line1);
            Assert.InRange(straightness, 0.35, 0.45);

            // Line 2: x = 50 (vertical line)
            var l2Points = new List<VisionPoint2D>
            {
                new VisionPoint2D(50.0, 0),
                new VisionPoint2D(50.0, 100)
            };
            var line2 = GeometryFitters.FitLine(l2Points);

            bool intersects = GdtEvaluator.IntersectLines(line1, line2, out var pt);
            Assert.True(intersects);
            Assert.InRange(pt.X, 49.5, 50.5);
            Assert.InRange(pt.Y, 49.5, 50.5);

            double perpError = GdtEvaluator.PerpendicularityError(line1, line2);
            Assert.True(perpError < 1.0, $"Lines should be perpendicular, got error {perpError}°");
        }

        [Fact]
        public void BlobAnalyzer_ExtractsBlobsWithOrientedBoundingBoxes()
        {
            int w = 60, h = 60;
            using (var img = ImageBuffer.CreateGray8(w, h))
            {
                unsafe
                {
                    // Draw a solid diagonal rectangle (20x10) from (20,20) to (40,30)
                    for (int y = 20; y < 35; y++)
                    {
                        byte* row = img.GetRowPointer(y);
                        for (int x = 20; x < 45; x++)
                        {
                            row[x] = 255;
                        }
                    }
                }

                var blobs = BlobAnalyzer.ExtractBlobs(img, threshold: 128, minArea: 50, computeOrientedBox: true);

                Assert.Single(blobs);
                var blob = blobs[0];

                Assert.True(blob.Area > 200);
                Assert.True(blob.OrientedBox.Width > 0);
                Assert.True(blob.OrientedBox.Height > 0);
                Assert.True(blob.OrientedBox.Contains(blob.CentroidX, blob.CentroidY));
            }
        }
    }
}
