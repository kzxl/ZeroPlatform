using System;
using System.Linq;
using Xunit;
using ZeroOcr.Core.Analysis;
using ZeroOcr.Core.Models;

namespace ZeroOcr.Tests;

public class OcrAnalysisTests
{
    [Fact]
    public void KeyValueExtractor_FindsRightAndBelowValues()
    {
        // Line 1: LOT: ABC-123
        var keyLot = new OcrWord("LOT:", new OcrRect(10, 20, 40, 15), 0.95f);
        var valLot = new OcrWord("ABC-123", new OcrRect(60, 20, 70, 15), 0.98f);
        var line1 = new OcrLine("LOT: ABC-123", new[] { keyLot, valLot });

        // Line 2 (Key): EXPIRY
        var keyExp = new OcrWord("EXPIRY", new OcrRect(10, 50, 50, 15), 0.92f);
        var line2 = new OcrLine("EXPIRY", new[] { keyExp });

        // Line 3 (Value below Line 2): 2028-12-31
        var valExp = new OcrWord("2028-12-31", new OcrRect(10, 75, 80, 15), 0.96f);
        var line3 = new OcrLine("2028-12-31", new[] { valExp });

        var result = OcrResult.Create(new[] { line1, line2, line3 }, TimeSpan.FromMilliseconds(5));

        var pairs = OcrKeyValueExtractor.Extract(result, new[] { "LOT", "EXPIRY" });

        Assert.Equal(2, pairs.Count);

        var lotPair = pairs.FirstOrDefault(p => p.KeyText == "LOT");
        Assert.NotNull(lotPair);
        Assert.Equal("ABC-123", lotPair.ValueText);
        Assert.Equal(SpatialDirection.Right, lotPair.Direction);

        var expPair = pairs.FirstOrDefault(p => p.KeyText == "EXPIRY");
        Assert.NotNull(expPair);
        Assert.Equal("2028-12-31", expPair.ValueText);
        Assert.Equal(SpatialDirection.Below, expPair.Direction);
    }

    [Fact]
    public void PatternMatcher_ExtractsDatesAndLotCodes()
    {
        var line1 = new OcrLine("MFG: 15/08/2026 EXP: 2028-12-31", new[]
        {
            new OcrWord("MFG:", new OcrRect(0, 0, 40, 20)),
            new OcrWord("15/08/2026", new OcrRect(50, 0, 80, 20)),
            new OcrWord("EXP:", new OcrRect(140, 0, 40, 20)),
            new OcrWord("2028-12-31", new OcrRect(190, 0, 90, 20))
        });

        var line2 = new OcrLine("BATCH-2026A MST: 0312345678", new[]
        {
            new OcrWord("BATCH-2026A", new OcrRect(0, 30, 90, 20)),
            new OcrWord("MST:", new OcrRect(100, 30, 40, 20)),
            new OcrWord("0312345678", new OcrRect(150, 30, 80, 20))
        });

        var result = OcrResult.Create(new[] { line1, line2 }, TimeSpan.FromMilliseconds(10));

        var dates = OcrPatternMatcher.FindDates(result);
        Assert.Equal(2, dates.Count);
        Assert.Contains(dates, d => d.Value == new DateTime(2026, 8, 15));
        Assert.Contains(dates, d => d.Value == new DateTime(2028, 12, 31));

        var taxIds = OcrPatternMatcher.FindTaxIds(result);
        Assert.Single(taxIds);
        Assert.Equal("0312345678", taxIds[0].Value);

        var lotCodes = OcrPatternMatcher.FindLotCodes(result);
        Assert.Single(lotCodes);
        Assert.Equal("BATCH-2026A", lotCodes[0].Value);
    }

    [Fact]
    public void LayoutAnalyzer_GroupsDisorderedWordsIntoLines()
    {
        // 3 words on line 1 (y ≈ 10) and 2 words on line 2 (y ≈ 50), given in mixed order
        var wLine2_2 = new OcrWord("WORLD", new OcrRect(100, 50, 50, 20));
        var wLine1_3 = new OcrWord("C#", new OcrRect(120, 10, 30, 20));
        var wLine1_1 = new OcrWord("FAST", new OcrRect(10, 10, 40, 20));
        var wLine2_1 = new OcrWord("HELLO", new OcrRect(20, 50, 60, 20));
        var wLine1_2 = new OcrWord("PURE", new OcrRect(60, 10, 40, 20));

        var words = new[] { wLine2_2, wLine1_3, wLine1_1, wLine2_1, wLine1_2 };

        var lines = OcrLayoutAnalyzer.GroupIntoLines(words);

        Assert.Equal(2, lines.Count);
        Assert.Equal("FAST PURE C#", lines[0].Text);
        Assert.Equal("HELLO WORLD", lines[1].Text);
    }
}
