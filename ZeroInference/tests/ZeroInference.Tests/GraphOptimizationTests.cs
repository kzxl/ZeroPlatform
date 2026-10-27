using System;
using Xunit;
using ZeroInference.Core.Engine;
using ZeroInference.Core.Graph;
using ZeroTensor.Core;

namespace ZeroInference.Tests
{
    public class GraphOptimizationTests
    {
        [Fact]
        public void GraphOptimizer_FusesConvAndBatchNorm()
        {
            var graph = new InferenceGraph();
            graph.SetInputs("input");
            graph.SetOutputs("bn_out");

            // Conv: 1 channel -> 1 channel, 3x3
            var convW = Tensor.Full<float>(2.0f, 1, 1, 3, 3);
            var convB = Tensor.Full<float>(1.0f, 1);
            graph.AddNode(new InferenceNode("conv1", NodeKind.Conv2D, new[] { "input" }, "c_out", convW, convB));

            // BatchNorm: mean=2, var=4, gamma=3, beta=0.5, eps=0
            var bn = new InferenceNode("bn1", NodeKind.BatchNormalization, new[] { "c_out" }, "bn_out");
            bn.ExtraAttributes["RunningMean"] = Tensor.FromArray(new[] { 2.0f }, 1);
            bn.ExtraAttributes["RunningVar"] = Tensor.FromArray(new[] { 4.0f }, 1);
            bn.ExtraAttributes["Gamma"] = Tensor.FromArray(new[] { 3.0f }, 1);
            bn.ExtraAttributes["Beta"] = Tensor.FromArray(new[] { 0.5f }, 1);
            bn.Epsilon = 0.0f;
            graph.AddNode(bn);

            // Execute unfused baseline
            var unoptimizedSession = InferenceEngine.CreateSession(graph, optimize: false);
            var testInput = Tensor.Full<float>(1.0f, 1, 1, 3, 3);
            var baselineOutput = unoptimizedSession.Run(testInput);

            // Optimize graph (should fuse Conv + BN into a single Conv node)
            var optimizedGraph = GraphOptimizer.Optimize(graph);
            Assert.Single(optimizedGraph.Nodes);
            Assert.Equal(NodeKind.Conv2D, optimizedGraph.Nodes[0].Kind);

            var optimizedSession = InferenceEngine.CreateSession(optimizedGraph, optimize: false);
            var fusedOutput = optimizedSession.Run(testInput);

            // Outputs must match closely
            var baseSpan = baselineOutput.AsReadOnlySpan();
            var fusedSpan = fusedOutput.AsReadOnlySpan();

            for (int i = 0; i < baseSpan.Length; i++)
            {
                Assert.InRange(fusedSpan[i], baseSpan[i] - 1e-4f, baseSpan[i] + 1e-4f);
            }
        }

        [Fact]
        public void GraphOptimizer_FusesConvAndRelu()
        {
            var graph = new InferenceGraph();
            graph.SetInputs("input");
            graph.SetOutputs("relu_out");

            var convW = Tensor.Ones<float>(1, 1, 2, 2);
            var convB = Tensor.Full<float>(-5.0f, 1); // Negative bias to test ReLU clipping
            graph.AddNode(new InferenceNode("conv", NodeKind.Conv2D, new[] { "input" }, "c_out", convW, convB));
            graph.AddNode(new InferenceNode("relu", NodeKind.Relu, new[] { "c_out" }, "relu_out"));

            var optimized = GraphOptimizer.Optimize(graph);

            // Should be fused into 1 FusedConvRelu node
            Assert.Single(optimized.Nodes);
            Assert.Equal(NodeKind.FusedConvRelu, optimized.Nodes[0].Kind);
            Assert.Equal("relu_out", optimized.Nodes[0].OutputName);

            var session = InferenceEngine.CreateSession(optimized, optimize: false);
            var input = Tensor.Full<float>(1.0f, 1, 1, 2, 2);
            var output = session.Run(input);

            // Sum = 4 * 1.0 - 5.0 = -1.0 -> ReLU(-1.0) = 0.0
            Assert.Equal(0.0f, output[0, 0, 0, 0]);
        }
    }
}
