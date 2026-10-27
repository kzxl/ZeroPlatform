using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ZeroGraphics.Imaging.Core;

namespace ZeroGraphics.Imaging.Fusion
{
    /// <summary>
    /// Mertens Exposure Fusion (Mertens, Kautz, Van Reeth 2007).
    /// Blends bracketed exposure sequences into a high-dynamic-range composite without requiring camera response
    /// calibration or tone mapping. Evaluates Contrast, Saturation, and Well-Exposedness metrics, combined seamlessly
    /// across multiresolution Burt-Adelson Laplacian and Gaussian pyramids to eliminate halos and edge seam artifacts.
    /// </summary>
    public static class MertensExposureFusion
    {
        private const float DefaultSigma = 0.2f;
        private const float TwoSigmaSq = 2f * DefaultSigma * DefaultSigma; // 2 * 0.04 = 0.08
        private const float Epsilon = 1e-12f;

        /// <summary>
        /// Fuses a bracketed sequence of Bgra32 ImageBuffers into an exposure-fused composite.
        /// </summary>
        public static unsafe ImageBuffer Fuse(
            IReadOnlyList<ImageBuffer> images,
            float wContrast = 1.0f,
            float wSaturation = 1.0f,
            float wExposedness = 1.0f,
            int maxLevels = 6)
        {
            if (images == null || images.Count == 0)
                throw new ArgumentException("Image list cannot be empty.", nameof(images));

            if (images.Count == 1)
                return images[0].Clone();

            int w = images[0].Width;
            int h = images[0].Height;

            // Convert input buffers to normalized float RGBA arrays
            var floatImages = new List<float[]>(images.Count);
            for (int i = 0; i < images.Count; i++)
            {
                var img = images[i];
                if (img.Width != w || img.Height != h)
                    throw new ArgumentException($"Image {i} dimensions ({img.Width}x{img.Height}) do not match stack ({w}x{h}).");
                if (img.Format != ImageFormatMode.Bgra32)
                    throw new NotSupportedException("MertensExposureFusion requires Bgra32 ImageBuffers.");

                float[] px = new float[w * h * 4];
                Parallel.For(0, h, y =>
                {
                    byte* row = img.GetRowPointer(y);
                    int rowIdx = y * w * 4;
                    for (int x = 0; x < w; x++)
                    {
                        int bx = x * 4;
                        int fx = rowIdx + x * 4;
                        px[fx] = row[bx + 2] * (1f / 255f);     // R
                        px[fx + 1] = row[bx + 1] * (1f / 255f); // G
                        px[fx + 2] = row[bx] * (1f / 255f);     // B
                        px[fx + 3] = row[bx + 3] * (1f / 255f); // A
                    }
                });
                floatImages.Add(px);
            }

            float[] fusedFloat = FuseRgbaFloat(floatImages, w, h, wContrast, wSaturation, wExposedness, maxLevels);

            // Convert back to Bgra32 ImageBuffer
            var result = new ImageBuffer(w, h, ImageFormatMode.Bgra32);
            Parallel.For(0, h, y =>
            {
                byte* dRow = result.GetRowPointer(y);
                int rowIdx = y * w * 4;
                for (int x = 0; x < w; x++)
                {
                    int bx = x * 4;
                    int fx = rowIdx + x * 4;
                    dRow[bx + 2] = ClampByte((int)(fusedFloat[fx] * 255f + 0.5f));     // R
                    dRow[bx + 1] = ClampByte((int)(fusedFloat[fx + 1] * 255f + 0.5f)); // G
                    dRow[bx] = ClampByte((int)(fusedFloat[fx + 2] * 255f + 0.5f));     // B
                    dRow[bx + 3] = ClampByte((int)(fusedFloat[fx + 3] * 255f + 0.5f)); // A
                }
            });

            return result;
        }

