using System;
using System.Collections.Generic;
using Xunit;
using ZeroGraphics.Imaging.Core;
using ZeroGraphics.Vision.Blob;
using ZeroGraphics.Vision.Matching;
using ZeroGraphics.Vision.Metrology;

namespace ZeroGraphics.Tests
{
    public class VisionTests
    {
        [Fact]
        public void NccTemplateMatcher_FindsPatternWithSubPixelAccuracy()
        {
            int sw = 80;
            int sh = 80;
            int tw = 16;
            int th = 16;

            using (var search = ImageBuffer.CreateGray8(sw, sh))
            using (var template = ImageBuffer.CreateGray8(tw, th))
            {
                // Fill search with smooth gradient to provide realistic contrast
                unsafe
                {
                    for (int y = 0; y < sh; y++)
                    {
                        byte* row = search.GetRowPointer(y);
                        for (int x = 0; x < sw; x++)
                        {
                            row[x] = (byte)((x * 2 + y) % 100);
                        }
                    }

                    // Create template with distinct high-contrast cross pattern
                    template.Clear(30);
                    for (int y = 0; y < th; y++)
                    {
                        byte* row = template.GetRowPointer(y);
                        for (int x = 0; x < tw; x++)
                        {
                            if (x == 8 || y == 8 || (x >= 6 && x <= 10 && y >= 6 && y <= 10))
                            {
                                row[x] = 230; // Bright cross mark
                            }
                        }
                    }

                    // Stamp template into search image at target coordinates (24, 30)
                    int targetX = 24;
                    int targetY = 30;
                    for (int ty = 0; ty < th; ty++)
                    {
                        byte* srcRow = template.GetRowPointer(ty);
                        byte* dstRow = search.GetRowPointer(targetY + ty);
                        for (int tx = 0; tx < tw; tx++)
                        {
                            dstRow[targetX + tx] = srcRow[tx];
                        }
                    }
                }

                // Execute Normalized Cross-Correlation Template Matching
                var result = NccTemplateMatcher.Match(search, template, minScore: 0.85, subPixelRefinement: true);

                Assert.True(result.IsFound);
                Assert.True(result.Score > 0.95, $"Expected score > 0.95, got {result.Score:F4}");
                Assert.True(Math.Abs(result.X - 24.0) < 0.2, $"Expected X near 24.0, got {result.X:F3}");
                Assert.True(Math.Abs(result.Y - 30.0) < 0.2, $"Expected Y near 30.0, got {result.Y:F3}");
                Assert.Equal(tw, result.Width);
                Assert.Equal(th, result.Height);
            }
        }

        [Fact]
        public void PoseAligner_CalculatesAccurateRotationAndTranslation()
        {
            // Nominal fiducial marks on CAD layout
            var nom1 = new VisionPoint2D(20.0, 20.0);
            var nom2 = new VisionPoint2D(120.0, 20.0); // Horizontal distance = 100.0

            // Apply known transformation: Rotate 30 deg, translate by (15.5, -8.2)
            double angleDeg = 30.0;
            double rad = angleDeg * (Math.PI / 180.0);
            double cos = Math.Cos(rad);
            double sin = Math.Sin(rad);

            double transX = 15.5;
            double transY = -8.2;

            double nomMidX = (nom1.X + nom2.X) * 0.5;
            double nomMidY = (nom1.Y + nom2.Y) * 0.5;

            VisionPoint2D Transform(VisionPoint2D p)
            {
                double rx = cos * (p.X - nomMidX) - sin * (p.Y - nomMidY);
                double ry = sin * (p.X - nomMidX) + cos * (p.Y - nomMidY);
                return new VisionPoint2D(nomMidX + rx + transX, nomMidY + ry + transY);
            }

            var meas1 = Transform(nom1);
            var meas2 = Transform(nom2);

            var pose = PoseAligner.CalculatePose(nom1, nom2, meas1, meas2);

            Assert.True(Math.Abs(pose.AngleDegrees - angleDeg) < 0.001, $"Expected angle {angleDeg}, got {pose.AngleDegrees}");
            Assert.True(Math.Abs(pose.TranslationX - transX) < 0.001, $"Expected TransX {transX}, got {pose.TranslationX}");
            Assert.True(Math.Abs(pose.TranslationY - transY) < 0.001, $"Expected TransY {transY}, got {pose.TranslationY}");
            Assert.True(Math.Abs(pose.Scale - 1.0) < 0.001, $"Expected rigid scale 1.0, got {pose.Scale}");
        }

