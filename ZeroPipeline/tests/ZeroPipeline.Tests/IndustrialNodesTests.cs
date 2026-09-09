using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using ZeroGraphics.Imaging.Core;
using ZeroPipeline.Core.Execution;
using ZeroPipeline.Core.Graph;
using ZeroPipeline.Core.Nodes;
using ZeroPipeline.Nodes.Comm;
using ZeroPipeline.Nodes.Flow;
using ZeroPipeline.Nodes.Inspection;
using ZeroPipeline.Nodes.Storage;
using ZeroPipeline.Nodes.Vision;

namespace ZeroPipeline.Tests
{
    public class IndustrialNodesTests
    {
        [Fact]
        public async Task BranchNode_RoutesPacketsCorrectly()
        {
            var graph = new PipelineGraph();
            var numbers = new[] { 2, 5, 8, 11 };
            var evenList = new List<int>();
            var oddList = new List<int>();

            var branch = new BranchNode<int>(x => x % 2 == 0);
            var evenSink = new ActionSinkNode<int>(evenList.Add, "EvenSink");
            var oddSink = new ActionSinkNode<int>(oddList.Add, "OddSink");

            graph.Connect(branch.TrueOutput, evenSink.Input);
            graph.Connect(branch.FalseOutput, oddSink.Input);

            var executor = new PipelineExecutor(graph);
            await executor.InitializeAsync();

            foreach (var n in numbers)
            {
                branch.Input.Deliver(new Core.Ports.DataPacket<int>(n));
                await executor.ExecuteStepAsync();
            }

            Assert.Equal(new[] { 2, 8 }, evenList);
            Assert.Equal(new[] { 5, 11 }, oddList);
        }

        [Fact]
        public async Task QualityRouterNode_RoutesPassedAndFailedResults()
        {
            var graph = new PipelineGraph();
            var router = new QualityRouterNode();

            var passed = new List<InspectionResult>();
            var failed = new List<InspectionResult>();

            var passSink = new ActionSinkNode<InspectionResult>(passed.Add, "PassSink");
            var failSink = new ActionSinkNode<InspectionResult>(failed.Add, "FailSink");

            graph.Connect(router.PassedOutput, passSink.Input);
            graph.Connect(router.FailedOutput, failSink.Input);

            var executor = new PipelineExecutor(graph);
            await executor.InitializeAsync();

            var r1 = InspectionResult.Pass("Length", 50.1, 49.0, 51.0);
            var r2 = InspectionResult.Fail("Width", 35.5, 29.0, 31.0);

            router.Input.Deliver(new Core.Ports.DataPacket<InspectionResult>(r1));
            await executor.ExecuteStepAsync();

            router.Input.Deliver(new Core.Ports.DataPacket<InspectionResult>(r2));
            await executor.ExecuteStepAsync();

            Assert.Single(passed);
            Assert.Same(r1, passed[0]);

            Assert.Single(failed);
            Assert.Same(r2, failed[0]);
        }

        [Fact]
        public async Task BatchAccumulatorNode_EmitsBatchesOfFixedSize()
        {
            var graph = new PipelineGraph();
            var batcher = new BatchAccumulatorNode<int>(batchSize: 3);
            var batches = new List<IReadOnlyList<int>>();
            var sink = new ActionSinkNode<IReadOnlyList<int>>(batches.Add);

            graph.Connect(batcher.Output, sink.Input);

            var executor = new PipelineExecutor(graph);
            await executor.InitializeAsync();

            // Send 1, 2 (no batch yet)
            batcher.Input.Deliver(new Core.Ports.DataPacket<int>(1));
            batcher.Input.Deliver(new Core.Ports.DataPacket<int>(2));
            await executor.ExecuteStepAsync();
            Assert.Empty(batches);

            // Send 3 (batch 1 ready: 1, 2, 3)
            batcher.Input.Deliver(new Core.Ports.DataPacket<int>(3));
            await executor.ExecuteStepAsync();
            Assert.Single(batches);
            Assert.Equal(new[] { 1, 2, 3 }, batches[0]);

            // Send 4, 5, 6 (batch 2 ready: 4, 5, 6)
            batcher.Input.Deliver(new Core.Ports.DataPacket<int>(4));
            batcher.Input.Deliver(new Core.Ports.DataPacket<int>(5));
            batcher.Input.Deliver(new Core.Ports.DataPacket<int>(6));
            await executor.ExecuteStepAsync();
            Assert.Equal(2, batches.Count);
            Assert.Equal(new[] { 4, 5, 6 }, batches[1]);
        }

