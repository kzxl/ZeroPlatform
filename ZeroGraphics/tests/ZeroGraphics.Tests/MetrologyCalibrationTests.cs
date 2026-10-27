using System;
using System.Drawing;
using Xunit;
using ZeroGraphics.Imaging.Core;
using ZeroGraphics.Vision.Calibration;
using ZeroGraphics.Vision.Metrology;

namespace ZeroGraphics.Tests
{
    public class MetrologyCalibrationTests
    {
        [Fact]
        public void CameraIntrinsics_DistortAndUndistortPoint_InvertsAccurately()
        {
            // Camera with wide-angle lens (barrel distortion)
            var intrinsics = new CameraIntrinsics(
                fx: 800.0, fy: 800.0,
                cx: 640.0, cy: 480.0,
                k1: -0.15, k2: 0.03,
                p1: 0.001, p2: -0.0005);

            double originalU = 400.0;
            double originalV = 300.0;

            // Apply forward distortion
            intrinsics.DistortPoint(originalU, originalV, out double distU, out double distV);

            Assert.NotEqual(originalU, distU);
            Assert.NotEqual(originalV, distV);

            // Invert distortion via Newton-Raphson
            intrinsics.UndistortPoint(distU, distV, out double restoredU, out double restoredV);

            Assert.Equal(originalU, restoredU, 3); // Within 0.001 pixel accuracy
            Assert.Equal(originalV, restoredV, 3);
        }

        [Fact]
        public void LensUndistorter_RectifiesImageBuffer_WithoutErrors()
        {
            var intrinsics = new CameraIntrinsics(
                fx: 500.0, fy: 500.0,
                cx: 160.0, cy: 120.0,
                k1: -0.1, k2: 0.02);

            using var src = ImageBuffer.CreateGray8(320, 240);
            using var dst = ImageBuffer.CreateGray8(320, 240);

            src.Clear(128);

            // Draw a crosshair in the source
            unsafe
            {
                for (int x = 0; x < 320; x++) src.GetRowPointer(120)[x] = 255;
                for (int y = 0; y < 240; y++) src.GetRowPointer(y)[160] = 255;
            }

            LensUndistorter.CreateRectificationMap(src.Width, src.Height, intrinsics, out var mapX, out var mapY);
            Assert.Equal(320 * 240, mapX.Length);

            LensUndistorter.UndistortWithMap(src, dst, mapX, mapY);

            // Verify center pixel remains white
            unsafe
            {
                byte centerVal = dst.GetRowPointer(120)[160];
                Assert.True(centerVal > 200, "Center pixel of crosshair should be retained after undistortion.");
            }
        }

        [Fact]
        public void MetricCalibration2D_TransformsPixelToMillimeter_Accurately()
        {
            // 1 pixel = 0.050 mm (50 microns)
            var calib = MetricCalibration2D.FromScale(0.050, MetricUnit.Millimeters);

            calib.PixelToMetric(100.0, 200.0, out double mx, out double my);
            Assert.Equal(5.0, mx, 4);
            Assert.Equal(10.0, my, 4);

            calib.MetricToPixel(mx, my, out double px, out double py);
            Assert.Equal(100.0, px, 4);
            Assert.Equal(200.0, py, 4);

            // Test 2-point reference calibration
            PointF ref1 = new PointF(100, 100);
            PointF ref2 = new PointF(200, 100); // 100 pixels apart
            double knownLengthMm = 25.4; // 1 inch = 25.4 mm

            var calibFromPts = MetricCalibration2D.FromTwoPoints(ref1, ref2, knownLengthMm);
            Assert.Equal(0.254, calibFromPts.ScaleX, 4);

            double measured = calibFromPts.MeasureDistance(ref1, ref2);
            Assert.Equal(knownLengthMm, measured, 3);
        }

        [Fact]
        public void MetricMeasurement_PointToLineAndCircle_CalculatesTrueDimensions()
        {
            // 0.1 mm per pixel
            var calib = MetricCalibration2D.FromScale(0.1, MetricUnit.Millimeters);

            // 1. Circle diameter
            PointF center = new PointF(100, 100);
            double pixelRadius = 50.0; // 5.0 mm radius -> 10.0 mm diameter

            var circleRes = MetricMeasurement.MeasureCircle(center, pixelRadius, calib);
            Assert.Equal(5.0, circleRes.MetricRadius, 3);
            Assert.Equal(10.0, circleRes.MetricDiameter, 3);
            Assert.Contains("10.000 mm", circleRes.Text);

            // 2. Point to Line
            PointF lineA = new PointF(0, 50);
            PointF lineB = new PointF(200, 50); // horizontal line at Y = 50
            PointF point = new PointF(100, 150); // 100 pixels away perpendicular

            var ptToLineRes = MetricMeasurement.MeasurePointToLine(point, lineA, lineB, calib);
            Assert.Equal(100.0, ptToLineRes.PixelDistance, 3);
            Assert.Equal(10.0, ptToLineRes.MetricDistance, 3); // 100 * 0.1 = 10.0 mm
        }

        [Fact]
        public void MetricMeasurement_CaliperRake_DetectsPartThickness()
        {
            // 0.2 mm per pixel
            var calib = MetricCalibration2D.FromScale(0.2, MetricUnit.Millimeters);

            using var img = ImageBuffer.CreateGray8(200, 100);
            img.Clear(255); // White background

            // Draw a black manufactured bar from X=50 to X=150 (100 pixels thick = 20.0 mm)
            unsafe
            {
                for (int y = 20; y < 80; y++)
                {
                    byte* row = img.GetRowPointer(y);
                    for (int x = 50; x <= 150; x++)
                    {
                        row[x] = 0;
                    }
                }
            }

            // Scan caliper horizontally through the center of the part
            PointF start = new PointF(20, 50);
            PointF end = new PointF(180, 50);

            var widthRes = MetricMeasurement.MeasureWidthWithCaliper(img, start, end, calib);

            Assert.NotNull(widthRes);
            Assert.InRange(widthRes.PixelDistance, 98.0, 102.0);
            Assert.InRange(widthRes.MetricDistance, 19.6, 20.4);
            Assert.Contains("mm", widthRes.Text);
        }
    }
}
