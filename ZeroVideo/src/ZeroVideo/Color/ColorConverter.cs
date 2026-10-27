using System;
using System.Runtime.CompilerServices;
using ZeroVideo.Core;

namespace ZeroVideo.Color
{
    /// <summary>
    /// Ultra-high-speed color space converter for industrial and consumer video streams.
    /// Implements pure C# integer-scaled ITU-R BT.601 color conversions without native dependencies.
    /// </summary>
    public static class ColorConverter
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte Clamp(int val)
        {
            if (val < 0) return 0;
            if (val > 255) return 255;
            return (byte)val;
        }

        /// <summary>
        /// Converts a planar YUV 4:2:0 frame (Yuv420p) to packed 24-bit RGB or BGR.
        /// </summary>
        public static void Yuv420pToRgb(VideoFrameBuffer src, VideoFrameBuffer dst, bool isBgr = false)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            if (dst == null) throw new ArgumentNullException(nameof(dst));
            if (src.Width != dst.Width || src.Height != dst.Height)
                throw new ArgumentException("Source and destination dimensions must match.");

            int w = src.Width;
            int h = src.Height;

            int uvHeight = (h + 1) / 2;
            int uvStride = (src.Stride + 1) / 2;
            int yPlaneSize = src.Stride * h;
            int uPlaneOffset = yPlaneSize;
            int uPlaneSize = uvStride * uvHeight;
            int vPlaneOffset = yPlaneSize + uPlaneSize;

            ReadOnlySpan<byte> s = src.AsReadOnlySpan();
            Span<byte> d = dst.AsSpan();

            int requiredSrc = vPlaneOffset + uPlaneSize;
            int requiredDst = h * dst.Stride;
            if (s.Length < requiredSrc || d.Length < requiredDst)
                throw new ArgumentException("Source frame buffer or destination buffer is too small for the specified video dimensions and stride.");

