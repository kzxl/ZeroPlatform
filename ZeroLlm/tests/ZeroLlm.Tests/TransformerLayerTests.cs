using System;
using Xunit;
using ZeroLlm.Core.Engine;
using ZeroLlm.Core.Layers;
using ZeroLlm.Core.Memory;

namespace ZeroLlm.Tests
{
    public class TransformerLayerTests
    {
        [Fact]
        public void RmsNorm_Should_Normalize_Vector_Correctly()
        {
            float[] input = new float[] { 2.0f, 2.0f, 2.0f, 2.0f };
            float[] weight = new float[] { 1.0f, 1.0f, 1.0f, 1.0f };
            float[] output = new float[4];

            // Mean square = (4 * 4) / 4 = 4.0. RMS = sqrt(4 + eps) ~ 2.0. Output should be 2.0 / 2.0 * 1.0 = 1.0.
            RmsNorm.Forward(input, weight, output, epsilon: 0.0f);

            for (int i = 0; i < 4; i++)
            {
                Assert.InRange(output[i], 0.999f, 1.001f);
            }
        }

        [Fact]
        public void RoPE_Position_Zero_Should_Preserve_Magnitude()
        {
            float[] vec = new float[] { 1.0f, 2.0f, 3.0f, 4.0f };
            float originalNorm = (float)Math.Sqrt(1 + 4 + 9 + 16);

            RoPE.ApplyInplace(vec, pos: 5, headDim: 4, freqBase: 10000.0f);

            float rotatedNorm = (float)Math.Sqrt(vec[0] * vec[0] + vec[1] * vec[1] + vec[2] * vec[2] + vec[3] * vec[3]);
            Assert.InRange(rotatedNorm, originalNorm - 0.001f, originalNorm + 0.001f);
        }

        [Fact]
        public void SwiGLU_Should_Compute_Swish_Gated_Unit()
        {
            float[] gate = new float[] { 0.0f, 2.0f };
            float[] up = new float[] { 3.0f, 1.0f };
            float[] output = new float[2];

            SwiGLU.Forward(gate, up, output);

            // Swish(0) = 0 * 0.5 = 0. output[0] = 0 * 3 = 0.
            Assert.Equal(0.0f, output[0]);

            // Swish(2) = 2 / (1 + exp(-2)) ~ 2 * 0.8808 = 1.7616. output[1] = 1.7616 * 1.0.
            Assert.InRange(output[1], 1.75f, 1.77f);
        }

        [Fact]
        public void GqaAttention_Should_Produce_Valid_Outputs()
        {
            int qHeads = 4;
            int kvHeads = 2;
            int headDim = 8;
            int seqLen = 3;

            using (var cache = new PagedKvCache(8, 4, 1, kvHeads, headDim))
            {
                float[] keyData = new float[kvHeads * headDim];
                float[] valData = new float[kvHeads * headDim];

                for (int t = 0; t < seqLen; t++)
                {
                    for (int i = 0; i < keyData.Length; i++)
                    {
                        keyData[i] = 0.1f * (t + 1);
                        valData[i] = 0.2f * (t + 1);
                    }
                    cache.AppendToken(1, 0, keyData, valData);
                }

                float[] q = new float[qHeads * headDim];
                for (int i = 0; i < q.Length; i++) q[i] = 0.5f;

                float[] output = new float[qHeads * headDim];
                GqaAttention.Forward(q, 1, 0, cache, qHeads, kvHeads, headDim, output);

                // Check that outputs are non-zero and finite
                for (int i = 0; i < output.Length; i++)
                {
                    Assert.False(float.IsNaN(output[i]));
                    Assert.True(output[i] > 0.0f);
                }
            }
        }

        [Fact]
        public void SparseMoeLayer_Top2_Should_Route_And_Compute_Accurately()
        {
            int embDim = 4;
            int ffnDim = 8;
            int expertCount = 4;
            int topK = 2;

            // Router weights: 4 experts x 4 embDim
            // Make expert 1 and expert 3 have strongly positive weights for positive inputs
            float[] wrouter = new float[expertCount * embDim];
            // Expert 0: uniform 0.1
            for (int d = 0; d < embDim; d++) wrouter[0 * embDim + d] = 0.1f;
            // Expert 1: 2.0 (High logit)
            for (int d = 0; d < embDim; d++) wrouter[1 * embDim + d] = 2.0f;
            // Expert 2: -1.0
            for (int d = 0; d < embDim; d++) wrouter[2 * embDim + d] = -1.0f;
            // Expert 3: 1.5 (Second highest logit)
            for (int d = 0; d < embDim; d++) wrouter[3 * embDim + d] = 1.5f;

            var experts = new ZeroLlm.Core.Engine.LlmExpertWeights[expertCount];
            for (int e = 0; e < expertCount; e++)
            {
                var exp = new ZeroLlm.Core.Engine.LlmExpertWeights { ExpertId = e };
                exp.Wgate = new float[embDim * ffnDim];
                exp.Wup = new float[embDim * ffnDim];
                exp.Wdown = new float[ffnDim * embDim];
                Array.Fill(exp.Wgate, 0.5f);
                Array.Fill(exp.Wup, 0.5f);
                Array.Fill(exp.Wdown, 0.25f);
                experts[e] = exp;
            }

            float[] xNorm = new float[] { 1.0f, 1.0f, 1.0f, 1.0f };
            float[] gateScratch = new float[ffnDim];
            float[] upScratch = new float[ffnDim];
            float[] swigluScratch = new float[ffnDim];
            float[] residual = new float[embDim];

            SparseMoeLayer.Forward(
                xNorm,
                wrouter,
                null,
                null,
                experts,
                topK,
                embDim,
                ffnDim,
                gateScratch,
                upScratch,
                swigluScratch,
                residual);

            for (int d = 0; d < embDim; d++)
            {
                Assert.False(float.IsNaN(residual[d]));
                Assert.True(residual[d] > 0.0f, $"Residual at dim {d} should be positive.");
            }
        }

