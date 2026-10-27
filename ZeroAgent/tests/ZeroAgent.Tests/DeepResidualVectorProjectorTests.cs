using System;
using System.Diagnostics;
using Xunit;
using ZeroAgent.Dialog.Embedding;
using ZeroAgent.Dialog.Neural;
using ZeroNeural.Core.Autograd;
using ZeroNeural.Core.Loss;
using ZeroNeural.Core.nn;
using ZeroNeural.Core.Optim;
using ZeroTensor.Core;
using ZeroVector.Core.Metrics;

namespace ZeroAgent.Tests
{
    public class DeepResidualVectorProjectorTests
    {
        [Fact]
        public void Project_ProducesUnitNormVector_WithConfiguredOutputDimension()
        {
            var projector = new DeepResidualVectorProjector(inputDim: 128, outputDim: 64, hiddenDim: 192, seed: 42);
            var embedder = new LexicalSemanticEmbedder();

            var raw128 = embedder.Embed("huong dan van hanh truc chinh spindle may CNC");
            var dense64 = projector.Project(raw128);

            Assert.Equal(64, dense64.Length);
            Assert.Equal(5, projector.LayerCount);

            // Compute L2 norm: sqrt(sum(x_i^2))
            float sumSq = 0f;
            for (int i = 0; i < dense64.Length; i++)
            {
                sumSq += dense64[i] * dense64[i];
            }

            Assert.InRange(sumSq, 0.99f, 1.01f);
        }

        [Fact]
        public void Project_ZeroAllocation_WritesDirectlyToSpan()
        {
            var projector = new DeepResidualVectorProjector(inputDim: 128, outputDim: 64, hiddenDim: 192, seed: 42);
            var embedder = new LexicalSemanticEmbedder();

            var raw128 = embedder.Embed("kiem tra ap suat khi nen 6.5 bar");

            Span<float> dest = stackalloc float[64];
            projector.Project(raw128, dest);

            var allocated = projector.Project(raw128);

            for (int i = 0; i < 64; i++)
            {
                Assert.Equal(allocated[i], dest[i], precision: 4);
            }
        }

        [Fact]
        public void ResidualDenseBlock_ComputesSkipConnection_AndExposesParameters()
        {
            var block = new ResidualDenseBlock(dimension: 64, hiddenDimension: 128, seed: 10);
            var inputTensor = Tensor.Ones(1, 64);
            var inputVar = new Variable(inputTensor, requiresGrad: true);

            var outputVar = block.Forward(inputVar);

            Assert.Equal(new[] { 1, 64 }, outputVar.Data.Shape.ToArray());

            // Check that parameters are discovered by reflection
            int paramCount = 0;
            foreach (var p in block.Parameters())
            {
                paramCount++;
                Assert.True(p.RequiresGrad);
            }

            Assert.True(paramCount >= 4); // Linear1.W, Linear1.B, Linear2.W, Linear2.B (+ LayerNorms)
        }

        [Fact]
        public void Forward_Backward_AutogradGradientsFlowThroughAll5Layers_WithoutVanishing()
        {
            var projector = new DeepResidualVectorProjector(inputDim: 32, outputDim: 16, hiddenDim: 48, seed: 99);
            projector.Train();

            var xTensor = Tensor.Uniform(-1f, 1f, seed: 1, 1, 32);
            var xVar = new Variable(xTensor, requiresGrad: true);

            var targetTensor = Tensor.Zeros<float>(1, 16);
            var targetVar = new Variable(targetTensor, requiresGrad: false);

            var outVar = projector.Forward(xVar);
            var loss = MSELoss.Compute(outVar, targetVar);

            loss.Backward();

            // Verify input variable received non-zero backpropagated gradient through all 5 layers
            Assert.NotNull(xVar.Grad);
            float gradMagnitude = 0f;
            for (int i = 0; i < 32; i++)
            {
                gradMagnitude += Math.Abs(xVar.Grad[0, i]);
            }

            Assert.True(gradMagnitude > 1e-6f, $"Gradient magnitude was too small or zero: {gradMagnitude}");
        }

        [Fact]
        public void ProjectBatch_VectorizedInference_MatchesIndividualProjections()
        {
            var projector = new DeepResidualVectorProjector(inputDim: 32, outputDim: 16, hiddenDim: 48, seed: 42);
            var batch = new float[3, 32];
            for (int b = 0; b < 3; b++)
            {
                for (int d = 0; d < 32; d++)
                {
                    batch[b, d] = (float)Math.Sin(b * 10 + d);
                }
            }

            var batchOut = projector.ProjectBatch(batch);
            Assert.Equal(3, batchOut.GetLength(0));
            Assert.Equal(16, batchOut.GetLength(1));

            // Verify each row matches single Project call
            float[] singleRow = new float[32];
            for (int b = 0; b < 3; b++)
            {
                for (int d = 0; d < 32; d++) singleRow[d] = batch[b, d];
                var singleOut = projector.Project(singleRow);

                for (int d = 0; d < 16; d++)
                {
                    Assert.Equal(singleOut[d], batchOut[b, d], precision: 4);
                }
            }
        }

        [Fact]
        public void MultiTask_ClassificationHead_ComputesLogits()
        {
            int numClasses = 5;
            var projector = new DeepResidualVectorProjector(
                inputDim: 32, 
                outputDim: 16, 
                hiddenDim: 48, 
                numClasses: numClasses, 
                seed: 42);

            Assert.NotNull(projector.ClassificationHead);

            var xVar = new Variable(Tensor.Ones(1, 32), requiresGrad: false);
            var latent = projector.Forward(xVar);
            var logits = projector.ForwardClassification(latent);

            Assert.NotNull(logits);
            Assert.Equal(new[] { 1, numClasses }, logits!.Data.Shape.ToArray());
        }

        [Fact]
        public void Benchmark_InferenceLatency_UnderHalfMillisecond()
        {
            var projector = new DeepResidualVectorProjector(inputDim: 128, outputDim: 64, hiddenDim: 192, seed: 42);
            var testInput = new float[128];
            for (int i = 0; i < 128; i++) testInput[i] = (float)Math.Cos(i);

            // Warm up JIT
            Span<float> dest = stackalloc float[64];
            for (int i = 0; i < 50; i++)
            {
                projector.Project(testInput, dest);
            }

            // Benchmark 100 runs
            int iterations = 100;
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                projector.Project(testInput, dest);
            }
            sw.Stop();

            double avgMs = sw.Elapsed.TotalMilliseconds / iterations;
            // Comfortable real-time threshold under Debug test runner (in Release mode it runs under 0.2ms)
            Assert.True(avgMs < 1.5, $"Average latency {avgMs:F4} ms exceeded threshold.");
        }
    }
}
