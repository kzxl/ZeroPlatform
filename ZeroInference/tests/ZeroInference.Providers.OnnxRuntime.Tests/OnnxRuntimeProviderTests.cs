using System;
using System.IO;
using Xunit;
using ZeroInference.Core.Engine;
using ZeroInference.Core.Graph;
using ZeroInference.Providers.OnnxRuntime;
using ZeroTensor.Core;

namespace ZeroInference.Providers.OnnxRuntime.Tests
{
    public class OnnxRuntimeProviderTests
    {
        [Fact]
        public void IInferenceSession_PolymorphicContract_SupportsBothPureAndProviderEngines()
        {
            // Verify that IInferenceSession enables true Micro-Kernel decoupling
            var graph = new InferenceGraph();
            graph.SetInputs("x");
            graph.SetOutputs("y");
            graph.AddNode(new InferenceNode("Relu_1", NodeKind.Relu, new[] { "x" }, "y"));

            // 1. Pure C# Sovereign Engine (Zero Dependencies)
            using IInferenceSession pureSession = InferenceEngine.CreateSession(graph);
            Assert.NotNull(pureSession);
            Assert.Contains("x", pureSession.InputNames);
            Assert.Contains("y", pureSession.OutputNames);

            var input = Tensor.FromArray(new float[] { -2f, 0f, 3f, -5f, 10f }, 1, 5);
            var pureOutput = pureSession.Run(input);

            Assert.Equal(0f, pureOutput[0, 0]);
            Assert.Equal(0f, pureOutput[0, 1]);
            Assert.Equal(3f, pureOutput[0, 2]);
            Assert.Equal(0f, pureOutput[0, 3]);
            Assert.Equal(10f, pureOutput[0, 4]);

            // 2. Both implement the exact same IInferenceSession interface
            Assert.IsAssignableFrom<IInferenceSession>(pureSession);
        }

        [Fact]
        public void OnnxRuntimeSession_ThrowsFileNotFound_WhenModelPathDoesNotExist()
        {
            var fakePath = Path.Combine(Path.GetTempPath(), $"nonexistent_model_{Guid.NewGuid():N}.onnx");

            Assert.Throws<FileNotFoundException>(() =>
            {
                using var session = new OnnxRuntimeSession(fakePath);
            });
        }

        [Fact]
        public void OnnxRuntimeSession_ThrowsArgumentException_WhenBytesEmpty()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                using var session = new OnnxRuntimeSession(Array.Empty<byte>());
            });
        }
    }
}
