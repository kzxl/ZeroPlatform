using System;
using System.Linq;
using Xunit;
using ZeroInference.Core.Engine;
using ZeroInference.Core.Graph;
using ZeroTensor.Core;

namespace ZeroInference.Tests
{
    public class ExecutionSessionTests
    {
        [Fact]
        public void FeedForward_ConvReluLinearSoftmax_RunsSuccessfully()
        {
            var graph = new InferenceGraph();
            graph.SetInputs("input");
            graph.SetOutputs("probs");

            // Input: [1, 1, 6, 6]
            // Conv 1 -> 2 channels, 3x3, stride 1, pad 0 -> [1, 2, 4, 4]
            var convW = Tensor.Ones<float>(2, 1, 3, 3);
            var convB = Tensor.Zeros<float>(2);
            graph.AddNode(new InferenceNode("conv1", NodeKind.Conv2D, new[] { "input" }, "c1", convW, convB) { Stride = 1, Padding = 0, KernelSize = 3 });

            // ReLU
            graph.AddNode(new InferenceNode("relu1", NodeKind.Relu, new[] { "c1" }, "r1"));

            // Flatten: [1, 2 * 4 * 4] = [1, 32]
            graph.AddNode(new InferenceNode("flat1", NodeKind.Flatten, new[] { "r1" }, "f1"));

            // Linear: 32 -> 3 classes
            var linW = Tensor.Full<float>(0.1f, 3, 32);
            var linB = Tensor.Zeros<float>(3);
            graph.AddNode(new InferenceNode("fc1", NodeKind.Linear, new[] { "f1" }, "logits", linW, linB));

            // Softmax
            graph.AddNode(new InferenceNode("sm1", NodeKind.Softmax, new[] { "logits" }, "probs"));

            var session = InferenceEngine.CreateSession(graph, optimize: true);

            var input = Tensor.Full<float>(1.0f, 1, 1, 6, 6);
            var output = session.Run(input);

            Assert.Equal(new TensorShape(1, 3), output.Shape);

            // Softmax probabilities must sum to 1.0
            float sumProb = output[0, 0] + output[0, 1] + output[0, 2];
            Assert.InRange(sumProb, 0.999f, 1.001f);
        }

        [Fact]
        public void ZeroAllocation_RepeatedRuns_YieldConsistentResults()
        {
            var graph = new InferenceGraph();
            graph.SetInputs("x");
            graph.SetOutputs("y");

            var w = Tensor.Full<float>(0.5f, 4, 4);
            var b = Tensor.Full<float>(1.0f, 4);
            graph.AddNode(new InferenceNode("fc", NodeKind.Linear, new[] { "x" }, "y", w, b));

            var session = InferenceEngine.CreateSession(graph, optimize: false);

            var input = Tensor.Full<float>(2.0f, 1, 4);

            for (int i = 0; i < 50; i++)
            {
                var outTensor = session.Run(input);
                // y = x * W^T + b = (4 * 2.0 * 0.5) + 1.0 = 4.0 + 1.0 = 5.0
                Assert.Equal(5.0f, outTensor[0, 0]);
                Assert.Equal(5.0f, outTensor[0, 1]);
                Assert.Equal(5.0f, outTensor[0, 2]);
                Assert.Equal(5.0f, outTensor[0, 3]);
            }
        }
    }
}
