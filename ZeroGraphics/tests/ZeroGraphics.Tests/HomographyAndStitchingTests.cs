using System;
using System.Drawing;
using Xunit;
using ZeroGraphics.Imaging.Core;
using ZeroGraphics.Vision.Stitching;

namespace ZeroGraphics.Tests
{
    public class HomographyAndStitchingTests
    {
        [Fact]
        public void TestHomography_EstimateAndInvert()
        {
            // 4 points of a rectangle
            var src = new PointF[]
            {
                new PointF(0, 0),
                new PointF(100, 0),
                new PointF(100, 80),
                new PointF(0, 80)
            };

            // Transformed points (translated by +15, +25, and scaled by 1.2x)
            var dst = new PointF[]
            {
                new PointF(15, 25),
                new PointF(135, 25),
                new PointF(135, 121),
                new PointF(15, 121)
            };

            var h = Homography2D.Estimate(src, dst);
            Assert.NotNull(h);

            // Verify mapping accuracy
            for (int i = 0; i < 4; i++)
            {
                var mapped = h.TransformPoint(src[i]);
                Assert.Equal(dst[i].X, mapped.X, precision: 2);
                Assert.Equal(dst[i].Y, mapped.Y, precision: 2);
            }

            // Invert
            var invH = h.Invert();
            for (int i = 0; i < 4; i++)
            {
                var back = invH.TransformPoint(dst[i]);
                Assert.Equal(src[i].X, back.X, precision: 2);
                Assert.Equal(src[i].Y, back.Y, precision: 2);
            }
        }

        [Fact]
        public unsafe void TestPerspectiveWarper_PureTranslation()
        {
            using (var src = ImageBuffer.CreateGray8(50, 50))
            {
                src.Clear(0);

                // Set a 10x10 block [10..19, 10..19] to 200
                for (int y = 10; y < 20; y++)
                {
                    byte* row = src.GetRowPointer(y);
                    for (int x = 10; x < 20; x++) row[x] = 200;
                }

                // Homography that translates by (+10, +5)
                var h = new Homography2D(1, 0, 10, 0, 1, 5, 0, 0, 1);

                using (var warped = PerspectiveWarper.Warp(src, h, 60, 60))
                {
                    Assert.Equal(60, warped.Width);
                    Assert.Equal(60, warped.Height);

                    // Block should now be located at [20..29, 15..24]
                    byte* row = warped.GetRowPointer(20);
                    Assert.Equal(200, row[25]);

                    // Original location should now be 0
                    byte* origRow = warped.GetRowPointer(12);
                    Assert.Equal(0, origRow[12]);
                }
            }
        }

        [Fact]
        public unsafe void TestImageStitcher_PairBlending()
        {
            int w = 50;
            int h = 40;

            using (var camLeft = ImageBuffer.CreateGray8(w, h))
            using (var camRight = ImageBuffer.CreateGray8(w, h))
            {
                camLeft.Clear(100);
                camRight.Clear(180);

                // Camera 2 is shifted 35 pixels to the right of Camera 1 (15 pixels overlap)
                var hRightToBase = new Homography2D(1, 0, 35, 0, 1, 0, 0, 0, 1);

                using (var stitched = ImageStitcher.StitchPair(camLeft, camRight, hRightToBase))
                {
                    // Total width should be 50 + 35 = 85
                    Assert.Equal(85, stitched.Width);
                    Assert.Equal(h, stitched.Height);

                    // Left non-overlap region: near x = 10 should be 100
                    byte* row = stitched.GetRowPointer(20);
                    Assert.Equal(100, row[10]);

                    // Right non-overlap region: near x = 75 should be 180
                    Assert.Equal(180, row[75]);

                    // Overlap region near x = 42: blended between 100 and 180
                    Assert.InRange(row[42], 100, 180);
                }
            }
        }
    }
}
