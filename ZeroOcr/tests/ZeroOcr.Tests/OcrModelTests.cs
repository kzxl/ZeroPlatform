using System;
using System.Linq;
using Xunit;
using ZeroOcr.Core.Models;

namespace ZeroOcr.Tests;

public class OcrModelTests
{
    [Fact]
    public void OcrWord_ClampsConfidence()
    {
        var w1 = new OcrWord("HELLO", new OcrRect(0, 0, 10, 10), 1.5f);
        var w2 = new OcrWord("WORLD", new OcrRect(15, 0, 10, 10), -0.5f);

        Assert.Equal(1.0f, w1.Confidence);
        Assert.Equal(0.0f, w2.Confidence);
        Assert.Equal("HELLO", w1.Text);
    }

    [Fact]
    public void OcrLine_CalculatesBoundsAndConfidence()
    {
        var w1 = new OcrWord("BATCH", new OcrRect(0, 0, 50, 20), 0.90f);
        var w2 = new OcrWord("2026A", new OcrRect(60, 0, 50, 20), 0.80f);

        var line = new OcrLine("BATCH 2026A", new[] { w1, w2 });

        Assert.Equal("BATCH 2026A", line.Text);
        Assert.Equal(2, line.Words.Count);
        Assert.Equal(0.85f, line.Confidence, 3);
        Assert.Equal(0f, line.BoundingBox.Left);
        Assert.Equal(110f, line.BoundingBox.Right);
    }

    [Fact]
    public void OcrResult_AggregatesAndFilters()
    {
        var w1 = new OcrWord("ZERO", new OcrRect(0, 0, 40, 20), 0.95f);
        var w2 = new OcrWord("PLATFORM", new OcrRect(50, 0, 60, 20), 0.99f);
        var line1 = new OcrLine("ZERO PLATFORM", new[] { w1, w2 });

        var w3 = new OcrWord("OCR", new OcrRect(0, 30, 30, 20), 0.92f);
        var line2 = new OcrLine("OCR", new[] { w3 });

        var result = OcrResult.Create(new[] { line1, line2 }, TimeSpan.FromMilliseconds(42), "en-US");

        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
        Assert.Equal(2, result.Lines.Count);
        Assert.Equal(3, result.Words.Count);
        Assert.Contains("ZERO PLATFORM", result.Text);
        Assert.Contains("OCR", result.Text);
        Assert.Equal(TimeSpan.FromMilliseconds(42), result.Elapsed);

        var found = result.FindWords(w => w.Text == "OCR").ToList();
        Assert.Single(found);
    }

    [Fact]
    public void OcrResult_Failed()
    {
        var failed = OcrResult.Failed("Engine timeout", TimeSpan.FromSeconds(1));
        Assert.False(failed.Success);
        Assert.Equal("Engine timeout", failed.ErrorMessage);
        Assert.Empty(failed.Lines);
        Assert.Equal(0.0f, failed.MeanConfidence);
    }
}
