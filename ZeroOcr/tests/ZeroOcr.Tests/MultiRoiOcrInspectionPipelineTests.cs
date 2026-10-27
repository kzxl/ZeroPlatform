using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using ZeroOcr.Core.Engines;
using ZeroOcr.Core.Imaging;
using ZeroOcr.Core.Inspection;
using ZeroOcr.Core.Interfaces;
using ZeroOcr.Core.Models;

namespace ZeroOcr.Tests;

public class MultiRoiOcrInspectionPipelineTests
{
    [Fact]
    public async Task MultiRoiPipeline_ExecutesParallelInspectionAndRemapsCoordinates()
    {
        // Master image 1000x1000
        int size = 500;
        byte[] bytes = new byte[size * size * 4];
        var masterImage = OcrImageBuffer.FromBgra32(size, size, bytes);

        Func<OcrImageBuffer, OcrOptions?, OcrResult> engineLogic = (buf, opt) =>
        {
            if (opt?.CustomProperties?.TryGetValue("Zone", out var zone) == true && (string)zone == "DateZone")
            {
                var word = new OcrWord("EXP: 2029-01-01", new OcrRect(5, 5, 80, 20), 0.99f);
                var line = new OcrLine("EXP: 2029-01-01", new[] { word });
                return OcrResult.Create(new[] { line }, TimeSpan.FromMilliseconds(1));
            }
            else
            {
                var word = new OcrWord("LOT-999-Z", new OcrRect(10, 10, 60, 20), 0.98f);
                var line = new OcrLine("LOT-999-Z", new[] { word });
                return OcrResult.Create(new[] { line }, TimeSpan.FromMilliseconds(1));
            }
        };

        var mock1 = new MockOcrEngine(engineLogic);
        var mock2 = new MockOcrEngine(engineLogic);

        var pipeline = new MultiRoiOcrInspectionPipeline(new[] { mock1, mock2 }, maxDegreeOfParallelism: 2);

        var specs = new List<RoiInspectionSpec>
        {
            new(
                zoneName: "DateZone",
                region: new OcrRect(100, 200, 150, 50),
                preprocessingMode: RoiPreprocessingMode.DotMatrixInkjet,
                evaluationRule: res => OcrInspectionJudge.JudgeExpiryDate(res, new DateTime(2026, 1, 1), "DateCheck")
            ),
            new(
                zoneName: "LotZone",
                region: new OcrRect(100, 300, 150, 50),
                preprocessingMode: RoiPreprocessingMode.LaserEtchedFoil,
                evaluationRule: res => OcrInspectionJudge.JudgeRegex(res, @"^LOT-\d{3}-[A-Z]$", "LotCheck")
            )
        };

        var outcome = await pipeline.InspectAsync(masterImage, specs);

        Assert.True(outcome.IsAllPassed);
        Assert.Equal(2, outcome.ZoneResults.Count);

        var dateZone = outcome.ZoneResults[0];
        Assert.Equal("DateZone", dateZone.ZoneName);
        Assert.True(dateZone.Verdict.IsPassed);

        // Check coordinate remapping to master frame:
        // Local word was at (5, 5), ROI was at (100, 200) -> Master box X = 105, Y = 205
        var masterWord = dateZone.RemappedLines[0].Words[0];
        Assert.Equal(105f, masterWord.BoundingBox.X);
        Assert.Equal(205f, masterWord.BoundingBox.Y);

        var lotZone = outcome.ZoneResults[1];
        Assert.Equal("LotZone", lotZone.ZoneName);
        Assert.True(lotZone.Verdict.IsPassed);
    }
}
