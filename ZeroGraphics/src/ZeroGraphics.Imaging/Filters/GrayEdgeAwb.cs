using System;
using System.Threading.Tasks;
using ZeroGraphics.Imaging.Core;

namespace ZeroGraphics.Imaging.Filters
{
    /// <summary>
    /// Edge-Based Computational Color Constancy and Auto White Balance (van de Weijer, Gevers, Gijsenij).
    /// Leverages the Gray-Edge hypothesis under the Minkowski p-norm framework to estimate scene illuminants
    /// without the single-color dominance failure modes of classical Gray World.
    /// </summary>
    public static class GrayEdgeAwb
    {
        private struct ChannelSums
        {
            public double R;
            public double G;
            public double B;
        }

        /// <summary>
        /// Estimates RGB White Balance multipliers using Edge-Based Color Constancy on a Bgra32 ImageBuffer.
        /// </summary>
        /// <param name="src">Source Bgra32 buffer.</param>
        /// <param name="order">Derivative order: 0 = Shades of Gray (intensities), 1 = 1st-Order Gray Edge (gradients), 2 = 2nd-Order Gray Edge (Laplacian).</param>
        /// <param name="minkowskiP">Minkowski norm exponent (typically 6 for robust edge weighting, 1 for arithmetic mean).</param>
        /// <param name="sigma">Gaussian pre-smoothing standard deviation (controls noise suppression).</param>
        /// <param name="rGain">Resulting Red channel gain (relative to Green = 1.0).</param>
        /// <param name="gGain">Resulting Green channel gain (= 1.0).</param>
        /// <param name="bGain">Resulting Blue channel gain (relative to Green = 1.0).</param>
        public static unsafe void EstimateIlluminant(
            ImageBuffer src,
            int order,
            int minkowskiP,
            float sigma,
            out float rGain,
            out float gGain,
            out float bGain)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            if (src.Format != ImageFormatMode.Bgra32)
                throw new NotSupportedException("GrayEdgeAwb requires Bgra32 buffers.");

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
                    pixels[fx] = sRow[bx + 2] / 255.0f;     // R
                    pixels[fx + 1] = sRow[bx + 1] / 255.0f; // G
                    pixels[fx + 2] = sRow[bx] / 255.0f;     // B
                    pixels[fx + 3] = sRow[bx + 3] / 255.0f; // A
                }
            });

            EstimateIlluminantRgbaFloat(pixels, w, h, order, minkowskiP, sigma, out rGain, out gGain, out bGain);
        }

        /// <summary>
        /// Estimates RGB White Balance multipliers on an interleaved RGBA float buffer [0..1].
        /// </summary>
        public static void EstimateIlluminantRgbaFloat(
            float[] pixels,
            int w,
            int h,
            int order,
            int minkowskiP,
            float sigma,
            out float rGain,
            out float gGain,
            out float bGain)
        {
            if (pixels == null) throw new ArgumentNullException(nameof(pixels));
            if (w <= 0 || h <= 0) throw new ArgumentException("Dimensions must be positive.");

            if (minkowskiP < 1) minkowskiP = 1;
            if (order < 0) order = 0;
            if (order > 2) order = 1;

            int n = w * h;
            double sumR = 0.0, sumG = 0.0, sumB = 0.0;
            object lockObj = new object();

            if (order == 0)
            {
                // Order 0: Shades of Gray (Minkowski p-norm of pixel intensities)
                Parallel.For(0, h, () => new ChannelSums(), (int y, ParallelLoopState loop, ChannelSums local) =>
                {
                    int row = y * w;
                    for (int x = 0; x < w; x++)
                    {
                        int p = (row + x) * 4;
                        double r = pixels[p];
                        double g = pixels[p + 1];
                        double b = pixels[p + 2];

                        local.R += Math.Pow(r, minkowskiP);
                        local.G += Math.Pow(g, minkowskiP);
                        local.B += Math.Pow(b, minkowskiP);
                    }
                    return local;
                },
                local =>
                {
                    lock (lockObj)
                    {
                        sumR += local.R;
                        sumG += local.G;
                        sumB += local.B;
                    }
                });
            }
            else if (order == 1)
            {
                // Order 1: 1st-Order Gray Edge (Sobel gradient magnitude per channel)
                Parallel.For(1, h - 1, () => new ChannelSums(), (int y, ParallelLoopState loop, ChannelSums local) =>
                {
                    int rowPrev = (y - 1) * w;
                    int rowCurr = y * w;
                    int rowNext = (y + 1) * w;

                    for (int x = 1; x < w - 1; x++)
                    {
                        for (int c = 0; c < 3; c++)
                        {
                            float tl = pixels[(rowPrev + x - 1) * 4 + c];
                            float tc = pixels[(rowPrev + x) * 4 + c];
                            float tr = pixels[(rowPrev + x + 1) * 4 + c];

                            float ml = pixels[(rowCurr + x - 1) * 4 + c];
                            float mr = pixels[(rowCurr + x + 1) * 4 + c];

                            float bl = pixels[(rowNext + x - 1) * 4 + c];
                            float bc = pixels[(rowNext + x) * 4 + c];
                            float br = pixels[(rowNext + x + 1) * 4 + c];

                            float gx = (tr + 2f * mr + br) - (tl + 2f * ml + bl);
                            float gy = (bl + 2f * bc + br) - (tl + 2f * tc + tr);
                            double mag = Math.Sqrt(gx * gx + gy * gy);

                            double pVal = Math.Pow(mag, minkowskiP);
                            if (c == 0) local.R += pVal;
                            else if (c == 1) local.G += pVal;
                            else local.B += pVal;
                        }
                    }
                    return local;
                },
                local =>
                {
                    lock (lockObj)
                    {
                        sumR += local.R;
                        sumG += local.G;
                        sumB += local.B;
                    }
                });
            }
            else
            {
                // Order 2: 2nd-Order Gray Edge (Laplacian second derivative)
                Parallel.For(1, h - 1, () => new ChannelSums(), (int y, ParallelLoopState loop, ChannelSums local) =>
                {
                    int rowPrev = (y - 1) * w;
                    int rowCurr = y * w;
                    int rowNext = (y + 1) * w;

                    for (int x = 1; x < w - 1; x++)
                    {
                        for (int c = 0; c < 3; c++)
                        {
                            float tc = pixels[(rowPrev + x) * 4 + c];
                            float ml = pixels[(rowCurr + x - 1) * 4 + c];
                            float mc = pixels[(rowCurr + x) * 4 + c];
                            float mr = pixels[(rowCurr + x + 1) * 4 + c];
                            float bc = pixels[(rowNext + x) * 4 + c];

                            double lap = Math.Abs(tc + ml + mr + bc - 4f * mc);
                            double pVal = Math.Pow(lap, minkowskiP);
                            if (c == 0) local.R += pVal;
                            else if (c == 1) local.G += pVal;
                            else local.B += pVal;
                        }
                    }
                    return local;
                },
                local =>
                {
                    lock (lockObj)
                    {
                        sumR += local.R;
                        sumG += local.G;
                        sumB += local.B;
                    }
                });
            }

            // Compute Minkowski norm roots
            double invP = 1.0 / minkowskiP;
            double eR = Math.Pow(sumR, invP);
            double eG = Math.Pow(sumG, invP);
            double eB = Math.Pow(sumB, invP);

            // Normalize illuminant vector to unit length
            double norm = Math.Sqrt(eR * eR + eG * eG + eB * eB);
            if (norm < 1e-9)
            {
                rGain = 1.0f;
                gGain = 1.0f;
                bGain = 1.0f;
                return;
            }

            eR /= norm;
            eG /= norm;
            eB /= norm;

            // Von Kries diagonal scaling: balance channels relative to Green
            gGain = 1.0f;
            rGain = (float)(eG / Math.Max(1e-6, eR));
            bGain = (float)(eG / Math.Max(1e-6, eB));

            // Clamp gains to reasonable photography range [0.1..10.0]
            rGain = MathCompat.Clamp(rGain, 0.1f, 10.0f);
            bGain = MathCompat.Clamp(bGain, 0.1f, 10.0f);
        }

        /// <summary>
        /// Automatically white balances a Bgra32 ImageBuffer in-place or into destination buffer.
        /// </summary>
        public static unsafe void Apply(
            ImageBuffer src,
            ImageBuffer dst,
            int order = 1,
            int minkowskiP = 6,
            float sigma = 1.0f)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            if (dst == null) throw new ArgumentNullException(nameof(dst));

            EstimateIlluminant(src, order, minkowskiP, sigma, out float rGain, out float gGain, out float bGain);

            int w = src.Width;
            int h = src.Height;

            Parallel.For(0, h, y =>
            {
                byte* sRow = src.GetRowPointer(y);
                byte* dRow = dst.GetRowPointer(y);

                for (int x = 0; x < w; x++)
                {
                    int bx = x * 4;
                    int b = (int)(sRow[bx] * bGain + 0.5f);
                    int g = (int)(sRow[bx + 1] * gGain + 0.5f);
                    int r = (int)(sRow[bx + 2] * rGain + 0.5f);

                    dRow[bx] = MathCompat.ClampToByte(b);
                    dRow[bx + 1] = MathCompat.ClampToByte(g);
                    dRow[bx + 2] = MathCompat.ClampToByte(r);
                    dRow[bx + 3] = sRow[bx + 3]; // Alpha
                }
            });
        }
    }
}