        [Fact]
        public async Task Vision_EdgeCaliperAndDimensionJudge_FullInspectionPipeline()
        {
            // 1. Create a 100x60 grayscale image with a white stripe between X=30 and X=60 (width = 30px)
            var image = ImageBuffer.CreateGray8(100, 60);
            unsafe
            {
                for (int y = 0; y < 60; y++)
                {
                    byte* row = image.GetRowPointer(y);
                    for (int x = 0; x < 100; x++)
                    {
                        row[x] = (x >= 30 && x <= 60) ? (byte)240 : (byte)20;
                    }
                }
            }

            // 2. Build Pipeline: ImageSource -> EdgeCaliperNode -> DimensionJudgeNode -> Sink
            var graph = new PipelineGraph();
            var source = new ImageSourceNode(new[] { image });
            var caliper = new EdgeCaliperNode(x1: 10, y1: 30, x2: 90, y2: 30, minMagnitude: 20.0);
            var judge = new DimensionJudgeNode("StripeWidth", nominalValue: 30.0, minTolerance: 28.0, maxTolerance: 32.0);

            InspectionResult? finalResult = null;
            var sink = new ActionSinkNode<InspectionResult>(r => finalResult = r);

            graph.Connect(source.Output, caliper.Input);
            graph.Connect(caliper.Output, judge.Input);
            graph.Connect(judge.Output, sink.Input);

            var executor = new PipelineExecutor(graph);
            await executor.ExecuteStepAsync();

            Assert.NotNull(finalResult);
            Assert.True(finalResult!.IsPassed);
            Assert.InRange(finalResult.MeasuredValue, 29.0, 31.0);
            Assert.Equal("StripeWidth", finalResult.InspectionName);
        }

        [Fact]
        public async Task Storage_TimeSeriesLogSink_LogsAndCompressesPoints()
        {
            var graph = new PipelineGraph();
            var logger = new TimeSeriesLogSinkNode(metricId: 42, blockSize: 3);

            var r1 = InspectionResult.Pass("Diameter", 10.5, 10.0, 11.0);
            var r2 = InspectionResult.Pass("Diameter", 10.6, 10.0, 11.0);
            var r3 = InspectionResult.Pass("Diameter", 10.7, 10.0, 11.0);

            var executor = new PipelineExecutor(graph.AddNode(logger));
            await executor.InitializeAsync();

            logger.Input.Deliver(new Core.Ports.DataPacket<InspectionResult>(r1));
            await executor.ExecuteStepAsync();

            logger.Input.Deliver(new Core.Ports.DataPacket<InspectionResult>(r2));
            await executor.ExecuteStepAsync();

            logger.Input.Deliver(new Core.Ports.DataPacket<InspectionResult>(r3));
            await executor.ExecuteStepAsync();

            Assert.Equal(3, logger.TotalLoggedPoints);
            Assert.Single(logger.CompressedBlocks);

            var block = logger.CompressedBlocks[0];
            Assert.Equal(42, block.MetricId);
            Assert.Equal(3, block.Count);
            Assert.InRange(block.Min, 10.49, 10.51);
            Assert.InRange(block.Max, 10.69, 10.71);

            // Verify decompression matches original values
            var decompressed = block.Decompress();
            Assert.Equal(3, decompressed.Count);
            Assert.Equal(10.5, decompressed[0].Value, 4);
            Assert.Equal(10.6, decompressed[1].Value, 4);
            Assert.Equal(10.7, decompressed[2].Value, 4);
        }

        [Fact]
        public async Task Comm_PlcRegisterSink_DispatchesStatusAndValues()
        {
            ushort writtenStatus = 0;
            ushort writtenValue = 0;

            var plcSink = new PlcRegisterSinkNode(
                registerWrittenCallback: (status, val) =>
                {
                    writtenStatus = status;
                    writtenValue = val;
                });

            var r = InspectionResult.Pass("Thickness", 12.34, 12.0, 13.0);
            plcSink.Input.Deliver(new Core.Ports.DataPacket<InspectionResult>(r));

            var graph = new PipelineGraph().AddNode(plcSink);
            var executor = new PipelineExecutor(graph);
            await executor.ExecuteStepAsync();

            Assert.Equal(1, (int)writtenStatus); // 1 = Passed
            Assert.Equal(1234, (int)writtenValue); // 12.34 * 100 = 1234
        }
    }
}
