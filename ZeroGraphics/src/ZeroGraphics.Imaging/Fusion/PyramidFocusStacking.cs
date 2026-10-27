using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ZeroGraphics.Imaging.Core;

namespace ZeroGraphics.Imaging.Fusion
{
    /// <summary>
    /// Multi-Scale Pyramid Focus Stacking.
    /// Fuses a series of focus-bracketed macro or landscape images into an extended depth-of-field composite.
    /// Uses localized high-frequency energy detection across Burt-Adelson Laplacian pyramid bands with soft-maximum
    /// power weighting, yielding all-in-focus results without boundary seams or halos.
    /// </summary>
    public static class PyramidFocusStacking
    {
        private const float Epsilon = 1e-12f;

        /// <summary>
        /// Fuses a series of focus-bracketed Bgra32 ImageBuffers into an all-in-focus composite.
        /// </summary>
        public static unsafe ImageBuffer Fuse(
            IReadOnlyList<ImageBuffer> images,
            int maxLevels = 6,
            float sharpnessPower = 8f)
        {
            if (images == null || images.Count == 0)
                throw new ArgumentException("Image list cannot be empty.", nameof(images));

            if (images.Count == 1)
                return images[0].Clone();

            int w = images[0].Width;
            int h = images[0].Height;

            var floatImages = new List<float[]>(images.Count);
            for (int i = 0; i < images.Count; i++)
            {
                var img = images[i];
                if (img.Width != w || img.Height != h)
                    throw new ArgumentException($"Image {i} dimensions ({img.Width}x{img.Height}) do not match stack ({w}x{h}).");
                if (img.Format != ImageFormatMode.Bgra32)
                    throw new NotSupportedException("PyramidFocusStacking requires Bgra32 ImageBuffers.");

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

            float[] fusedFloat = FuseRgbaFloat(floatImages, w, h, maxLevels, sharpnessPower);

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
        /// Fuses a series of focus-bracketed interleaved RGBA float images [0..1] into an all-in-focus composite.
        /// </summary>
        public static float[] FuseRgbaFloat(
            IReadOnlyList<float[]> images,
            int w,
            int h,
            int maxLevels = 6,
            float sharpnessPower = 8f)
        {
            if (images == null || images.Count == 0)
                throw new ArgumentException("Image list cannot be empty.", nameof(images));
            if (w <= 0 || h <= 0)
                throw new ArgumentException("Dimensions must be positive.");

            int nFrames = images.Count;
            if (nFrames == 1)
                return (float[])images[0].Clone();

            // 1. Build Gaussian and Laplacian pyramids for all input frames
            var allLaps = new List<List<PyramidLevel4C>>(nFrames);
            for (int k = 0; k < nFrames; k++)
            {
                var gauss = ImagePyramid.BuildGaussianPyramid4C(images[k], w, h, maxLevels);
                var lap = ImagePyramid.BuildLaplacianPyramid4C(gauss);
                allLaps.Add(lap);
            }

            int levelCount = allLaps[0].Count;
            var fusedLaplacian = new List<PyramidLevel4C>(levelCount);

            // 2. Process each pyramid level
            for (int l = 0; l < levelCount; l++)
            {
                int lw = allLaps[0][l].Width;
                int lh = allLaps[0][l].Height;
                int nPix = lw * lh;
                float[] fusedData = new float[nPix * 4];

                if (l == levelCount - 1)
                {
                    // Coarsest base level: average the low-frequency background
                    float invN = 1f / nFrames;
                    Parallel.For(0, nPix, i =>
                    {
                        int p = i * 4;
                        float sumR = 0f, sumG = 0f, sumB = 0f, sumA = 0f;
                        for (int k = 0; k < nFrames; k++)
                        {
                            var data = allLaps[k][l].Data;
                            sumR += data[p];
                            sumG += data[p + 1];
                            sumB += data[p + 2];
                            sumA += data[p + 3];
                        }
                        fusedData[p] = sumR * invN;
                        fusedData[p + 1] = sumG * invN;
                        fusedData[p + 2] = sumB * invN;
                        fusedData[p + 3] = sumA * invN;
                    });
                }
                else
                {
                    // Band-pass detail levels: compute local sharpness energy map for each frame
                    float[][] energy = new float[nFrames][];
                    for (int k = 0; k < nFrames; k++)
                    {
                        energy[k] = ComputeLocalEnergy(allLaps[k][l].Data, lw, lh);
                    }

                    // Soft-max power weighting based on relative sharpness
                    Parallel.For(0, nPix, i =>
                    {
                        int p = i * 4;

                        // Find max energy for numerical stability
                        float maxE = 0f;
                        for (int k = 0; k < nFrames; k++)
                        {
                            if (energy[k][i] > maxE) maxE = energy[k][i];
                        }

                        // Compute weights with power exponent
                        float sumW = Epsilon;
                        Span<float> wK = stackalloc float[nFrames];
                        for (int k = 0; k < nFrames; k++)
                        {
                            float normE = (maxE > 1e-9f) ? (energy[k][i] / maxE) : 1f;
                            float wgt = MathF.Pow(normE, sharpnessPower);
                            wK[k] = wgt;
                            sumW += wgt;
                        }

                        float invSum = 1f / sumW;
                        float r = 0f, g = 0f, b = 0f, a = 0f;
                        for (int k = 0; k < nFrames; k++)
                        {
                            float weight = wK[k] * invSum;
                            var data = allLaps[k][l].Data;
                            r += data[p] * weight;
                            g += data[p + 1] * weight;
                            b += data[p + 2] * weight;
                            a += data[p + 3] * weight;
                        }

                        fusedData[p] = r;
                        fusedData[p + 1] = g;
                        fusedData[p + 2] = b;
                        fusedData[p + 3] = a;
                    });
                }

                fusedLaplacian.Add(new PyramidLevel4C(fusedData, lw, lh));
            }

            // 3. Reconstruct full-resolution image from fused Laplacian pyramid
            float[] fused = ImagePyramid.ReconstructLaplacian4C(fusedLaplacian);

            // Clamp results to [0..1]
            Parallel.For(0, w * h, i =>
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
        /// Computes local 3x3 windowed high-frequency detail energy from Laplacian coefficients.
        /// </summary>
        private static float[] ComputeLocalEnergy(float[] lapData, int w, int h)
        {
            float[] rawEnergy = new float[w * h];
            Parallel.For(0, h, y =>
            {
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    int p = (row + x) * 4;
                    float r = lapData[p];
                    float g = lapData[p + 1];
                    float b = lapData[p + 2];
                    rawEnergy[row + x] = r * r + g * g + b * b;
                }
            });

            // 3x3 local box smoothing for region consistency
            float[] smoothEnergy = new float[w * h];
            Parallel.For(0, h, y =>
            {
                int row = y * w;
                int ym = Math.Max(0, y - 1) * w;
                int yp = Math.Min(h - 1, y + 1) * w;

                for (int x = 0; x < w; x++)
                {
                    int xm = Math.Max(0, x - 1);
                    int xp = Math.Min(w - 1, x + 1);

                    float sum =
                        rawEnergy[ym + xm] + rawEnergy[ym + x] + rawEnergy[ym + xp] +
                        rawEnergy[row + xm] + rawEnergy[row + x] + rawEnergy[row + xp] +
                        rawEnergy[yp + xm] + rawEnergy[yp + x] + rawEnergy[yp + xp];

                    smoothEnergy[row + x] = sum * (1f / 9f);
                }
            });

            return smoothEnergy;
        }

        private static byte ClampByte(int val)
        {
            if (val < 0) return 0;
            if (val > 255) return 255;
            return (byte)val;
        }
    }
}