        /// <summary>
        /// Fuses a bracketed sequence of interleaved RGBA float arrays [0..1] into an exposure-fused composite.
        /// </summary>
        public static float[] FuseRgbaFloat(
            IReadOnlyList<float[]> images,
            int w,
            int h,
            float wContrast = 1.0f,
            float wSaturation = 1.0f,
            float wExposedness = 1.0f,
            int maxLevels = 6)
        {
            if (images == null || images.Count == 0)
                throw new ArgumentException("Image list cannot be empty.", nameof(images));
            if (w <= 0 || h <= 0)
                throw new ArgumentException("Dimensions must be positive.");

            int nFrames = images.Count;
            if (nFrames == 1)
                return (float[])images[0].Clone();

            int nPixels = w * h;

            // 1. Calculate unnormalized weight maps
            float[][] weights = new float[nFrames][];
            for (int k = 0; k < nFrames; k++)
            {
                weights[k] = ComputeQualityWeight(images[k], w, h, wContrast, wSaturation, wExposedness);
            }

            // 2. Normalize weight maps per pixel so that sum_k W_k(p) == 1.0
            Parallel.For(0, nPixels, p =>
            {
                float sum = Epsilon;
                for (int k = 0; k < nFrames; k++)
                {
                    sum += weights[k][p];
                }

                float invSum = 1f / sum;
                for (int k = 0; k < nFrames; k++)
                {
                    weights[k][p] *= invSum;
                }
            });

            // 3. Construct Gaussian pyramids for weights & Laplacian pyramids for images
            // And accumulate into fused Laplacian pyramid: L_l(F) = sum_k G_l(W_k) * L_l(I_k)
            List<PyramidLevel4C>? fusedLaplacian = null;

            for (int k = 0; k < nFrames; k++)
            {
                // Build Gaussian pyramid for weight map k
                var weightGauss = ImagePyramid.BuildGaussianPyramid1C(weights[k], w, h, maxLevels);

                // Build Gaussian & Laplacian pyramid for image k
                var imageGauss = ImagePyramid.BuildGaussianPyramid4C(images[k], w, h, maxLevels);
                var imageLap = ImagePyramid.BuildLaplacianPyramid4C(imageGauss);

                int levelCount = imageLap.Count;

                if (fusedLaplacian == null)
                {
                    fusedLaplacian = new List<PyramidLevel4C>(levelCount);
                    for (int l = 0; l < levelCount; l++)
                    {
                        var lapLvl = imageLap[l];
                        var wtLvl = weightGauss[l];
                        float[] fusedData = new float[lapLvl.Data.Length];

                        Parallel.For(0, lapLvl.Width * lapLvl.Height, i =>
                        {
                            float wt = wtLvl.Data[i];
                            int p = i * 4;
                            fusedData[p] = lapLvl.Data[p] * wt;
                            fusedData[p + 1] = lapLvl.Data[p + 1] * wt;
                            fusedData[p + 2] = lapLvl.Data[p + 2] * wt;
                            fusedData[p + 3] = lapLvl.Data[p + 3] * wt;
                        });

                        fusedLaplacian.Add(new PyramidLevel4C(fusedData, lapLvl.Width, lapLvl.Height));
                    }
                }
                else
                {
                    for (int l = 0; l < levelCount; l++)
                    {
                        var lapLvl = imageLap[l];
                        var wtLvl = weightGauss[l];
                        var fusedLvl = fusedLaplacian[l];

                        Parallel.For(0, lapLvl.Width * lapLvl.Height, i =>
                        {
                            float wt = wtLvl.Data[i];
                            int p = i * 4;
                            fusedLvl.Data[p] += lapLvl.Data[p] * wt;
                            fusedLvl.Data[p + 1] += lapLvl.Data[p + 1] * wt;
                            fusedLvl.Data[p + 2] += lapLvl.Data[p + 2] * wt;
                            fusedLvl.Data[p + 3] += lapLvl.Data[p + 3] * wt;
                        });
                    }
                }
            }

            // 4. Reconstruct composite image from fused Laplacian pyramid
            float[] fused = ImagePyramid.ReconstructLaplacian4C(fusedLaplacian!);

            // Clamp results to valid bounds [0..1]
            Parallel.For(0, nPixels, i =>
            {
                int p = i * 4;
                fused[p] = MathCompat.Clamp(fused[p], 0f, 1f);
                fused[p + 1] = MathCompat.Clamp(fused[p + 1], 0f, 1f);
                fused[p + 2] = MathCompat.Clamp(fused[p + 2], 0f, 1f);
                fused[p + 3] = 1.0f;
            });

            return fused;
        }

        /// <summary>
        /// Computes pixel quality metric W = C^wC * S^wS * E^wE.
        /// </summary>
        private static float[] ComputeQualityWeight(
            float[] img,
            int w,
            int h,
            float wC,
            float wS,
            float wE)
        {
            float[] weights = new float[w * h];

            // 1. Calculate luminance plane for contrast
            float[] lum = new float[w * h];
            Parallel.For(0, h, y =>
            {
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    int p = (row + x) * 4;
                    lum[row + x] = 0.2126f * img[p] + 0.7152f * img[p + 1] + 0.0722f * img[p + 2];
                }
            });

            // 2. Evaluate metrics per pixel
            Parallel.For(0, h, y =>
            {
                int row = y * w;
                int rowPrev = Math.Max(0, y - 1) * w;
                int rowNext = Math.Min(h - 1, y + 1) * w;

                for (int x = 0; x < w; x++)
                {
                    int p = (row + x) * 4;
                    int xm = Math.Max(0, x - 1);
                    int xp = Math.Min(w - 1, x + 1);

                    float r = img[p];
                    float g = img[p + 1];
                    float b = img[p + 2];

                    // Contrast C: discrete Laplacian magnitude on luminance
                    float centerLum = lum[row + x];
                    float lapLum = MathF.Abs(
                        lum[rowPrev + x] +
                        lum[rowNext + x] +
                        lum[row + xm] +
                        lum[row + xp] -
                        4f * centerLum);

                    // Saturation S: standard deviation across RGB channels
                    float mean = (r + g + b) * (1f / 3f);
                    float dr = r - mean, dg = g - mean, db = b - mean;
                    float sat = MathF.Sqrt((dr * dr + dg * dg + db * db) * (1f / 3f));

                    // Well-Exposedness E: Gaussian distance to midtone (0.5)
                    float er = MathF.Exp(-((r - 0.5f) * (r - 0.5f)) / TwoSigmaSq);
                    float eg = MathF.Exp(-((g - 0.5f) * (g - 0.5f)) / TwoSigmaSq);
                    float eb = MathF.Exp(-((b - 0.5f) * (b - 0.5f)) / TwoSigmaSq);
                    float expMetric = er * eg * eb;

                    // Combine metrics with exponential weighting
                    float cTerm = (wC != 0f) ? MathF.Pow(lapLum + 1e-4f, wC) : 1f;
                    float sTerm = (wS != 0f) ? MathF.Pow(sat + 1e-4f, wS) : 1f;
                    float eTerm = (wE != 0f) ? MathF.Pow(expMetric + 1e-4f, wE) : 1f;

                    weights[row + x] = cTerm * sTerm * eTerm;
                }
            });

            return weights;
        }

        private static byte ClampByte(int val)
        {
            if (val < 0) return 0;
            if (val > 255) return 255;
            return (byte)val;
        }
    }
}
