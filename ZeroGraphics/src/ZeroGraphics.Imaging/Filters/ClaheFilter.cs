using System;
using System.Buffers;
using ZeroGraphics.Imaging.Core;

namespace ZeroGraphics.Imaging.Filters
{
    /// <summary>
    /// Contrast Limited Adaptive Histogram Equalization (CLAHE).
    /// Enhances local contrast and reveals hidden details in poorly lit, shadowy, or overexposed industrial inspection areas.
    /// Pure C# unsafe implementation with zero GC heap allocations.
    /// </summary>
    public static class ClaheFilter
    {
        /// <summary>
        /// Linearly stretches pixel intensities from [min, max] to full dynamic range [0, 255].
        /// Zero allocations, ultra-fast linear contrast normalization.
        /// </summary>
        public static unsafe void StretchContrast(ImageBuffer src, ImageBuffer dst)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            if (dst == null) throw new ArgumentNullException(nameof(dst));
            if (src.Format != ImageFormatMode.Gray8 || dst.Format != ImageFormatMode.Gray8)
                throw new NotSupportedException("StretchContrast requires Gray8 images.");

            int w = src.Width;
            int h = src.Height;

            byte min = 255;
            byte max = 0;

            for (int y = 0; y < h; y++)
            {
                byte* row = src.GetRowPointer(y);
                for (int x = 0; x < w; x++)
                {
                    byte v = row[x];
                    if (v < min) min = v;
                    if (v > max) max = v;
                }
            }

            if (max <= min)
            {
                src.CopyTo(dst);
                return;
            }

            float scale = 255.0f / (max - min);

            for (int y = 0; y < h; y++)
            {
                byte* sRow = src.GetRowPointer(y);
                byte* dRow = dst.GetRowPointer(y);
                for (int x = 0; x < w; x++)
                {
                    int v = (int)((sRow[x] - min) * scale + 0.5f);
                    dRow[x] = ClampByte(v);
                }
            }
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private static byte ClampByte(int value)
        {
            if (value < 0) return 0;
            if (value > 255) return 255;
            return (byte)value;
        }

        /// <summary>
        /// Applies CLAHE to an 8-bit grayscale image.
        /// </summary>
        /// <param name="src">Source grayscale image.</param>
        /// <param name="dst">Destination grayscale image (can be same as src for in-place).</param>
        /// <param name="clipLimit">Normalized contrast clipping limit (typical 2.0 to 4.0; higher means more contrast).</param>
        /// <param name="tilesX">Number of horizontal contextual tiles (default 8).</param>
        /// <param name="tilesY">Number of vertical contextual tiles (default 8).</param>
        public static unsafe void Apply(
            ImageBuffer src,
            ImageBuffer dst,
            float clipLimit = 3.0f,
            int tilesX = 8,
            int tilesY = 8)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            if (dst == null) throw new ArgumentNullException(nameof(dst));
            if (src.Format != ImageFormatMode.Gray8 || dst.Format != ImageFormatMode.Gray8)
                throw new NotSupportedException("CLAHE currently operates directly on Gray8 images.");
            if (src.Width != dst.Width || src.Height != dst.Height)
                throw new ArgumentException("Source and destination dimensions must match.");

            int w = src.Width;
            int h = src.Height;

            tilesX = Clamp(tilesX, 2, 32);
            tilesY = Clamp(tilesY, 2, 32);

            int tileSizeX = w / tilesX;
            int tileSizeY = h / tilesY;
            if (tileSizeX < 4 || tileSizeY < 4) return;

            int totalTiles = tilesX * tilesY;
            // Lookup tables: 256 bytes per tile CDF mapping
            byte[] cdfPool = ArrayPool<byte>.Shared.Rent(totalTiles * 256);

