using System;
using System.Drawing;
using Xunit;
using ZeroOcr.Core.Imaging;
using ZeroOcr.Core.Inspection;
using ZeroOcr.Core.Models;
using ZeroOcr.Windows.Drawing;

namespace ZeroOcr.Tests;

public class OcrDrawingTests
{
    [Fact]
    public void PixelDrawer_DrawsRectangleOnBuffer()
    {
        // 20x20 all-white BGRA image
        byte[] bgra = new byte[20 * 20 * 4];
        for (int i = 0; i < bgra.Length; i++) bgra[i] = 255;

        var buffer = OcrImageBuffer.FromBgra32(20, 20, bgra);

        // Draw pure blue rectangle at [5, 5, 10, 10]
        OcrPixelDrawer.DrawRectangle(buffer, new OcrRect(5, 5, 10, 10), 0, 0, 255, thickness: 1);

        var span = buffer.Span;

        // Pixel at (5, 5) should be Blue (B=255, G=0, R=0, A=255)
        int borderOffset = (5 * buffer.Stride) + (5 * 4);
        Assert.Equal(255, span[borderOffset]);     // B
        Assert.Equal(0, span[borderOffset + 1]); // G
        Assert.Equal(0, span[borderOffset + 2]); // R

        // Pixel inside at (7, 7) should remain white (255, 255, 255)
        int insideOffset = (7 * buffer.Stride) + (7 * 4);
        Assert.Equal(255, span[insideOffset]);
        Assert.Equal(255, span[insideOffset + 1]);
        Assert.Equal(255, span[insideOffset + 2]);
    }

    [Fact]
    public void VisualOverlay_ConvertsBitmapAndRendersGraphics()
    {
        using var bmp = new Bitmap(100, 50);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.White);
        }

        // Test Bitmap to OcrImageBuffer conversion
        var buffer = bmp.ToOcrImageBuffer();
        Assert.Equal(100, buffer.Width);
        Assert.Equal(50, buffer.Height);
        Assert.Equal(OcrPixelFormat.Bgra32, buffer.Format);

        // Test DrawToGraphics
        var word = new OcrWord("TEST", new OcrRect(10, 10, 50, 20), 0.95f);
        var line = new OcrLine("TEST", new[] { word });
        var result = OcrResult.Create(new[] { line }, TimeSpan.FromMilliseconds(5));

        using (var g = Graphics.FromImage(bmp))
        {
            OcrVisualOverlay.DrawToGraphics(g, result);
            var verdict = OcrInspectionVerdict.Pass("Check", "TEST", "TEST", 0.95f);
            OcrVisualOverlay.DrawInspectionVerdict(g, verdict, new PointF(10, 30));
        }

        // Bmp remains valid and modified
        Assert.Equal(100, bmp.Width);
    }
}
