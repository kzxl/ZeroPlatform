using System;
using System.Collections.Generic;
using Xunit;
using ZeroOcr.Core.Models;
using ZeroOcr.Inference.PostProcessing;

namespace ZeroOcr.Tests;

public class OcrLineMergerTests
{
    [Fact]
    public void MergeHorizontalLines_WhenTwoFragmentsOnSameBaseline_MergesIntoSingleLine()
    {
        var line1 = new OcrLine(
            "Viêm mô tế bào",
            new[] { new OcrWord("Viêm", new OcrRect(10, 100, 40, 20), 0.95f), new OcrWord("tế bào", new OcrRect(60, 100, 60, 20), 0.95f) },
            new OcrRect(10, 100, 120, 20),
            0.95f);

        var line2 = new OcrLine(
            "và áp xe của miệng",
            new[] { new OcrWord("và", new OcrRect(145, 101, 20, 20), 0.90f) },
            new OcrRect(145, 101, 150, 20),
            0.90f);

        var input = new List<OcrLine> { line1, line2 };
        var merged = OcrLineMerger.MergeHorizontalLines(input, maxBaselineOffsetRatio: 0.45f, maxHorizontalGapRatio: 3.0f);

        Assert.Single(merged);
        Assert.Equal("Viêm mô tế bào và áp xe của miệng", merged[0].Text);
        Assert.True(merged[0].BoundingBox.Width >= 270);
        Assert.Equal(3, merged[0].Words.Count);
    }

    [Fact]
    public void MergeHorizontalLines_WhenLinesOnDifferentBaselines_KeepsSeparated()
    {
        var line1 = new OcrLine("Dòng trên", Array.Empty<OcrWord>(), new OcrRect(10, 50, 100, 20), 0.9f);
        var line2 = new OcrLine("Dòng dưới", Array.Empty<OcrWord>(), new OcrRect(10, 150, 100, 20), 0.9f);

        var input = new List<OcrLine> { line1, line2 };
        var merged = OcrLineMerger.MergeHorizontalLines(input);

        Assert.Equal(2, merged.Count);
        Assert.Equal("Dòng trên", merged[0].Text);
        Assert.Equal("Dòng dưới", merged[1].Text);
    }

    [Fact]
    public void SortReadingOrder_OrdersTopToBottomAndLeftToRight()
    {
        var lineBottom = new OcrLine("Third", Array.Empty<OcrWord>(), new OcrRect(10, 200, 100, 20), 0.9f);
        var lineTopRight = new OcrLine("Second", Array.Empty<OcrWord>(), new OcrRect(250, 50, 100, 20), 0.9f);
        var lineTopLeft = new OcrLine("First", Array.Empty<OcrWord>(), new OcrRect(10, 50, 100, 20), 0.9f);

        var list = new List<OcrLine> { lineBottom, lineTopRight, lineTopLeft };
        var sorted = OcrLineMerger.SortReadingOrder(list);

        Assert.Equal("First", sorted[0].Text);
        Assert.Equal("Second", sorted[1].Text);
        Assert.Equal("Third", sorted[2].Text);
    }
}
