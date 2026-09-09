using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZeroPipeline.Core.Execution;
using ZeroPipeline.Core.Graph;
using ZeroPipeline.Core.Nodes;

namespace ZeroPipeline.Tests
{
    public class PipelineExecutionTests
    {
        private class NumberSourceNode : SourceNode<int>
        {
            private int _current;
            public NumberSourceNode(int initial = 1, string? name = null) : base(name ?? "NumberSource")
            {
                _current = initial;
            }

            protected override Task OnExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
            {
                Output.Emit(_current++);
                return Task.CompletedTask;
            }
        }

        private class FaultyNode : TransformNode<int, int>
        {
            public FaultyNode() : base("FaultyNode") { }

            protected override Task<int> ProcessAsync(int input, PipelineContext context, CancellationToken cancellationToken)
            {
                throw new InvalidOperationException("Simulated hardware or algorithm fault!");
            }
        }

        [Fact]
        public async Task SingleStepExecution_FlowsDataThroughPipeline()
        {
            var graph = new PipelineGraph();
            var source = new NumberSourceNode(10);
            var transform = new FuncTransformNode<int, int>(x => x * 3);
            var collected = new List<int>();
            var sink = new ActionSinkNode<int>(collected.Add);

            graph.Connect(source.Output, transform.Input);
            graph.Connect(transform.Output, sink.Input);

            var executor = new PipelineExecutor(graph);
            var context = new PipelineContext();

            // Execute 1st step: 10 * 3 = 30
            await executor.ExecuteStepAsync(context);
            Assert.Single(collected);
            Assert.Equal(30, collected[0]);

            // Execute 2nd step: 11 * 3 = 33
            await executor.ExecuteStepAsync(context);
            Assert.Equal(2, collected.Count);
            Assert.Equal(33, collected[1]);

            Assert.Equal(2, (int)context.CycleId);
        }

        [Fact]
        public async Task FanOutBroadcasting_EmitsToMultipleSinks()
        {
            var graph = new PipelineGraph();
            var source = new NumberSourceNode(100);

            var list1 = new List<int>();
            var list2 = new List<int>();
            var list3 = new List<int>();

            var sink1 = new ActionSinkNode<int>(list1.Add, "Sink1");
            var sink2 = new ActionSinkNode<int>(list2.Add, "Sink2");
            var sink3 = new ActionSinkNode<int>(list3.Add, "Sink3");

            graph.Connect(source.Output, sink1.Input);
            graph.Connect(source.Output, sink2.Input);
            graph.Connect(source.Output, sink3.Input);

            var executor = new PipelineExecutor(graph);
            await executor.ExecuteStepAsync();

            Assert.Single(list1);
            Assert.Equal(100, list1[0]);

            Assert.Single(list2);
            Assert.Equal(100, list2[0]);

            Assert.Single(list3);
            Assert.Equal(100, list3[0]);
        }

        [Fact]
        public async Task ContinuousStreaming_CancelsGracefully()
        {
            var graph = new PipelineGraph();
            var source = new NumberSourceNode(1);
            var collected = new List<int>();
            var sink = new ActionSinkNode<int>(collected.Add);

            graph.Connect(source.Output, sink.Input);

            var executor = new PipelineExecutor(graph);
            using var cts = new CancellationTokenSource();

            var streamingTask = Task.Run(async () =>
            {
                await executor.StartStreamingAsync(loopIntervalMs: 5, cancellationToken: cts.Token);
            });

            // Let it run for a short duration
            await Task.Delay(50);
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => streamingTask);

            // Verify multiple items were produced and consumed
            Assert.True(collected.Count >= 2, $"Expected at least 2 items, got {collected.Count}");
        }

        [Fact]
        public async Task ErrorIsolation_TriggersNodeFaultedEvent()
        {
            var graph = new PipelineGraph();
            var source = new NumberSourceNode(1);
            var faulty = new FaultyNode();
            var sink = new ActionSinkNode<int>(_ => { });

            graph.Connect(source.Output, faulty.Input);
            graph.Connect(faulty.Output, sink.Input);

            var executor = new PipelineExecutor(graph);
            IPipelineNode? faultedNode = null;
            Exception? caughtException = null;

            executor.NodeFaulted += (node, ex) =>
            {
                faultedNode = node;
                caughtException = ex;
            };

            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await executor.ExecuteStepAsync();
            });

            Assert.Same(faulty, faultedNode);
            Assert.NotNull(caughtException);
            Assert.Equal("Simulated hardware or algorithm fault!", caughtException!.Message);
            Assert.Equal(NodeState.Faulted, faulty.State);
        }

        [Fact]
        public async Task LifecycleEvents_TriggerDuringExecution()
        {
            var graph = new PipelineGraph();
            var source = new NumberSourceNode(1);
            var sink = new ActionSinkNode<int>(_ => { });

            graph.Connect(source.Output, sink.Input);

            var executor = new PipelineExecutor(graph);
            var executingNodes = new List<string>();
            var completedNodes = new List<string>();

            executor.NodeExecuting += (node, _) => executingNodes.Add(node.Name);
            executor.NodeCompleted += (node, _) => completedNodes.Add(node.Name);

            await executor.ExecuteStepAsync();

            Assert.Equal(2, executingNodes.Count);
            Assert.Contains("NumberSource", executingNodes);
            Assert.Contains("Sink<Int32>", executingNodes);

            Assert.Equal(2, completedNodes.Count);
            Assert.Contains("NumberSource", completedNodes);
            Assert.Contains("Sink<Int32>", completedNodes);
        }
    }
}
