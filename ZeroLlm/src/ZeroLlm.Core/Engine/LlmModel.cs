using System;
using System.Collections.Generic;
using System.IO;
using ZeroLlm.Core.Format;
using ZeroLlm.Core.Quantization;

namespace ZeroLlm.Core.Engine
{
    public sealed class LlmExpertWeights
    {
        public int ExpertId { get; set; }
        public float[] Wgate { get; set; } = Array.Empty<float>();
        public float[] Wup { get; set; } = Array.Empty<float>();
        public float[] Wdown { get; set; } = Array.Empty<float>();

        public BlockQ8_0[]? Wgate_Q8 { get; set; }
        public BlockQ8_0[]? Wup_Q8 { get; set; }
        public BlockQ8_0[]? Wdown_Q8 { get; set; }

        public BlockQ4_0[]? Wgate_Q4 { get; set; }
        public BlockQ4_0[]? Wup_Q4 { get; set; }
        public BlockQ4_0[]? Wdown_Q4 { get; set; }
    }

    public sealed class LlmLayerWeights
    {
        public float[] AttnNorm { get; set; } = Array.Empty<float>();
        public float[] Wq { get; set; } = Array.Empty<float>();
        public float[] Wk { get; set; } = Array.Empty<float>();
        public float[] Wv { get; set; } = Array.Empty<float>();
        public float[] Wo { get; set; } = Array.Empty<float>();
        public float[] FfnNorm { get; set; } = Array.Empty<float>();
        public float[] Wgate { get; set; } = Array.Empty<float>();
        public float[] Wup { get; set; } = Array.Empty<float>();
        public float[] Wdown { get; set; } = Array.Empty<float>();

        // Quantized 8-bit representations (Q8_0)
        public BlockQ8_0[]? Wq_Q8 { get; set; }
        public BlockQ8_0[]? Wk_Q8 { get; set; }
        public BlockQ8_0[]? Wv_Q8 { get; set; }
        public BlockQ8_0[]? Wo_Q8 { get; set; }
        public BlockQ8_0[]? Wgate_Q8 { get; set; }
        public BlockQ8_0[]? Wup_Q8 { get; set; }
        public BlockQ8_0[]? Wdown_Q8 { get; set; }

        // Quantized 4-bit representations (Q4_0)
        public BlockQ4_0[]? Wq_Q4 { get; set; }
        public BlockQ4_0[]? Wk_Q4 { get; set; }
        public BlockQ4_0[]? Wv_Q4 { get; set; }
        public BlockQ4_0[]? Wo_Q4 { get; set; }
        public BlockQ4_0[]? Wgate_Q4 { get; set; }
        public BlockQ4_0[]? Wup_Q4 { get; set; }
        public BlockQ4_0[]? Wdown_Q4 { get; set; }

        // Sparse Mixture-of-Experts (MoE) Weights
        public float[] Wrouter { get; set; } = Array.Empty<float>();
        public BlockQ8_0[]? Wrouter_Q8 { get; set; }
        public BlockQ4_0[]? Wrouter_Q4 { get; set; }
        public LlmExpertWeights[]? Experts { get; set; }
        public LlmExpertWeights[]? SharedExperts { get; set; }
        public bool IsMoE => (Experts != null && Experts.Length > 0) || (SharedExperts != null && SharedExperts.Length > 0);
    }

    /// <summary>
    /// In-memory representation of an SLM/LLM's weights and architecture.
    /// </summary>
    public sealed class LlmModel
    {
        public LlmModelConfig Config { get; }
        public GgufTensorType QuantizationType { get; set; } = GgufTensorType.F32;
        public bool IsQuantized => QuantizationType != GgufTensorType.F32;

        public float[] TokenEmbeddings { get; set; } = Array.Empty<float>();
        public BlockQ8_0[]? TokenEmbeddings_Q8 { get; set; }
        public BlockQ4_0[]? TokenEmbeddings_Q4 { get; set; }

        public LlmLayerWeights[] Layers { get; set; } = Array.Empty<LlmLayerWeights>();
        public float[] FinalNorm { get; set; } = Array.Empty<float>();

        public float[] LmHead { get; set; } = Array.Empty<float>();
        public BlockQ8_0[]? LmHead_Q8 { get; set; }
        public BlockQ4_0[]? LmHead_Q4 { get; set; }

