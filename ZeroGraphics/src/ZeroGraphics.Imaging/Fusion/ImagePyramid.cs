using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ZeroGraphics.Imaging.Core;

namespace ZeroGraphics.Imaging.Fusion
{
    /// <summary>
    /// Multi-Scale Burt-Adelson Image Pyramid (Gaussian and Laplacian).
    /// Uses 5-tap separable binomial filtering [1, 4, 6, 4, 1] / 16 with boundary clamping.
    /// Supports exact multiresolution decomposition and reconstruction for exposure fusion and focus stacking.
    /// </summary>
    public static class ImagePyramid
    {
        private static readonly float[] Kernel5 = { 1f / 16f, 4f / 16f, 6f / 16f, 4f / 16f, 1f / 16f };

        #region Single-Channel (Grayscale / Weight Map) Pyramid Operations

        /// <summary>
        /// Downsamples a 1-channel float buffer by 2x using the 5-tap Burt-Adelson filter.
        /// </summary>
        public static float[] Downsample1C(float[] src, int srcW, int srcH, out int dstW, out int dstH)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            int targetW = Math.Max(1, (srcW + 1) / 2);
            int targetH = Math.Max(1, (srcH + 1) / 2);
            dstW = targetW;
            dstH = targetH;

            // Step 1: Horizontal filter evaluated only at even columns: 2 * x_dst
            float[] tempH = new float[targetW * srcH];
            Parallel.For(0, srcH, y =>
            {
                int srcRow = y * srcW;
                int dstRow = y * targetW;
                for (int xd = 0; xd < targetW; xd++)
                {
                    int xs = xd * 2;
                    float sum = 0f;
                    for (int k = -2; k <= 2; k++)
                    {
                        int sx = MathCompat.Clamp(xs + k, 0, srcW - 1);
                        sum += src[srcRow + sx] * Kernel5[k + 2];
                    }
                    tempH[dstRow + xd] = sum;
                }
            });

            // Step 2: Vertical filter evaluated only at even rows: 2 * y_dst
            float[] dst = new float[targetW * targetH];
            Parallel.For(0, targetH, yd =>
            {
                int ys = yd * 2;
                int dstRow = yd * targetW;
                for (int xd = 0; xd < targetW; xd++)
                {
                    float sum = 0f;
                    for (int k = -2; k <= 2; k++)
                    {
                        int sy = MathCompat.Clamp(ys + k, 0, srcH - 1);
                        sum += tempH[sy * targetW + xd] * Kernel5[k + 2];
                    }
                    dst[dstRow + xd] = sum;
                }
            });

            return dst;
        }

        /// <summary>
        /// Upsamples a 1-channel float buffer by 2x to exact target dimensions (dstW, dstH).
        /// </summary>
        public static float[] Upsample1C(float[] src, int srcW, int srcH, int dstW, int dstH)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            if (dstW <= 0 || dstH <= 0) throw new ArgumentException("Dimensions must be positive.");

            // Step 1: Horizontal expansion from srcW to dstW
            float[] tempH = new float[dstW * srcH];
            Parallel.For(0, srcH, y =>
            {
                int srcRow = y * srcW;
                int dstRow = y * dstW;
                for (int xd = 0; xd < dstW; xd++)
                {
                    int k = xd / 2;
                    if ((xd & 1) == 0) // Even index: 6/8 s[k] + 1/8 s[k-1] + 1/8 s[k+1]
                    {
                        int km = Math.Max(0, k - 1);
                        int kp = Math.Min(srcW - 1, k + 1);
                        tempH[dstRow + xd] = (6f * src[srcRow + k] + src[srcRow + km] + src[srcRow + kp]) * 0.125f;
                    }
                    else // Odd index: 1/2 s[k] + 1/2 s[k+1]
                    {
                        int kp = Math.Min(srcW - 1, k + 1);
                        tempH[dstRow + xd] = (src[srcRow + k] + src[srcRow + kp]) * 0.5f;
                    }
                }
            });

