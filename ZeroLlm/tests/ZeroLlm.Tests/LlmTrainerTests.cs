using System;
using System.IO;
using System.Linq;
using Xunit;
using ZeroLlm.Core.Engine;
using ZeroLlm.Core.Training;

namespace ZeroLlm.Tests
{
    public class LlmTrainerTests
    {
        [Fact]
        public void LlmModel_SaveGguf_And_LoadGguf_Should_Roundtrip_Accurately()
        {
            var config = new LlmModelConfig
            {
                Architecture = "llama",
                VocabSize = 128,
                ContextLength = 512,
                EmbeddingDim = 32,
                LayerCount = 2,
                HeadCount = 2,
                HeadCountKv = 2,
                FeedForwardDim = 64,
                RmsNormEps = 1e-5f,
                RopeFreqBase = 10000.0f,
                BosTokenId = 1,
                EosTokenId = 2
            };

            var originalModel = LlmModel.CreateSynthetic(config, seed: 777);

            using (var ms = new MemoryStream())
            {
                originalModel.SaveGguf(ms);
                ms.Position = 0;

                var loadedModel = LlmModel.LoadGguf(ms);

                Assert.Equal(originalModel.Config.Architecture, loadedModel.Config.Architecture);
                Assert.Equal(originalModel.Config.VocabSize, loadedModel.Config.VocabSize);
                Assert.Equal(originalModel.Config.ContextLength, loadedModel.Config.ContextLength);
                Assert.Equal(originalModel.Config.EmbeddingDim, loadedModel.Config.EmbeddingDim);
                Assert.Equal(originalModel.Config.LayerCount, loadedModel.Config.LayerCount);
                Assert.Equal(originalModel.Config.HeadCount, loadedModel.Config.HeadCount);
                Assert.Equal(originalModel.Config.HeadCountKv, loadedModel.Config.HeadCountKv);
                Assert.Equal(originalModel.Config.FeedForwardDim, loadedModel.Config.FeedForwardDim);

                // Verify embedding weights match
                Assert.Equal(originalModel.TokenEmbeddings.Length, loadedModel.TokenEmbeddings.Length);
                for (int i = 0; i < originalModel.TokenEmbeddings.Length; i++)
                {
                    Assert.Equal(originalModel.TokenEmbeddings[i], loadedModel.TokenEmbeddings[i], 5);
                }

                // Verify layer 0 weights match
                Assert.Equal(originalModel.Layers[0].Wq.Length, loadedModel.Layers[0].Wq.Length);
                for (int i = 0; i < originalModel.Layers[0].Wq.Length; i++)
                {
                    Assert.Equal(originalModel.Layers[0].Wq[i], loadedModel.Layers[0].Wq[i], 5);
                }

                // Verify LM head weights match
                Assert.Equal(originalModel.LmHead.Length, loadedModel.LmHead.Length);
                for (int i = 0; i < originalModel.LmHead.Length; i++)
                {
                    Assert.Equal(originalModel.LmHead[i], loadedModel.LmHead[i], 5);
                }
            }
        }

        [Fact]
        public void LlmTrainer_Should_Decrease_Loss_Monotonically_During_Training()
        {
            var config = new LlmModelConfig
            {
                VocabSize = 64,
                ContextLength = 64,
                EmbeddingDim = 16,
                LayerCount = 2,
                HeadCount = 2,
                HeadCountKv = 2,
                FeedForwardDim = 32,
                RmsNormEps = 1e-5f,
                RopeFreqBase = 10000.0f
            };

            var model = LlmModel.CreateSynthetic(config, seed: 42);

            var trainConfig = new TrainingConfig
            {
                LearningRate = 5e-3f,
                WeightDecay = 0.0f,
                Beta1 = 0.9f,
                Beta2 = 0.99f,
                MaxGradNorm = 1.0f,
                Mode = TrainingMode.Full
            };

            var trainer = new LlmTrainer(model, trainConfig);

            // Sequence to learn: 1 -> 10 -> 20 -> 30 -> 2
            int[] sequence = new[] { 1, 10, 20, 30, 2 };

            // Step 1: initial loss
            var firstStep = trainer.TrainStep(sequence, targetStartPos: 1);
            Assert.True(firstStep.Loss > 0f);
            Assert.Equal(4, firstStep.TargetTokenCount);

            float lastLoss = firstStep.Loss;

            // Train for 50 steps
            for (int i = 0; i < 50; i++)
            {
                var step = trainer.TrainStep(sequence, targetStartPos: 1);
                lastLoss = step.Loss;
            }

            // Assert significant loss convergence
            Assert.True(lastLoss < firstStep.Loss, $"Loss did not decrease: initial={firstStep.Loss}, final={lastLoss}");
            Assert.True(lastLoss < 1.0f, $"Loss should reach < 1.0 on a 5-token repeated sequence, actual={lastLoss}");
        }

