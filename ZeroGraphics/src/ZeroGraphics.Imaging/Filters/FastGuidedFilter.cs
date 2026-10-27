using System;
using System.Threading.Tasks;
using ZeroGraphics.Imaging.Core;

namespace ZeroGraphics.Imaging.Filters
{
    /// <summary>
    /// High-Performance O(1) Linear-Time Edge-Preserving Guided Filter (He, Sun, Tang).
    /// Provides edge-aware smoothing without gradient reversal or halo artifacts.
    /// Supports both full-resolution filtering and subsampled Fast Guided Filtering for real-time throughput.
    /// </summary>
    public static class FastGuidedFilter
    {
        /// <summary>
        /// Applies Guided Filtering to a Bgra32 ImageBuffer.
        /// </summary>
        /// <param name="guide">Guidance image buffer (can be same as src for self-guided smoothing).</param>
        /// <param name="src">Input image buffer to be smoothed.</param>
        /// <param name="dst">Destination output image buffer.</param>
        /// <param name="radius">Local window radius (default 4).</param>
        /// <param name="eps">Regularization parameter controlling edge sensitivity (default 0.02f).</param>
        /// <param name="subsample">Subsampling ratio (1 = full resolution, 2 or 4 = accelerated Fast Guided Filter).</param>
        public static unsafe void Apply(
            ImageBuffer guide,
            ImageBuffer src,
            ImageBuffer dst,
            int radius = 4,
            float eps = 0.02f,
            int subsample = 1)
        {
            if (guide == null) throw new ArgumentNullException(nameof(guide));
            if (src == null) throw new ArgumentNullException(nameof(src));
            if (dst == null) throw new ArgumentNullException(nameof(dst));
            if (src.Format != ImageFormatMode.Bgra32 || dst.Format != ImageFormatMode.Bgra32)
                throw new NotSupportedException("FastGuidedFilter requires Bgra32 buffers.");

            int w = src.Width;
            int h = src.Height;
            if (w < 4 || h < 4)
            {
                src.CopyTo(dst);
                return;
            }

            // Convert to normalized float arrays
            float[] guidePx = new float[w * h * 4];
            float[] srcPx = new float[w * h * 4];
            float[] dstPx = new float[w * h * 4];

            Parallel.For(0, h, y =>
            {
                byte* gRow = guide.GetRowPointer(y);
                byte* sRow = src.GetRowPointer(y);
                int rowIdx = y * w * 4;
                for (int x = 0; x < w; x++)
                {
                    int bx = x * 4;
                    int fx = rowIdx + x * 4;
                    guidePx[fx] = gRow[bx + 2] / 255.0f;     // R
                    guidePx[fx + 1] = gRow[bx + 1] / 255.0f; // G
                    guidePx[fx + 2] = gRow[bx] / 255.0f;     // B
                    guidePx[fx + 3] = gRow[bx + 3] / 255.0f; // A

                    srcPx[fx] = sRow[bx + 2] / 255.0f;     // R
                    srcPx[fx + 1] = sRow[bx + 1] / 255.0f; // G
                    srcPx[fx + 2] = sRow[bx] / 255.0f;     // B
                    srcPx[fx + 3] = sRow[bx + 3] / 255.0f; // A
                }
            });

            ApplyRgbaFloat(guidePx, srcPx, dstPx, w, h, radius, eps, subsample);

            // Convert back to Bgra32
            Parallel.For(0, h, y =>
            {
                byte* dRow = dst.GetRowPointer(y);
                int rowIdx = y * w * 4;
                for (int x = 0; x < w; x++)
                {
                    int bx = x * 4;
                    int fx = rowIdx + x * 4;
                    dRow[bx + 2] = ClampByte((int)(dstPx[fx] * 255.0f + 0.5f));     // R
                    dRow[bx + 1] = ClampByte((int)(dstPx[fx + 1] * 255.0f + 0.5f)); // G
                    dRow[bx] = ClampByte((int)(dstPx[fx + 2] * 255.0f + 0.5f));     // B
                    dRow[bx + 3] = ClampByte((int)(dstPx[fx + 3] * 255.0f + 0.5f)); // A
                }
            });
        }

