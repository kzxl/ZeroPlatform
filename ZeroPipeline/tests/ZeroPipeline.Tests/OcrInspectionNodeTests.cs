using System;
using System.Threading.Tasks;
using Xunit;
using ZeroGraphics.Imaging.Core;
using ZeroOcr.Core.Engines;
using ZeroOcr.Core.Models;
using ZeroPipeline.Core.Execution;
using ZeroPipeline.Core.Graph;
using ZeroPipeline.Core.Nodes;
using ZeroPipeline.Core.Ports;
using ZeroPipeline.Nodes.Flow;
using ZeroPipeline.Nodes.Inspection;

namespace ZeroPipeline.Tests;

public class OcrInspectionNodeTests
{
    [Fact]
    public async Task OcrInspectionNode_EvaluatesSubstringAndRegexInPipeline()
    {
        // 1. Setup mock engine
        var mockEngine = new MockOcrEngine((buf, opt) =>
        {
            var word1 = new OcrWord("BATCH-2026", new OcrRect(10, 10, 80, 20), 0.99f);
            var word2 = new OcrWord("PASS", new OcrRect(100, 10, 40, 20), 0.95f);
            var line = new OcrLine("BATCH-2026 PASS", new[] { word1, word2 });
            return OcrResult.Create(new[] { line }, TimeSpan.FromMilliseconds(1));
        });

        OcrEngineRegistry.Default.Register(mockEngine, setAsDefault: true);

        // 2. Setup image buffer
        using var img = new ImageBuffer(100, 50, ImageFormatMode.Bgra32);

        // 3. Test Substring Node (Pass)
        var subNode = new OcrInspectionNode("BatchCheck", "BATCH-2026", OcrInspectionMode.Substring);
        InspectionResult? subResult = null;
        var subSink = new ActionSinkNode<InspectionResult>(r => subResult = r);

        var graph1 = new PipelineGraph();
        graph1.Connect(subNode.Output, subSink.Input);
        var executor1 = new PipelineExecutor(graph1.AddNode(subNode));
        await executor1.InitializeAsync();

        subNode.Input.Deliver(new DataPacket<ImageBuffer>(img));
        await executor1.ExecuteStepAsync();

        Assert.NotNull(subResult);
        Assert.True(subResult!.IsPassed);
        Assert.Equal("BatchCheck", subResult.InspectionName);

        // 4. Test Regex Node (Pass)
        var rxNode = new OcrInspectionNode("LotRegexCheck", @"^BATCH-\d{4}$", OcrInspectionMode.Regex);
        InspectionResult? rxResult = null;
        var rxSink = new ActionSinkNode<InspectionResult>(r => rxResult = r);

        var graph2 = new PipelineGraph();
        graph2.Connect(rxNode.Output, rxSink.Input);
        var executor2 = new PipelineExecutor(graph2.AddNode(rxNode));
        await executor2.InitializeAsync();

        rxNode.Input.Deliver(new DataPacket<ImageBuffer>(img));
        await executor2.ExecuteStepAsync();

        Assert.NotNull(rxResult);
        Assert.True(rxResult!.IsPassed);

        // 5. Test Mismatch (Fail)
        var failNode = new OcrInspectionNode("MismatchCheck", "MISSING_TEXT", OcrInspectionMode.Substring);
        InspectionResult? failResult = null;
        var failSink = new ActionSinkNode<InspectionResult>(r => failResult = r);

        var graph3 = new PipelineGraph();
        graph3.Connect(failNode.Output, failSink.Input);
        var executor3 = new PipelineExecutor(graph3.AddNode(failNode));
        await executor3.InitializeAsync();

        failNode.Input.Deliver(new DataPacket<ImageBuffer>(img));
        await executor3.ExecuteStepAsync();

        Assert.NotNull(failResult);
        Assert.False(failResult!.IsPassed);
    }
}
