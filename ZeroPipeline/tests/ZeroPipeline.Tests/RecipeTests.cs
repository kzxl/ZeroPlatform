using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZeroPipeline.Core.Execution;
using ZeroPipeline.Core.Nodes;
using ZeroPipeline.Core.Ports;
using ZeroPipeline.Recipe.Builder;
using ZeroPipeline.Recipe.Models;
using ZeroPipeline.Recipe.Registry;
using ZeroPipeline.Recipe.Serialization;

namespace ZeroPipeline.Tests
{
    public class RecipeTests
    {
        public class RecipeSourceNode : SourceNode<int>
        {
            public int StepValue { get; set; } = 1;

            public RecipeSourceNode(string name = "Source", string? id = null) : base(name, id) { }

            protected override Task OnExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
            {
                Output.Emit(StepValue);
                return Task.CompletedTask;
            }
        }

        public class RecipeMultiplierNode : TransformNode<int, int>
        {
            public int Factor { get; set; } = 2;

            public RecipeMultiplierNode(string name = "Multiplier", string? id = null) : base(name, id) { }

            protected override Task<int> ProcessAsync(int input, PipelineContext context, CancellationToken cancellationToken)
            {
                return Task.FromResult(input * Factor);
            }
        }

        public class RecipeCollectorNode : SinkNode<int>
        {
            public List<int> ReceivedValues { get; } = new List<int>();

            public RecipeCollectorNode(string name = "Collector", string? id = null) : base(name, id) { }

            protected override Task ConsumeAsync(int input, PipelineContext context, CancellationToken cancellationToken)
            {
                ReceivedValues.Add(input);
                return Task.CompletedTask;
            }
        }

        [Fact]
        public void JsonSerialization_Roundtrip_PreservesAllFields()
        {
            var recipe = new RecipeModel
            {
                RecipeId = "RECIPE-001",
                Name = "AOI Pin Measurement",
                Version = "2.1.0",
                Description = "Inspects pin width and pitch",
                CreatedAtUtc = new DateTime(2026, 9, 9, 9, 0, 0, DateTimeKind.Utc)
            };

            recipe.Nodes.Add(new NodeRecipeModel
            {
                Id = "node-1",
                Name = "CameraSource",
                NodeType = "ImageSourceNode",
                Parameters = new Dictionary<string, string>
                {
                    { "format", "Gray8" },
                    { "timeoutMs", "1500" }
                }
            });

            recipe.Nodes.Add(new NodeRecipeModel
            {
                Id = "node-2",
                Name = "WidthCaliper",
                NodeType = "EdgeCaliperNode",
                Parameters = new Dictionary<string, string>
                {
                    { "x1", "10.5" },
                    { "y1", "20.0" }
                }
            });

            recipe.Connections.Add(new ConnectionRecipeModel
            {
                SourceNodeId = "node-1",
                SourcePortName = "Output",
                TargetNodeId = "node-2",
                TargetPortName = "Input"
            });

            // Serialize to JSON
            string json = RecipeJsonSerializer.Serialize(recipe, pretty: true);
            Assert.Contains("\"recipeId\": \"RECIPE-001\"", json);
            Assert.Contains("\"name\": \"AOI Pin Measurement\"", json);
            Assert.Contains("\"version\": \"2.1.0\"", json);
            Assert.Contains("\"node-1\"", json);
            Assert.Contains("\"node-2\"", json);

            // Deserialize back to RecipeModel
            var deserialized = RecipeJsonSerializer.Deserialize(json);

            Assert.Equal(recipe.RecipeId, deserialized.RecipeId);
            Assert.Equal(recipe.Name, deserialized.Name);
            Assert.Equal(recipe.Version, deserialized.Version);
            Assert.Equal(recipe.Description, deserialized.Description);
            Assert.Equal(recipe.CreatedAtUtc, deserialized.CreatedAtUtc);

            Assert.Equal(2, deserialized.Nodes.Count);
            Assert.Equal("node-1", deserialized.Nodes[0].Id);
            Assert.Equal("CameraSource", deserialized.Nodes[0].Name);
            Assert.Equal("ImageSourceNode", deserialized.Nodes[0].NodeType);
            Assert.Equal("Gray8", deserialized.Nodes[0].Parameters["format"]);
            Assert.Equal("1500", deserialized.Nodes[0].Parameters["timeoutMs"]);

            Assert.Single(deserialized.Connections);
            Assert.Equal("node-1", deserialized.Connections[0].SourceNodeId);
            Assert.Equal("Output", deserialized.Connections[0].SourcePortName);
            Assert.Equal("node-2", deserialized.Connections[0].TargetNodeId);
            Assert.Equal("Input", deserialized.Connections[0].TargetPortName);
        }

