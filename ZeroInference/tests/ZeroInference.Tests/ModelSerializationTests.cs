using System;
using System.IO;
using Xunit;
using ZeroInference.Core.Engine;
using ZeroInference.Core.Format;
using ZeroInference.Core.Graph;
using ZeroTensor.Core;

namespace ZeroInference.Tests
{
    public class ModelSerializationTests
    {
        [Fact]
        public void ZeroModel_SaveAndLoad_RoundtripsIdenticalInference()
        {
            var graph = new InferenceGraph();
            graph.SetInputs("in");
            graph.SetOutputs("out");

            var w = Tensor.FromArray(new float[] { 1.5f, -0.5f, 2.0f, 0.8f }, 2, 2);
            var b = Tensor.FromArray(new float[] { 0.2f, -0.1f }, 2);

            graph.AddNode(new InferenceNode("linear", NodeKind.Linear, new[] { "in" }, "l_out", w, b));
            graph.AddNode(new InferenceNode("relu", NodeKind.Relu, new[] { "l_out" }, "out"));

            string tempFile = Path.Combine(Path.GetTempPath(), $"test_model_{Guid.NewGuid():N}.zeromodel");

            try
            {
                // Save model
                ZeroModelSerializer.Save(graph, tempFile, modelName: "TestModel");
                Assert.True(File.Exists(tempFile));

                // Load back
                var loadedGraph = ZeroModelSerializer.Load(tempFile, out string modelName);
                Assert.Equal("TestModel", modelName);
                Assert.Equal(graph.Nodes.Count, loadedGraph.Nodes.Count);

                // Run both models on identical input
                var sessionOriginal = InferenceEngine.CreateSession(graph, optimize: false);
                var sessionLoaded = InferenceEngine.CreateSession(loadedGraph, optimize: false);

                var input = Tensor.FromArray(new float[] { 2.0f, 3.0f }, 1, 2);
                var out1 = sessionOriginal.Run(input);
                var out2 = sessionLoaded.Run(input);

                Assert.Equal(out1[0, 0], out2[0, 0]);
                Assert.Equal(out1[0, 1], out2[0, 1]);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }
    }
}
