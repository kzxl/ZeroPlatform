using System;
using System.Drawing;
using ZeroGraphics.Imaging.Core;

namespace ZeroGraphics.Vision.Stitching
{
    /// <summary>
    /// High-performance bilinear perspective image warper using inverse homography mapping.
    /// Provides zero-gap texture warping and lens rectification across Gray8 and Bgra32 buffers.
    /// </summary>
    public static class PerspectiveWarper
    {
        /// <summary>
        /// Computes the bounding rectangle enclosing the 4 corners of an image transformed by Homography H.
        /// </summary>
        public static RectangleF ComputeTransformedBounds(int width, int height, Homography2D homography)
        {
            if (homography == null) throw new ArgumentNullException(nameof(homography));

            var c0 = homography.TransformPoint(0, 0);
            var c1 = homography.TransformPoint(width - 1, 0);
            var c2 = homography.TransformPoint(width - 1, height - 1);
            var c3 = homography.TransformPoint(0, height - 1);

            float minX = Math.Min(Math.Min(c0.X, c1.X), Math.Min(c2.X, c3.X));
            float maxX = Math.Max(Math.Max(c0.X, c1.X), Math.Max(c2.X, c3.X));
            float minY = Math.Min(Math.Min(c0.Y, c1.Y), Math.Min(c2.Y, c3.Y));
            float maxY = Math.Max(Math.Max(c0.Y, c1.Y), Math.Max(c2.Y, c3.Y));

            return new RectangleF(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        /// <summary>
        /// Warps the source image using Homography H into a new destination buffer.
        /// </summary>
        public static ImageBuffer Warp(
            ImageBuffer src,
            Homography2D homography,
            int dstWidth,
            int dstHeight)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            if (homography == null) throw new ArgumentNullException(nameof(homography));
            if (dstWidth <= 0 || dstHeight <= 0) throw new ArgumentOutOfRangeException("Destination dimensions must be positive.");

            var dst = new ImageBuffer(dstWidth, dstHeight, src.Format);
            WarpInto(src, dst, homography);
            return dst;
        }

        /// <summary>
        /// Warps the source image into an existing destination buffer using backward bilinear interpolation.
        /// </summary>
        public static unsafe void WarpInto(
            ImageBuffer src,
            ImageBuffer dst,
            Homography2D homography)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            if (dst == null) throw new ArgumentNullException(nameof(dst));
            if (homography == null) throw new ArgumentNullException(nameof(homography));
            if (src.Format != dst.Format)
                throw new ArgumentException("Source and destination must have matching ImageFormatMode.");

            // H maps: src -> dst.
            // Backward mapping requires H^-1: dst -> src.
            Homography2D invH = homography.Invert();

            int sw = src.Width;
            int sh = src.Height;
            int dw = dst.Width;
            int dh = dst.Height;

            double h00 = invH.H00, h01 = invH.H01, h02 = invH.H02;
            double h10 = invH.H10, h11 = invH.H11, h12 = invH.H12;
            double h20 = invH.H20, h21 = invH.H21, h22 = invH.H22;

            if (src.Format == ImageFormatMode.Gray8)
            {
                for (int dy = 0; dy < dh; dy++)
                {
                    byte* pDstRow = dst.GetRowPointer(dy);

                    for (int dx = 0; dx < dw; dx++)
                    {
                        double w = h20 * dx + h21 * dy + h22;
                        if (Math.Abs(w) < 1e-12) continue;

                        double invW = 1.0 / w;
                        double sx = (h00 * dx + h01 * dy + h02) * invW;
                        double sy = (h10 * dx + h11 * dy + h12) * invW;

                        if (sx >= 0.0 && sx < sw - 1 && sy >= 0.0 && sy < sh - 1)
                        {
                            int x0 = (int)sx;
                            int y0 = (int)sy;
                            double fx = sx - x0;
                            double fy = sy - y0;

                            byte* r0 = src.GetRowPointer(y0);
                            byte* r1 = src.GetRowPointer(y0 + 1);

                            double val = (1.0 - fx) * (1.0 - fy) * r0[x0] +
                                         fx * (1.0 - fy) * r0[x0 + 1] +
                                         (1.0 - fx) * fy * r1[x0] +
                                         fx * fy * r1[x0 + 1];

                            pDstRow[dx] = (byte)Math.Max(0, Math.Min(255, (int)(val + 0.5)));
                        }
                    }
                }
            }
            else // Bgra32
            {
                for (int dy = 0; dy < dh; dy++)
                {
                    uint* pDstRow = (uint*)dst.GetRowPointer(dy);

                    for (int dx = 0; dx < dw; dx++)
                    {
                        double w = h20 * dx + h21 * dy + h22;
                        if (Math.Abs(w) < 1e-12) continue;

                        double invW = 1.0 / w;
                        double sx = (h00 * dx + h01 * dy + h02) * invW;
                        double sy = (h10 * dx + h11 * dy + h12) * invW;

                        if (sx >= 0.0 && sx < sw - 1 && sy >= 0.0 && sy < sh - 1)
                        {
                            int x0 = (int)sx;
                            int y0 = (int)sy;
                            double fx = sx - x0;
                            double fy = sy - y0;

                            byte* r0 = src.GetRowPointer(y0) + x0 * 4;
                            byte* r1 = src.GetRowPointer(y0 + 1) + x0 * 4;

                            // Bilinear on B, G, R, A
                            byte b = Interpolate(r0[0], r0[4], r1[0], r1[4], fx, fy);
                            byte g = Interpolate(r0[1], r0[5], r1[1], r1[5], fx, fy);
                            byte r = Interpolate(r0[2], r0[6], r1[2], r1[6], fx, fy);
                            byte a = Interpolate(r0[3], r0[7], r1[3], r1[7], fx, fy);

                            pDstRow[dx] = (uint)(b | (g << 8) | (r << 16) | (a << 24));
                        }
                    }
                }
            }
        }

        private static byte Interpolate(byte p00, byte p10, byte p01, byte p11, double fx, double fy)
        {
            double val = (1.0 - fx) * (1.0 - fy) * p00 +
                         fx * (1.0 - fy) * p10 +
                         (1.0 - fx) * fy * p01 +
                         fx * fy * p11;
            return (byte)Math.Max(0, Math.Min(255, (int)(val + 0.5)));
        }
    }
}
