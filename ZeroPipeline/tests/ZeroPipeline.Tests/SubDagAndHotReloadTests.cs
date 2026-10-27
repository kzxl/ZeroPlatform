using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZeroPipeline.Core.Execution;
using ZeroPipeline.Core.Graph;
using ZeroPipeline.Core.Nodes;
using ZeroPipeline.Core.Ports;
using ZeroPipeline.Recipe.Builder;
using ZeroPipeline.Recipe.Models;
using ZeroPipeline.Recipe.Registry;

namespace ZeroPipeline.Tests
{
    public class SubDagAndHotReloadTests
    {
        private sealed class FixedSourceNode<T> : SourceNode<T>
        {
            private readonly T _value;

            public FixedSourceNode(T value, string? name = null) : base(name ?? "FixedSource")
            {
                _value = value;
            }

            protected override Task OnExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
            {
                Output.Emit(_value, context.CycleId);
                return Task.CompletedTask;
            }
        }

        private sealed class BinaryMathNode : PipelineNode
        {
            public InputPort<int> InputA { get; }
            public InputPort<int> InputB { get; }
            public OutputPort<int> OutputSum { get; }
            public OutputPort<int> OutputDiff { get; }

            public BinaryMathNode(string name = "BinaryMath") : base(name)
            {
                InputA = AddInputPort<int>("InputA");
                InputB = AddInputPort<int>("InputB");
                OutputSum = AddOutputPort<int>("Sum");
                OutputDiff = AddOutputPort<int>("Diff");
            }

            protected override Task OnExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
            {
                if (InputA.TryReceive(out var pktA) && InputB.TryReceive(out var pktB))
                {
                    OutputSum.Emit(pktA.Payload + pktB.Payload, context.CycleId);
                    OutputDiff.Emit(pktA.Payload - pktB.Payload, context.CycleId);
                }
                return Task.CompletedTask;
            }
        }

        [Fact]
        public async Task SubDagNode_EncapsulatesSubGraphAndExecutesSingleCycle()
        {
            // Outer graph: Source -> SubDagNode -> Sink
            var outerGraph = new PipelineGraph();
            var source = new FixedSourceNode<int>(5, "Source_5");
            var collected = new List<int>();
            var sink = new ActionSinkNode<int>(collected.Add, "OuterSink");

            // Sub-DAG: Add10 -> MultiplyBy2
            var subDagNode = new SubDagNode("Macro_MathProcessor");
            var add10 = new FuncTransformNode<int, int>(x => x + 10, "Inner_Add10");
            var mul2 = new FuncTransformNode<int, int>(x => x * 2, "Inner_Mul2");

            subDagNode.SubGraph.AddNode(add10);
            subDagNode.SubGraph.AddNode(mul2);
            subDagNode.SubGraph.Connect(add10.Output, mul2.Input);

            // Expose ports
            var externalIn = subDagNode.ExposeInput("InNumber", add10.Input);
            var externalOut = subDagNode.ExposeOutput("OutNumber", mul2.Output);

            // Connect outer graph
            outerGraph.AddNode(subDagNode);
            outerGraph.Connect(source.Output, externalIn);
            outerGraph.Connect(externalOut, sink.Input);

            var executor = new PipelineExecutor(outerGraph);
            var context = new PipelineContext();

            // Run cycle: (5 + 10) * 2 = 30
            await executor.ExecuteStepAsync(context);

            Assert.Single(collected);
            Assert.Equal(30, collected[0]);
        }

        [Fact]
        public async Task SubDagNode_HandlesMultipleInputsAndOutputs()
        {
            var outerGraph = new PipelineGraph();
            var sourceA = new FixedSourceNode<int>(20, "Source_A");
            var sourceB = new FixedSourceNode<int>(8, "Source_B");

            var sums = new List<int>();
            var diffs = new List<int>();
            var sumSink = new ActionSinkNode<int>(sums.Add, "SumSink");
            var diffSink = new ActionSinkNode<int>(diffs.Add, "DiffSink");

            var subDag = new SubDagNode("BinaryMathSubDag");
            var mathNode = new BinaryMathNode("InnerMath");
            subDag.SubGraph.AddNode(mathNode);

            var inA = subDag.ExposeInput("A", mathNode.InputA);
            var inB = subDag.ExposeInput("B", mathNode.InputB);
            var outSum = subDag.ExposeOutput("Sum", mathNode.OutputSum);
            var outDiff = subDag.ExposeOutput("Diff", mathNode.OutputDiff);

            outerGraph.AddNode(subDag);
            outerGraph.Connect(sourceA.Output, inA);
            outerGraph.Connect(sourceB.Output, inB);
            outerGraph.Connect(outSum, sumSink.Input);
            outerGraph.Connect(outDiff, diffSink.Input);

            var executor = new PipelineExecutor(outerGraph);
            await executor.ExecuteStepAsync();

            Assert.Single(sums);
            Assert.Equal(28, sums[0]); // 20 + 8

            Assert.Single(diffs);
            Assert.Equal(12, diffs[0]); // 20 - 8
        }

