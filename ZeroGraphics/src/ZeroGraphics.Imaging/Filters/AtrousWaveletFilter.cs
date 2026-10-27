using System;
using System.Threading.Tasks;
using ZeroGraphics.Imaging.Core;

namespace ZeroGraphics.Imaging.Filters
{
    /// <summary>
    /// Undecimated À Trous B-Spline Wavelet Filter (Holschneider et al., Starck & Murtagh).
    /// Provides shift-invariant, multiscale noise shrinkage on stationary wavelet planes
    /// using cubic B3-spline filters [1, 4, 6, 4, 1] / 16 with dyadic hole dilation (step = 2^s).
    /// Suppresses high-frequency sensor noise and chroma grain while strictly preserving edge acutance.
    /// </summary>
    public static class AtrousWaveletFilter
    {
        private static readonly float[] Spline5 = { 1f / 16f, 4f / 16f, 6f / 16f, 4f / 16f, 1f / 16f };

        /// <summary>
        /// Applies À Trous B-Spline Wavelet Denoising on a Bgra32 ImageBuffer.
        /// </summary>
        public static unsafe void Apply(
            ImageBuffer src,
            ImageBuffer dst,
            float lumaStrength = 0.5f,
            float chromaStrength = 0.8f,
            int scales = 4)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            if (dst == null) throw new ArgumentNullException(nameof(dst));
            if (src.Format != ImageFormatMode.Bgra32 || dst.Format != ImageFormatMode.Bgra32)
                throw new NotSupportedException("AtrousWaveletFilter requires Bgra32 buffers.");

            int w = src.Width;
            int h = src.Height;

            float[] pixels = new float[w * h * 4];
            Parallel.For(0, h, y =>
            {
                byte* sRow = src.GetRowPointer(y);
                int rowIdx = y * w * 4;
                for (int x = 0; x < w; x++)
                {
                    int bx = x * 4;
                    int fx = rowIdx + x * 4;
                    pixels[fx] = sRow[bx + 2] * (1f / 255f);     // R
                    pixels[fx + 1] = sRow[bx + 1] * (1f / 255f); // G
                    pixels[fx + 2] = sRow[bx] * (1f / 255f);     // B
                    pixels[fx + 3] = sRow[bx + 3] * (1f / 255f); // A
                }
            });

            ApplyRgbaFloat(pixels, w, h, lumaStrength, chromaStrength, scales);

            Parallel.For(0, h, y =>
            {
                byte* dRow = dst.GetRowPointer(y);
                int rowIdx = y * w * 4;
                for (int x = 0; x < w; x++)
                {
                    int bx = x * 4;
                    int fx = rowIdx + x * 4;
                    dRow[bx + 2] = ClampByte((int)(pixels[fx] * 255f + 0.5f));     // R
                    dRow[bx + 1] = ClampByte((int)(pixels[fx + 1] * 255f + 0.5f)); // G
                    dRow[bx] = ClampByte((int)(pixels[fx + 2] * 255f + 0.5f));     // B
                    dRow[bx + 3] = ClampByte((int)(pixels[fx + 3] * 255f + 0.5f)); // A
                }
            });
        }

        /// <summary>
        /// High-performance À Trous B-Spline Wavelet Denoising on interleaved RGBA float buffer [0..1].
        /// Operates in decoupled Luminance and Chrominance (Y, Cb, Cr) space. Modifies pixels in-place.
        /// </summary>
        public static void ApplyRgbaFloat(
            float[] pixels,
            int w,
            int h,
            float lumaStrength = 0.5f,
            float chromaStrength = 0.8f,
            int scales = 4)
        {
            if (pixels == null) throw new ArgumentNullException(nameof(pixels));
            if (w < 4 || h < 4) return;
            if (lumaStrength < 1e-4f && chromaStrength < 1e-4f) return;

            scales = MathCompat.Clamp(scales, 1, 6);
            int n = w * h;

            // 1. Convert RGB to decoupled Y (Luma), Cb (Chroma-B), Cr (Chroma-R)
            float[] planeY = new float[n];
            float[] planeCb = new float[n];
            float[] planeCr = new float[n];

            Parallel.For(0, h, y =>
            {
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    int p = (row + x) * 4;
                    float r = pixels[p];
                    float g = pixels[p + 1];
                    float b = pixels[p + 2];

                    float lum = 0.2126f * r + 0.7152f * g + 0.0722f * b;
                    planeY[row + x] = lum;
                    planeCb[row + x] = b - lum;
                    planeCr[row + x] = r - lum;
                }
            });