        public LlmModel(LlmModelConfig config)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// Creates a initialized model with deterministic synthetic weights for tests and simulation.
        /// </summary>
        public static LlmModel CreateSynthetic(LlmModelConfig config, int seed = 42)
        {
            var rand = new Random(seed);
            var model = new LlmModel(config)
            {
                TokenEmbeddings = CreateRandomArray(config.VocabSize * config.EmbeddingDim, rand, 0.02f),
                Layers = new LlmLayerWeights[config.LayerCount],
                FinalNorm = CreateConstantArray(config.EmbeddingDim, 1.0f),
                LmHead = CreateRandomArray(config.EmbeddingDim * config.VocabSize, rand, 0.02f)
            };

            int qDim = config.HeadCount * config.HeadDim;
            int kvDim = config.HeadCountKv * config.HeadDim;

            for (int l = 0; l < config.LayerCount; l++)
            {
                var layer = new LlmLayerWeights
                {
                    AttnNorm = CreateConstantArray(config.EmbeddingDim, 1.0f),
                    Wq = CreateRandomArray(config.EmbeddingDim * qDim, rand, 0.02f),
                    Wk = CreateRandomArray(config.EmbeddingDim * kvDim, rand, 0.02f),
                    Wv = CreateRandomArray(config.EmbeddingDim * kvDim, rand, 0.02f),
                    Wo = CreateRandomArray(qDim * config.EmbeddingDim, rand, 0.02f),
                    FfnNorm = CreateConstantArray(config.EmbeddingDim, 1.0f)
                };

                if (config.IsMoE)
                {
                    if (config.ExpertCount > 0)
                    {
                        layer.Wrouter = CreateRandomArray(config.EmbeddingDim * config.ExpertCount, rand, 0.02f);
                        layer.Experts = new LlmExpertWeights[config.ExpertCount];
                        for (int e = 0; e < config.ExpertCount; e++)
                        {
                            layer.Experts[e] = new LlmExpertWeights
                            {
                                ExpertId = e,
                                Wgate = CreateRandomArray(config.EmbeddingDim * config.FeedForwardDim, rand, 0.02f),
                                Wup = CreateRandomArray(config.EmbeddingDim * config.FeedForwardDim, rand, 0.02f),
                                Wdown = CreateRandomArray(config.FeedForwardDim * config.EmbeddingDim, rand, 0.02f)
                            };
                        }
                    }

                    if (config.SharedExpertCount > 0)
                    {
                        layer.SharedExperts = new LlmExpertWeights[config.SharedExpertCount];
                        for (int se = 0; se < config.SharedExpertCount; se++)
                        {
                            layer.SharedExperts[se] = new LlmExpertWeights
                            {
                                ExpertId = -1 - se,
                                Wgate = CreateRandomArray(config.EmbeddingDim * config.FeedForwardDim, rand, 0.02f),
                                Wup = CreateRandomArray(config.EmbeddingDim * config.FeedForwardDim, rand, 0.02f),
                                Wdown = CreateRandomArray(config.FeedForwardDim * config.EmbeddingDim, rand, 0.02f)
                            };
                        }
                    }
                }
                else
                {
                    layer.Wgate = CreateRandomArray(config.EmbeddingDim * config.FeedForwardDim, rand, 0.02f);
                    layer.Wup = CreateRandomArray(config.EmbeddingDim * config.FeedForwardDim, rand, 0.02f);
                    layer.Wdown = CreateRandomArray(config.FeedForwardDim * config.EmbeddingDim, rand, 0.02f);
                }

                model.Layers[l] = layer;
            }

            return model;
        }

