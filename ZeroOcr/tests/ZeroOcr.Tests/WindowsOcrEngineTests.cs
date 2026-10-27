using System;
using System.Threading.Tasks;
using Xunit;
using ZeroOcr.Core.Imaging;
using ZeroOcr.Core.Interfaces;
using ZeroOcr.Windows;

namespace ZeroOcr.Tests;

public class WindowsOcrEngineTests
{
    [Fact]
    public void WindowsOcrEngine_InitializationAndLanguages()
    {
        var engine = new WindowsOcrEngine();

        Assert.Equal("Windows.Media.Ocr", engine.Name);

        // Windows 10+ has at least 1 language installed by default on Windows workstations
        if (engine.IsAvailable)
        {
            Assert.NotEmpty(engine.SupportedLanguages);
            var firstLang = engine.SupportedLanguages[0];
            Assert.True(engine.IsLanguageSupported(firstLang));
        }
    }

    [Fact]
    public async Task WindowsOcrEngine_RecognizesImageWithoutCrashing()
    {
        var engine = new WindowsOcrEngine();

        if (!engine.IsAvailable)
            return; // Skip on headless environments without OCR packages

        // 100x40 solid white image with some dark pixels in BGRA32
        int width = 100;
        int height = 40;
        byte[] bgra = new byte[width * height * 4];

        for (int i = 0; i < bgra.Length; i += 4)
        {
            bgra[i] = 255;     // B
            bgra[i + 1] = 255; // G
            bgra[i + 2] = 255; // R
            bgra[i + 3] = 255; // A
        }

        var img = OcrImageBuffer.FromBgra32(width, height, bgra);
        var result = await engine.RecognizeAsync(img, new OcrOptions());

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.Lines);
        Assert.NotNull(result.Words);
    }
}
