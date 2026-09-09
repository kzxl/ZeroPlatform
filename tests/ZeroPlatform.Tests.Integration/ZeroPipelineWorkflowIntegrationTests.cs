using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using ZeroGraphics.Imaging.Core;
using ZeroPipeline.Core.Execution;
using ZeroPipeline.Core.Graph;
using ZeroPipeline.Nodes.Comm;
using ZeroPipeline.Nodes.Flow;
using ZeroPipeline.Nodes.Inspection;
using ZeroPipeline.Nodes.Storage;
using ZeroPipeline.Nodes.Vision;

namespace ZeroPlatform.Tests.Integration
{
    public class ZeroPipelineWorkflowIntegrationTests
    {
        [Fact]
        public async Task EndToEnd_AutomatedOpticalInspection_QualityRouting_TSDB_And_PLC()
        {
            // Scenario:
            // 2 Parts inspected:
            // Part 1: Width = 30px -> within tolerance [28..32] -> Passed -> routes to PassedOutput -> logs to TSDB & PLC writes OK (1)
            // Part 2: Width = 45px -> out of tolerance [28..32] -> Failed -> routes to FailedOutput -> logs to TSDB & PLC writes NG (2)

            var frame1 = CreateSyntheticPartImage(stripeWidth: 30);
            var frame2 = CreateSyntheticPartImage(stripeWidth: 45);

            var graph = new PipelineGraph();

            // 1. Source Node
            var source = new ImageSourceNode(new[] { frame1, frame2 }, "CameraFeed");

            // 2. Grayscale Converter Node
            var grayscale = new ImageGrayscaleNode("GrayscaleConverter");

            // 3. Caliper Metrology Node
            var caliper = new EdgeCaliperNode(x1: 10, y1: 30, x2: 90, y2: 30, minMagnitude: 20.0, name: "WidthCaliper");

            // 4. Dimensional Tolerance Judge Node
            var judge = new DimensionJudgeNode("PartWidth", nominalValue: 30.0, minTolerance: 28.0, maxTolerance: 32.0, name: "ToleranceJudge");

            // 5. Flow Quality Router (Passed vs Failed)
            var router = new QualityRouterNode("QualityGate");

            // 6. Passed and Failed Collectors
            var passedList = new List<InspectionResult>();
            var failedList = new List<InspectionResult>();
            var passedCollector = new ZeroPipeline.Core.Nodes.ActionSinkNode<InspectionResult>(passedList.Add, "PassedCollector");
            var failedCollector = new ZeroPipeline.Core.Nodes.ActionSinkNode<InspectionResult>(failedList.Add, "FailedCollector");

            // 7. TimeSeries TSDB Logger
            var tsdbSink = new TimeSeriesLogSinkNode(metricId: 101, blockSize: 2, name: "TSDB_Storage");

            // 8. PLC Register Sink (tracks register updates)
            ushort lastPlcStatus = 0;
            ushort lastPlcValue = 0;
            var plcSink = new PlcRegisterSinkNode(
                registerWrittenCallback: (status, val) =>
                {
                    lastPlcStatus = status;
                    lastPlcValue = val;
                },
                name: "PLC_Controller");

            // Wire DAG connections:
            graph.Connect(source.Output, grayscale.Input);
            graph.Connect(grayscale.Output, caliper.Input);
            graph.Connect(caliper.Output, judge.Input);

            // Judge Output fans out to QualityRouter, TSDB Logger, and PLC Sink:
            graph.Connect(judge.Output, router.Input);
            graph.Connect(judge.Output, tsdbSink.Input);
            graph.Connect(judge.Output, plcSink.Input);

            // Router splits to Passed and Failed collectors:
            graph.Connect(router.PassedOutput, passedCollector.Input);
            graph.Connect(router.FailedOutput, failedCollector.Input);

            // Validate DAG acyclic property
            graph.Validate();
            Assert.Equal(9, graph.NodeCount);

            var executor = new PipelineExecutor(graph);
            await executor.InitializeAsync();

            // Execute Frame 1 (Part 1: OK)
            await executor.ExecuteStepAsync();

            Assert.Single(passedList);
            Assert.Empty(failedList);
            Assert.Equal(1, (int)lastPlcStatus); // 1 = Passed
            Assert.InRange(lastPlcValue, 2900, 3100); // 30.00mm * 100 = 3000

            // Execute Frame 2 (Part 2: NG)
            await executor.ExecuteStepAsync();

            Assert.Single(passedList);
            Assert.Single(failedList);
            Assert.Equal(2, (int)lastPlcStatus); // 2 = Failed (NG)
            Assert.InRange(lastPlcValue, 4400, 4600); // 45.00mm * 100 = 4500

            // Verify TSDB compressed block has both points
            Assert.Equal(2, tsdbSink.TotalLoggedPoints);
            Assert.Single(tsdbSink.CompressedBlocks);

            var block = tsdbSink.CompressedBlocks[0];
            Assert.Equal(101, block.MetricId);
            Assert.Equal(2, block.Count);
            Assert.True(block.Max > block.Min);

            var decompressed = block.Decompress();
            Assert.Equal(2, decompressed.Count);
            Assert.InRange(decompressed[0].Value, 29.0, 31.0);
            Assert.InRange(decompressed[1].Value, 44.0, 46.0);
        }

        private static ImageBuffer CreateSyntheticPartImage(int stripeWidth)
        {
            var image = ImageBuffer.CreateBgra32(100, 60);
            int startX = 20;
            int endX = startX + stripeWidth;

            unsafe
            {
                for (int y = 0; y < 60; y++)
                {
                    byte* row = image.GetRowPointer(y);
                    for (int x = 0; x < 100; x++)
                    {
                        byte val = (x >= startX && x <= endX) ? (byte)230 : (byte)25;
                        int offset = x * 4;
                        row[offset] = val;     // B
                        row[offset + 1] = val; // G
                        row[offset + 2] = val; // R
                        row[offset + 3] = 255; // A
                    }
                }
            }

            return image;
        }
    }
}
