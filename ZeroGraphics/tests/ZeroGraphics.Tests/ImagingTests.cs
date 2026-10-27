using System;
using System.Drawing;
using Xunit;
using ZeroGraphics.Imaging.Core;
using ZeroGraphics.Imaging.Filters;

namespace ZeroGraphics.Tests
{
    public class ImagingTests
    {
        [Fact]
        public void ImageBuffer_CreateAndBitmapInterop_WorksAccurately()
        {
            int width = 80;
            int height = 60;

            using (var buffer = ImageBuffer.CreateBgra32(width, height))
            {
                // Fill with Cyan: R=6, G=182, B=212
                buffer.Clear(6, 182, 212, 255);

                using (Bitmap bmp = buffer.ToBitmap())
                {
                    Assert.Equal(width, bmp.Width);
                    Assert.Equal(height, bmp.Height);

                    Color pixel = bmp.GetPixel(40, 30);
                    Assert.Equal(6, pixel.R);
                    Assert.Equal(182, pixel.G);
                    Assert.Equal(212, pixel.B);

                    // Test roundtrip from Bitmap
                    using (var roundtrip = ImageBuffer.FromBitmap(bmp))
                    {
                        Assert.Equal(buffer.Width, roundtrip.Width);
                        Assert.Equal(buffer.Height, roundtrip.Height);

                        unsafe
                        {
                            byte* p = roundtrip.GetRowPointer(30);
                            Assert.Equal(212, p[40 * 4]);     // B
                            Assert.Equal(182, p[40 * 4 + 1]); // G
                            Assert.Equal(6, p[40 * 4 + 2]);   // R
                        }
                    }
                }
            }
        }

        [Fact]
        public void ColorTransform_ToGrayscaleAndLut_MatchesSpecification()
        {
            using (var src = ImageBuffer.CreateBgra32(10, 10))
            using (var dst = ImageBuffer.CreateGray8(10, 10))
            {
                // Pure Red
                src.Clear(255, 0, 0, 255);
                ColorTransform.ToGrayscale(src, dst);
                unsafe
                {
                    // Fixed point ITU-R BT.709: (54 * 255) >> 8 = 53 or 54
                    Assert.InRange(dst.GetRowPointer(0)[0], 52, 55);
                }

                // Pure Green
                src.Clear(0, 255, 0, 255);
                ColorTransform.ToGrayscale(src, dst);
                unsafe
                {
                    // (183 * 255) >> 8 = 182
                    Assert.InRange(dst.GetRowPointer(0)[0], 180, 184);
                }

                // Test Invert
                using (var inverted = ImageBuffer.CreateGray8(10, 10))
                {
                    ColorTransform.Invert(dst, inverted);
                    unsafe
                    {
                        Assert.Equal((byte)(255 - dst.GetRowPointer(0)[0]), inverted.GetRowPointer(0)[0]);
                    }
                }
            }
        }

        [Fact]
        public void Thresholding_Otsu_FindsOptimalBimodalThreshold()
        {
            int width = 100;
            int height = 100;

            using (var gray = ImageBuffer.CreateGray8(width, height))
            using (var bin = ImageBuffer.CreateGray8(width, height))
            {
                // Create a bimodal image: Left half dark (val=30), Right half bright (val=210)
                unsafe
                {
                    for (int y = 0; y < height; y++)
                    {
                        byte* row = gray.GetRowPointer(y);
                        for (int x = 0; x < width; x++)
                        {
                            row[x] = x < 50 ? (byte)30 : (byte)210;
                        }
                    }
                }

                // Compute Otsu threshold
                byte threshold = Thresholding.OtsuBinarize(gray, bin);

                // Threshold should be located cleanly between the two peaks: 30 and 210
                Assert.InRange(threshold, 35, 205);

                // Verify binarization
                unsafe
                {
                    Assert.Equal(0, bin.GetRowPointer(50)[20]);    // Left side -> background (0)
                    Assert.Equal(255, bin.GetRowPointer(50)[80]);  // Right side -> foreground (255)
                }
            }
        }