        [Fact]
        public void EdgeCaliper1D_DetectsSubPixelEdge()
        {
            using (var image = ImageBuffer.CreateGray8(100, 100))
            {
                // Create sharp step edge at x = 50.0: left = 30, right = 220
                unsafe
                {
                    for (int y = 0; y < 100; y++)
                    {
                        byte* row = image.GetRowPointer(y);
                        for (int x = 0; x < 100; x++)
                        {
                            row[x] = (byte)(x < 50 ? 30 : 220);
                        }
                    }
                }

                // Scan horizontal caliper rake from (20, 50) to (80, 50)
                var edge = EdgeCaliper1D.FindStrongestEdge(image, 20.0, 50.0, 80.0, 50.0, minMagnitude: 20.0);

                Assert.NotNull(edge);
                Assert.Equal(EdgePolarity.DarkToLight, edge.Value.Polarity);
                // Edge transition occurs between pixel 49 and 50 (sub-pixel ~49.5)
                Assert.True(Math.Abs(edge.Value.X - 49.5) < 0.7, $"Expected edge X ~49.5, got {edge.Value.X:F3}");
                Assert.True(Math.Abs(edge.Value.Y - 50.0) < 0.1, $"Expected edge Y ~50.0, got {edge.Value.Y:F3}");
                Assert.True(edge.Value.Magnitude > 80.0, $"Expected high gradient magnitude, got {edge.Value.Magnitude}");
            }
        }

        [Fact]
        public void GeometryFitters_FitsLineAndCircleWithSubPixelPrecision()
        {
            // 1. Line fitting test: points along line y = 1.5 * x + 10.0
            var linePoints = new List<VisionPoint2D>();
            for (int i = 0; i < 20; i++)
            {
                double x = 10.0 + i * 5.0;
                double y = 1.5 * x + 10.0;
                linePoints.Add(new VisionPoint2D(x, y));
            }

            var line = GeometryFitters.FitLine(linePoints);
            Assert.True(line.RmsError < 0.001, $"Expected zero line RMS error, got {line.RmsError}");
            // Normal angle tangent should match slope
            Assert.True(line.DistanceTo(20.0, 1.5 * 20.0 + 10.0) < 0.001);

            // 2. Circle fitting test: points along circle (cx=60, cy=45, R=30)
            double trueCx = 60.0;
            double trueCy = 45.0;
            double trueR = 30.0;

            var circlePoints = new List<VisionPoint2D>();
            for (int deg = 0; deg < 360; deg += 15)
            {
                double rad = deg * (Math.PI / 180.0);
                double x = trueCx + trueR * Math.Cos(rad);
                double y = trueCy + trueR * Math.Sin(rad);
                circlePoints.Add(new VisionPoint2D(x, y));
            }

            var circle = GeometryFitters.FitCircle(circlePoints);
            Assert.True(Math.Abs(circle.CenterX - trueCx) < 0.01, $"Expected CenterX {trueCx}, got {circle.CenterX}");
            Assert.True(Math.Abs(circle.CenterY - trueCy) < 0.01, $"Expected CenterY {trueCy}, got {circle.CenterY}");
            Assert.True(Math.Abs(circle.Radius - trueR) < 0.01, $"Expected Radius {trueR}, got {circle.Radius}");
            Assert.True(circle.RmsError < 0.01, $"Expected small circle RMS error, got {circle.RmsError}");

            // 3. Concentricity measurement test
            var circleInner = new FittedCircle2D(60.1, 44.9, 15.0, 0.01);
            double concentricity = MetrologyMeasurements.Concentricity(circle, circleInner);
            Assert.True(Math.Abs(concentricity - Math.Sqrt(0.1 * 0.1 + 0.1 * 0.1)) < 0.02);
        }

        [Fact]
        public void BlobAnalyzer_SegmentsBlobsAndCalculatesMetrics()
        {
            using (var image = ImageBuffer.CreateGray8(120, 120))
            {
                image.Clear(0); // Black background

                // Object 1: 10x10 square at (10, 10) -> Area = 100, Centroid = (14.5, 14.5)
                // Object 2: 20x20 square at (50, 50) -> Area = 400, Centroid = (59.5, 59.5)
                unsafe
                {
                    for (int y = 10; y < 20; y++)
                    {
                        byte* row = image.GetRowPointer(y);
                        for (int x = 10; x < 20; x++) row[x] = 255;
                    }

                    for (int y = 50; y < 70; y++)
                    {
                        byte* row = image.GetRowPointer(y);
                        for (int x = 50; x < 70; x++) row[x] = 255;
                    }
                }

                var blobs = BlobAnalyzer.ExtractBlobs(image, threshold: 128, minArea: 50);

                Assert.Equal(2, blobs.Count);

                // Sorted by Area descending: Blob 0 is the 20x20 square, Blob 1 is the 10x10 square
                var blobBig = blobs[0];
                Assert.Equal(400, blobBig.Area);
                Assert.True(Math.Abs(blobBig.CentroidX - 59.5) < 0.01);
                Assert.True(Math.Abs(blobBig.CentroidY - 59.5) < 0.01);
                Assert.Equal(20, blobBig.Width);
                Assert.Equal(20, blobBig.Height);

                var blobSmall = blobs[1];
                Assert.Equal(100, blobSmall.Area);
                Assert.True(Math.Abs(blobSmall.CentroidX - 14.5) < 0.01);
                Assert.True(Math.Abs(blobSmall.CentroidY - 14.5) < 0.01);
                Assert.Equal(10, blobSmall.Width);
                Assert.Equal(10, blobSmall.Height);
            }
        }
    }
}
