using System;
using ZeroGraphics.Core.Native;
using ZeroGraphics.Imaging.Core;

namespace ZeroGraphics.Imaging.Filters
{
    /// <summary>
    /// Mathematical morphology filters for binary and grayscale image analysis:
    /// - Dilation (Expand white regions / fill small gaps)
    /// - Erosion (Shrink white regions / eliminate noise specks)
    /// - Opening (Erosion followed by Dilation: removes isolated salt noise)
    /// - Closing (Dilation followed by Erosion: connects broken lines and fills pinholes)
    /// </summary>
    public static unsafe class Morphology
    {
        /// <summary>
        /// Applies Morphological Dilation (Max filter over rectangular structuring element).
        /// </summary>
        public static void Dilate(ImageBuffer src, ImageBuffer dst, int radius = 1)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            if (dst == null) throw new ArgumentNullException(nameof(dst));
            if (radius < 1) radius = 1;

            int width = src.Width;
            int height = src.Height;

            if (radius == 1 &&
                src.Format == ImageFormatMode.Gray8 &&
                dst.Format == ImageFormatMode.Gray8 &&
                src.Scan0 != dst.Scan0 &&
                ZeroGraphicsNativeBridge.ShouldDispatchImage(width, height) &&
                ZeroGraphicsNativeBridge.TryMorphology3x3(src.Scan0, src.Stride, dst.Scan0, dst.Stride, width, height, mode: 1))
            {
                return;
            }

            for (int y = 0; y < height; y++)
            {
                byte* dstRow = dst.GetRowPointer(y);
                int yMin = global::System.Math.Max(0, y - radius);
                int yMax = global::System.Math.Min(height - 1, y + radius);

                for (int x = 0; x < width; x++)
                {
                    int xMin = global::System.Math.Max(0, x - radius);
                    int xMax = global::System.Math.Min(width - 1, x + radius);

                    byte maxVal = 0;
                    for (int ky = yMin; ky <= yMax; ky++)
                    {
                        byte* srcRow = src.GetRowPointer(ky);
                        for (int kx = xMin; kx <= xMax; kx++)
                        {
                            byte val = srcRow[kx];
                            if (val > maxVal)
                            {
                                maxVal = val;
                                if (maxVal == 255) break; // Early exit on maximum possible value
                            }
                        }
                        if (maxVal == 255) break;
                    }
                    dstRow[x] = maxVal;
                }
            }
        }

        /// <summary>
        /// Applies Morphological Erosion (Min filter over rectangular structuring element).
        /// </summary>
        public static void Erode(ImageBuffer src, ImageBuffer dst, int radius = 1)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            if (dst == null) throw new ArgumentNullException(nameof(dst));
            if (radius < 1) radius = 1;

            int width = src.Width;
            int height = src.Height;

            if (radius == 1 &&
                src.Format == ImageFormatMode.Gray8 &&
                dst.Format == ImageFormatMode.Gray8 &&
                src.Scan0 != dst.Scan0 &&
                ZeroGraphicsNativeBridge.ShouldDispatchImage(width, height) &&
                ZeroGraphicsNativeBridge.TryMorphology3x3(src.Scan0, src.Stride, dst.Scan0, dst.Stride, width, height, mode: 0))
            {
                return;
            }

            for (int y = 0; y < height; y++)
            {
                byte* dstRow = dst.GetRowPointer(y);
                int yMin = global::System.Math.Max(0, y - radius);
                int yMax = global::System.Math.Min(height - 1, y + radius);

                for (int x = 0; x < width; x++)
                {
                    int xMin = global::System.Math.Max(0, x - radius);
                    int xMax = global::System.Math.Min(width - 1, x + radius);

                    byte minVal = 255;
                    for (int ky = yMin; ky <= yMax; ky++)
                    {
                        byte* srcRow = src.GetRowPointer(ky);
                        for (int kx = xMin; kx <= xMax; kx++)
                        {
                            byte val = srcRow[kx];
                            if (val < minVal)
                            {
                                minVal = val;
                                if (minVal == 0) break; // Early exit on minimum possible value
                            }
                        }
                        if (minVal == 0) break;
                    }
                    dstRow[x] = minVal;
                }
            }
        }

        /// <summary>
        /// Morphological Opening (Erosion followed by Dilation).
        /// Removes small foreground noise, specks, and thin protrusions without altering overall object size.
        /// </summary>
        public static void Open(ImageBuffer src, ImageBuffer dst, int radius = 1)
        {
            using (var temp = new ImageBuffer(src.Width, src.Height, src.Format))
            {
                Erode(src, temp, radius);
                Dilate(temp, dst, radius);
            }
        }

        /// <summary>
        /// Morphological Closing (Dilation followed by Erosion).
        /// Fills small holes, bridges thin gaps, and smooths object contours.
        /// </summary>
        public static void Close(ImageBuffer src, ImageBuffer dst, int radius = 1)
        {
            using (var temp = new ImageBuffer(src.Width, src.Height, src.Format))
            {
                Dilate(src, temp, radius);
                Erode(temp, dst, radius);
            }
        }
    }
}