        /// <summary>
        /// Serializes the model weights and architecture hyperparameters into standard GGUF v3 binary format.
        /// </summary>
        public void SaveGguf(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            var writer = new GgufWriter();
            var arch = Config.Architecture ?? "llama";

            // Metadata
            writer.AddMetadata("general.architecture", arch);
            writer.AddMetadata($"{arch}.vocab_size", (uint)Config.VocabSize);
            writer.AddMetadata($"{arch}.context_length", (uint)Config.ContextLength);
            writer.AddMetadata($"{arch}.embedding_length", (uint)Config.EmbeddingDim);
            writer.AddMetadata($"{arch}.block_count", (uint)Config.LayerCount);
            writer.AddMetadata($"{arch}.feed_forward_length", (uint)Config.FeedForwardDim);
            writer.AddMetadata($"{arch}.attention.head_count", (uint)Config.HeadCount);
            writer.AddMetadata($"{arch}.attention.head_count_kv", (uint)Config.HeadCountKv);
            writer.AddMetadata($"{arch}.attention.layer_norm_rms_epsilon", Config.RmsNormEps);
            writer.AddMetadata($"{arch}.rope.freq_base", Config.RopeFreqBase);
            writer.AddMetadata("tokenizer.ggml.bos_token_id", (uint)Config.BosTokenId);
            writer.AddMetadata("tokenizer.ggml.eos_token_id", (uint)Config.EosTokenId);

            if (Config.IsMoE)
            {
                writer.AddMetadata($"{arch}.expert_count", (uint)Config.ExpertCount);
                writer.AddMetadata($"{arch}.expert_used_count", (uint)Config.ExpertUsedCount);
                if (Config.SharedExpertCount > 0)
                {
                    writer.AddMetadata($"{arch}.expert_shared_count", (uint)Config.SharedExpertCount);
                }
            }

            // Tensors
            // 1. Embeddings
            WriteTensor(writer, "token_embd.weight",
                new ulong[] { (ulong)Config.EmbeddingDim, (ulong)Config.VocabSize },
                TokenEmbeddings, TokenEmbeddings_Q8, TokenEmbeddings_Q4);

            // 2. Layers
            int qDim = Config.HeadCount * Config.HeadDim;
            int kvDim = Config.HeadCountKv * Config.HeadDim;

            for (int l = 0; l < Config.LayerCount; l++)
            {
                var layer = Layers[l];
                writer.AddTensor($"blk.{l}.attn_norm.weight", new ulong[] { (ulong)Config.EmbeddingDim }, GgufTensorType.F32, FloatArrayToBytes(layer.AttnNorm));
                WriteTensor(writer, $"blk.{l}.attn_q.weight", new ulong[] { (ulong)Config.EmbeddingDim, (ulong)qDim }, layer.Wq, layer.Wq_Q8, layer.Wq_Q4);
                WriteTensor(writer, $"blk.{l}.attn_k.weight", new ulong[] { (ulong)Config.EmbeddingDim, (ulong)kvDim }, layer.Wk, layer.Wk_Q8, layer.Wk_Q4);
                WriteTensor(writer, $"blk.{l}.attn_v.weight", new ulong[] { (ulong)Config.EmbeddingDim, (ulong)kvDim }, layer.Wv, layer.Wv_Q8, layer.Wv_Q4);
                WriteTensor(writer, $"blk.{l}.attn_output.weight", new ulong[] { (ulong)qDim, (ulong)Config.EmbeddingDim }, layer.Wo, layer.Wo_Q8, layer.Wo_Q4);
                writer.AddTensor($"blk.{l}.ffn_norm.weight", new ulong[] { (ulong)Config.EmbeddingDim }, GgufTensorType.F32, FloatArrayToBytes(layer.FfnNorm));

                if (Config.IsMoE)
                {
                    if (layer.Experts != null && layer.Experts.Length > 0)
                    {
                        WriteTensor(writer, $"blk.{l}.ffn_gate_inp.weight", new ulong[] { (ulong)Config.EmbeddingDim, (ulong)Config.ExpertCount }, layer.Wrouter, layer.Wrouter_Q8, layer.Wrouter_Q4);
                        for (int e = 0; e < layer.Experts.Length; e++)
                        {
                            var exp = layer.Experts[e];
                            WriteTensor(writer, $"blk.{l}.ffn_gate.{e}.weight", new ulong[] { (ulong)Config.EmbeddingDim, (ulong)Config.FeedForwardDim }, exp.Wgate, exp.Wgate_Q8, exp.Wgate_Q4);
                            WriteTensor(writer, $"blk.{l}.ffn_up.{e}.weight", new ulong[] { (ulong)Config.EmbeddingDim, (ulong)Config.FeedForwardDim }, exp.Wup, exp.Wup_Q8, exp.Wup_Q4);
                            WriteTensor(writer, $"blk.{l}.ffn_down.{e}.weight", new ulong[] { (ulong)Config.FeedForwardDim, (ulong)Config.EmbeddingDim }, exp.Wdown, exp.Wdown_Q8, exp.Wdown_Q4);
                        }
                    }

                    if (layer.SharedExperts != null && layer.SharedExperts.Length > 0)
                    {
                        for (int se = 0; se < layer.SharedExperts.Length; se++)
                        {
                            var sExp = layer.SharedExperts[se];
                            WriteTensor(writer, $"blk.{l}.ffn_gate_shexp.{se}.weight", new ulong[] { (ulong)Config.EmbeddingDim, (ulong)Config.FeedForwardDim }, sExp.Wgate, sExp.Wgate_Q8, sExp.Wgate_Q4);
                            WriteTensor(writer, $"blk.{l}.ffn_up_shexp.{se}.weight", new ulong[] { (ulong)Config.EmbeddingDim, (ulong)Config.FeedForwardDim }, sExp.Wup, sExp.Wup_Q8, sExp.Wup_Q4);
                            WriteTensor(writer, $"blk.{l}.ffn_down_shexp.{se}.weight", new ulong[] { (ulong)Config.FeedForwardDim, (ulong)Config.EmbeddingDim }, sExp.Wdown, sExp.Wdown_Q8, sExp.Wdown_Q4);
                        }
                    }
                }
                else
                {
                    WriteTensor(writer, $"blk.{l}.ffn_gate.weight", new ulong[] { (ulong)Config.EmbeddingDim, (ulong)Config.FeedForwardDim }, layer.Wgate, layer.Wgate_Q8, layer.Wgate_Q4);
                    WriteTensor(writer, $"blk.{l}.ffn_up.weight", new ulong[] { (ulong)Config.EmbeddingDim, (ulong)Config.FeedForwardDim }, layer.Wup, layer.Wup_Q8, layer.Wup_Q4);
                    WriteTensor(writer, $"blk.{l}.ffn_down.weight", new ulong[] { (ulong)Config.FeedForwardDim, (ulong)Config.EmbeddingDim }, layer.Wdown, layer.Wdown_Q8, layer.Wdown_Q4);
                }
            }

            // 3. Final norm & LM head
            writer.AddTensor("output_norm.weight", new ulong[] { (ulong)Config.EmbeddingDim }, GgufTensorType.F32, FloatArrayToBytes(FinalNorm));
            WriteTensor(writer, "output.weight", new ulong[] { (ulong)Config.EmbeddingDim, (ulong)Config.VocabSize }, LmHead, LmHead_Q8, LmHead_Q4);

            writer.WriteTo(stream);
        }

