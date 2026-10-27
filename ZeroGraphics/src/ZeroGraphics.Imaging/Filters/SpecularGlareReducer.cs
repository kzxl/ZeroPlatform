using System;
using ZeroGraphics.Imaging.Core;

namespace ZeroGraphics.Imaging.Filters
{
    /// <summary>
    /// Industrial specular glare and highlight suppressor.
    /// Detects blinding saturated glare spots caused by ring-light reflections on metal parts or glossy packaging,
    /// and reconstructs the underlying surface intensity to preserve barcode bars and edge contours.
    /// </summary>
    public static class SpecularGlareReducer
    {
        /// <summary>
        /// Suppresses specular reflections on a grayscale image.
        /// </summary>
        /// <param name="src">Input image buffer.</param>
        /// <param name="dst">Destination image buffer.</param>
        /// <param name="glareThreshold">Pixel intensity threshold considered blinding reflection (default 245).</param>
        /// <param name="searchRadius">Neighborhood radius to sample non-glare background (default 3 pixels).</param>
        public static unsafe void SuppressGlare(
            ImageBuffer src,
            ImageBuffer dst,
            byte glareThreshold = 245,
            int searchRadius = 3)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            if (dst == null) throw new ArgumentNullException(nameof(dst));
            if (src.Format != ImageFormatMode.Gray8 || dst.Format != ImageFormatMode.Gray8)
                throw new NotSupportedException("Glare suppression requires Gray8 images.");

            int w = src.Width;
            int h = src.Height;

            // Copy source to destination first
            src.CopyTo(dst);

            for (int y = 0; y < h; y++)
            {
                byte* pDstRow = dst.GetRowPointer(y);
                byte* pSrcRow = src.GetRowPointer(y);

                for (int x = 0; x < w; x++)
                {
                    if (pSrcRow[x] >= glareThreshold)
                    {
                        // Sample non-glare surrounding context
                        int sum = 0;
                        int count = 0;

                        for (int dy = -searchRadius; dy <= searchRadius; dy++)
                        {
                            int ny = y + dy;
                            if (ny < 0 || ny >= h) continue;

                            byte* nRow = src.GetRowPointer(ny);
                            for (int dx = -searchRadius; dx <= searchRadius; dx++)
                            {
                                int nx = x + dx;
                                if (nx < 0 || nx >= w) continue;

                                byte neighbor = nRow[nx];
                                if (neighbor < glareThreshold)
                                {
                                    sum += neighbor;
                                    count++;
                                }
                            }
                        }

                        if (count > 0)
                        {
                            pDstRow[x] = (byte)(sum / count);
                        }
                    }
                }
            }
        }
    }
}
