using System;
using Xunit;
using ZeroOcr.Core.Inspection;
using ZeroOcr.Core.Models;

namespace ZeroOcr.Tests;

public class OcrInspectionTests
{
    [Fact]
    public void InspectionJudge_SubstringAndRegex()
    {
        var line = new OcrLine("LOT-2026-X9 PASS", new[]
        {
            new OcrWord("LOT-2026-X9", new OcrRect(0, 0, 100, 20), 0.96f),
            new OcrWord("PASS", new OcrRect(110, 0, 40, 20), 0.99f)
        });
        var result = OcrResult.Create(new[] { line }, TimeSpan.FromMilliseconds(2));

        var subVerdict = OcrInspectionJudge.JudgeSubstring(result, "LOT-2026-X9");
        Assert.True(subVerdict.IsPassed);

        var rxVerdict = OcrInspectionJudge.JudgeRegex(result, @"^LOT-\d{4}-[A-Z]\d$");
        Assert.True(rxVerdict.IsPassed);
        Assert.Equal("LOT-2026-X9", rxVerdict.Actual);

        var failVerdict = OcrInspectionJudge.JudgeSubstring(result, "DEFECTIVE");
        Assert.False(failVerdict.IsPassed);
    }

    [Fact]
    public void InspectionJudge_ExpiryDate()
    {
        var lineValid = new OcrLine("EXP: 31/12/2030", new[]
        {
            new OcrWord("EXP:", new OcrRect(0, 0, 40, 20)),
            new OcrWord("31/12/2030", new OcrRect(50, 0, 80, 20))
        });
        var resultValid = OcrResult.Create(new[] { lineValid }, TimeSpan.FromMilliseconds(1));

        var verdictValid = OcrInspectionJudge.JudgeExpiryDate(resultValid, referenceDate: new DateTime(2026, 1, 1));
        Assert.True(verdictValid.IsPassed);

        var lineExpired = new OcrLine("EXP: 01/01/2020", new[]
        {
            new OcrWord("EXP:", new OcrRect(0, 0, 40, 20)),
            new OcrWord("01/01/2020", new OcrRect(50, 0, 80, 20))
        });
        var resultExpired = OcrResult.Create(new[] { lineExpired }, TimeSpan.FromMilliseconds(1));

        var verdictExpired = OcrInspectionJudge.JudgeExpiryDate(resultExpired, referenceDate: new DateTime(2026, 1, 1));
        Assert.False(verdictExpired.IsPassed);
        Assert.Contains("expired", verdictExpired.Message, StringComparison.OrdinalIgnoreCase);
    }
}
