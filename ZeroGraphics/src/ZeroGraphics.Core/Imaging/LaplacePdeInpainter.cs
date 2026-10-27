using System;
using System.Buffers;

namespace ZeroGraphics.Core.Imaging
{
    using Math = global::System.Math;
    /// <summary>
    /// Pure C# non-destructive image inpainting engine based on partial differential equation (PDE) Laplace smoothing
    /// and Poisson gradient diffusion. Seamlessly synthesizes missing pixels, blemishes, spots, and scratches.
    /// </summary>
    public static class LaplacePdeInpainter
    {
        /// <summary>
        /// Inpaints masked pixels in an interleaved 32-bit RGBA float image buffer (values 0.0f - 1.0f).
        /// </summary>
        /// <param name="rgbaPixels">Interleaved R, G, B, A float pixels (length must be at least width * height * 4).</param>
        /// <param name="width">Image width in pixels.</param>
        /// <param name="height">Image height in pixels.</param>
        /// <param name="mask">Binary mask of length width * height (true indicates damaged/hole pixel).</param>
        /// <param name="iterations">Number of PDE relaxation iterations (typically 8 to 20).</param>
        /// <param name="strength">Blending strength of inpainted region into original pixels (0.0f - 1.0f).</param>
        public static void InpaintRgba(
            Span<float> rgbaPixels,
            int width,
            int height,
            ReadOnlySpan<bool> mask,
            int iterations = 10,
            float strength = 1.0f)
        {
            if (rgbaPixels.Length < width * height * 4 || mask.Length < width * height)
            {
                throw new ArgumentException("Pixel buffer or mask buffer is smaller than specified width * height.");
            }

            if (iterations <= 0 || strength <= 0.0f) return;

            // 1. Locate bounding box of the mask to minimize processing area
            int minX = width, maxX = -1, minY = height, maxY = -1;
            int holeCount = 0;

            for (int y = 0; y < height; y++)
            {
                int rowOff = y * width;
                for (int x = 0; x < width; x++)
                {
                    if (mask[rowOff + x])
                    {
                        holeCount++;
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }

            if (holeCount == 0 || maxX < minX || maxY < minY) return;

            // Expand bounding box by 1 pixel for boundary conditions
            int rx0 = Math.Max(0, minX - 1);
            int ry0 = Math.Max(0, minY - 1);
            int rx1 = Math.Min(width - 1, maxX + 1);
            int ry1 = Math.Min(height - 1, maxY + 1);

            int rw = rx1 - rx0 + 1;
            int rh = ry1 - ry0 + 1;
            int regionSize = rw * rh;

            // 2. Rent working buffers from ArrayPool
            var pool = ArrayPool<float>.Shared;
            float[] bufR = pool.Rent(regionSize);
            float[] bufG = pool.Rent(regionSize);
            float[] bufB = pool.Rent(regionSize);

            try
            {
                // 3. Initialize working buffer and compute ambient boundary mean
                float sumR = 0f, sumG = 0f, sumB = 0f;
                int borderCount = 0;

                for (int y = 0; y < rh; y++)
                {
                    int gy = ry0 + y;
                    int gRow = gy * width;
                    for (int x = 0; x < rw; x++)
                    {
                        int gx = rx0 + x;
                        int mIdx = gRow + gx;
                        int rIdx = y * rw + x;
                        int pOff = (gRow + gx) * 4;

                        bufR[rIdx] = rgbaPixels[pOff];
                        bufG[rIdx] = rgbaPixels[pOff + 1];
                        bufB[rIdx] = rgbaPixels[pOff + 2];

                        if (!mask[mIdx])
                        {
                            sumR += rgbaPixels[pOff];
                            sumG += rgbaPixels[pOff + 1];
                            sumB += rgbaPixels[pOff + 2];
                            borderCount++;
                        }
                    }
                }

                // Fill holes with ambient mean to accelerate Laplace convergence
                if (borderCount > 0)
                {
                    float avgR = sumR / borderCount;
                    float avgG = sumG / borderCount;
                    float avgB = sumB / borderCount;

                    for (int y = 0; y < rh; y++)
                    {
                        int gRow = (ry0 + y) * width;
                        for (int x = 0; x < rw; x++)
                        {
                            int mIdx = gRow + (rx0 + x);
                            if (mask[mIdx])
                            {
                                int rIdx = y * rw + x;
                                bufR[rIdx] = avgR;
                                bufG[rIdx] = avgG;
                                bufB[rIdx] = avgB;
                            }
                        }
                    }
                }

                // 4. Perform iterative 4-neighbor Laplace smoothing over masked pixels
                for (int iter = 0; iter < iterations; iter++)
                {
                    for (int y = 1; y < rh - 1; y++)
                    {
                        int gy = ry0 + y;
                        int gRow = gy * width;
                        for (int x = 1; x < rw - 1; x++)
                        {
                            int gx = rx0 + x;
                            int mIdx = gRow + gx;
                            if (!mask[mIdx]) continue;

                            int idx = y * rw + x;
                            int up = (y - 1) * rw + x;
                            int down = (y + 1) * rw + x;
                            int left = y * rw + (x - 1);
                            int right = y * rw + (x + 1);

                            bufR[idx] = 0.25f * (bufR[up] + bufR[down] + bufR[left] + bufR[right]);
                            bufG[idx] = 0.25f * (bufG[up] + bufG[down] + bufG[left] + bufG[right]);
                            bufB[idx] = 0.25f * (bufB[up] + bufB[down] + bufB[left] + bufB[right]);
                        }
                    }
                }

                // 5. Write diffused pixels back with strength blending
                for (int y = 0; y < rh; y++)
                {
                    int gy = ry0 + y;
                    int gRow = gy * width;
                    for (int x = 0; x < rw; x++)
                    {
                        int gx = rx0 + x;
                        int mIdx = gRow + gx;
                        if (!mask[mIdx]) continue;

                        int rIdx = y * rw + x;
                        int pOff = (gRow + gx) * 4;

                        if (strength >= 0.999f)
                        {
                            rgbaPixels[pOff] = bufR[rIdx];
                            rgbaPixels[pOff + 1] = bufG[rIdx];
                            rgbaPixels[pOff + 2] = bufB[rIdx];
                        }
                        else
                        {
                            rgbaPixels[pOff] = rgbaPixels[pOff] + (bufR[rIdx] - rgbaPixels[pOff]) * strength;
                            rgbaPixels[pOff + 1] = rgbaPixels[pOff + 1] + (bufG[rIdx] - rgbaPixels[pOff + 1]) * strength;
                            rgbaPixels[pOff + 2] = rgbaPixels[pOff + 2] + (bufB[rIdx] - rgbaPixels[pOff + 2]) * strength;
                        }
                    }
                }
            }
            finally
            {
                pool.Return(bufR);
                pool.Return(bufG);
                pool.Return(bufB);
            }
        }

        /// <summary>
        /// Inpaints a circular spot region defined by center and radius.
        /// </summary>
        public static void InpaintSpotRgba(
            Span<float> rgbaPixels,
            int width,
            int height,
            int centerX,
            int centerY,
            int radius,
            int iterations = 10,
            float strength = 1.0f)
        {
            if (radius <= 0) return;

            int x0 = Math.Max(0, centerX - radius);
            int x1 = Math.Min(width - 1, centerX + radius);
            int y0 = Math.Max(0, centerY - radius);
            int y1 = Math.Min(height - 1, centerY + radius);

            int maskLen = width * height;
            bool[] mask = new bool[maskLen];
            int radSq = radius * radius;

            for (int y = y0; y <= y1; y++)
            {
                int dy = y - centerY;
                int rowOff = y * width;
                for (int x = x0; x <= x1; x++)
                {
                    int dx = x - centerX;
                    if (dx * dx + dy * dy <= radSq)
                    {
                        mask[rowOff + x] = true;
                    }
                }
            }

            InpaintRgba(rgbaPixels, width, height, mask, iterations, strength);
        }

        /// <summary>
        /// Inpaints masked pixels in a 32-bit BGRA byte image buffer (standard Windows/WPF bitmap format).
        /// </summary>
        public static void InpaintBgra32(
            Span<byte> bgraBytes,
            int width,
            int height,
            ReadOnlySpan<bool> mask,
            int iterations = 10,
            float strength = 1.0f)
        {
            if (bgraBytes.Length < width * height * 4 || mask.Length < width * height)
            {
                throw new ArgumentException("Byte buffer or mask buffer is smaller than specified width * height.");
            }

            int count = width * height;
            var pool = ArrayPool<float>.Shared;
            float[] rgba = pool.Rent(count * 4);

            try
            {
                // Convert BGRA byte [0-255] to RGBA float [0.0-1.0]
                for (int i = 0; i < count; i++)
                {
                    int bOff = i * 4;
                    rgba[bOff] = bgraBytes[bOff + 2] / 255.0f;     // R
                    rgba[bOff + 1] = bgraBytes[bOff + 1] / 255.0f; // G
                    rgba[bOff + 2] = bgraBytes[bOff] / 255.0f;     // B
                    rgba[bOff + 3] = bgraBytes[bOff + 3] / 255.0f; // A
                }

                InpaintRgba(rgba.AsSpan(0, count * 4), width, height, mask, iterations, strength);

                // Convert back RGBA float [0.0-1.0] to BGRA byte [0-255]
                for (int i = 0; i < count; i++)
                {
                    int bOff = i * 4;
                    bgraBytes[bOff] = (byte)Math.Max(0, Math.Min(255, (int)Math.Round(rgba[bOff + 2] * 255.0f))); // B
                    bgraBytes[bOff + 1] = (byte)Math.Max(0, Math.Min(255, (int)Math.Round(rgba[bOff + 1] * 255.0f))); // G
                    bgraBytes[bOff + 2] = (byte)Math.Max(0, Math.Min(255, (int)Math.Round(rgba[bOff] * 255.0f))); // R
                    // Alpha is preserved
                }
            }
            finally
            {
                pool.Return(rgba);
            }
        }
    }
}