        [Fact]
        public void Thresholding_AdaptiveBradley_HandlesNonUniformIllumination()
        {
            int width = 120;
            int height = 60;

            using (var gray = ImageBuffer.CreateGray8(width, height))
            using (var bin = ImageBuffer.CreateGray8(width, height))
            {
                // Create strong horizontal gradient (60 on left, 220 on right)
                unsafe
                {
                    for (int y = 0; y < height; y++)
                    {
                        byte* row = gray.GetRowPointer(y);
                        for (int x = 0; x < width; x++)
                        {
                            int background = 60 + (x * 160) / width;
                            row[x] = (byte)background;
                        }
                    }

                    // Place two dark barcode bars: one in dark region (x=20) and one in bright region (x=100)
                    for (int y = 10; y < 50; y++)
                    {
                        byte* row = gray.GetRowPointer(y);
                        // Dark bar at x=20 (background is ~86, bar is 20)
                        row[20] = 20;
                        // Dark bar at x=100 (background is ~193, bar is 80)
                        row[100] = 80;
                    }
                }

                Thresholding.AdaptiveThresholdBradley(gray, bin, windowRatio: 0.2f, percentage: 0.15f);

                // Both bars should be detected as black (0) despite the huge global lighting difference
                unsafe
                {
                    Assert.Equal(0, bin.GetRowPointer(30)[20]);
                    Assert.Equal(0, bin.GetRowPointer(30)[100]);

                    // Surrounding background should be white (255)
                    Assert.Equal(255, bin.GetRowPointer(30)[5]);
                    Assert.Equal(255, bin.GetRowPointer(30)[60]);
                    Assert.Equal(255, bin.GetRowPointer(30)[115]);
                }
            }
        }

        [Fact]
        public void Convolution_SeparableGaussian_SmoothsImpulseSymmetrically()
        {
            int width = 31;
            int height = 31;

            using (var src = ImageBuffer.CreateGray8(width, height))
            using (var dst = ImageBuffer.CreateGray8(width, height))
            {
                src.Clear(0);
                // Center impulse
                unsafe
                {
                    src.GetRowPointer(15)[15] = 255;
                }

                ConvolutionFilters.GaussianBlur(src, dst, sigma: 1.5f);

                unsafe
                {
                    byte center = dst.GetRowPointer(15)[15];
                    Assert.True(center > 0, "Center should have diffused energy.");

                    // Check horizontal symmetry
                    Assert.Equal(dst.GetRowPointer(15)[14], dst.GetRowPointer(15)[16]);
                    // Check vertical symmetry
                    Assert.Equal(dst.GetRowPointer(14)[15], dst.GetRowPointer(16)[15]);
                }
            }
        }

        [Fact]
        public void Convolution_Sobel_DetectsEdgesOfSolidShape()
        {
            int width = 50;
            int height = 50;

            using (var src = ImageBuffer.CreateGray8(width, height))
            using (var dst = ImageBuffer.CreateGray8(width, height))
            {
                src.Clear(0);

                // Draw solid white box inside: [15..35, 15..35]
                unsafe
                {
                    for (int y = 15; y <= 35; y++)
                    {
                        byte* row = src.GetRowPointer(y);
                        for (int x = 15; x <= 35; x++)
                        {
                            row[x] = 255;
                        }
                    }
                }

                ConvolutionFilters.SobelEdgeDetection(src, dst);

                unsafe
                {
                    // Center of box is uniform white -> Sobel gradient must be 0
                    Assert.Equal(0, dst.GetRowPointer(25)[25]);

                    // Far outside is uniform black -> Sobel gradient must be 0
                    Assert.Equal(0, dst.GetRowPointer(5)[5]);

                    // Boundary transition at x=15 should have very strong gradient
                    byte edgeMagnitude = dst.GetRowPointer(25)[15];
                    Assert.True(edgeMagnitude > 180, $"Edge gradient at boundary should be strong, got {edgeMagnitude}");
                }
            }
        }

        [Fact]
        public void Morphology_DilationAndErosion_ExpandsAndShrinksCorrectly()
        {
            int width = 30;
            int height = 30;

            using (var src = ImageBuffer.CreateGray8(width, height))
            using (var dilated = ImageBuffer.CreateGray8(width, height))
            using (var eroded = ImageBuffer.CreateGray8(width, height))
            {
                src.Clear(0);
                // Single white pixel at (15, 15)
                unsafe
                {
                    src.GetRowPointer(15)[15] = 255;
                }

                // Dilation with radius 1 should expand pixel to 3x3 square
                Morphology.Dilate(src, dilated, radius: 1);

                unsafe
                {
                    // Center and 8 neighbors must be 255
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            Assert.Equal(255, dilated.GetRowPointer(15 + dy)[15 + dx]);
                        }
                    }

                    // Pixel outside 3x3 should still be 0
                    Assert.Equal(0, dilated.GetRowPointer(15)[13]);
                    Assert.Equal(0, dilated.GetRowPointer(15)[17]);
                }

                // Erosion of that 3x3 square with radius 1 should shrink it back to the single pixel at (15, 15)
                Morphology.Erode(dilated, eroded, radius: 1);

                unsafe
                {
                    Assert.Equal(255, eroded.GetRowPointer(15)[15]);
                    Assert.Equal(0, eroded.GetRowPointer(15)[14]);
                    Assert.Equal(0, eroded.GetRowPointer(15)[16]);
                }
            }
        }
    }
}