        public void SaveGguf(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentNullException(nameof(filePath));
            using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                SaveGguf(fs);
            }
        }

        /// <summary>
        /// Deserializes a model from standard GGUF binary format stream.
        /// </summary>
        public static LlmModel LoadGguf(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            var reader = GgufReader.FromStream(stream);
            var arch = reader.Metadata.Architecture ?? "llama";

            int vocabSize = (int)reader.Metadata.GetValueOrDefault($"{arch}.vocab_size", 32000u);
            int contextLength = (int)reader.Metadata.GetValueOrDefault($"{arch}.context_length", 2048u);
            int embeddingDim = (int)reader.Metadata.GetValueOrDefault($"{arch}.embedding_length", 256u);
            int layerCount = (int)reader.Metadata.GetValueOrDefault($"{arch}.block_count", 4u);
            int feedForwardDim = (int)reader.Metadata.GetValueOrDefault($"{arch}.feed_forward_length", 512u);
            int headCount = (int)reader.Metadata.GetValueOrDefault($"{arch}.attention.head_count", 4u);
            int headCountKv = (int)reader.Metadata.GetValueOrDefault($"{arch}.attention.head_count_kv", (uint)headCount);
            float rmsNormEps = reader.Metadata.GetValueOrDefault($"{arch}.attention.layer_norm_rms_epsilon", 1e-5f);
            float ropeFreqBase = reader.Metadata.GetValueOrDefault($"{arch}.rope.freq_base", 10000.0f);
            int bosTokenId = (int)reader.Metadata.GetValueOrDefault("tokenizer.ggml.bos_token_id", 1u);
            int eosTokenId = (int)reader.Metadata.GetValueOrDefault("tokenizer.ggml.eos_token_id", 2u);
            int expertCount = (int)reader.Metadata.GetValueOrDefault($"{arch}.expert_count", 0u);
            int expertUsedCount = (int)reader.Metadata.GetValueOrDefault($"{arch}.expert_used_count", 0u);
            int sharedExpertCount = (int)reader.Metadata.GetValueOrDefault($"{arch}.expert_shared_count", 0u);

            var tensorMap = new Dictionary<string, GgufTensorInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in reader.Tensors)
            {
                tensorMap[t.Name] = t;
            }