        [Fact]
        public async Task HotReloadPipelineRunner_SwapsGraphAtRuntimeWithoutLoss()
        {
            // Initial Graph 1: Value * 2
            var source1 = new FixedSourceNode<int>(10, "Src1");
            var transform1 = new FuncTransformNode<int, int>(x => x * 2, "Mul2");
            var results = new List<int>();
            var sink1 = new ActionSinkNode<int>(results.Add, "Sink1");

            var graph1 = new PipelineGraph();
            graph1.Connect(source1.Output, transform1.Input);
            graph1.Connect(transform1.Output, sink1.Input);

            var runner = new HotReloadPipelineRunner(graph1);
            await runner.InitializeAsync();

            PipelineGraph? oldGraphSeen = null;
            PipelineGraph? newGraphSeen = null;
            runner.GraphSwapped += (oldG, newG) =>
            {
                oldGraphSeen = oldG;
                newGraphSeen = newG;
            };

            // Cycle 1: 10 * 2 = 20
            await runner.ExecuteSingleCycleAsync();
            Assert.Single(results);
            Assert.Equal(20, results[0]);
            Assert.Equal(0, runner.ReloadCount);

            // Prepare Graph 2: Value * 5
            var source2 = new FixedSourceNode<int>(10, "Src2");
            var transform2 = new FuncTransformNode<int, int>(x => x * 5, "Mul5");
            var sink2 = new ActionSinkNode<int>(results.Add, "Sink2");

            var graph2 = new PipelineGraph();
            graph2.Connect(source2.Output, transform2.Input);
            graph2.Connect(transform2.Output, sink2.Input);

            // Hot reload to Graph 2
            await runner.ReloadAsync(graph2);
            Assert.Equal(1, runner.ReloadCount);
            Assert.Same(graph1, oldGraphSeen);
            Assert.Same(graph2, newGraphSeen);
            Assert.Same(graph2, runner.ActiveGraph);

            // Cycle 2: 10 * 5 = 50
            await runner.ExecuteSingleCycleAsync();
            Assert.Equal(2, results.Count);
            Assert.Equal(50, results[1]);
        }

        [Fact]
        public async Task HotReloadPipelineRunner_ReloadRecipeAsync_BuildsAndSwapsLiveRecipe()
        {
            // Register test nodes in custom registry
            var registry = new NodeRegistry();
            registry.Register("TestSource", (id, name, p) =>
            {
                int val = p != null && p.TryGetValue("Value", out var vStr) ? int.Parse(vStr) : 100;
                return new FixedSourceNode<int>(val, name);
            });
            registry.Register("TestMultiplier", (id, name, p) =>
            {
                int factor = p != null && p.TryGetValue("Factor", out var fStr) ? int.Parse(fStr) : 1;
                return new FuncTransformNode<int, int>(x => x * factor, name);
            });

            var outputs = new List<int>();
            registry.Register("TestCollector", (id, name, p) => new ActionSinkNode<int>(outputs.Add, name));

            var builder = new RecipeGraphBuilder(registry);

            // Recipe 1: 100 * 2 = 200
            var recipe1 = new RecipeModel
            {
                Name = "Inspection_Recipe_v1",
                Nodes = new List<NodeRecipeModel>
                {
                    new NodeRecipeModel { Id = "s", NodeType = "TestSource", Parameters = { ["Value"] = "100" } },
                    new NodeRecipeModel { Id = "m", NodeType = "TestMultiplier", Parameters = { ["Factor"] = "2" } },
                    new NodeRecipeModel { Id = "c", NodeType = "TestCollector" }
                },
                Connections = new List<ConnectionRecipeModel>
                {
                    new ConnectionRecipeModel { SourceNodeId = "s", SourcePortName = "Output", TargetNodeId = "m", TargetPortName = "Input" },
                    new ConnectionRecipeModel { SourceNodeId = "m", SourcePortName = "Output", TargetNodeId = "c", TargetPortName = "Input" }
                }
            };

            var initialGraph = builder.BuildGraph(recipe1);
            var runner = new HotReloadPipelineRunner(initialGraph);
            await runner.InitializeAsync();

            await runner.ExecuteSingleCycleAsync();
            Assert.Single(outputs);
            Assert.Equal(200, outputs[0]);

            // Recipe 2: 100 * 10 = 1000
            var recipe2 = new RecipeModel
            {
                Name = "Inspection_Recipe_v2",
                Nodes = new List<NodeRecipeModel>
                {
                    new NodeRecipeModel { Id = "s", NodeType = "TestSource", Parameters = { ["Value"] = "100" } },
                    new NodeRecipeModel { Id = "m", NodeType = "TestMultiplier", Parameters = { ["Factor"] = "10" } },
                    new NodeRecipeModel { Id = "c", NodeType = "TestCollector" }
                },
                Connections = new List<ConnectionRecipeModel>
                {
                    new ConnectionRecipeModel { SourceNodeId = "s", SourcePortName = "Output", TargetNodeId = "m", TargetPortName = "Input" },
                    new ConnectionRecipeModel { SourceNodeId = "m", SourcePortName = "Output", TargetNodeId = "c", TargetPortName = "Input" }
                }
            };

            await runner.ReloadRecipeAsync(recipe2, builder);

            await runner.ExecuteSingleCycleAsync();
            Assert.Equal(2, outputs.Count);
            Assert.Equal(1000, outputs[1]);
        }
    }
}
