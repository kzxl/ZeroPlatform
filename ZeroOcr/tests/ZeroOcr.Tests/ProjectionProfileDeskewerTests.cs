using System;
using Xunit;
using ZeroOcr.Core.Imaging;

namespace ZeroOcr.Tests;

public class ProjectionProfileDeskewerTests
{
    [Fact]
    public void DetectSkewAngle_ZeroOnHorizontalText()
    {
        // 100x100 image with 3 crisp horizontal bands (thickness 5 pixels each)
        int size = 100;
        byte[] pixels = new byte[size * size];

        for (int y = 20; y <= 24; y++)
            for (int x = 10; x < size - 10; x++) pixels[y * size + x] = 255;

        for (int y = 50; y <= 54; y++)
            for (int x = 10; x < size - 10; x++) pixels[y * size + x] = 255;

        for (int y = 80; y <= 84; y++)
            for (int x = 10; x < size - 10; x++) pixels[y * size + x] = 255;

        var img = OcrImageBuffer.FromGray8(size, size, pixels);
        var skew = ProjectionProfileDeskewer.DetectSkewAngle(img, -10, 10, 1.0, 0.2);

        // Optimal angle for horizontal lines should be ~0.0 degrees
        Assert.InRange(Math.Abs(skew.AngleDegrees), 0.0, 1.0);
        Assert.True(skew.PeakVariance > 0);
    }

    [Fact]
    public void RotateDeskew_SmallAngleIsNoOp()
    {
        byte[] pixels = new byte[100];
        var img = OcrImageBuffer.FromGray8(10, 10, pixels);

        var result = ProjectionProfileDeskewer.RotateDeskew(img, 0.01);
        Assert.Same(img, result); // Skips processing if angle < 0.05
    }
}
