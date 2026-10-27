using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using ZeroLlm.Core.Engine;
using ZeroLlm.Core.Generation;
using ZeroLlm.Core.Sampling;
using ZeroTokenizer.Core.Tiktoken;

namespace ZeroLlm.Tests
{
    public class LlmEngineTests
    {
        [Fact]
        public async Task LlmEngine_Should_Generate_Tokens_End_To_End()
        {
            var config = new LlmModelConfig
            {
                VocabSize = 256,
                EmbeddingDim = 32,
                LayerCount = 2,
                HeadCount = 2,
                HeadCountKv = 2,
                FeedForwardDim = 64,
                EosTokenId = 255
            };

            var model = LlmModel.CreateSynthetic(config, seed: 123);
            var tokenizer = TiktokenTokenizer.CreateCl100kBase();

            var sampling = new SamplingConfig
            {
                Temperature = 0.0f,
                MaxTokens = 5
            };

            using (var engine = new LlmEngine(model, tokenizer, sampling))
            {
                var completion = await engine.CompleteAsync("Hello");
                Assert.NotNull(completion);

                var streamTokens = new List<string>();
                await foreach (var piece in engine.GenerateStreamAsync("Test"))
                {
                    streamTokens.Add(piece);
                }

                Assert.NotEmpty(streamTokens);
            }
        }

        [Fact]
        public async Task LlmEngine_Should_Apply_LogitProcessor_During_Streaming_Generation()
        {
            var config = new LlmModelConfig
            {
                VocabSize = 256,
                EmbeddingDim = 32,
                LayerCount = 2,
                HeadCount = 2,
                HeadCountKv = 2,
                FeedForwardDim = 64,
                EosTokenId = 255
            };

            var model = LlmModel.CreateSynthetic(config, seed: 123);
            var tokenizer = TiktokenTokenizer.CreateCl100kBase();

            var sampling = new SamplingConfig
            {
                Temperature = 0.0f,
                MaxTokens = 3
            };

            using (var engine = new LlmEngine(model, tokenizer, sampling))
            {
                // Mask all tokens except token 42
                var completion = await engine.CompleteAsync("Hi", sampling, logitProcessor: logits =>
                {
                    for (int i = 0; i < logits.Length; i++)
                    {
                        if (i != 42) logits[i] = -1e9f;
                    }
                });

                // Decode of token 42 repeated 3 times
                string expectedPiece = tokenizer.Decode(new[] { 42 });
                Assert.Contains(expectedPiece, completion);
            }
        }

        [Fact]
        public void PrefillPrompt_Batched_ProducesValidLogitsAndPopulatesKvCache()
        {
            var config = new LlmModelConfig
            {
                VocabSize = 128,
                EmbeddingDim = 32,
                LayerCount = 2,
                HeadCount = 2,
                HeadCountKv = 2,
                FeedForwardDim = 64,
                EosTokenId = 127
            };

            var model = LlmModel.CreateSynthetic(config, seed: 777);
            var tokenizer = TiktokenTokenizer.CreateCl100kBase();

            using (var engine = new LlmEngine(model, tokenizer))
            {
                int[] promptTokens = new[] { 5, 12, 23, 34, 45, 56 };
                float[] batchedLogits = new float[config.VocabSize];

                engine.PrefillPrompt(promptTokens, seqId: 42, batchedLogits, startPos: 0);

                Assert.Equal(promptTokens.Length, engine.KvCache.GetSequenceLength(42));
                for (int i = 0; i < batchedLogits.Length; i++)
                {
                    Assert.False(float.IsNaN(batchedLogits[i]));
                    Assert.False(float.IsInfinity(batchedLogits[i]));
                }

                // Compare against sequential single-token prefill
                float[] seqLogits = new float[config.VocabSize];
                for (int t = 0; t < promptTokens.Length; t++)
                {
                    engine.PrefillPrompt(promptTokens.AsSpan(t, 1), seqId: 43, seqLogits, startPos: t);
                }

                for (int i = 0; i < config.VocabSize; i++)
                {
                    Assert.Equal(seqLogits[i], batchedLogits[i], precision: 4);
                }
            }
        }

        [Fact]
        public void PromptLookupDrafter_MatchesLongestNgramAndProposesContinuations()
        {
            var drafter = new PromptLookupDrafter(maxNgramSize: 4, minNgramSize: 2, defaultDraftLength: 3);

            // Context contains pattern [10, 20, 30, 40, 50, 60] earlier, and ends with [10, 20, 30]
            int[] context = new[] { 1, 2, 10, 20, 30, 40, 50, 60, 7, 8, 10, 20, 30 };

            int[] drafted = drafter.Draft(context, maxDraftTokens: 3);
            Assert.Equal(new[] { 40, 50, 60 }, drafted);

            // When a 2-gram appears twice, the most recent occurrence should be preferred
            int[] contextRecent = new[] { 5, 6, 100, 101, 9, 5, 6, 200, 201, 8, 5, 6 };
            int[] draftedRecent = drafter.ProposeDraftTokens(contextRecent, maxDraftTokens: 2);
            Assert.Equal(new[] { 200, 201 }, draftedRecent);

            // When no N-gram matches, returns empty
            int[] noMatch = new[] { 1, 2, 3, 4, 99, 98 };
            Assert.Empty(drafter.Draft(noMatch));
        }
    }
}
