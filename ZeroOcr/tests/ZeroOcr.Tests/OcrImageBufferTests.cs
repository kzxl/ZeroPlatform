using System;
using Xunit;
using ZeroOcr.Core.Imaging;
using ZeroOcr.Core.Models;

namespace ZeroOcr.Tests;

public class OcrImageBufferTests
{
    [Fact]
    public void OcrImageBuffer_CreatesAndValidatesDimensions()
    {
        byte[] bytes = new byte[100 * 50 * 4];
        var buffer = OcrImageBuffer.FromBgra32(100, 50, bytes);

        Assert.Equal(100, buffer.Width);
        Assert.Equal(50, buffer.Height);
        Assert.Equal(400, buffer.Stride);
        Assert.Equal(OcrPixelFormat.Bgra32, buffer.Format);
        Assert.Equal(4, buffer.BytesPerPixel);
        Assert.Equal(bytes.Length, buffer.Span.Length);
    }

    [Fact]
    public void OcrImageBuffer_ThrowsOnInvalidStrideOrLength()
    {
        byte[] shortBytes = new byte[10];
        Assert.Throws<ArgumentException>(() =>
            OcrImageBuffer.FromBgra32(100, 50, shortBytes));

        byte[] validBytes = new byte[400];
        Assert.Throws<ArgumentException>(() =>
            new OcrImageBuffer(10, 10, 10, OcrPixelFormat.Bgra32, validBytes));
    }

    [Fact]
    public void OcrImageBuffer_CropExtractsExactRegion()
    {
        // 4x4 grayscale image
        // 0  1  2  3
        // 4  5  6  7
        // 8  9  10 11
        // 12 13 14 15
        byte[] data = new byte[16];
        for (int i = 0; i < 16; i++) data[i] = (byte)i;

        var img = OcrImageBuffer.FromGray8(4, 4, data);
        var cropped = img.Crop(new OcrRect(1, 1, 2, 2));

        Assert.Equal(2, cropped.Width);
        Assert.Equal(2, cropped.Height);
        Assert.Equal(2, cropped.Stride);

        var span = cropped.Span;
        // row 0: 5, 6
        // row 1: 9, 10
        Assert.Equal(5, span[0]);
        Assert.Equal(6, span[1]);
        Assert.Equal(9, span[2]);
        Assert.Equal(10, span[3]);
    }
}
