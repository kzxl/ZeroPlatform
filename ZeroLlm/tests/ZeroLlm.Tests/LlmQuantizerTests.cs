using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using ZeroLlm.Core.Engine;
using ZeroLlm.Core.Format;
using ZeroLlm.Core.Quantization;
using ZeroLlm.Core.Sampling;
using ZeroTokenizer.Core.Abstractions;

namespace ZeroLlm.Tests
{
    public class LlmQuantizerTests
    {
        private class DummyTokenizer : ITokenizer
        {
            public int VocabularySize => 128;
            public int CountTokens(ReadOnlySpan<char> text) => 4;
            public int Encode(ReadOnlySpan<char> text, Span<int> destinationTokenIds)
            {
                destinationTokenIds[0] = 1;
                destinationTokenIds[1] = 10;
                destinationTokenIds[2] = 20;
                destinationTokenIds[3] = 30;
                return 4;
            }
            public int[] Encode(string text) => new int[] { 1, 10, 20, 30 };
            public string Decode(ReadOnlySpan<int> tokenIds) => " token";
            public int Decode(ReadOnlySpan<int> tokenIds, Span<char> destinationText)
            {
                " token".AsSpan().CopyTo(destinationText);
                return 6;
            }
        }

        [Fact]
        public void QuantizeAndDequantize_Q8_RoundtripFidelity()
        {
            float[] floats = new float[64];
            for (int i = 0; i < 64; i++)
            {
                floats[i] = (float)Math.Sin(i * 0.1);
            }

            var blocks = LlmQuantizer.QuantizeToQ8_0(floats);
            Assert.Equal(2, blocks.Length);

            float[] dequantized = LlmQuantizer.DequantizeQ8(blocks);
            Assert.Equal(floats.Length, dequantized.Length);

            for (int i = 0; i < 64; i++)
            {
                Assert.InRange(Math.Abs(floats[i] - dequantized[i]), 0.0f, 0.05f);
            }
        }

        [Fact]
        public void QuantizeAndDequantize_Q4_RoundtripFidelity()
        {
            float[] floats = new float[64];
            for (int i = 0; i < 64; i++)
            {
                floats[i] = (float)Math.Cos(i * 0.1);
            }

            var blocks = LlmQuantizer.QuantizeToQ4_0(floats);
            Assert.Equal(2, blocks.Length);

            float[] dequantized = LlmQuantizer.DequantizeQ4(blocks);
            Assert.Equal(floats.Length, dequantized.Length);

            for (int i = 0; i < 64; i++)
            {
                Assert.InRange(Math.Abs(floats[i] - dequantized[i]), 0.0f, 0.25f);
            }
        }

        [Fact]
        public void ByteConversion_Q8AndQ4_ExactPreservation()
        {
            float[] floats = new float[32];
            for (int i = 0; i < 32; i++) floats[i] = i * 0.25f;

            // Q8_0
            var q8Blocks = LlmQuantizer.QuantizeToQ8_0(floats);
            byte[] q8Bytes = LlmQuantizer.Q8BlocksToBytes(q8Blocks);
            Assert.Equal(34, q8Bytes.Length);

            var q8Restored = LlmQuantizer.BytesToQ8Blocks(q8Bytes);
            Assert.Single(q8Restored);
            Assert.Equal(q8Blocks[0].Scale, q8Restored[0].Scale);

            // Q4_0
            var q4Blocks = LlmQuantizer.QuantizeToQ4_0(floats);
            byte[] q4Bytes = LlmQuantizer.Q4BlocksToBytes(q4Blocks);
            Assert.Equal(18, q4Bytes.Length);

            var q4Restored = LlmQuantizer.BytesToQ4Blocks(q4Bytes);
            Assert.Single(q4Restored);
            Assert.Equal(q4Blocks[0].Scale, q4Restored[0].Scale);
        }

