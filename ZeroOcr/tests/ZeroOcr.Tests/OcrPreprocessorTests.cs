using System;
using Xunit;
using ZeroOcr.Core.Imaging;

namespace ZeroOcr.Tests;

public class OcrPreprocessorTests
{
    [Fact]
    public void ToGrayscale_ConvertsBgra32Accurately()
    {
        // 1 pixel pure red BGRA: B=0, G=0, R=255, A=255
        // Expected Gray = (255 * 299) / 1000 = 76
        byte[] bgra = { 0, 0, 255, 255 };
        var src = OcrImageBuffer.FromBgra32(1, 1, bgra);

        var gray = OcrPreprocessor.ToGrayscale(src);

        Assert.Equal(OcrPixelFormat.Gray8, gray.Format);
        Assert.Equal(76, gray.Span[0]);
    }

    [Fact]
    public void BinarizeFixed_ThresholdsCorrectly()
    {
        byte[] grays = { 50, 100, 128, 150, 200 };
        var src = OcrImageBuffer.FromGray8(5, 1, grays);

        var binary = OcrPreprocessor.Binarize(src, 128);

        Assert.Equal(0, binary.Span[0]);
        Assert.Equal(0, binary.Span[1]);
        Assert.Equal(255, binary.Span[2]);
        Assert.Equal(255, binary.Span[3]);
        Assert.Equal(255, binary.Span[4]);
    }

    [Fact]
    public void Invert_InvertsAllPixels()
    {
        byte[] grays = { 0, 50, 255 };
        var src = OcrImageBuffer.FromGray8(3, 1, grays);

        var inverted = OcrPreprocessor.Invert(src);

        Assert.Equal(255, inverted.Span[0]);
        Assert.Equal(205, inverted.Span[1]);
        Assert.Equal(0, inverted.Span[2]);
    }

    [Fact]
    public void CalculateOtsuThreshold_DetectsBimodalSplit()
    {
        // Half dark (value 20), half bright (value 220)
        byte[] grays = new byte[100];
        for (int i = 0; i < 50; i++) grays[i] = 20;
        for (int i = 50; i < 100; i++) grays[i] = 220;

        var src = OcrImageBuffer.FromGray8(10, 10, grays);
        byte threshold = OcrPreprocessor.CalculateOtsuThreshold(src);

        // Optimal threshold between 20 and 220 should be around 20-219
        Assert.InRange(threshold, (byte)20, (byte)220);
    }
}
