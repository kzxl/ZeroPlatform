using System;
using System.Threading.Tasks;
using ZeroGraphics.Imaging.Core;

namespace ZeroGraphics.Imaging.Filters
{
    /// <summary>
    /// Directed 4-Way Median Filter for Impulse Noise and Sensor Defect Suppression.
    /// Evaluates directional gradient variance along 4 principal axes (0°, 45°, 90°, 135°) to identify
    /// edge-parallel contours before computing the local median.
    /// Eliminates hot pixels, dead pixels, and salt-and-pepper noise without blunting sharp corners or thin lines.
    /// </summary>
    public static class DirectedMedianFilter
    {
        /// <summary>
        /// Applies Directed Median Filtering to a Bgra32 ImageBuffer.
        /// </summary>
        /// <param name="src">Source Bgra32 buffer.</param>
        /// <param name="dst">Destination Bgra32 buffer.</param>
        /// <param name="threshold">Threshold for impulse detection [0..1] (default 0.05f = ~13/255).</param>
        public static unsafe void Apply(ImageBuffer src, ImageBuffer dst, float threshold = 0.05f)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            if (dst == null) throw new ArgumentNullException(nameof(dst));
            if (src.Format != ImageFormatMode.Bgra32 || dst.Format != ImageFormatMode.Bgra32)
                throw new NotSupportedException("DirectedMedianFilter requires Bgra32 buffers.");

            int w = src.Width;
            int h = src.Height;
            if (w < 3 || h < 3)
            {
                src.CopyTo(dst);
                return;
            }

            int byteThreshold = (int)(Math.Max(0.001f, threshold) * 255.0f + 0.5f);

            // Copy source to destination first for boundaries
            src.CopyTo(dst);

            Parallel.For(1, h - 1, y =>
            {
                byte* prevRow = src.GetRowPointer(y - 1);
                byte* currRow = src.GetRowPointer(y);
                byte* nextRow = src.GetRowPointer(y + 1);
                byte* dstRow = dst.GetRowPointer(y);

                for (int x = 1; x < w - 1; x++)
                {
                    int bx = x * 4;

                    // Filter each color channel B, G, R
                    for (int c = 0; c < 3; c++)
                    {
                        int ch = bx + c;
                        byte center = currRow[ch];

                        // 4 Directional Rays:
                        // 0: Horizontal
                        byte h0 = currRow[ch - 4], h1 = center, h2 = currRow[ch + 4];
                        int varH = Math.Abs(h0 - h1) + Math.Abs(h2 - h1);

                        // 1: Vertical
                        byte v0 = prevRow[ch], v1 = center, v2 = nextRow[ch];
                        int varV = Math.Abs(v0 - v1) + Math.Abs(v2 - v1);

                        // 2: Main Diagonal (45°)
                        byte d0 = prevRow[ch - 4], d1 = center, d2 = nextRow[ch + 4];
                        int varD = Math.Abs(d0 - d1) + Math.Abs(d2 - d1);

                        // 3: Anti-Diagonal (135°)
                        byte a0 = prevRow[ch + 4], a1 = center, a2 = nextRow[ch - 4];
                        int varA = Math.Abs(a0 - a1) + Math.Abs(a2 - a1);

                        // Find minimum variation ray (parallel to edge)
                        int minVar = varH;
                        byte p0 = h0, p2 = h2;

                        if (varV < minVar)
                        {
                            minVar = varV;
                            p0 = v0; p2 = v2;
                        }
                        if (varD < minVar)
                        {
                            minVar = varD;
                            p0 = d0; p2 = d2;
                        }
                        if (varA < minVar)
                        {
                            p0 = a0; p2 = a2;
                        }

                        // 3-point median
                        byte med = Median3Byte(p0, center, p2);

                        // Replace only if anomaly exceeds threshold
                        if (Math.Abs(center - med) >= byteThreshold)
                        {
                            dstRow[ch] = med;
                        }
                        else
                        {
                            dstRow[ch] = center;
                        }
                    }

                    dstRow[bx + 3] = currRow[bx + 3]; // Copy alpha
                }
            });
        }

        /// <summary>
        /// High-performance Directed Median Filtering on interleaved RGBA float buffer [0..1].
        /// Modifies pixels in-place.
        /// </summary>
        public static void ApplyRgbaFloat(float[] pixels, int w, int h, float threshold = 0.05f)
        {
            if (pixels == null) throw new ArgumentNullException(nameof(pixels));
            if (w < 3 || h < 3) return;

            float[] clone = (float[])pixels.Clone();

            Parallel.For(1, h - 1, y =>
            {
                int rowPrev = (y - 1) * w;
                int rowCurr = y * w;
                int rowNext = (y + 1) * w;

                for (int x = 1; x < w - 1; x++)
                {
                    int p = (rowCurr + x) * 4;

                    for (int c = 0; c < 3; c++)
                    {
                        float center = clone[p + c];

                        // 0: Horizontal
                        float h0 = clone[(rowCurr + x - 1) * 4 + c];
                        float h2 = clone[(rowCurr + x + 1) * 4 + c];
                        float varH = Math.Abs(h0 - center) + Math.Abs(h2 - center);

                        // 1: Vertical
                        float v0 = clone[(rowPrev + x) * 4 + c];
                        float v2 = clone[(rowNext + x) * 4 + c];
                        float varV = Math.Abs(v0 - center) + Math.Abs(v2 - center);

                        // 2: Main Diagonal (45°)
                        float d0 = clone[(rowPrev + x - 1) * 4 + c];
                        float d2 = clone[(rowNext + x + 1) * 4 + c];
                        float varD = Math.Abs(d0 - center) + Math.Abs(d2 - center);

                        // 3: Anti-Diagonal (135°)
                        float a0 = clone[(rowPrev + x + 1) * 4 + c];
                        float a2 = clone[(rowNext + x - 1) * 4 + c];
                        float varA = Math.Abs(a0 - center) + Math.Abs(a2 - center);

                        float minVar = varH;
                        float p0 = h0, p2 = h2;

                        if (varV < minVar)
                        {
                            minVar = varV;
                            p0 = v0; p2 = v2;
                        }
                        if (varD < minVar)
                        {
                            minVar = varD;
                            p0 = d0; p2 = d2;
                        }
                        if (varA < minVar)
                        {
                            p0 = a0; p2 = a2;
                        }

                        float med = Median3Float(p0, center, p2);

                        if (Math.Abs(center - med) >= threshold)
                        {
                            pixels[p + c] = med;
                        }
                    }
                }
            });
        }

        private static byte Median3Byte(byte a, byte b, byte c)
        {
            if (a > b)
            {
                if (b > c) return b;
                return a > c ? c : a;
            }
            else
            {
                if (a > c) return a;
                return b > c ? c : b;
            }
        }

        private static float Median3Float(float a, float b, float c)
        {
            if (a > b)
            {
                if (b > c) return b;
                return a > c ? c : a;
            }
            else
            {
                if (a > c) return a;
                return b > c ? c : b;
            }
        }
    }
}
