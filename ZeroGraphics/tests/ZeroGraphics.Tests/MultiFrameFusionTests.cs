using System;
using System.Collections.Generic;
using Xunit;
using ZeroGraphics.Imaging.Core;
using ZeroGraphics.Imaging.Fusion;

namespace ZeroGraphics.Tests
{
    public class MultiFrameFusionTests
    {
        [Fact]
        public void ImagePyramid_DecompositionAndReconstruction_IsLossless()
        {
            int w = 64, h = 64;
            float[] original = new float[w * h * 4];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int p = (y * w + x) * 4;
                    original[p] = (float)x / w;
                    original[p + 1] = (float)y / h;
                    original[p + 2] = (float)(x + y) / (w + h);
                    original[p + 3] = 1.0f;
                }
            }

            var gauss = ImagePyramid.BuildGaussianPyramid4C(original, w, h, maxLevels: 4);
            Assert.True(gauss.Count >= 3);

            var lap = ImagePyramid.BuildLaplacianPyramid4C(gauss);
            Assert.Equal(gauss.Count, lap.Count);

            float[] reconstructed = ImagePyramid.ReconstructLaplacian4C(lap);
            Assert.Equal(original.Length, reconstructed.Length);

            // Verify mean absolute error is tiny (< 1e-4)
            double totalErr = 0.0;
            for (int i = 0; i < original.Length; i++)
            {
                totalErr += Math.Abs(original[i] - reconstructed[i]);
            }
            double mae = totalErr / original.Length;
            Assert.True(mae < 1e-4, $"Pyramid reconstruction error too large: MAE = {mae}");
        }

        [Fact]
        public void MertensExposureFusion_RecoversShadowsAndHighlights()
        {
            int w = 32, h = 32;

            // Frame 1: Underexposed (-2 EV)
            // Left half (shadows) = 0.02 (crushed dark)
            // Right half (highlights) = 0.45 (well-exposed)
            float[] under = new float[w * h * 4];

            // Frame 2: Overexposed (+2 EV)
            // Left half (shadows) = 0.50 (well-exposed)
            // Right half (highlights) = 0.98 (clipped bright)
            float[] over = new float[w * h * 4];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int p = (y * w + x) * 4;
                    bool isShadow = x < 16;

                    float underVal = isShadow ? 0.02f : 0.45f;
                    float overVal = isShadow ? 0.50f : 0.98f;

                    under[p] = underVal; under[p + 1] = underVal; under[p + 2] = underVal; under[p + 3] = 1.0f;
                    over[p] = overVal; over[p + 1] = overVal; over[p + 2] = overVal; over[p + 3] = 1.0f;
                }
            }

            var fused = MertensExposureFusion.FuseRgbaFloat(new[] { under, over }, w, h, maxLevels: 3);
            Assert.NotNull(fused);

            // Left side (shadows) should have taken the well-exposed information from 'over' (> 0.35)
            float fusedShadow = fused[(16 * w + 8) * 4];
            Assert.True(fusedShadow > 0.35f, $"Shadow area not recovered: {fusedShadow}");

            // Right side (highlights) should have taken the well-exposed information from 'under' (< 0.65)
            float fusedHighlight = fused[(16 * w + 24) * 4];
            Assert.True(fusedHighlight < 0.65f, $"Highlight area not recovered: {fusedHighlight}");
        }

        [Fact]
        public void PyramidFocusStacking_CombinesComplementarySharpZones()
        {
            int w = 32, h = 32;

            // Frame 1: Sharp high-contrast pattern on Left (x < 16), flat on Right (x >= 16)
            float[] frame1 = new float[w * h * 4];

            // Frame 2: Flat on Left (x < 16), sharp high-contrast pattern on Right (x >= 16)
            float[] frame2 = new float[w * h * 4];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int p = (y * w + x) * 4;
                    float pattern = ((x + y) % 2 == 0) ? 0.9f : 0.1f;
                    float flat = 0.5f;

                    float v1 = (x < 16) ? pattern : flat;
                    float v2 = (x < 16) ? flat : pattern;

                    frame1[p] = v1; frame1[p + 1] = v1; frame1[p + 2] = v1; frame1[p + 3] = 1.0f;
                    frame2[p] = v2; frame2[p + 1] = v2; frame2[p + 2] = v2; frame2[p + 3] = 1.0f;
                }
            }

            var fused = PyramidFocusStacking.FuseRgbaFloat(new[] { frame1, frame2 }, w, h, maxLevels: 3);
            Assert.NotNull(fused);

            // Test sharpness on Left half: check difference between adjacent pixels (4, 4) and (5, 4)
            float leftDiff = Math.Abs(fused[(4 * w + 4) * 4] - fused[(4 * w + 5) * 4]);
            Assert.True(leftDiff > 0.3f, $"Left sharp region not preserved: diff = {leftDiff}");

            // Test sharpness on Right half: check difference between adjacent pixels (20, 4) and (21, 4)
            float rightDiff = Math.Abs(fused[(4 * w + 20) * 4] - fused[(4 * w + 21) * 4]);
            Assert.True(rightDiff > 0.3f, $"Right sharp region not preserved: diff = {rightDiff}");
        }

        [Fact]
        public void ImageBuffer_FusionOverloads_ExecuteCorrectly()
        {
            var buf1 = new ImageBuffer(16, 16, ImageFormatMode.Bgra32);
            var buf2 = new ImageBuffer(16, 16, ImageFormatMode.Bgra32);

            var fusedExposure = MertensExposureFusion.Fuse(new[] { buf1, buf2 }, maxLevels: 2);
            Assert.NotNull(fusedExposure);
            Assert.Equal(16, fusedExposure.Width);
            Assert.Equal(16, fusedExposure.Height);

            var fusedFocus = PyramidFocusStacking.Fuse(new[] { buf1, buf2 }, maxLevels: 2);
            Assert.NotNull(fusedFocus);
            Assert.Equal(16, fusedFocus.Width);
            Assert.Equal(16, fusedFocus.Height);
        }
    }
}