            // Step 2: Vertical expansion from srcH to dstH
            float[] dst = new float[dstW * dstH];
            Parallel.For(0, dstH, yd =>
            {
                int k = yd / 2;
                int dstRow = yd * dstW;
                if ((yd & 1) == 0)
                {
                    int km = Math.Max(0, k - 1);
                    int kp = Math.Min(srcH - 1, k + 1);
                    int rC = k * dstW;
                    int rM = km * dstW;
                    int rP = kp * dstW;
                    for (int xd = 0; xd < dstW; xd++)
                    {
                        dst[dstRow + xd] = (6f * tempH[rC + xd] + tempH[rM + xd] + tempH[rP + xd]) * 0.125f;
                    }
                }
                else
                {
                    int kp = Math.Min(srcH - 1, k + 1);
                    int rC = k * dstW;
                    int rP = kp * dstW;
                    for (int xd = 0; xd < dstW; xd++)
                    {
                        dst[dstRow + xd] = (tempH[rC + xd] + tempH[rP + xd]) * 0.5f;
                    }
                }
            });

            return dst;
        }

        /// <summary>
        /// Builds an N-level Gaussian pyramid for a single-channel float array.
        /// </summary>
        public static List<PyramidLevel1C> BuildGaussianPyramid1C(float[] src, int w, int h, int maxLevels = 6)
        {
            var levels = new List<PyramidLevel1C>
            {
                new PyramidLevel1C(src, w, h)
            };

            int curW = w;
            int curH = h;
            float[] cur = src;

            for (int lvl = 1; lvl < maxLevels; lvl++)
            {
                if (curW <= 8 || curH <= 8) break;
                float[] next = Downsample1C(cur, curW, curH, out int nextW, out int nextH);
                levels.Add(new PyramidLevel1C(next, nextW, nextH));
                cur = next;
                curW = nextW;
                curH = nextH;
            }

            return levels;
        }

        #endregion

        #region 4-Channel Interleaved RGBA Float Pyramid Operations

        /// <summary>
        /// Downsamples a 4-channel interleaved RGBA float buffer by 2x using the 5-tap Burt-Adelson filter.
        /// </summary>
        public static float[] Downsample4C(float[] src, int srcW, int srcH, out int dstW, out int dstH)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            int targetW = Math.Max(1, (srcW + 1) / 2);
            int targetH = Math.Max(1, (srcH + 1) / 2);
            dstW = targetW;
            dstH = targetH;

            // Step 1: Horizontal filter
            float[] tempH = new float[targetW * srcH * 4];
            Parallel.For(0, srcH, y =>
            {
                int srcRow = y * srcW * 4;
                int dstRow = y * targetW * 4;
                for (int xd = 0; xd < targetW; xd++)
                {
                    int xs = xd * 2;
                    int dp = dstRow + xd * 4;

                    float sumR = 0f, sumG = 0f, sumB = 0f, sumA = 0f;
                    for (int k = -2; k <= 2; k++)
                    {
                        int sx = MathCompat.Clamp(xs + k, 0, srcW - 1);
                        int sp = srcRow + sx * 4;
                        float w = Kernel5[k + 2];
                        sumR += src[sp] * w;
                        sumG += src[sp + 1] * w;
                        sumB += src[sp + 2] * w;
                        sumA += src[sp + 3] * w;
                    }
                    tempH[dp] = sumR;
                    tempH[dp + 1] = sumG;
                    tempH[dp + 2] = sumB;
                    tempH[dp + 3] = sumA;
                }
            });

            // Step 2: Vertical filter
            float[] dst = new float[targetW * targetH * 4];
            Parallel.For(0, targetH, yd =>
            {
                int ys = yd * 2;
                int dstRow = yd * targetW * 4;
                for (int xd = 0; xd < targetW; xd++)
                {
                    int dp = dstRow + xd * 4;
                    float sumR = 0f, sumG = 0f, sumB = 0f, sumA = 0f;
                    for (int k = -2; k <= 2; k++)
                    {
                        int sy = MathCompat.Clamp(ys + k, 0, srcH - 1);
                        int sp = (sy * targetW + xd) * 4;
                        float w = Kernel5[k + 2];
                        sumR += tempH[sp] * w;
                        sumG += tempH[sp + 1] * w;
                        sumB += tempH[sp + 2] * w;
                        sumA += tempH[sp + 3] * w;
                    }
                    dst[dp] = sumR;
                    dst[dp + 1] = sumG;
                    dst[dp + 2] = sumB;
                    dst[dp + 3] = sumA;
                }
            });

            return dst;
        }

        /// <summary>
        /// Upsamples a 4-channel interleaved RGBA float buffer by 2x to exact target dimensions (dstW, dstH).
        /// </summary>
        public static float[] Upsample4C(float[] src, int srcW, int srcH, int dstW, int dstH)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            if (dstW <= 0 || dstH <= 0) throw new ArgumentException("Dimensions must be positive.");

            // Step 1: Horizontal expansion
            float[] tempH = new float[dstW * srcH * 4];
            Parallel.For(0, srcH, y =>
            {
                int srcRow = y * srcW * 4;
                int dstRow = y * dstW * 4;
                for (int xd = 0; xd < dstW; xd++)
                {
                    int k = xd / 2;
                    int dp = dstRow + xd * 4;
                    if ((xd & 1) == 0)
                    {
                        int km = Math.Max(0, k - 1);
                        int kp = Math.Min(srcW - 1, k + 1);
                        int pC = srcRow + k * 4;
                        int pM = srcRow + km * 4;
                        int pP = srcRow + kp * 4;
                        for (int c = 0; c < 4; c++)
                        {
                            tempH[dp + c] = (6f * src[pC + c] + src[pM + c] + src[pP + c]) * 0.125f;
                        }
                    }
                    else
                    {
                        int kp = Math.Min(srcW - 1, k + 1);
                        int pC = srcRow + k * 4;
                        int pP = srcRow + kp * 4;
                        for (int c = 0; c < 4; c++)
                        {
                            tempH[dp + c] = (src[pC + c] + src[pP + c]) * 0.5f;
                        }
                    }
                }
            });

            // Step 2: Vertical expansion
            float[] dst = new float[dstW * dstH * 4];
            Parallel.For(0, dstH, yd =>
            {
                int k = yd / 2;
                int dstRow = yd * dstW * 4;
                if ((yd & 1) == 0)
                {
                    int km = Math.Max(0, k - 1);
                    int kp = Math.Min(srcH - 1, k + 1);
                    int rC = k * dstW * 4;
                    int rM = km * dstW * 4;
                    int rP = kp * dstW * 4;
                    for (int xd = 0; xd < dstW; xd++)
                    {
                        int dp = dstRow + xd * 4;
                        int xp = xd * 4;
                        for (int c = 0; c < 4; c++)
                        {
                            dst[dp + c] = (6f * tempH[rC + xp + c] + tempH[rM + xp + c] + tempH[rP + xp + c]) * 0.125f;
                        }
                    }
                }
                else
                {
                    int kp = Math.Min(srcH - 1, k + 1);
                    int rC = k * dstW * 4;
                    int rP = kp * dstW * 4;
                    for (int xd = 0; xd < dstW; xd++)
                    {
                        int dp = dstRow + xd * 4;
                        int xp = xd * 4;
                        for (int c = 0; c < 4; c++)
                        {
                            dst[dp + c] = (tempH[rC + xp + c] + tempH[rP + xp + c]) * 0.5f;
                        }
                    }
                }
            });

            return dst;
        }

        /// <summary>
        /// Builds an N-level Gaussian pyramid for a 4-channel RGBA float array.
        /// </summary>
        public static List<PyramidLevel4C> BuildGaussianPyramid4C(float[] src, int w, int h, int maxLevels = 6)
        {
            var levels = new List<PyramidLevel4C>
            {
                new PyramidLevel4C(src, w, h)
            };

            int curW = w;
            int curH = h;
            float[] cur = src;

            for (int lvl = 1; lvl < maxLevels; lvl++)
            {
                if (curW <= 8 || curH <= 8) break;
                float[] next = Downsample4C(cur, curW, curH, out int nextW, out int nextH);
                levels.Add(new PyramidLevel4C(next, nextW, nextH));
                cur = next;
                curW = nextW;
                curH = nextH;
            }

            return levels;
        }

        /// <summary>
        /// Builds an N-level Laplacian pyramid for a 4-channel RGBA float array.
        /// Level l: L_l = G_l - Upsample(G_{l+1}). Coarsest level is G_{last}.
        /// </summary>
        public static List<PyramidLevel4C> BuildLaplacianPyramid4C(List<PyramidLevel4C> gauss)
        {
            if (gauss == null || gauss.Count == 0)
                throw new ArgumentException("Gaussian pyramid cannot be empty.", nameof(gauss));

            int count = gauss.Count;
            var lap = new List<PyramidLevel4C>(count);

            for (int l = 0; l < count - 1; l++)
            {
                var cur = gauss[l];
                var next = gauss[l + 1];

                float[] expanded = Upsample4C(next.Data, next.Width, next.Height, cur.Width, cur.Height);
                float[] diff = new float[cur.Data.Length];

                Parallel.For(0, cur.Data.Length, i =>
                {
                    diff[i] = cur.Data[i] - expanded[i];
                });

                lap.Add(new PyramidLevel4C(diff, cur.Width, cur.Height));
            }

            // Coarsest band is residual Gaussian level
            var last = gauss[count - 1];
            float[] lastCopy = (float[])last.Data.Clone();
            lap.Add(new PyramidLevel4C(lastCopy, last.Width, last.Height));

            return lap;
        }

        /// <summary>
        /// Reconstructs a full-resolution 4-channel RGBA float image from its Laplacian pyramid.
        /// Successively upsamples and adds from coarsest to finest level.
        /// </summary>
        public static float[] ReconstructLaplacian4C(List<PyramidLevel4C> lap)
        {
            if (lap == null || lap.Count == 0)
                throw new ArgumentException("Laplacian pyramid cannot be empty.", nameof(lap));

            int count = lap.Count;
            float[] current = (float[])lap[count - 1].Data.Clone();
            int curW = lap[count - 1].Width;
            int curH = lap[count - 1].Height;

            for (int l = count - 2; l >= 0; l--)
            {
                var target = lap[l];
                float[] up = Upsample4C(current, curW, curH, target.Width, target.Height);
                float[] combined = new float[target.Data.Length];

                Parallel.For(0, target.Data.Length, i =>
                {
                    combined[i] = up[i] + target.Data[i];
                });

                current = combined;
                curW = target.Width;
                curH = target.Height;
            }

            return current;
        }

        #endregion
    }

    /// <summary>
    /// Represents one level of a 1-channel pyramid (e.g. weight map).
    /// </summary>
    public sealed class PyramidLevel1C
    {
        public float[] Data { get; }
        public int Width { get; }
        public int Height { get; }

        public PyramidLevel1C(float[] data, int width, int height)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            Width = width;
            Height = height;
        }
    }

    /// <summary>
    /// Represents one level of a 4-channel RGBA pyramid.
    /// </summary>
    public sealed class PyramidLevel4C
    {
        public float[] Data { get; }
        public int Width { get; }
        public int Height { get; }

        public PyramidLevel4C(float[] data, int width, int height)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            Width = width;
            Height = height;
        }
    }
}