        /// <summary>
        /// High-performance Guided Filter on interleaved RGBA float buffers [0..1].
        /// Filters each RGB channel independently using the guidance image luminance (or self-guidance).
        /// </summary>
        public static void ApplyRgbaFloat(
            float[] guide,
            float[] src,
            float[] dst,
            int w,
            int h,
            int radius = 4,
            float eps = 0.02f,
            int subsample = 1)
        {
            if (guide == null) throw new ArgumentNullException(nameof(guide));
            if (src == null) throw new ArgumentNullException(nameof(src));
            if (dst == null) throw new ArgumentNullException(nameof(dst));
            if (w <= 0 || h <= 0) throw new ArgumentException("Dimensions must be positive.");

            if (radius < 1) radius = 1;
            if (eps < 1e-6f) eps = 1e-6f;
            if (subsample < 1) subsample = 1;

            int n = w * h;

            // Compute guidance luminance plane: I
            float[] I = new float[n];
            Parallel.For(0, h, y =>
            {
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    int p = (row + x) * 4;
                    I[row + x] = 0.2126f * guide[p] + 0.7152f * guide[p + 1] + 0.0722f * guide[p + 2];
                }
            });

            // If subsample > 1, downscale images for accelerated Fast Guided Filter
            int sw = (w + subsample - 1) / subsample;
            int sh = (h + subsample - 1) / subsample;
            int sRadius = Math.Max(1, radius / subsample);

            float[] subI = (subsample > 1) ? DownsampleArea(I, w, h, sw, sh) : I;
            float[] meanI = BoxFilter(subI, sw, sh, sRadius);
            float[] varI = BoxFilter(MultiplyArrays(subI, subI), sw, sh, sRadius);
            Parallel.For(0, sw * sh, i =>
            {
                varI[i] = Math.Max(0f, varI[i] - meanI[i] * meanI[i]);
            });

            // Filter each color channel c in {0, 1, 2}
            float[] pChannel = new float[n];
            for (int c = 0; c < 3; c++)
            {
                int ch = c;
                Parallel.For(0, n, i =>
                {
                    pChannel[i] = src[i * 4 + ch];
                });

                float[] subP = (subsample > 1) ? DownsampleArea(pChannel, w, h, sw, sh) : pChannel;
                float[] meanP = BoxFilter(subP, sw, sh, sRadius);
                float[] corrIP = BoxFilter(MultiplyArrays(subI, subP), sw, sh, sRadius);

                float[] subA = new float[sw * sh];
                float[] subB = new float[sw * sh];

                Parallel.For(0, sw * sh, i =>
                {
                    float cov = corrIP[i] - meanI[i] * meanP[i];
                    float a = cov / (varI[i] + eps);
                    float b = meanP[i] - a * meanI[i];
                    subA[i] = a;
                    subB[i] = b;
                });

                float[] meanA = BoxFilter(subA, sw, sh, sRadius);
                float[] meanB = BoxFilter(subB, sw, sh, sRadius);

                float[] fullA = (subsample > 1) ? UpsampleBilinear(meanA, sw, sh, w, h) : meanA;
                float[] fullB = (subsample > 1) ? UpsampleBilinear(meanB, sw, sh, w, h) : meanB;

                // q = meanA * I + meanB
                Parallel.For(0, n, i =>
                {
                    float q = fullA[i] * I[i] + fullB[i];
                    dst[i * 4 + ch] = Math.Max(0f, q);
                });
            }

            // Copy alpha channel intact
            Parallel.For(0, n, i =>
            {
                dst[i * 4 + 3] = src[i * 4 + 3];
            });
        }

