using System;
using Xunit;
using ZeroGraphics.Imaging.Core;
using ZeroGraphics.Imaging.Filters;

namespace ZeroGraphics.Tests
{
    public class BayerDemosaicingTests
    {
        [Fact]
        public void BayerDemosaicing_DemosaicToGray8_ConvertsSensorSuccessfully()
        {
            int w = 64;
            int h = 64;

            using var bayer = new ImageBuffer(w, h, ImageFormatMode.BayerRG8);
            using var gray = ImageBuffer.CreateGray8(w, h);

            // Fill Bayer pattern with constant green response
            unsafe
            {
                for (int y = 0; y < h; y++)
                {
                    byte* row = bayer.GetRowPointer(y);
                    for (int x = 0; x < w; x++)
                    {
                        row[x] = 180;
                    }
                }
            }

            BayerDemosaicing.DemosaicToGray8(bayer, gray);

            unsafe
            {
                for (int y = 5; y < h - 5; y++)
                {
                    byte* row = gray.GetRowPointer(y);
                    for (int x = 5; x < w - 5; x++)
                    {
                        Assert.Equal(180, row[x]);
                    }
                }
            }
        }

        [Fact]
        public void BayerDemosaicing_DemosaicToBgra32_ReconstructsColorComponents()
        {
            int w = 32;
            int h = 32;

            using var bayer = new ImageBuffer(w, h, ImageFormatMode.BayerRG8);
            using var bgra = new ImageBuffer(w, h, ImageFormatMode.Bgra32);

            // Synthesize Pure Red patch (R=240, G=0, B=0)
            // BayerRG8: row 0: R G R G, row 1: G B G B
            unsafe
            {
                for (int y = 0; y < h; y++)
                {
                    byte* row = bayer.GetRowPointer(y);
                    bool isRedRow = (y % 2 == 0);

                    for (int x = 0; x < w; x++)
                    {
                        if (isRedRow && (x % 2 == 0))
                        {
                            row[x] = 240; // Red sensor pixel
                        }
                        else
                        {
                            row[x] = 0; // Green / Blue sensor pixels
                        }
                    }
                }
            }

            BayerDemosaicing.DemosaicToBgra32(bayer, bgra);

            // Check center pixel in reconstructed BGRA32 image
            unsafe
            {
                byte* center = bgra.GetRowPointer(16) + 16 * 4;
                byte b = center[0];
                byte g = center[1];
                byte r = center[2];
                byte a = center[3];

                Assert.True(r > 180, $"Red channel should be strongly preserved, got {r}");
                Assert.True(b < 50, $"Blue channel should remain low, got {b}");
                Assert.Equal(255, a);
            }
        }
    }
}