            // 2. Denoise Luminance plane if lumaStrength > 0
            if (lumaStrength > 1e-4f)
            {
                DenoisePlane(planeY, w, h, lumaStrength * 0.08f, scales);
            }

            // 3. Denoise Chrominance planes if chromaStrength > 0
            if (chromaStrength > 1e-4f)
            {
                DenoisePlane(planeCb, w, h, chromaStrength * 0.12f, scales);
                DenoisePlane(planeCr, w, h, chromaStrength * 0.12f, scales);
            }

            // 4. Reconstruct RGB from denoised Y, Cb, Cr
            Parallel.For(0, h, y =>
            {
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    int idx = row + x;
                    int p = idx * 4;

                    float lum = planeY[idx];
                    float cb = planeCb[idx];
                    float cr = planeCr[idx];

                    float r = lum + cr;
                    float b = lum + cb;
                    float g = (lum - 0.2126f * r - 0.0722f * b) * (1f / 0.7152f);

                    pixels[p] = MathCompat.Clamp(r, 0f, 1f);
                    pixels[p + 1] = MathCompat.Clamp(g, 0f, 1f);
                    pixels[p + 2] = MathCompat.Clamp(b, 0f, 1f);
                }
            });
        }

        /// <summary>
        /// Performs À Trous decomposition, scale-dependent shrinkage, and synthesis on a single plane.
        /// </summary>
        private static void DenoisePlane(float[] plane, int w, int h, float baseThreshold, int scales)
        {
            int n = w * h;
            float[] current = (float[])plane.Clone();

            // Accumulator for reconstructed synthesized output
            float[] reconstructed = new float[n];

            for (int s = 0; s < scales; s++)
            {
                int step = 1 << s;

                // Convolve current approximation plane with dyadic B3-spline kernel
                float[] next = ConvolveAtrous(current, w, h, step);

                // Wavelet detail plane: w_s = current - next
                // Scale-dependent threshold: noise energy decays by ~2^(-0.6 * s)
                float scaleThresh = baseThreshold * MathF.Pow(2f, -0.6f * s);

                Parallel.For(0, n, i =>
                {
                    float detail = current[i] - next[i];
                    float absDet = MathF.Abs(detail);

                    if (absDet > 1e-7f)
                    {
                        if (absDet <= scaleThresh)
                        {
                            detail = 0f; // Eliminate noise within threshold
                        }
                        else
                        {
                            // Soft threshold with continuous ramp to preserve sharp structural edges
                            float shrink = absDet - scaleThresh;
                            float blend = Math.Min(1f, shrink / (2f * scaleThresh));
                            detail = MathF.Sign(detail) * (shrink * (1f - blend) + absDet * blend);
                        }
                    }

                    reconstructed[i] += detail;
                });

                current = next;
            }

            // Add the final smooth residual approximation
            Parallel.For(0, n, i =>
            {
                plane[i] = reconstructed[i] + current[i];
            });
        }

        /// <summary>
        /// 2D Separable horizontal and vertical convolution with B3-spline kernel dilated by step.
        /// </summary>
        private static float[] ConvolveAtrous(float[] src, int w, int h, int step)
        {
            // 1. Horizontal pass
            float[] tempH = new float[w * h];
            Parallel.For(0, h, y =>
            {
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    float sum = 0f;
                    for (int k = -2; k <= 2; k++)
                    {
                        int sx = MathCompat.Clamp(x + k * step, 0, w - 1);
                        sum += src[row + sx] * Spline5[k + 2];
                    }
                    tempH[row + x] = sum;
                }
            });

            // 2. Vertical pass
            float[] dst = new float[w * h];
            Parallel.For(0, h, y =>
            {
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    float sum = 0f;
                    for (int k = -2; k <= 2; k++)
                    {
                        int sy = MathCompat.Clamp(y + k * step, 0, h - 1);
                        sum += tempH[sy * w + x] * Spline5[k + 2];
                    }
                    dst[row + x] = sum;
                }
            });

            return dst;
        }

        private static byte ClampByte(int val)
        {
            if (val < 0) return 0;
            if (val > 255) return 255;
            return (byte)val;
        }
    }
}
