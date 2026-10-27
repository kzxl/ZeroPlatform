namespace ZeroLlm.Core.Engine
{
    /// <summary>
    /// Architecture hyperparameters for a Transformer model.
    /// </summary>
    public sealed class LlmModelConfig
    {
        public string Architecture { get; set; } = "llama";
        public int VocabSize { get; set; } = 32000;
        public int ContextLength { get; set; } = 2048;
        public int EmbeddingDim { get; set; } = 256;
        public int LayerCount { get; set; } = 4;
        public int HeadCount { get; set; } = 4;
        public int HeadCountKv { get; set; } = 2;
        public int HeadDim => EmbeddingDim / HeadCount;
        public int FeedForwardDim { get; set; } = 512;
        public float RmsNormEps { get; set; } = 1e-5f;
        public float RopeFreqBase { get; set; } = 10000.0f;
        public int BosTokenId { get; set; } = 1;
        public int EosTokenId { get; set; } = 2;

        /// <summary>
        /// Total number of Mixture-of-Experts (MoE) FFN experts per layer. 0 or 1 signifies a standard dense model.
        /// </summary>
        public int ExpertCount { get; set; } = 0;

        /// <summary>
        /// Number of activated experts routed per token (Top-K).
        /// </summary>
        public int ExpertUsedCount { get; set; } = 0;

        /// <summary>
        /// Number of always-active shared experts per layer (DeepSeek / Qwen-MoE architecture).
        /// Shared experts preserve foundational linguistic and domain reasoning across all tokens.
        /// </summary>
        public int SharedExpertCount { get; set; } = 0;

        /// <summary>
        /// Indicates whether this configuration defines a Sparse Mixture-of-Experts architecture.
        /// </summary>
        public bool IsMoE => ExpertCount > 1 || SharedExpertCount > 0;

        /// <summary>
        /// Creates a canonical configuration for a generic Micro-SLM (~11.5M parameters).
        /// Tailored for high-speed sub-10ms CPU inference and compact memory footprint (~5.8MB INT4, ~11.5MB INT8).
        /// </summary>
        public static LlmModelConfig CreateMicro(
            int vocabSize = 16000,
            int contextLength = 1024,
            int embeddingDim = 256,
            int layerCount = 6,
            int headCount = 8,
            int headCountKv = 2,
            int feedForwardDim = 512)
        {
            return new LlmModelConfig
            {
                Architecture = "llama",
                VocabSize = vocabSize,
                ContextLength = contextLength,
                EmbeddingDim = embeddingDim,
                LayerCount = layerCount,
                HeadCount = headCount,
                HeadCountKv = headCountKv,
                FeedForwardDim = feedForwardDim,
                RmsNormEps = 1e-5f,
                RopeFreqBase = 10000.0f,
                BosTokenId = 1,
                EosTokenId = 2
            };
        }

        /// <summary>
        /// Creates a configuration for a Sparse Mixture-of-Experts Micro-SLM (e.g. 4 experts, Top-2 active).
        /// </summary>
        public static LlmModelConfig CreateMicroMoE(
            int vocabSize = 16000,
            int contextLength = 1024,
            int embeddingDim = 256,
            int layerCount = 6,
            int headCount = 8,
            int headCountKv = 2,
            int feedForwardDim = 512,
            int expertCount = 4,
            int expertUsedCount = 2,
            int sharedExpertCount = 1)
        {
            return new LlmModelConfig
            {
                Architecture = "llama",
                VocabSize = vocabSize,
                ContextLength = contextLength,
                EmbeddingDim = embeddingDim,
                LayerCount = layerCount,
                HeadCount = headCount,
                HeadCountKv = headCountKv,
                FeedForwardDim = feedForwardDim,
                RmsNormEps = 1e-5f,
                RopeFreqBase = 10000.0f,
                BosTokenId = 1,
                EosTokenId = 2,
                ExpertCount = expertCount,
                ExpertUsedCount = expertUsedCount,
                SharedExpertCount = sharedExpertCount
            };
        }

        /// <summary>
        /// Convenience preset for the internal Vietnamese ERP Micro-SLM (~11.5M parameters).
        /// </summary>
        public static LlmModelConfig CreateVietnameseErpMicro(int vocabSize = 16000, int contextLength = 1024)
            => CreateMicro(vocabSize, contextLength);

        /// <summary>
        /// Creates a practical Small-Scale MoE SLM (~45M total parameters, ~25M active FLOPs per token).
        /// Balances multi-domain ERP knowledge retention with high-speed CPU execution.
        /// </summary>
        public static LlmModelConfig CreateSmallMoE(
            int vocabSize = 16000,
            int contextLength = 2048,
            int embeddingDim = 512,
            int layerCount = 8,
            int headCount = 8,
            int headCountKv = 4,
            int feedForwardDim = 1024,
            int expertCount = 4,
            int expertUsedCount = 2,
            int sharedExpertCount = 1)
        {
            return new LlmModelConfig
            {
                Architecture = "llama",
                VocabSize = vocabSize,
                ContextLength = contextLength,
                EmbeddingDim = embeddingDim,
                LayerCount = layerCount,
                HeadCount = headCount,
                HeadCountKv = headCountKv,
                FeedForwardDim = feedForwardDim,
                RmsNormEps = 1e-5f,
                RopeFreqBase = 10000.0f,
                BosTokenId = 1,
                EosTokenId = 2,
                ExpertCount = expertCount,
                ExpertUsedCount = expertUsedCount,
                SharedExpertCount = sharedExpertCount
            };
        }
    }
}
