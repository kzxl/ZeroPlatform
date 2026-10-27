using System;
using ZeroOcr.Core.Imaging;
using ZeroOcr.Core.Models;

namespace ZeroOcr.Inference.Geometry;

/// <summary>
/// High-performance, zero-allocation pure C# geometric perspective transformer.
/// Rectifies oriented 4-vertex quadrilateral bounding boxes (OcrQuad) into horizontal rectangular image patches.
/// </summary>
public static class QuadPerspectiveTransformer
{
    /// <summary>
    /// Extracts and rectifies the text region defined by <paramref name="quad"/> into a horizontal rectangular buffer.
    /// Uses bilinear coordinate mapping and sub-pixel bilinear interpolation.
    /// </summary>
    /// <param name="source">The source image buffer.</param>
    /// <param name="quad">The 4-vertex quadrilateral (TopLeft, TopRight, BottomRight, BottomLeft).</param>
    /// <param name="targetHeight">Optional fixed target height (e.g. 48 for SVTR). If null, calculated from quad height.</param>
    /// <returns>A new unskewed, rectangular OcrImageBuffer.</returns>
    public static OcrImageBuffer RectifyQuad(
        OcrImageBuffer source,
        OcrQuad quad,
        int? targetHeight = null)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));

        // 1. Calculate natural width and height of the quadrilateral
        double topWidth = Distance(quad.TopLeft, quad.TopRight);
        double bottomWidth = Distance(quad.BottomLeft, quad.BottomRight);
        double leftHeight = Distance(quad.TopLeft, quad.BottomLeft);
        double rightHeight = Distance(quad.TopRight, quad.BottomRight);

        int naturalWidth = Math.Max(4, (int)Math.Round(Math.Max(topWidth, bottomWidth)));
        int naturalHeight = Math.Max(4, (int)Math.Round(Math.Max(leftHeight, rightHeight)));

        int dstWidth;
        int dstHeight;

        if (targetHeight.HasValue && targetHeight.Value > 0)
        {
            dstHeight = targetHeight.Value;
            double aspectRatio = (double)naturalWidth / naturalHeight;
            dstWidth = Math.Max(8, (int)Math.Round(dstHeight * aspectRatio));
        }
        else
        {
            dstWidth = naturalWidth;
            dstHeight = naturalHeight;
        }

        int bpp = source.BytesPerPixel;
        int dstStride = ((dstWidth * bpp) + 3) & ~3;
        byte[] dstBytes = new byte[dstStride * dstHeight];
        var dstBuffer = new OcrImageBuffer(dstWidth, dstHeight, dstStride, source.Format, dstBytes);

        var srcSpan = source.Span;
        int srcWidth = source.Width;
        int srcHeight = source.Height;
        int srcStride = source.Stride;

        float s0x = quad.TopLeft.X, s0y = quad.TopLeft.Y;
        float s1x = quad.TopRight.X, s1y = quad.TopRight.Y;
        float s2x = quad.BottomRight.X, s2y = quad.BottomRight.Y;
        float s3x = quad.BottomLeft.X, s3y = quad.BottomLeft.Y;

        float invDstW = dstWidth > 1 ? 1.0f / (dstWidth - 1) : 0f;
        float invDstH = dstHeight > 1 ? 1.0f / (dstHeight - 1) : 0f;

        for (int v = 0; v < dstHeight; v++)
        {
            float t = v * invDstH;
            float omt = 1.0f - t;

            // Interpolate left and right edge coordinates at vertical position t
            float edgeLeftX = omt * s0x + t * s3x;
            float edgeLeftY = omt * s0y + t * s3y;
            float edgeRightX = omt * s1x + t * s2x;
            float edgeRightY = omt * s1y + t * s2y;

            int dstRowOffset = v * dstStride;

            for (int u = 0; u < dstWidth; u++)
            {
                float s = u * invDstW;

                // Source sample coordinates
                float srcX = (1.0f - s) * edgeLeftX + s * edgeRightX;
                float srcY = (1.0f - s) * edgeLeftY + s * edgeRightY;

                int x0 = (int)Math.Floor(srcX);
                int y0 = (int)Math.Floor(srcY);

                int dstPixelOffset = dstRowOffset + (u * bpp);

                if (x0 >= 0 && x0 < srcWidth - 1 && y0 >= 0 && y0 < srcHeight - 1)
                {
                    float fx = srcX - x0;
                    float fy = srcY - y0;
                    float omfx = 1.0f - fx;
                    float omfy = 1.0f - fy;

                    int row0 = y0 * srcStride;
                    int row1 = (y0 + 1) * srcStride;

                    for (int c = 0; c < bpp; c++)
                    {
                        byte p00 = srcSpan[row0 + x0 * bpp + c];
                        byte p10 = srcSpan[row0 + (x0 + 1) * bpp + c];
                        byte p01 = srcSpan[row1 + x0 * bpp + c];
                        byte p11 = srcSpan[row1 + (x0 + 1) * bpp + c];

                        float val = omfx * omfy * p00 +
                                    fx * omfy * p10 +
                                    omfx * fy * p01 +
                                    fx * fy * p11;

                        dstBytes[dstPixelOffset + c] = (byte)Math.Max(0, Math.Min(255, (int)Math.Round(val)));
                    }
                }
                else
                {
                    // Clamped boundary fallback
                    int cx = Math.Max(0, Math.Min(srcWidth - 1, (int)Math.Round(srcX)));
                    int cy = Math.Max(0, Math.Min(srcHeight - 1, (int)Math.Round(srcY)));
                    int srcOffset = cy * srcStride + cx * bpp;

                    for (int c = 0; c < bpp; c++)
                    {
                        dstBytes[dstPixelOffset + c] = srcSpan[srcOffset + c];
                    }
                }
            }
        }

        return dstBuffer;
    }

    private static double Distance(OcrPoint a, OcrPoint b)
    {
        double dx = a.X - b.X;
        double dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
