using System;
using Xunit;
using ZeroGraphics.Imaging.Core;
using ZeroGraphics.Imaging.Filters;

namespace ZeroGraphics.Tests
{
    public class WaveletAndInpaintTests
    {
        [Fact]
        public void AtrousWaveletFilter_SuppressesNoiseWhilePreservingEdge()
        {
            int w = 32, h = 32;
            float[] pixels = new float[w * h * 4];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int p = (y * w + x) * 4;
                    // Step edge: left = 0.2, right = 0.8
                    float baseVal = (x < 16) ? 0.2f : 0.8f;
                    // Alternating high-frequency noise
                    float noise = ((x + y) % 2 == 0) ? 0.06f : -0.06f;
                    float v = baseVal + noise;

                    pixels[p] = v;
                    pixels[p + 1] = v;
                    pixels[p + 2] = v;
                    pixels[p + 3] = 1.0f;
                }
            }

            AtrousWaveletFilter.ApplyRgbaFloat(pixels, w, h, lumaStrength: 0.8f, chromaStrength: 0.5f, scales: 3);

            // Noise in flat left region should be significantly attenuated
            float p1 = pixels[(8 * w + 5) * 4];
            float p2 = pixels[(8 * w + 6) * 4];
            float noiseDiff = MathF.Abs(p1 - p2);
            Assert.True(noiseDiff < 0.04f, $"Noise not sufficiently suppressed: diff={noiseDiff}");

            // Step edge between x=14 and x=18 must remain sharp (> 0.5 step)
            float left = pixels[(16 * w + 13) * 4];
            float right = pixels[(16 * w + 18) * 4];
            Assert.True(right - left > 0.5f, $"Step edge blunted: {right - left}");
        }

        [Fact]
        public void FastMarchingInpaint_FillsHoleSeamlessly()
        {
            int w = 32, h = 32;
            float[] pixels = new float[w * h * 4];
            bool[] mask = new bool[w * h];

            // Horizontal gradient from 0.2 to 0.8
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int p = (y * w + x) * 4;
                    float v = 0.2f + 0.6f * ((float)x / w);
                    pixels[p] = v;
                    pixels[p + 1] = v;
                    pixels[p + 2] = v;
                    pixels[p + 3] = 1.0f;
                }
            }

            // Punch a 6x6 square hole centered at (16, 16)
            for (int y = 13; y <= 18; y++)
            {
                for (int x = 13; x <= 18; x++)
                {
                    int idx = y * w + x;
                    mask[idx] = true;
                    // Corrupt hole with zeros
                    pixels[idx * 4] = 0f;
                    pixels[idx * 4 + 1] = 0f;
                    pixels[idx * 4 + 2] = 0f;
                }
            }

            FastMarchingInpaint.InpaintRgbaFloat(pixels, w, h, mask, radius: 3);

            // The center of the hole at (16, 16) should be reconstructed close to gradient (~0.5)
            float centerVal = pixels[(16 * w + 16) * 4];
            Assert.True(centerVal > 0.35f && centerVal < 0.65f, $"Hole not reconstructed smoothly: {centerVal}");
        }

        [Fact]
        public void ImageBuffer_WaveletAndInpaint_OverloadsExecute()
        {
            var bufSrc = new ImageBuffer(16, 16, ImageFormatMode.Bgra32);
            var bufDst = new ImageBuffer(16, 16, ImageFormatMode.Bgra32);
            bool[] mask = new bool[16 * 16];
            mask[8 * 16 + 8] = true;

            AtrousWaveletFilter.Apply(bufSrc, bufDst, lumaStrength: 0.5f, chromaStrength: 0.5f, scales: 2);
            Assert.NotNull(bufDst);

            FastMarchingInpaint.Inpaint(bufSrc, bufDst, mask, radius: 2);
            Assert.NotNull(bufDst);
        }
    }
}