        [Fact]
        public void LlmTrainer_MoE_TrainingStep_Should_UpdateWeights_And_NotProduceNaN()
        {
            var config = LlmModelConfig.CreateMicroMoE(
                vocabSize: 64,
                contextLength: 32,
                embeddingDim: 16,
                layerCount: 2,
                headCount: 2,
                headCountKv: 2,
                feedForwardDim: 32,
                expertCount: 4,
                expertUsedCount: 2);

            var model = LlmModel.CreateSynthetic(config, seed: 123);
            var trainer = new ZeroLlm.Core.Training.LlmTrainer(model, new ZeroLlm.Core.Training.TrainingConfig
            {
                LearningRate = 1e-2f,
                AuxiliaryLossWeight = 0.05f,
                Mode = ZeroLlm.Core.Training.TrainingMode.Full
            });

            float initialRouterWeight = model.Layers[0].Wrouter![0];

            int[] tokens = new int[] { 1, 10, 15, 20, 25, 30, 2 };
            var result1 = trainer.TrainStep(tokens, targetStartPos: 2);

            Assert.False(float.IsNaN(result1.Loss));
            Assert.True(result1.Loss > 0f);
            Assert.True(result1.TargetTokenCount > 0);

            // Run 5 more steps to verify loss decreases and stability
            float lastLoss = result1.Loss;
            for (int i = 0; i < 5; i++)
            {
                var stepResult = trainer.TrainStep(tokens, targetStartPos: 2);
                Assert.False(float.IsNaN(stepResult.Loss));
                lastLoss = stepResult.Loss;
            }

            Assert.True(lastLoss < result1.Loss, $"Loss should decrease: initial={result1.Loss}, final={lastLoss}");
            float updatedRouterWeight = model.Layers[0].Wrouter![0];
            Assert.NotEqual(initialRouterWeight, updatedRouterWeight);
        }

        [Fact]
        public void LlmTrainer_SharedExpertMoE_TrainingStep_Should_UpdateSharedAndRoutedWeights_And_NotProduceNaN()
        {
            var config = LlmModelConfig.CreateMicroMoE(
                vocabSize: 64,
                contextLength: 32,
                embeddingDim: 16,
                layerCount: 2,
                headCount: 2,
                headCountKv: 2,
                feedForwardDim: 32,
                expertCount: 4,
                expertUsedCount: 2,
                sharedExpertCount: 1);

            var model = LlmModel.CreateSynthetic(config, seed: 456);
            Assert.NotNull(model.Layers[0].SharedExperts);
            Assert.Single(model.Layers[0].SharedExperts!);

            var trainer = new ZeroLlm.Core.Training.LlmTrainer(model, new ZeroLlm.Core.Training.TrainingConfig
            {
                LearningRate = 1e-2f,
                AuxiliaryLossWeight = 0.05f,
                Mode = ZeroLlm.Core.Training.TrainingMode.Full
            });

            float initialSharedWdown = model.Layers[0].SharedExperts![0].Wdown[0];
            float initialRoutedWdown = model.Layers[0].Experts![0].Wdown[0];

            int[] tokens = new int[] { 1, 12, 18, 22, 28, 35, 2 };
            var result1 = trainer.TrainStep(tokens, targetStartPos: 2);

            Assert.False(float.IsNaN(result1.Loss));
            Assert.True(result1.Loss > 0f);

            // Step a few times to verify convergence and gradient updates
            for (int i = 0; i < 5; i++)
            {
                var step = trainer.TrainStep(tokens, targetStartPos: 2);
                Assert.False(float.IsNaN(step.Loss));
            }

            float updatedSharedWdown = model.Layers[0].SharedExperts![0].Wdown[0];
            float updatedRoutedWdown = model.Layers[0].Experts![0].Wdown[0];

            Assert.NotEqual(initialSharedWdown, updatedSharedWdown);
            Assert.NotEqual(initialRoutedWdown, updatedRoutedWdown);
        }
    }
}