            if (tensorMap.TryGetValue("token_embd.weight", out var embTensor) && embTensor.Dimensions.Length >= 2)
            {
                embeddingDim = (int)embTensor.Dimensions[0];
                vocabSize = (int)embTensor.Dimensions[1];
            }

            var config = new LlmModelConfig
            {
                Architecture = arch,
                VocabSize = vocabSize,
                ContextLength = contextLength,
                EmbeddingDim = embeddingDim,
                LayerCount = layerCount,
                FeedForwardDim = feedForwardDim,
                HeadCount = headCount,
                HeadCountKv = headCountKv,
                RmsNormEps = rmsNormEps,
                RopeFreqBase = ropeFreqBase,
                BosTokenId = bosTokenId,
                EosTokenId = eosTokenId,
                ExpertCount = expertCount,
                ExpertUsedCount = expertUsedCount,
                SharedExpertCount = sharedExpertCount
            };

            var model = new LlmModel(config)
            {
                Layers = new LlmLayerWeights[layerCount]
            };

            if (tensorMap.TryGetValue("token_embd.weight", out var tEmb))
            {
                LoadTensor(stream, reader.TensorDataOffset, tEmb, out var f32, out var q8, out var q4);
                model.TokenEmbeddings = f32;
                model.TokenEmbeddings_Q8 = q8;
                model.TokenEmbeddings_Q4 = q4;
            }