            try
            {
                fixed (byte* pCdf = cdfPool)
                {
                    // 1. Calculate clipped CDF mapping for each tile
                    int* hist = stackalloc int[256];

                    for (int ty = 0; ty < tilesY; ty++)
                    {
                        int yStart = ty * tileSizeY;
                        int yEnd = (ty == tilesY - 1) ? h : yStart + tileSizeY;
                        int tileH = yEnd - yStart;

                        for (int tx = 0; tx < tilesX; tx++)
                        {
                            int xStart = tx * tileSizeX;
                            int xEnd = (tx == tilesX - 1) ? w : xStart + tileSizeX;
                            int tileW = xEnd - xStart;
                            int tilePixels = tileW * tileH;

                            // Clear histogram
                            for (int i = 0; i < 256; i++) hist[i] = 0;

                            // Compute tile histogram
                            for (int y = yStart; y < yEnd; y++)
                            {
                                byte* row = src.GetRowPointer(y);
                                for (int x = xStart; x < xEnd; x++)
                                {
                                    hist[row[x]]++;
                                }
                            }

                            // Calculate clip limit threshold
                            int actualClipLimit = (int)Math.Max(1, (clipLimit * tilePixels) / 256.0f);

                            // Clip histogram and accumulate excess
                            int excess = 0;
                            for (int i = 0; i < 256; i++)
                            {
                                if (hist[i] > actualClipLimit)
                                {
                                    excess += hist[i] - actualClipLimit;
                                    hist[i] = actualClipLimit;
                                }
                            }

                            // Evenly redistribute excess among all bins
                            int bonusPerBin = excess / 256;
                            int remainder = excess % 256;

                            for (int i = 0; i < 256; i++)
                            {
                                hist[i] += bonusPerBin;
                                if (i < remainder) hist[i]++;
                            }

                            // Compute CDF and normalize to 0..255
                            byte* tileCdf = pCdf + (ty * tilesX + tx) * 256;
                            int sum = 0;
                            float scale = 255.0f / tilePixels;

                            for (int i = 0; i < 256; i++)
                            {
                                sum += hist[i];
                                int val = (int)(sum * scale + 0.5f);
                                tileCdf[i] = ClampByte(val);
                            }
                        }
                    }

                    // 2. Bilinear interpolation across tiles for smooth pixel enhancement
                    for (int y = 0; y < h; y++)
                    {
                        byte* pSrcRow = src.GetRowPointer(y);
                        byte* pDstRow = dst.GetRowPointer(y);

                        // Find vertical tile context
                        float yNorm = ((float)y / tileSizeY) - 0.5f;
                        int ty1 = (int)Math.Floor(yNorm);
                        int ty2 = ty1 + 1;
                        float fy = yNorm - ty1;

                        ty1 = Clamp(ty1, 0, tilesY - 1);
                        ty2 = Clamp(ty2, 0, tilesY - 1);

                        for (int x = 0; x < w; x++)
                        {
                            byte val = pSrcRow[x];

                            // Find horizontal tile context
                            float xNorm = ((float)x / tileSizeX) - 0.5f;
                            int tx1 = (int)Math.Floor(xNorm);
                            int tx2 = tx1 + 1;
                            float fx = xNorm - tx1;

                            tx1 = Clamp(tx1, 0, tilesX - 1);
                            tx2 = Clamp(tx2, 0, tilesX - 1);

                            byte cdf00 = pCdf[(ty1 * tilesX + tx1) * 256 + val];
                            byte cdf10 = pCdf[(ty1 * tilesX + tx2) * 256 + val];
                            byte cdf01 = pCdf[(ty2 * tilesX + tx1) * 256 + val];
                            byte cdf11 = pCdf[(ty2 * tilesX + tx2) * 256 + val];

                            // Bilinear interpolation formula
                            float interpolated = (1.0f - fx) * (1.0f - fy) * cdf00 +
                                                 fx * (1.0f - fy) * cdf10 +
                                                 (1.0f - fx) * fy * cdf01 +
                                                 fx * fy * cdf11;

                            pDstRow[x] = ClampByte((int)(interpolated + 0.5f));
                        }
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(cdfPool);
            }
        }
    }
}
