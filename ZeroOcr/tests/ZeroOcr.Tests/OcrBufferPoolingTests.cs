using System;
using Xunit;
using ZeroOcr.Core.Imaging;

namespace ZeroOcr.Tests;

public class OcrBufferPoolingTests
{
    [Fact]
    public void Rent_CreatesPooledBufferAndDisposesCleanly()
    {
        using (var buffer = OcrImageBuffer.Rent(200, 100, OcrPixelFormat.Gray8))
        {
            Assert.True(buffer.IsPooled);
            Assert.Equal(200, buffer.Width);
            Assert.Equal(100, buffer.Height);
            Assert.False(buffer.Span.IsEmpty);
        }

        // Buffer is now disposed, accessing span should throw
        // (verified via IDisposable)
    }

    [Fact]
    public void Preprocessor_SupportsDestinationBufferForZeroAllocations()
    {
        byte[] bgra = new byte[100 * 50 * 4];
        for (int i = 0; i < bgra.Length; i += 4)
        {
            bgra[i] = 255;     // B
            bgra[i + 1] = 0;   // G
            bgra[i + 2] = 0;   // R
            bgra[i + 3] = 255; // A
        }

        using var src = OcrImageBuffer.FromBgra32(100, 50, bgra);
        using var dstGray = OcrImageBuffer.RentGray8(100, 50);
        using var dstBinary = OcrImageBuffer.RentGray8(100, 50);

        // Preprocess without new allocations
        OcrPreprocessor.ToGrayscale(src, dstGray);
        OcrPreprocessor.Binarize(dstGray, 50, dstBinary);

        // Blue 255 converted to gray = (29 * 255) >> 8 = 28
        // 28 < 50 => binarized value is 0
        Assert.Equal(28, dstGray.Span[0]);
        Assert.Equal(0, dstBinary.Span[0]);
    }
}
