using System;
using System.Threading.Tasks;
using Xunit;
using ZeroOcr.Core.Engines;
using ZeroOcr.Core.Imaging;
using ZeroOcr.Core.Interfaces;
using ZeroOcr.Core.Models;

namespace ZeroOcr.Tests;

public class MockOcrEngineTests
{
    [Fact]
    public async Task MockOcrEngine_ReturnsRecognizedTokens()
    {
        var engine = new MockOcrEngine();

        Assert.Equal("MockOcrEngine", engine.Name);
        Assert.True(engine.IsAvailable);
        Assert.True(engine.IsLanguageSupported("en-US"));
        Assert.True(engine.IsLanguageSupported("vi-VN"));
        Assert.False(engine.IsLanguageSupported("fr-FR"));

        byte[] fakePixels = new byte[100 * 50 * 4];
        var img = OcrImageBuffer.FromBgra32(100, 50, fakePixels);

        var result = await engine.RecognizeAsync(img, new OcrOptions { LanguageTag = "en-US" });

        Assert.True(result.Success);
        Assert.Equal("MOCK TEXT", result.Text);
        Assert.Equal(2, result.Words.Count);
        Assert.Equal("MOCK", result.Words[0].Text);
        Assert.Equal("TEXT", result.Words[1].Text);
    }

    [Fact]
    public async Task MockOcrEngine_HandlesCustomResponseFactory()
    {
        var engine = new MockOcrEngine((buf, opt) =>
        {
            var word = new OcrWord("CUSTOM_123", new OcrRect(0, 0, 80, 20), 0.99f);
            var line = new OcrLine("CUSTOM_123", new[] { word });
            return OcrResult.Create(new[] { line }, TimeSpan.FromMilliseconds(1), opt?.LanguageTag);
        });

        byte[] pixels = new byte[400];
        var img = OcrImageBuffer.FromBgra32(10, 10, pixels);
        var result = await engine.RecognizeAsync(img);

        Assert.True(result.Success);
        Assert.Equal("CUSTOM_123", result.Text);
    }
}