        [Fact]
        public void InplaceQuantize_ProducesValidQ8AndQ4Models()
        {
            var config = new LlmModelConfig
            {
                Architecture = "llama",
                VocabSize = 128,
                ContextLength = 64,
                EmbeddingDim = 64,
                LayerCount = 2,
                FeedForwardDim = 128,
                HeadCount = 2,
                HeadCountKv = 2
            };

            var fp32Model = LlmModel.CreateSynthetic(config, seed: 123);
            Assert.False(fp32Model.IsQuantized);
            Assert.Equal(GgufTensorType.F32, fp32Model.QuantizationType);

            // 1. Quantize to Q8_0
            var q8Model = LlmQuantizer.Quantize(fp32Model, GgufTensorType.Q8_0);
            Assert.True(q8Model.IsQuantized);
            Assert.Equal(GgufTensorType.Q8_0, q8Model.QuantizationType);
            Assert.NotNull(q8Model.TokenEmbeddings_Q8);
            Assert.NotNull(q8Model.Layers[0].Wq_Q8);
            Assert.NotNull(q8Model.LmHead_Q8);

            // 2. Quantize to Q4_0
            var q4Model = LlmQuantizer.Quantize(fp32Model, GgufTensorType.Q4_0);
            Assert.True(q4Model.IsQuantized);
            Assert.Equal(GgufTensorType.Q4_0, q4Model.QuantizationType);
            Assert.NotNull(q4Model.TokenEmbeddings_Q4);
            Assert.NotNull(q4Model.Layers[0].Wq_Q4);
            Assert.NotNull(q4Model.LmHead_Q4);
        }

        [Fact]
        public void GgufSerialization_Roundtrip_PreservesQuantizedWeights()
        {
            var config = new LlmModelConfig
            {
                Architecture = "llama",
                VocabSize = 128,
                ContextLength = 64,
                EmbeddingDim = 64,
                LayerCount = 2,
                FeedForwardDim = 128,
                HeadCount = 2,
                HeadCountKv = 2
            };

            var fp32Model = LlmModel.CreateSynthetic(config, seed: 42);

            // Q8_0 GGUF Roundtrip
            using (var q8Ms = new MemoryStream())
            {
                LlmQuantizer.QuantizeModel(fp32Model, q8Ms, GgufTensorType.Q8_0);
                q8Ms.Position = 0;

                var loadedQ8 = LlmModel.LoadGguf(q8Ms);
                Assert.True(loadedQ8.IsQuantized);
                Assert.Equal(GgufTensorType.Q8_0, loadedQ8.QuantizationType);
                Assert.NotNull(loadedQ8.TokenEmbeddings_Q8);
                Assert.NotNull(loadedQ8.Layers[0].Wq_Q8);
                Assert.NotNull(loadedQ8.LmHead_Q8);
            }

            // Q4_0 GGUF Roundtrip
            using (var q4Ms = new MemoryStream())
            {
                LlmQuantizer.QuantizeModel(fp32Model, q4Ms, GgufTensorType.Q4_0);
                q4Ms.Position = 0;

                var loadedQ4 = LlmModel.LoadGguf(q4Ms);
                Assert.True(loadedQ4.IsQuantized);
                Assert.Equal(GgufTensorType.Q4_0, loadedQ4.QuantizationType);
                Assert.NotNull(loadedQ4.TokenEmbeddings_Q4);
                Assert.NotNull(loadedQ4.Layers[0].Wq_Q4);
                Assert.NotNull(loadedQ4.LmHead_Q4);
            }
        }

        [Fact]
        public async Task LlmEngine_GeneratesTokensFromQuantizedModels()
        {
            var config = new LlmModelConfig
            {
                Architecture = "llama",
                VocabSize = 128,
                ContextLength = 64,
                EmbeddingDim = 64,
                LayerCount = 2,
                FeedForwardDim = 128,
                HeadCount = 2,
                HeadCountKv = 2
            };

            var fp32Model = LlmModel.CreateSynthetic(config, seed: 99);
            var tokenizer = new DummyTokenizer();
            var sampling = new SamplingConfig { MaxTokens = 5, Temperature = 0.0f };

            // 1. Q8_0 Execution
            var q8Model = LlmQuantizer.Quantize(fp32Model, GgufTensorType.Q8_0);
            using (var engineQ8 = new LlmEngine(q8Model, tokenizer, sampling))
            {
                string outputQ8 = await engineQ8.CompleteAsync("Xin chào");
                Assert.NotNull(outputQ8);
                Assert.NotEmpty(outputQ8);
            }

            // 2. Q4_0 Execution
            var q4Model = LlmQuantizer.Quantize(fp32Model, GgufTensorType.Q4_0);
            using (var engineQ4 = new LlmEngine(q4Model, tokenizer, sampling))
            {
                string outputQ4 = await engineQ4.CompleteAsync("Xin chào");
                Assert.NotNull(outputQ4);
                Assert.NotEmpty(outputQ4);
            }
        }
    }
}