        [Fact]
        public async Task RecipeGraphBuilder_BuildsAndExecutesPipeline()
        {
            var registry = new NodeRegistry();
            registry.Register<RecipeSourceNode>("RecipeSourceNode");
            registry.Register<RecipeMultiplierNode>("RecipeMultiplierNode");
            registry.Register<RecipeCollectorNode>("RecipeCollectorNode");

            var recipe = new RecipeModel
            {
                RecipeId = "RECIPE-MATH",
                Name = "Math Pipeline",
                Nodes = new List<NodeRecipeModel>
                {
                    new NodeRecipeModel
                    {
                        Id = "src",
                        Name = "Source",
                        NodeType = "RecipeSourceNode",
                        Parameters = new Dictionary<string, string> { { "StepValue", "7" } }
                    },
                    new NodeRecipeModel
                    {
                        Id = "mul",
                        Name = "Multiplier",
                        NodeType = "RecipeMultiplierNode",
                        Parameters = new Dictionary<string, string> { { "Factor", "5" } }
                    },
                    new NodeRecipeModel
                    {
                        Id = "sink",
                        Name = "Collector",
                        NodeType = "RecipeCollectorNode"
                    }
                },
                Connections = new List<ConnectionRecipeModel>
                {
                    new ConnectionRecipeModel
                    {
                        SourceNodeId = "src",
                        SourcePortName = "Output",
                        TargetNodeId = "mul",
                        TargetPortName = "Input"
                    },
                    new ConnectionRecipeModel
                    {
                        SourceNodeId = "mul",
                        SourcePortName = "Output",
                        TargetNodeId = "sink",
                        TargetPortName = "Input"
                    }
                }
            };

            var builder = new RecipeGraphBuilder(registry);
            var graph = builder.BuildGraph(recipe);

            Assert.Equal(3, graph.NodeCount);
            Assert.Equal(2, graph.ConnectionCount);

            var executor = new PipelineExecutor(graph);
            await executor.ExecuteStepAsync();

            var collector = graph.FindNode("sink") as RecipeCollectorNode;
            Assert.NotNull(collector);
            Assert.Single(collector!.ReceivedValues);
            Assert.Equal(35, collector.ReceivedValues[0]); // 7 * 5 = 35
        }

        [Fact]
        public void RecipeGraphBuilder_ExportGraphToRecipe_MatchesTopology()
        {
            var registry = new NodeRegistry();
            registry.Register<RecipeSourceNode>("RecipeSourceNode");
            registry.Register<RecipeMultiplierNode>("RecipeMultiplierNode");

            var builder = new RecipeGraphBuilder(registry);

            var src = new RecipeSourceNode("Source", "src-1") { StepValue = 12 };
            var mul = new RecipeMultiplierNode("Multiplier", "mul-1") { Factor = 3 };

            var graph = new Core.Graph.PipelineGraph();
            graph.Connect(src.Output, mul.Input);

            var exported = builder.ExportRecipe(graph, "EXPORT-01", "Exported Math Recipe", "1.2.0");

            Assert.Equal("EXPORT-01", exported.RecipeId);
            Assert.Equal("Exported Math Recipe", exported.Name);
            Assert.Equal("1.2.0", exported.Version);
            Assert.Equal(2, exported.Nodes.Count);
            Assert.Single(exported.Connections);

            var exportedSrc = exported.Nodes.Find(n => n.Id == "src-1");
            Assert.NotNull(exportedSrc);
            Assert.Equal("12", exportedSrc!.Parameters["StepValue"]);

            var exportedMul = exported.Nodes.Find(n => n.Id == "mul-1");
            Assert.NotNull(exportedMul);
            Assert.Equal("3", exportedMul!.Parameters["Factor"]);

            Assert.Equal("src-1", exported.Connections[0].SourceNodeId);
            Assert.Equal("Output", exported.Connections[0].SourcePortName);
            Assert.Equal("mul-1", exported.Connections[0].TargetNodeId);
            Assert.Equal("Input", exported.Connections[0].TargetPortName);
        }
    }
}
