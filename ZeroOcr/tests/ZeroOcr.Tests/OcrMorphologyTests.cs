using System;
using Xunit;
using ZeroOcr.Core.Imaging;

namespace ZeroOcr.Tests;

public class OcrMorphologyTests
{
    [Fact]
    public void Dilate_BridgesDisconnectedDots()
    {
        // 5x5 binary image with two dots separated by 1 blank pixel horizontally:
        // row 2: [0, 255, 0, 255, 0]
        byte[] pixels = new byte[25];
        pixels[2 * 5 + 1] = 255;
        pixels[2 * 5 + 3] = 255;

        var src = OcrImageBuffer.FromGray8(5, 5, pixels);

        // Dilate with horizontal line of width 3 (kernel: [1, 1, 1], anchor = 1)
        var dilated = OcrMorphology.Dilate(src, StructuringElement.HorizontalLine(3));

        var span = dilated.Span;

        // Gap at (x=2, y=2) should now be bridged (255)
        Assert.Equal(255, span[2 * 5 + 1]);
        Assert.Equal(255, span[2 * 5 + 2]); // Bridged!
        Assert.Equal(255, span[2 * 5 + 3]);
    }

    [Fact]
    public void Erode_ShrinksForeground()
    {
        // 5x5 solid white image
        byte[] pixels = new byte[25];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = 255;

        var src = OcrImageBuffer.FromGray8(5, 5, pixels);

        // Erode with 3x3 box
        var eroded = OcrMorphology.Erode(src, StructuringElement.Rectangle(3, 3));
        var span = eroded.Span;

        // Border pixels at (0, 0) should be 0 because 3x3 doesn't fit outside borders
        Assert.Equal(0, span[0]);
        // Center pixel at (2, 2) should remain 255 because 3x3 fits completely
        Assert.Equal(255, span[2 * 5 + 2]);
    }

    [Fact]
    public void Close_FusesAndRestoresStrokeWidth()
    {
        // Single isolated point at center (2, 2) in 5x5
        byte[] pixels = new byte[25];
        pixels[2 * 5 + 2] = 255;

        var src = OcrImageBuffer.FromGray8(5, 5, pixels);
        var closed = OcrMorphology.Close(src, StructuringElement.Rectangle(3, 3));

        // Point is preserved
        Assert.Equal(255, closed.Span[2 * 5 + 2]);
    }
}