        /// <summary>
        /// Pure O(1) separable 2-pass sliding window Box Filter.
        /// </summary>
        public static float[] BoxFilter(float[] src, int w, int h, int radius)
        {
            float[] temp = new float[w * h];
            float[] dst = new float[w * h];

            // 1. Horizontal pass
            Parallel.For(0, h, y =>
            {
                int row = y * w;
                float sum = 0f;
                int count = 0;

                for (int x = 0; x <= Math.Min(radius, w - 1); x++)
                {
                    sum += src[row + x];
                    count++;
                }

                for (int x = 0; x < w; x++)
                {
                    temp[row + x] = sum / count;

                    int addX = x + radius + 1;
                    if (addX < w)
                    {
                        sum += src[row + addX];
                        count++;
                    }

                    int subX = x - radius;
                    if (subX >= 0)
                    {
                        sum -= src[row + subX];
                        count--;
                    }
                }
            });

            // 2. Vertical pass
            Parallel.For(0, w, x =>
            {
                float sum = 0f;
                int count = 0;

                for (int y = 0; y <= Math.Min(radius, h - 1); y++)
                {
                    sum += temp[y * w + x];
                    count++;
                }

                for (int y = 0; y < h; y++)
                {
                    dst[y * w + x] = sum / count;

                    int addY = y + radius + 1;
                    if (addY < h)
                    {
                        sum += temp[addY * w + x];
                        count++;
                    }

                    int subY = y - radius;
                    if (subY >= 0)
                    {
                        sum -= temp[subY * w + x];
                        count--;
                    }
                }
            });

            return dst;
        }

        private static float[] MultiplyArrays(float[] a, float[] b)
        {
            float[] result = new float[a.Length];
            Parallel.For(0, a.Length, i =>
            {
                result[i] = a[i] * b[i];
            });
            return result;
        }

        private static float[] DownsampleArea(float[] src, int w, int h, int dw, int dh)
        {
            float[] dst = new float[dw * dh];
            float scaleX = (float)w / dw;
            float scaleY = (float)h / dh;

            Parallel.For(0, dh, dy =>
            {
                int sy0 = (int)(dy * scaleY);
                int sy1 = Math.Min(h, (int)((dy + 1) * scaleY));
                if (sy1 <= sy0) sy1 = Math.Min(h, sy0 + 1);

                for (int dx = 0; dx < dw; dx++)
                {
                    int sx0 = (int)(dx * scaleX);
                    int sx1 = Math.Min(w, (int)((dx + 1) * scaleX));
                    if (sx1 <= sx0) sx1 = Math.Min(w, sx0 + 1);

                    float sum = 0f;
                    int count = 0;
                    for (int y = sy0; y < sy1; y++)
                    {
                        int row = y * w;
                        for (int x = sx0; x < sx1; x++)
                        {
                            sum += src[row + x];
                            count++;
                        }
                    }
                    dst[dy * dw + dx] = count > 0 ? sum / count : 0f;
                }
            });

            return dst;
        }

        private static float[] UpsampleBilinear(float[] src, int sw, int sh, int dw, int dh)
        {
            float[] dst = new float[dw * dh];
            float scaleX = (float)(sw - 1) / Math.Max(1, dw - 1);
            float scaleY = (float)(sh - 1) / Math.Max(1, dh - 1);

            Parallel.For(0, dh, dy =>
            {
                float sy = dy * scaleY;
                int y0 = (int)sy;
                int y1 = Math.Min(sh - 1, y0 + 1);
                float ty = sy - y0;

                int dRow = dy * dw;
                int sRow0 = y0 * sw;
                int sRow1 = y1 * sw;

                for (int dx = 0; dx < dw; dx++)
                {
                    float sx = dx * scaleX;
                    int x0 = (int)sx;
                    int x1 = Math.Min(sw - 1, x0 + 1);
                    float tx = sx - x0;

                    float p00 = src[sRow0 + x0];
                    float p10 = src[sRow0 + x1];
                    float p01 = src[sRow1 + x0];
                    float p11 = src[sRow1 + x1];

                    float top = p00 + (p10 - p00) * tx;
                    float bot = p01 + (p11 - p01) * tx;
                    dst[dRow + dx] = top + (bot - top) * ty;
                }
            });

            return dst;
        }

        private static byte ClampByte(int v)
        {
            if (v < 0) return 0;
            if (v > 255) return 255;
            return (byte)v;
        }
    }
}