            for (int y = 0; y < h; y++)
            {
                int yRow = y * src.Stride;
                int uvRow = (y / 2) * uvStride;
                int dRow = y * dst.Stride;

                int x = 0;
                int wPairs = w - (w & 1); // Process pairs of pixels sharing UV
                for (; x < wPairs; x += 2)
                {
                    int uVal = s[uPlaneOffset + uvRow + (x >> 1)] - 128;
                    int vVal = s[vPlaneOffset + uvRow + (x >> 1)] - 128;

                    // Fixed-point BT.601 chroma deltas (computed once per pixel pair)
                    int cR = (359 * vVal) >> 8;
                    int cG = (88 * uVal + 183 * vVal) >> 8;
                    int cB = (454 * uVal) >> 8;

                    // Pixel 1
                    int yVal1 = s[yRow + x];
                    int dstIdx1 = dRow + (x * 3);
                    if (isBgr)
                    {
                        d[dstIdx1] = Clamp(yVal1 + cB);
                        d[dstIdx1 + 1] = Clamp(yVal1 - cG);
                        d[dstIdx1 + 2] = Clamp(yVal1 + cR);
                    }
                    else
                    {
                        d[dstIdx1] = Clamp(yVal1 + cR);
                        d[dstIdx1 + 1] = Clamp(yVal1 - cG);
                        d[dstIdx1 + 2] = Clamp(yVal1 + cB);
                    }

                    // Pixel 2
                    int yVal2 = s[yRow + x + 1];
                    int dstIdx2 = dstIdx1 + 3;
                    if (isBgr)
                    {
                        d[dstIdx2] = Clamp(yVal2 + cB);
                        d[dstIdx2 + 1] = Clamp(yVal2 - cG);
                        d[dstIdx2 + 2] = Clamp(yVal2 + cR);
                    }
                    else
                    {
                        d[dstIdx2] = Clamp(yVal2 + cR);
                        d[dstIdx2 + 1] = Clamp(yVal2 - cG);
                        d[dstIdx2 + 2] = Clamp(yVal2 + cB);
                    }
                }

                // Trailing odd pixel if width is odd
                for (; x < w; x++)
                {
                    int yVal = s[yRow + x];
                    int uVal = s[uPlaneOffset + uvRow + (x >> 1)] - 128;
                    int vVal = s[vPlaneOffset + uvRow + (x >> 1)] - 128;

                    int r = yVal + ((359 * vVal) >> 8);
                    int g = yVal - ((88 * uVal + 183 * vVal) >> 8);
                    int b = yVal + ((454 * uVal) >> 8);

                    int dstIdx = dRow + (x * 3);
                    if (isBgr)
                    {
                        d[dstIdx] = Clamp(b);
                        d[dstIdx + 1] = Clamp(g);
                        d[dstIdx + 2] = Clamp(r);
                    }
                    else
                    {
                        d[dstIdx] = Clamp(r);
                        d[dstIdx + 1] = Clamp(g);
                        d[dstIdx + 2] = Clamp(b);
                    }
                }
            }
        }

        /// <summary>
        /// Converts a semi-planar NV12 frame to packed 24-bit RGB or BGR.
        /// </summary>
        public static void Nv12ToRgb(VideoFrameBuffer src, VideoFrameBuffer dst, bool isBgr = false)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            if (dst == null) throw new ArgumentNullException(nameof(dst));
            if (src.Width != dst.Width || src.Height != dst.Height)
                throw new ArgumentException("Source and destination dimensions must match.");

            int w = src.Width;
            int h = src.Height;

            int uvOffset = src.Stride * h;
            int uvHeight = (h + 1) / 2;
            int requiredSrc = uvOffset + uvHeight * src.Stride;
            int requiredDst = h * dst.Stride;

            ReadOnlySpan<byte> s = src.AsReadOnlySpan();
            Span<byte> d = dst.AsSpan();

            if (s.Length < requiredSrc || d.Length < requiredDst)
                throw new ArgumentException("Source frame buffer or destination buffer is too small for the specified video dimensions and stride.");

            for (int y = 0; y < h; y++)
            {
                int yRow = y * src.Stride;
                int uvRow = uvOffset + (y / 2) * src.Stride;
                int dRow = y * dst.Stride;

                int x = 0;
                int wPairs = w - (w & 1);
                for (; x < wPairs; x += 2)
                {
                    int uvIdx = uvRow + x;
                    int uVal = s[uvIdx] - 128;
                    int vVal = s[uvIdx + 1] - 128;

                    int cR = (359 * vVal) >> 8;
                    int cG = (88 * uVal + 183 * vVal) >> 8;
                    int cB = (454 * uVal) >> 8;

                    // Pixel 1
                    int yVal1 = s[yRow + x];
                    int dstIdx1 = dRow + (x * 3);
                    if (isBgr)
                    {
                        d[dstIdx1] = Clamp(yVal1 + cB);
                        d[dstIdx1 + 1] = Clamp(yVal1 - cG);
                        d[dstIdx1 + 2] = Clamp(yVal1 + cR);
                    }
                    else
                    {
                        d[dstIdx1] = Clamp(yVal1 + cR);
                        d[dstIdx1 + 1] = Clamp(yVal1 - cG);
                        d[dstIdx1 + 2] = Clamp(yVal1 + cB);
                    }

                    // Pixel 2
                    int yVal2 = s[yRow + x + 1];
                    int dstIdx2 = dstIdx1 + 3;
                    if (isBgr)
                    {
                        d[dstIdx2] = Clamp(yVal2 + cB);
                        d[dstIdx2 + 1] = Clamp(yVal2 - cG);
                        d[dstIdx2 + 2] = Clamp(yVal2 + cR);
                    }
                    else
                    {
                        d[dstIdx2] = Clamp(yVal2 + cR);
                        d[dstIdx2 + 1] = Clamp(yVal2 - cG);
                        d[dstIdx2 + 2] = Clamp(yVal2 + cB);
                    }
                }

                // Trailing odd pixel if width is odd
                for (; x < w; x++)
                {
                    int yVal = s[yRow + x];
                    int uvIdx = uvRow + ((x >> 1) << 1);
                    int uVal = s[uvIdx] - 128;
                    int vVal = (uvIdx + 1 < s.Length) ? (s[uvIdx + 1] - 128) : 0;

                    int r = yVal + ((359 * vVal) >> 8);
                    int g = yVal - ((88 * uVal + 183 * vVal) >> 8);
                    int b = yVal + ((454 * uVal) >> 8);

                    int dstIdx = dRow + (x * 3);
                    if (isBgr)
                    {
                        d[dstIdx] = Clamp(b);
                        d[dstIdx + 1] = Clamp(g);
                        d[dstIdx + 2] = Clamp(r);
                    }
                    else
                    {
                        d[dstIdx] = Clamp(r);
                        d[dstIdx + 1] = Clamp(g);
                        d[dstIdx + 2] = Clamp(b);
                    }
                }
            }
        }

        /// <summary>
        /// Converts RGB24 or BGR24 to 8-bit monochrome grayscale.
        /// </summary>
        public static void RgbToGray8(VideoFrameBuffer src, VideoFrameBuffer dst, bool isBgr = false)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            if (dst == null) throw new ArgumentNullException(nameof(dst));
            if (src.Width != dst.Width || src.Height != dst.Height)
                throw new ArgumentException("Source and destination dimensions must match.");

            int w = src.Width;
            int h = src.Height;

            int requiredSrc = h * src.Stride;
            int requiredDst = h * dst.Stride;

            ReadOnlySpan<byte> s = src.AsReadOnlySpan();
            Span<byte> d = dst.AsSpan();

            if (s.Length < requiredSrc || d.Length < requiredDst)
                throw new ArgumentException("Source frame buffer or destination buffer is too small for the specified video dimensions and stride.");

            for (int y = 0; y < h; y++)
            {
                int srcRow = y * src.Stride;
                int dstRow = y * dst.Stride;

                for (int x = 0; x < w; x++)
                {
                    int sIdx = srcRow + (x * 3);
                    int r = isBgr ? s[sIdx + 2] : s[sIdx];
                    int g = s[sIdx + 1];
                    int b = isBgr ? s[sIdx] : s[sIdx + 2];

                    // Rec. 601 luma: 0.299 R + 0.587 G + 0.114 B
                    int luma = (r * 77 + g * 150 + b * 29) >> 8;
                    d[dstRow + x] = (byte)luma;
                }
            }
        }
    }
}
