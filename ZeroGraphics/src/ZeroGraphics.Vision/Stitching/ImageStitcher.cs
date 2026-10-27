using System;
using System.Drawing;
using ZeroGraphics.Imaging.Core;

namespace ZeroGraphics.Vision.Stitching
{
    /// <summary>
    /// Multi-camera panoramic image stitcher with homography alignment and linear seam feathering.
    /// Combines multiple camera fields of view into a unified high-resolution inspection canvas.
    /// </summary>
    public static class ImageStitcher
    {
        /// <summary>
        /// Stitches a target image into a base image coordinate frame using a computed homography.
        /// Overlapping seams are blended smoothly via distance-weighted linear feathering.
        /// </summary>
        /// <param name="baseImage">Base reference image (Camera 1).</param>
        /// <param name="targetImage">Adjacent image to align and stitch (Camera 2).</param>
        /// <param name="targetToBaseHomography">Homography mapping target image pixels to base image coordinates.</param>
        /// <returns>Unified stitched ImageBuffer.</returns>
        public static unsafe ImageBuffer StitchPair(
            ImageBuffer baseImage,
            ImageBuffer targetImage,
            Homography2D targetToBaseHomography)
        {
            if (baseImage == null) throw new ArgumentNullException(nameof(baseImage));
            if (targetImage == null) throw new ArgumentNullException(nameof(targetImage));
            if (targetToBaseHomography == null) throw new ArgumentNullException(nameof(targetToBaseHomography));
            if (baseImage.Format != targetImage.Format)
                throw new ArgumentException("Base and target images must have matching formats.");

            int bw = baseImage.Width;
            int bh = baseImage.Height;
            int tw = targetImage.Width;
            int th = targetImage.Height;

            // 1. Compute canvas bounding box enclosing both base image and warped target image
            var targetBounds = PerspectiveWarper.ComputeTransformedBounds(tw, th, targetToBaseHomography);

            float minX = Math.Min(0.0f, targetBounds.Left);
            float minY = Math.Min(0.0f, targetBounds.Top);
            float maxX = Math.Max(bw, targetBounds.Right);
            float maxY = Math.Max(bh, targetBounds.Bottom);

            int canvasWidth = (int)Math.Ceiling(maxX - minX);
            int canvasHeight = (int)Math.Ceiling(maxY - minY);

            // Shift homography to canvas coordinates if minX or minY < 0
            double offsetX = -minX;
            double offsetY = -minY;

            var shiftH = new Homography2D(1, 0, offsetX, 0, 1, offsetY, 0, 0, 1);
            var adjustedTargetH = shiftH.Multiply(targetToBaseHomography);
            var invAdjustedTargetH = adjustedTargetH.Invert();

            var canvas = new ImageBuffer(canvasWidth, canvasHeight, baseImage.Format);
            int bpp = baseImage.BytesPerPixel;

            // 2. Render and blend into canvas
            int baseOffsetX = (int)Math.Round(offsetX);
            int baseOffsetY = (int)Math.Round(offsetY);

            // Copy base image directly into its shifted canvas position
            for (int by = 0; by < bh; by++)
            {
                int cy = baseOffsetY + by;
                if (cy < 0 || cy >= canvasHeight) continue;

                byte* pBaseRow = baseImage.GetRowPointer(by);
                byte* pCanvasRow = canvas.GetRowPointer(cy) + baseOffsetX * bpp;

                int copyBytes = Math.Min(bw, canvasWidth - baseOffsetX) * bpp;
                if (copyBytes > 0)
                {
                    Buffer.MemoryCopy(pBaseRow, pCanvasRow, copyBytes, copyBytes);
                }
            }

            // 3. Warp target image and blend in overlapping zones
            double h00 = invAdjustedTargetH.H00, h01 = invAdjustedTargetH.H01, h02 = invAdjustedTargetH.H02;
            double h10 = invAdjustedTargetH.H10, h11 = invAdjustedTargetH.H11, h12 = invAdjustedTargetH.H12;
            double h20 = invAdjustedTargetH.H20, h21 = invAdjustedTargetH.H21, h22 = invAdjustedTargetH.H22;

            if (baseImage.Format == ImageFormatMode.Gray8)
            {
                for (int cy = 0; cy < canvasHeight; cy++)
                {
                    byte* pCanvasRow = canvas.GetRowPointer(cy);

                    for (int cx = 0; cx < canvasWidth; cx++)
                    {
                        double w = h20 * cx + h21 * cy + h22;
                        if (Math.Abs(w) < 1e-12) continue;

                        double invW = 1.0 / w;
                        double tx = (h00 * cx + h01 * cy + h02) * invW;
                        double ty = (h10 * cx + h11 * cy + h12) * invW;

                        if (tx >= 0.0 && tx < tw - 1 && ty >= 0.0 && ty < th - 1)
                        {
                            // Sample target image via bilinear interpolation
                            int x0 = (int)tx;
                            int y0 = (int)ty;
                            double fx = tx - x0;
                            double fy = ty - y0;

                            byte* r0 = targetImage.GetRowPointer(y0);
                            byte* r1 = targetImage.GetRowPointer(y0 + 1);

                            double targetVal = (1.0 - fx) * (1.0 - fy) * r0[x0] +
                                               fx * (1.0 - fy) * r0[x0 + 1] +
                                               (1.0 - fx) * fy * r1[x0] +
                                               fx * fy * r1[x0 + 1];

                            // Check if canvas pixel already has base image content
                            int baseLocalX = cx - baseOffsetX;
                            int baseLocalY = cy - baseOffsetY;
                            bool insideBase = (baseLocalX >= 0 && baseLocalX < bw && baseLocalY >= 0 && baseLocalY < bh);

                            if (insideBase && pCanvasRow[cx] > 0)
                            {
                                // Overlap blending: linear feathering based on distance to target border
                                double distBorder = Math.Min(Math.Min(tx, tw - 1 - tx), Math.Min(ty, th - 1 - ty));
                                double weightTarget = Math.Max(0.0, Math.Min(1.0, distBorder / 20.0));
                                double blended = (1.0 - weightTarget) * pCanvasRow[cx] + weightTarget * targetVal;
                                pCanvasRow[cx] = (byte)Math.Max(0, Math.Min(255, (int)(blended + 0.5)));
                            }
                            else
                            {
                                pCanvasRow[cx] = (byte)Math.Max(0, Math.Min(255, (int)(targetVal + 0.5)));
                            }
                        }
                    }
                }
            }
            else // Bgra32
            {
                for (int cy = 0; cy < canvasHeight; cy++)
                {
                    uint* pCanvasRow = (uint*)canvas.GetRowPointer(cy);

                    for (int cx = 0; cx < canvasWidth; cx++)
                    {
                        double w = h20 * cx + h21 * cy + h22;
                        if (Math.Abs(w) < 1e-12) continue;

                        double invW = 1.0 / w;
                        double tx = (h00 * cx + h01 * cy + h02) * invW;
                        double ty = (h10 * cx + h11 * cy + h12) * invW;

                        if (tx >= 0.0 && tx < tw - 1 && ty >= 0.0 && ty < th - 1)
                        {
                            int x0 = (int)tx;
                            int y0 = (int)ty;
                            double fx = tx - x0;
                            double fy = ty - y0;

                            byte* r0 = targetImage.GetRowPointer(y0) + x0 * 4;
                            byte* r1 = targetImage.GetRowPointer(y0 + 1) + x0 * 4;

                            byte tb = Interpolate(r0[0], r0[4], r1[0], r1[4], fx, fy);
                            byte tg = Interpolate(r0[1], r0[5], r1[1], r1[5], fx, fy);
                            byte tr = Interpolate(r0[2], r0[6], r1[2], r1[6], fx, fy);
                            byte ta = Interpolate(r0[3], r0[7], r1[3], r1[7], fx, fy);

                            int baseLocalX = cx - baseOffsetX;
                            int baseLocalY = cy - baseOffsetY;
                            bool insideBase = (baseLocalX >= 0 && baseLocalX < bw && baseLocalY >= 0 && baseLocalY < bh);

                            if (insideBase && (pCanvasRow[cx] & 0xFF000000) != 0)
                            {
                                uint basePix = pCanvasRow[cx];
                                byte bb = (byte)(basePix & 0xFF);
                                byte bg = (byte)((basePix >> 8) & 0xFF);
                                byte br = (byte)((basePix >> 16) & 0xFF);
                                byte ba = (byte)((basePix >> 24) & 0xFF);

                                double distBorder = Math.Min(Math.Min(tx, tw - 1 - tx), Math.Min(ty, th - 1 - ty));
                                double weight = Math.Max(0.0, Math.Min(1.0, distBorder / 20.0));

                                byte b = (byte)((1.0 - weight) * bb + weight * tb);
                                byte g = (byte)((1.0 - weight) * bg + weight * tg);
                                byte r = (byte)((1.0 - weight) * br + weight * tr);
                                byte a = (byte)((1.0 - weight) * ba + weight * ta);

                                pCanvasRow[cx] = (uint)(b | (g << 8) | (r << 16) | (a << 24));
                            }
                            else
                            {
                                pCanvasRow[cx] = (uint)(tb | (tg << 8) | (tr << 16) | (ta << 24));
                            }
                        }
                    }
                }
            }

            return canvas;
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