            for (int l = 0; l < layerCount; l++)
            {
                var layer = new LlmLayerWeights();
                if (tensorMap.TryGetValue($"blk.{l}.attn_norm.weight", out var tAttnNorm))
                    layer.AttnNorm = ReadTensorFloats(stream, reader.TensorDataOffset, tAttnNorm);

                if (tensorMap.TryGetValue($"blk.{l}.attn_q.weight", out var tWq))
                {
                    LoadTensor(stream, reader.TensorDataOffset, tWq, out var f32, out var q8, out var q4);
                    layer.Wq = f32; layer.Wq_Q8 = q8; layer.Wq_Q4 = q4;
                }
                if (tensorMap.TryGetValue($"blk.{l}.attn_k.weight", out var tWk))
                {
                    LoadTensor(stream, reader.TensorDataOffset, tWk, out var f32, out var q8, out var q4);
                    layer.Wk = f32; layer.Wk_Q8 = q8; layer.Wk_Q4 = q4;
                }
                if (tensorMap.TryGetValue($"blk.{l}.attn_v.weight", out var tWv))
                {
                    LoadTensor(stream, reader.TensorDataOffset, tWv, out var f32, out var q8, out var q4);
                    layer.Wv = f32; layer.Wv_Q8 = q8; layer.Wv_Q4 = q4;
                }
                if (tensorMap.TryGetValue($"blk.{l}.attn_output.weight", out var tWo))
                {
                    LoadTensor(stream, reader.TensorDataOffset, tWo, out var f32, out var q8, out var q4);
                    layer.Wo = f32; layer.Wo_Q8 = q8; layer.Wo_Q4 = q4;
                }
                if (tensorMap.TryGetValue($"blk.{l}.ffn_norm.weight", out var tFfnNorm))
                    layer.FfnNorm = ReadTensorFloats(stream, reader.TensorDataOffset, tFfnNorm);

                if (expertCount > 1)
                {
                    layer.Experts = new LlmExpertWeights[expertCount];
                    if (tensorMap.TryGetValue($"blk.{l}.ffn_gate_inp.weight", out var tRouter))
                    {
                        LoadTensor(stream, reader.TensorDataOffset, tRouter, out var f32, out var q8, out var q4);
                        layer.Wrouter = f32; layer.Wrouter_Q8 = q8; layer.Wrouter_Q4 = q4;
                    }
                    for (int e = 0; e < expertCount; e++)
                    {
                        var exp = new LlmExpertWeights { ExpertId = e };
                        if (tensorMap.TryGetValue($"blk.{l}.ffn_gate.{e}.weight", out var tGateExp))
                        {
                            LoadTensor(stream, reader.TensorDataOffset, tGateExp, out var f32, out var q8, out var q4);
                            exp.Wgate = f32; exp.Wgate_Q8 = q8; exp.Wgate_Q4 = q4;
                        }
                        if (tensorMap.TryGetValue($"blk.{l}.ffn_up.{e}.weight", out var tUpExp))
                        {
                            LoadTensor(stream, reader.TensorDataOffset, tUpExp, out var f32, out var q8, out var q4);
                            exp.Wup = f32; exp.Wup_Q8 = q8; exp.Wup_Q4 = q4;
                        }
                        if (tensorMap.TryGetValue($"blk.{l}.ffn_down.{e}.weight", out var tDownExp))
                        {
                            LoadTensor(stream, reader.TensorDataOffset, tDownExp, out var f32, out var q8, out var q4);
                            exp.Wdown = f32; exp.Wdown_Q8 = q8; exp.Wdown_Q4 = q4;
                        }
                        layer.Experts[e] = exp;
                    }
                }

                if (sharedExpertCount > 0)
                {
                    layer.SharedExperts = new LlmExpertWeights[sharedExpertCount];
                    for (int se = 0; se < sharedExpertCount; se++)
                    {
                        var sExp = new LlmExpertWeights { ExpertId = -1 - se };
                        if (tensorMap.TryGetValue($"blk.{l}.ffn_gate_shexp.{se}.weight", out var tGateSExp))
                        {
                            LoadTensor(stream, reader.TensorDataOffset, tGateSExp, out var f32, out var q8, out var q4);
                            sExp.Wgate = f32; sExp.Wgate_Q8 = q8; sExp.Wgate_Q4 = q4;
                        }
                        if (tensorMap.TryGetValue($"blk.{l}.ffn_up_shexp.{se}.weight", out var tUpSExp))
                        {
                            LoadTensor(stream, reader.TensorDataOffset, tUpSExp, out var f32, out var q8, out var q4);
                            sExp.Wup = f32; sExp.Wup_Q8 = q8; sExp.Wup_Q4 = q4;
                        }
                        if (tensorMap.TryGetValue($"blk.{l}.ffn_down_shexp.{se}.weight", out var tDownSExp))
                        {
                            LoadTensor(stream, reader.TensorDataOffset, tDownSExp, out var f32, out var q8, out var q4);
                            sExp.Wdown = f32; sExp.Wdown_Q8 = q8; sExp.Wdown_Q4 = q4;
                        }
                        layer.SharedExperts[se] = sExp;
                    }
                }

                if (expertCount <= 1 && sharedExpertCount <= 0)
                {
                    if (tensorMap.TryGetValue($"blk.{l}.ffn_gate.weight", out var tWgate))
                    {
                        LoadTensor(stream, reader.TensorDataOffset, tWgate, out var f32, out var q8, out var q4);
                        layer.Wgate = f32; layer.Wgate_Q8 = q8; layer.Wgate_Q4 = q4;
                    }
                    if (tensorMap.TryGetValue($"blk.{l}.ffn_up.weight", out var tWup))
                    {
                        LoadTensor(stream, reader.TensorDataOffset, tWup, out var f32, out var q8, out var q4);
                        layer.Wup = f32; layer.Wup_Q8 = q8; layer.Wup_Q4 = q4;
                    }
                    if (tensorMap.TryGetValue($"blk.{l}.ffn_down.weight", out var tWdown))
                    {
                        LoadTensor(stream, reader.TensorDataOffset, tWdown, out var f32, out var q8, out var q4);
                        layer.Wdown = f32; layer.Wdown_Q8 = q8; layer.Wdown_Q4 = q4;
                    }
                }

                model.Layers[l] = layer;
            }

            if (tensorMap.TryGetValue("output_norm.weight", out var tFinalNorm))
                model.FinalNorm = ReadTensorFloats(stream, reader.TensorDataOffset, tFinalNorm);

            if (tensorMap.TryGetValue("output.weight", out var tLmHead))
            {
                LoadTensor(stream, reader.TensorDataOffset, tLmHead, out var f32, out var q8, out var q4);
                model.LmHead = f32;
                model.LmHead_Q8 = q8;
                model.LmHead_Q4 = q4;
            }
            else
            {
                model.LmHead = model.TokenEmbeddings; // Tied weights fallback
                model.LmHead_Q8 = model.TokenEmbeddings_Q8;
                model.LmHead_Q4 = model.TokenEmbeddings_Q4;
            }

