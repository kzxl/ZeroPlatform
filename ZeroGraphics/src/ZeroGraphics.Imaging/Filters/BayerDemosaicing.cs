using System;
using ZeroGraphics.Imaging.Core;

namespace ZeroGraphics.Imaging.Filters
{
    /// <summary>
    /// High-performance Bayer CFA (Color Filter Array) demosaicing engine.
    /// Converts raw sensor streams (BayerRG8, BayerBG8, BayerGB8, BayerGR8) to Gray8 or BGRA32 with zero GC allocations.
    /// </summary>
    public static class BayerDemosaicing
    {
        /// <summary>
        /// Converts raw Bayer sensor data directly to 8-bit Grayscale in sub-millisecond time.
        /// Ideal for industrial barcode reading, edge caliper inspection, and template matching from color cameras.
        /// </summary>
        public static unsafe void DemosaicToGray8(ImageBuffer src, ImageBuffer dst)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            if (dst == null) throw new ArgumentNullException(nameof(dst));
            if (dst.Format != ImageFormatMode.Gray8)
                throw new ArgumentException("Destination image must be Gray8 format.");
            if (src.Width != dst.Width || src.Height != dst.Height)
                throw new ArgumentException("Dimensions must match.");

            int w = src.Width;
            int h = src.Height;

            // Pattern offsets: determine if (x, y) is Red, Green, or Blue
            GetBayerOffsets(src.Format, out int rRow, out int rCol);

            for (int y = 0; y < h; y++)
            {
                byte* pSrcRow = src.GetRowPointer(y);
                byte* pDstRow = dst.GetRowPointer(y);
                byte* pPrevRow = (y > 0) ? src.GetRowPointer(y - 1) : pSrcRow;
                byte* pNextRow = (y < h - 1) ? src.GetRowPointer(y + 1) : pSrcRow;

                bool isRorBRow = ((y % 2) == rRow);

                for (int x = 0; x < w; x++)
                {
                    bool isGreen = ((x % 2) != (isRorBRow ? rCol : (1 - rCol)));

                    if (isGreen)
                    {
                        // Direct Green pixel carries luminance primary
                        pDstRow[x] = pSrcRow[x];
                    }
                    else
                    {
                        // R or B pixel: Average 4 neighboring green pixels
                        int gLeft = (x > 0) ? pSrcRow[x - 1] : pSrcRow[x];
                        int gRight = (x < w - 1) ? pSrcRow[x + 1] : pSrcRow[x];
                        int gUp = pPrevRow[x];
                        int gDown = pNextRow[x];

                        pDstRow[x] = (byte)((gLeft + gRight + gUp + gDown + 2) >> 2);
                    }
                }
            }
        }

        /// <summary>
        /// Reconstructs full 32-bit color BGRA image from raw Bayer sensor pattern using bilinear demosaicing.
        /// </summary>
        public static unsafe void DemosaicToBgra32(ImageBuffer src, ImageBuffer dst)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            if (dst == null) throw new ArgumentNullException(nameof(dst));
            if (dst.Format != ImageFormatMode.Bgra32)
                throw new ArgumentException("Destination image must be Bgra32 format.");
            if (src.Width != dst.Width || src.Height != dst.Height)
                throw new ArgumentException("Dimensions must match.");

            int w = src.Width;
            int h = src.Height;

            GetBayerOffsets(src.Format, out int rRow, out int rCol);
            int bRow = 1 - rRow;
            int bCol = 1 - rCol;

            for (int y = 0; y < h; y++)
            {
                byte* pSrcRow = src.GetRowPointer(y);
                byte* pPrevRow = (y > 0) ? src.GetRowPointer(y - 1) : pSrcRow;
                byte* pNextRow = (y < h - 1) ? src.GetRowPointer(y + 1) : pSrcRow;

                byte* pDstRow = dst.GetRowPointer(y);

                bool isRedRow = ((y % 2) == rRow);
                bool isBlueRow = ((y % 2) == bRow);

                for (int x = 0; x < w; x++)
                {
                    int xPrev = (x > 0) ? x - 1 : x;
                    int xNext = (x < w - 1) ? x + 1 : x;

                    byte r, g, b;

                    if (isRedRow && (x % 2 == rCol))
                    {
                        // Center is Red
                        r = pSrcRow[x];
                        // Green: average 4 cross neighbors
                        g = (byte)((pSrcRow[xPrev] + pSrcRow[xNext] + pPrevRow[x] + pNextRow[x] + 2) >> 2);
                        // Blue: average 4 diagonal neighbors
                        b = (byte)((pPrevRow[xPrev] + pPrevRow[xNext] + pNextRow[xPrev] + pNextRow[xNext] + 2) >> 2);
                    }
                    else if (isBlueRow && (x % 2 == bCol))
                    {
                        // Center is Blue
                        b = pSrcRow[x];
                        // Green: average 4 cross neighbors
                        g = (byte)((pSrcRow[xPrev] + pSrcRow[xNext] + pPrevRow[x] + pNextRow[x] + 2) >> 2);
                        // Red: average 4 diagonal neighbors
                        r = (byte)((pPrevRow[xPrev] + pPrevRow[xNext] + pNextRow[xPrev] + pNextRow[xNext] + 2) >> 2);
                    }
                    else if (isRedRow)
                    {
                        // Center is Green (in Red row)
                        g = pSrcRow[x];
                        // Red: average left and right
                        r = (byte)((pSrcRow[xPrev] + pSrcRow[xNext] + 1) >> 1);
                        // Blue: average top and bottom
                        b = (byte)((pPrevRow[x] + pNextRow[x] + 1) >> 1);
                    }
                    else
                    {
                        // Center is Green (in Blue row)
                        g = pSrcRow[x];
                        // Red: average top and bottom
                        r = (byte)((pPrevRow[x] + pNextRow[x] + 1) >> 1);
                        // Blue: average left and right
                        b = (byte)((pSrcRow[xPrev] + pSrcRow[xNext] + 1) >> 1);
                    }

                    int dstIdx = x * 4;
                    pDstRow[dstIdx + 0] = b;
                    pDstRow[dstIdx + 1] = g;
                    pDstRow[dstIdx + 2] = r;
                    pDstRow[dstIdx + 3] = 255; // Alpha
                }
            }
        }

        private static void GetBayerOffsets(ImageFormatMode format, out int rRow, out int rCol)
        {
            switch (format)
            {
                case ImageFormatMode.BayerRG8:
                    rRow = 0; rCol = 0;
                    break;
                case ImageFormatMode.BayerGR8:
                    rRow = 0; rCol = 1;
                    break;
                case ImageFormatMode.BayerGB8:
                    rRow = 1; rCol = 0;
                    break;
                case ImageFormatMode.BayerBG8:
                    rRow = 1; rCol = 1;
                    break;
                default:
                    // Default to BayerRG8
                    rRow = 0; rCol = 0;
                    break;
            }
        }
    }
}
