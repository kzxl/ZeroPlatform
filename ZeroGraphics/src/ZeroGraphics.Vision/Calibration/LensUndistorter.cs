using System;
using ZeroGraphics.Imaging.Core;

namespace ZeroGraphics.Vision.Calibration
{
    /// <summary>
    /// Performs high-throughput geometric lens undistortion and rectification for camera frames.
    /// Supports pre-calculated remap coordinate tables with zero-allocation bilinear sampling.
    /// </summary>
    public static class LensUndistorter
    {
        /// <summary>
        /// Pre-calculates the reverse mapping coordinate grids (MapX, MapY) for a given resolution and intrinsic model.
        /// </summary>
        public static void CreateRectificationMap(
            int width, int height,
            CameraIntrinsics intrinsics,
            out float[] mapX, out float[] mapY)
        {
            if (width <= 0 || height <= 0) throw new ArgumentException("Invalid dimensions.");
            if (intrinsics == null) throw new ArgumentNullException(nameof(intrinsics));

            int total = width * height;
            mapX = new float[total];
            mapY = new float[total];

            for (int y = 0; y < height; y++)
            {
                int rowOffset = y * width;
                for (int x = 0; x < width; x++)
                {
                    // To find which source pixel maps to destination (x, y), apply forward distortion to ideal (x, y)
                    intrinsics.DistortPoint(x, y, out double srcX, out double srcY);
                    mapX[rowOffset + x] = (float)srcX;
                    mapY[rowOffset + x] = (float)srcY;
                }
            }
        }

        /// <summary>
        /// Undistorts a camera frame using precomputed rectification maps.
        /// </summary>
        public static unsafe void UndistortWithMap(
            ImageBuffer src,
            ImageBuffer dst,
            float[] mapX,
            float[] mapY)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            if (dst == null) throw new ArgumentNullException(nameof(dst));
            if (src.Width != dst.Width || src.Height != dst.Height)
                throw new ArgumentException("Source and destination dimensions must match.");
            if (src.Format != dst.Format)
                throw new ArgumentException("Source and destination pixel formats must match.");

            int w = src.Width;
            int h = src.Height;

            if (src.Format == ImageFormatMode.Gray8)
            {
                fixed (float* pMapX = mapX, pMapY = mapY)
                {
                    for (int y = 0; y < h; y++)
                    {
                        byte* pDstRow = dst.GetRowPointer(y);
                        int rowOffset = y * w;

                        for (int x = 0; x < w; x++)
                        {
                            float sx = pMapX[rowOffset + x];
                            float sy = pMapY[rowOffset + x];

                            pDstRow[x] = SampleBilinearGray(src, sx, sy, w, h);
                        }
                    }
                }
            }
            else if (src.Format == ImageFormatMode.Bgra32)
            {
                fixed (float* pMapX = mapX, pMapY = mapY)
                {
                    for (int y = 0; y < h; y++)
                    {
                        uint* pDstRow = (uint*)dst.GetRowPointer(y);
                        int rowOffset = y * w;

                        for (int x = 0; x < w; x++)
                        {
                            float sx = pMapX[rowOffset + x];
                            float sy = pMapY[rowOffset + x];

                            pDstRow[x] = SampleBilinearBgra(src, sx, sy, w, h);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Direct single-call undistortion without explicit map management.
        /// </summary>
        public static void Undistort(ImageBuffer src, ImageBuffer dst, CameraIntrinsics intrinsics)
        {
            CreateRectificationMap(src.Width, src.Height, intrinsics, out var mapX, out var mapY);
            UndistortWithMap(src, dst, mapX, mapY);
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

        private static unsafe byte SampleBilinearGray(ImageBuffer src, float sx, float sy, int w, int h)
        {
            if (sx < 0 || sx >= w - 1 || sy < 0 || sy >= h - 1)
            {
                if (sx < -1 || sx > w || sy < -1 || sy > h) return 0;
                int cx = Clamp((int)Math.Round(sx), 0, w - 1);
                int cy = Clamp((int)Math.Round(sy), 0, h - 1);
                return src.GetRowPointer(cy)[cx];
            }

            int x0 = (int)sx;
            int y0 = (int)sy;
            int x1 = x0 + 1;
            int y1 = y0 + 1;

            float fx = sx - x0;
            float fy = sy - y0;

            byte* row0 = src.GetRowPointer(y0);
            byte* row1 = src.GetRowPointer(y1);

            float v00 = row0[x0];
            float v10 = row0[x1];
            float v01 = row1[x0];
            float v11 = row1[x1];

            float val = (1.0f - fx) * (1.0f - fy) * v00 +
                        fx * (1.0f - fy) * v10 +
                        (1.0f - fx) * fy * v01 +
                        fx * fy * v11;

            return ClampByte((int)(val + 0.5f));
        }

        private static unsafe uint SampleBilinearBgra(ImageBuffer src, float sx, float sy, int w, int h)
        {
            if (sx < 0 || sx >= w - 1 || sy < 0 || sy >= h - 1)
            {
                int cx = Clamp((int)Math.Round(sx), 0, w - 1);
                int cy = Clamp((int)Math.Round(sy), 0, h - 1);
                return ((uint*)src.GetRowPointer(cy))[cx];
            }

            int x0 = (int)sx;
            int y0 = (int)sy;
            int x1 = x0 + 1;
            int y1 = y0 + 1;

            float fx = sx - x0;
            float fy = sy - y0;

            byte* p00 = src.GetRowPointer(y0) + x0 * 4;
            byte* p10 = src.GetRowPointer(y0) + x1 * 4;
            byte* p01 = src.GetRowPointer(y1) + x0 * 4;
            byte* p11 = src.GetRowPointer(y1) + x1 * 4;

            float w00 = (1.0f - fx) * (1.0f - fy);
            float w10 = fx * (1.0f - fy);
            float w01 = (1.0f - fx) * fy;
            float w11 = fx * fy;

            byte b = ClampByte((int)(w00 * p00[0] + w10 * p10[0] + w01 * p01[0] + w11 * p11[0] + 0.5f));
            byte g = ClampByte((int)(w00 * p00[1] + w10 * p10[1] + w01 * p01[1] + w11 * p11[1] + 0.5f));
            byte r = ClampByte((int)(w00 * p00[2] + w10 * p10[2] + w01 * p01[2] + w11 * p11[2] + 0.5f));
            byte a = ClampByte((int)(w00 * p00[3] + w10 * p10[3] + w01 * p01[3] + w11 * p11[3] + 0.5f));

            return (uint)(b | (g << 8) | (r << 16) | (a << 24));
        }
    }
}