            bool hasQ4 = model.TokenEmbeddings_Q4 != null || (model.Layers.Length > 0 && model.Layers[0].Wq_Q4 != null);
            bool hasQ8 = model.TokenEmbeddings_Q8 != null || (model.Layers.Length > 0 && model.Layers[0].Wq_Q8 != null);
            if (hasQ4) model.QuantizationType = GgufTensorType.Q4_0;
            else if (hasQ8) model.QuantizationType = GgufTensorType.Q8_0;
            else model.QuantizationType = GgufTensorType.F32;

            return model;
        }

        public static LlmModel LoadGguf(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentNullException(nameof(filePath));
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return LoadGguf(fs);
            }
        }

        private static byte[] FloatArrayToBytes(float[] array)
        {
            var bytes = new byte[array.Length * sizeof(float)];
            Buffer.BlockCopy(array, 0, bytes, 0, bytes.Length);
            return bytes;
        }

        private static float[] BytesToFloatArray(byte[] bytes)
        {
            var floats = new float[bytes.Length / sizeof(float)];
            Buffer.BlockCopy(bytes, 0, floats, 0, bytes.Length);
            return floats;
        }

        private static void WriteTensor(
            GgufWriter writer,
            string name,
            ulong[] dims,
            float[]? f32,
            BlockQ8_0[]? q8,
            BlockQ4_0[]? q4)
        {
            if (q8 != null && q8.Length > 0)
            {
                writer.AddTensor(name, dims, GgufTensorType.Q8_0, LlmQuantizer.Q8BlocksToBytes(q8));
            }
            else if (q4 != null && q4.Length > 0)
            {
                writer.AddTensor(name, dims, GgufTensorType.Q4_0, LlmQuantizer.Q4BlocksToBytes(q4));
            }
            else if (f32 != null && f32.Length > 0)
            {
                writer.AddTensor(name, dims, GgufTensorType.F32, FloatArrayToBytes(f32));
            }
        }

        private static void LoadTensor(
            Stream stream,
            long tensorDataOffset,
            GgufTensorInfo tensor,
            out float[] f32,
            out BlockQ8_0[]? q8,
            out BlockQ4_0[]? q4)
        {
            f32 = Array.Empty<float>();
            q8 = null;
            q4 = null;

            byte[] bytes = ReadTensorBytes(stream, tensorDataOffset, tensor);
            switch (tensor.Type)
            {
                case GgufTensorType.Q8_0:
                    q8 = LlmQuantizer.BytesToQ8Blocks(bytes);
                    break;
                case GgufTensorType.Q4_0:
                    q4 = LlmQuantizer.BytesToQ4Blocks(bytes);
                    break;
                case GgufTensorType.F32:
                default:
                    f32 = BytesToFloatArray(bytes);
                    break;
            }
        }

        private static byte[] ReadTensorBytes(Stream stream, long tensorDataOffset, GgufTensorInfo tensor)
        {
            stream.Seek(tensorDataOffset + (long)tensor.Offset, SeekOrigin.Begin);
            int byteLen = (int)tensor.GetSizeInBytes();
            byte[] bytes = new byte[byteLen];
            int read = 0;
            while (read < byteLen)
            {
                int r = stream.Read(bytes, read, byteLen - read);
                if (r <= 0) break;
                read += r;
            }
            return bytes;
        }

        private static float[] ReadTensorFloats(Stream stream, long tensorDataOffset, GgufTensorInfo tensor)
        {
            byte[] bytes = ReadTensorBytes(stream, tensorDataOffset, tensor);
            return BytesToFloatArray(bytes);
        }

        private static float[] CreateRandomArray(int size, Random rand, float scale)
        {
            var arr = new float[size];
            for (int i = 0; i < size; i++)
            {
                arr[i] = (float)(rand.NextDouble() * 2.0 - 1.0) * scale;
            }
            return arr;
        }

        private static float[] CreateConstantArray(int size, float value)
        {
            var arr = new float[size];
            for (int i = 0; i < size; i++) arr[i] = value;
            return arr;
        }
    }
}
