using System;
using ZeroOcr.Core.Models;

namespace ZeroOcr.Core.Imaging;

/// <summary>
/// Sovereign pure C# zero-dependency pixel drawing operations for marking OCR results on image buffers.
/// </summary>
public static class OcrPixelDrawer
{
    /// <summary>
    /// Draws a colored bounding box outline directly onto a Bgra32/Rgba32 image buffer.
    /// </summary>
    public static void DrawRectangle(
        OcrImageBuffer buffer,
        OcrRect rect,
        byte r, byte g, byte b,
        int thickness = 2)
    {
        if (buffer.Format != OcrPixelFormat.Bgra32 && buffer.Format != OcrPixelFormat.Rgba32)
            throw new NotSupportedException("Pixel drawing currently supports Bgra32 and Rgba32 buffers.");

        int x0 = Math.Max(0, (int)Math.Floor(rect.Left));
        int y0 = Math.Max(0, (int)Math.Floor(rect.Top));
        int x1 = Math.Min(buffer.Width - 1, (int)Math.Ceiling(rect.Right));
        int y1 = Math.Min(buffer.Height - 1, (int)Math.Ceiling(rect.Bottom));

        if (x0 >= x1 || y0 >= y1) return;

        bool isBgra = buffer.Format == OcrPixelFormat.Bgra32;
        var span = buffer.Span;

        unsafe
        {
            fixed (byte* pBase = span)
            {
                // Top and bottom horizontal lines
                for (int t = 0; t < thickness; t++)
                {
                    int topY = y0 + t;
                    int botY = y1 - t;

                    if (topY <= y1)
                    {
                        for (int x = x0; x <= x1; x++)
                            SetPixel(pBase, buffer.Stride, x, topY, r, g, b, isBgra);
                    }

                    if (botY >= y0 && botY != topY)
                    {
                        for (int x = x0; x <= x1; x++)
                            SetPixel(pBase, buffer.Stride, x, botY, r, g, b, isBgra);
                    }
                }

                // Left and right vertical lines
                for (int t = 0; t < thickness; t++)
                {
                    int leftX = x0 + t;
                    int rightX = x1 - t;

                    if (leftX <= x1)
                    {
                        for (int y = y0; y <= y1; y++)
                            SetPixel(pBase, buffer.Stride, leftX, y, r, g, b, isBgra);
                    }

                    if (rightX >= x0 && rightX != leftX)
                    {
                        for (int y = y0; y <= y1; y++)
                            SetPixel(pBase, buffer.Stride, rightX, y, r, g, b, isBgra);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Renders all word or line bounding boxes from an OCR result onto a copy of the image buffer.
    /// </summary>
    public static OcrImageBuffer RenderOverlay(
        OcrImageBuffer source,
        OcrResult result,
        bool drawWords = true,
        int thickness = 2)
    {
        // Clone buffer memory
        byte[] cloneBytes = source.Memory.ToArray();
        var buffer = new OcrImageBuffer(source.Width, source.Height, source.Stride, source.Format, cloneBytes);

        if (drawWords)
        {
            foreach (var word in result.Words)
            {
                // Green for high confidence (>= 80%), Yellow for medium, Red for low (< 50%)
                byte r = word.Confidence >= 0.8f ? (byte)0 : (byte)255;
                byte g = word.Confidence >= 0.5f ? (byte)255 : (byte)0;
                byte b = 0;

                DrawRectangle(buffer, word.BoundingBox, r, g, b, thickness);
            }
        }
        else
        {
            foreach (var line in result.Lines)
            {
                DrawRectangle(buffer, line.BoundingBox, 0, 200, 255, thickness);
            }
        }

        return buffer;
    }

    private static unsafe void SetPixel(byte* pBase, int stride, int x, int y, byte r, byte g, byte b, bool isBgra)
    {
        byte* pPixel = pBase + (y * stride) + (x * 4);
        if (isBgra)
        {
            pPixel[0] = b;
            pPixel[1] = g;
            pPixel[2] = r;
            pPixel[3] = 255;
        }
        else
        {
            pPixel[0] = r;
            pPixel[1] = g;
            pPixel[2] = b;
            pPixel[3] = 255;
        }
    }
}
