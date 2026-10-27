using System;
using System.Buffers;
using ZeroOcr.Core.Models;

namespace ZeroOcr.Core.Imaging;

/// <summary>
/// Structuring element configuration for morphological operations.
/// </summary>
public readonly struct StructuringElement
{
    public int Width { get; }
    public int Height { get; }
    public int AnchorX { get; }
    public int AnchorY { get; }
    public byte[] Mask { get; }

    public StructuringElement(int width, int height, byte[] mask, int? anchorX = null, int? anchorY = null)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException("Dimensions must be positive.");
        if (mask.Length < width * height) throw new ArgumentException("Mask array length is insufficient.", nameof(mask));
        Width = width;
        Height = height;
        AnchorX = anchorX ?? (width / 2);
        AnchorY = anchorY ?? (height / 2);
        Mask = mask;
    }

    /// <summary>
    /// Creates a rectangular structuring element (all 1s).
    /// </summary>
    public static StructuringElement Rectangle(int width, int height)
    {
        byte[] mask = new byte[width * height];
        for (int i = 0; i < mask.Length; i++) mask[i] = 1;
        return new StructuringElement(width, height, mask);
    }

    /// <summary>
    /// Creates a horizontal line structuring element specifically for bridging continuous horizontal inkjet dots.
    /// </summary>
    public static StructuringElement HorizontalLine(int width) => Rectangle(width, 1);

    /// <summary>
    /// Creates a 3x3 cross (diamond) structuring element.
    /// </summary>
    public static StructuringElement Cross3x3() =>
        new(3, 3, new byte[]
        {
            0, 1, 0,
            1, 1, 1,
            0, 1, 0
        });
}

/// <summary>
/// High-performance morphological filtering utilities for industrial OCR (Dot-matrix bridging & noise reduction).
/// </summary>
public static class OcrMorphology
{
    /// <summary>
    /// Performs morphological binary dilation: bridges disconnected dots in dot-matrix text.
    /// Foreground pixels are expected to be 255 (white) on 0 (black background).
    /// </summary>
    public static OcrImageBuffer Dilate(OcrImageBuffer binaryImage, StructuringElement se)
    {
        if (binaryImage.Format != OcrPixelFormat.Gray8)
            throw new ArgumentException("Input must be a Gray8 binary image.", nameof(binaryImage));

        int width = binaryImage.Width;
        int height = binaryImage.Height;
        int srcStride = binaryImage.Stride;
        byte[] dst = ArrayPool<byte>.Shared.Rent(width * height);

        try
        {
            var src = binaryImage.Span;
            Array.Clear(dst, 0, width * height);

            int kw = se.Width;
            int kh = se.Height;
            int ax = se.AnchorX;
            int ay = se.AnchorY;
            var mask = se.Mask;

            for (int y = 0; y < height; y++)
            {
                int dstRow = y * width;
                for (int x = 0; x < width; x++)
                {
                    if (src[y * srcStride + x] == 0)
                        continue;

                    for (int ky = 0; ky < kh; ky++)
                    {
                        int ny = y + ky - ay;
                        if ((uint)ny >= (uint)height) continue;

                        int nRow = ny * width;
                        int maskRow = ky * kw;

                        for (int kx = 0; kx < kw; kx++)
                        {
                            if (mask[maskRow + kx] == 0) continue;

                            int nx = x + kx - ax;
                            if ((uint)nx < (uint)width)
                            {
                                dst[nRow + nx] = 255;
                            }
                        }
                    }
                }
            }

            byte[] result = new byte[width * height];
            Buffer.BlockCopy(dst, 0, result, 0, width * height);
            return new OcrImageBuffer(width, height, width, OcrPixelFormat.Gray8, result);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(dst);
        }
    }

    /// <summary>
    /// Performs morphological binary erosion: shrinks boundaries of foreground regions.
    /// </summary>
    public static OcrImageBuffer Erode(OcrImageBuffer binaryImage, StructuringElement se)
    {
        if (binaryImage.Format != OcrPixelFormat.Gray8)
            throw new ArgumentException("Input must be a Gray8 binary image.", nameof(binaryImage));

        int width = binaryImage.Width;
        int height = binaryImage.Height;
        int srcStride = binaryImage.Stride;
        byte[] dst = ArrayPool<byte>.Shared.Rent(width * height);

        try
        {
            var src = binaryImage.Span;
            Array.Clear(dst, 0, width * height);

            int kw = se.Width;
            int kh = se.Height;
            int ax = se.AnchorX;
            int ay = se.AnchorY;
            var mask = se.Mask;

            for (int y = 0; y < height; y++)
            {
                int dstRow = y * width;
                for (int x = 0; x < width; x++)
                {
                    bool fits = true;

                    for (int ky = 0; ky < kh && fits; ky++)
                    {
                        int ny = y + ky - ay;
                        if ((uint)ny >= (uint)height)
                        {
                            fits = false;
                            break;
                        }

                        int srcRow = ny * srcStride;
                        int maskRow = ky * kw;

                        for (int kx = 0; kx < kw; kx++)
                        {
                            if (mask[maskRow + kx] == 0) continue;

                            int nx = x + kx - ax;
                            if ((uint)nx >= (uint)width || src[srcRow + nx] == 0)
                            {
                                fits = false;
                                break;
                            }
                        }
                    }

                    if (fits)
                    {
                        dst[dstRow + x] = 255;
                    }
                }
            }

            byte[] result = new byte[width * height];
            Buffer.BlockCopy(dst, 0, result, 0, width * height);
            return new OcrImageBuffer(width, height, width, OcrPixelFormat.Gray8, result);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(dst);
        }
    }

    /// <summary>
    /// Morphological Closing (Dilation followed by Erosion).
    /// Best for dot-matrix text: Fuses inter-dot gaps without permanently fattening character strokes!
    /// </summary>
    public static OcrImageBuffer Close(OcrImageBuffer binaryImage, StructuringElement se)
    {
        var dilated = Dilate(binaryImage, se);
        return Erode(dilated, se);
    }
}