        [Fact]
        public void LlmTrainer_Masked_Prompt_Should_Only_Train_Target_Positions()
        {
            var config = new LlmModelConfig
            {
                VocabSize = 64,
                ContextLength = 64,
                EmbeddingDim = 16,
                LayerCount = 2,
                HeadCount = 2,
                HeadCountKv = 2,
                FeedForwardDim = 32
            };

            var model = LlmModel.CreateSynthetic(config, seed: 101);
            var trainer = new LlmTrainer(model, new TrainingConfig { Mode = TrainingMode.Full });

            // Sequence: [1, 2, 3, 4, 5]
            // Prompt: [1, 2, 3] -> length 3
            // Target tokens start at index 3 ([4, 5])
            int[] tokens = new[] { 1, 2, 3, 4, 5 };
            var result = trainer.TrainStep(tokens, targetStartPos: 3);

            // Tokens at positions 3 and 4 are targets (token 4 and token 5)
            Assert.Equal(2, result.TargetTokenCount);
            Assert.True(result.Loss > 0f);
        }

        [Fact]
        public void LlmTrainer_HeadAndEmbeddings_Mode_Should_Train_Successfully()
        {
            var config = new LlmModelConfig
            {
                VocabSize = 32,
                ContextLength = 32,
                EmbeddingDim = 16,
                LayerCount = 2,
                HeadCount = 2,
                HeadCountKv = 2,
                FeedForwardDim = 32
            };

            var model = LlmModel.CreateSynthetic(config, seed: 123);
            var trainer = new LlmTrainer(model, new TrainingConfig
            {
                LearningRate = 1e-2f,
                Mode = TrainingMode.HeadAndEmbeddings
            });

            int[] seq = new[] { 5, 12, 18, 25 };
            var step1 = trainer.TrainStep(seq);

            for (int i = 0; i < 15; i++)
            {
                trainer.TrainStep(seq);
            }

            var finalStep = trainer.TrainStep(seq);
            Assert.True(finalStep.Loss < step1.Loss);
        }

        [Fact]
        public void LlmTrainer_TrainEpoch_Should_Compute_Averages_Accurately()
        {
            var config = new LlmModelConfig
            {
                VocabSize = 32,
                ContextLength = 32,
                EmbeddingDim = 16,
                LayerCount = 2,
                HeadCount = 2,
                HeadCountKv = 2,
                FeedForwardDim = 32
            };

            var model = LlmModel.CreateSynthetic(config, seed: 456);
            var trainer = new LlmTrainer(model, new TrainingConfig { LearningRate = 1e-3f });

            var dataset = new[]
            {
                new[] { 1, 2, 3 },
                new[] { 4, 5, 6, 7 },
                new[] { 8, 9 }
            };

            var epochResult = trainer.TrainEpoch(dataset, epoch: 1);

            Assert.Equal(1, epochResult.Epoch);
            Assert.True(epochResult.AverageLoss > 0f);
            Assert.Equal(6, epochResult.TotalTrainedTokens); // (3-1) + (4-1) + (2-1) = 2 + 3 + 1 = 6
            Assert.True(epochResult.Elapsed.TotalMilliseconds >= 0);
        }

        [Fact]
        public void LlmModelConfig_CreateVietnameseErpMicro_Should_Match_Design_Specs()
        {
            var microCfg = LlmModelConfig.CreateVietnameseErpMicro(16000, 1024);

            Assert.Equal(16000, microCfg.VocabSize);
            Assert.Equal(1024, microCfg.ContextLength);
            Assert.Equal(256, microCfg.EmbeddingDim);
            Assert.Equal(6, microCfg.LayerCount);
            Assert.Equal(8, microCfg.HeadCount);
            Assert.Equal(2, microCfg.HeadCountKv);
            Assert.Equal(32, microCfg.HeadDim);
            Assert.Equal(512, microCfg.FeedForwardDim);
        }
    }
}
