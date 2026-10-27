using System;
using Xunit;
using ZeroGraphics.Core.Imaging;

namespace ZeroGraphics.Tests
{
    public class LaplacePdeInpainterTests
    {
        [Fact]
        public void InpaintRgba_UniformWhiteWithCorruptedHole_RestoresWhiteHole()
        {
            int w = 8, h = 8;
            var rgba = new float[w * h * 4];
            var mask = new bool[w * h];

            // Fill image with 1.0f (white)
            for (int i = 0; i < rgba.Length; i += 4)
            {
                rgba[i] = 1.0f;
                rgba[i + 1] = 1.0f;
                rgba[i + 2] = 1.0f;
                rgba[i + 3] = 1.0f;
            }

            // Corrupt center 2x2 (x: 3..4, y: 3..4) with 0.0f (black) and mark mask
            for (int y = 3; y <= 4; y++)
            {
                for (int x = 3; x <= 4; x++)
                {
                    int idx = y * w + x;
                    mask[idx] = true;
                    rgba[idx * 4] = 0.0f;
                    rgba[idx * 4 + 1] = 0.0f;
                    rgba[idx * 4 + 2] = 0.0f;
                }
            }

            LaplacePdeInpainter.InpaintRgba(rgba, w, h, mask, iterations: 20);

            // Verify center pixels were restored close to 1.0f
            for (int y = 3; y <= 4; y++)
            {
                for (int x = 3; x <= 4; x++)
                {
                    int idx = y * w + x;
                    Assert.InRange(rgba[idx * 4], 0.95f, 1.05f);
                    Assert.InRange(rgba[idx * 4 + 1], 0.95f, 1.05f);
                    Assert.InRange(rgba[idx * 4 + 2], 0.95f, 1.05f);
                }
            }
        }

        [Fact]
        public void InpaintSpotRgba_CircularBlemish_RestoresAmbientColor()
        {
            int w = 12, h = 12;
            var rgba = new float[w * h * 4];

            // Fill with 0.6f gray
            for (int i = 0; i < rgba.Length; i += 4)
            {
                rgba[i] = 0.6f;
                rgba[i + 1] = 0.6f;
                rgba[i + 2] = 0.6f;
                rgba[i + 3] = 1.0f;
            }

            // Inpaint spot at center (6, 6) with radius 2
            LaplacePdeInpainter.InpaintSpotRgba(rgba, w, h, centerX: 6, centerY: 6, radius: 2, iterations: 15);

            int centerIdx = (6 * w + 6) * 4;
            Assert.InRange(rgba[centerIdx], 0.58f, 0.62f);
        }

        [Fact]
        public void InpaintBgra32_ByteBitmap_DiffusesSeamlessly()
        {
            int w = 8, h = 8;
            var bgra = new byte[w * h * 4];
            var mask = new bool[w * h];

            // Pure Blue: B=255, G=0, R=0, A=255
            for (int i = 0; i < bgra.Length; i += 4)
            {
                bgra[i] = 255;
                bgra[i + 1] = 0;
                bgra[i + 2] = 0;
                bgra[i + 3] = 255;
            }

            // Hole in center (3,3)
            int holeIdx = 3 * w + 3;
            mask[holeIdx] = true;
            bgra[holeIdx * 4] = 0;
            bgra[holeIdx * 4 + 1] = 0;
            bgra[holeIdx * 4 + 2] = 0;

            LaplacePdeInpainter.InpaintBgra32(bgra, w, h, mask, iterations: 20);

            // Check B channel is restored to ~255 and R/G remain ~0
            Assert.True(bgra[holeIdx * 4] >= 250);
            Assert.True(bgra[holeIdx * 4 + 1] <= 5);
            Assert.True(bgra[holeIdx * 4 + 2] <= 5);
        }
    }
}
