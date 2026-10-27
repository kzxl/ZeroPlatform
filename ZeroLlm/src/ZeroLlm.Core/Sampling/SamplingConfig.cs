using System.Collections.Generic;

namespace ZeroLlm.Core.Sampling
{
    /// <summary>
    /// Configuration parameters for LLM text generation and token sampling.
    /// </summary>
    public sealed class SamplingConfig
    {
        public float Temperature { get; set; } = 0.7f;
        public float TopP { get; set; } = 0.9f;
        public float MinP { get; set; } = 0.0f;
        public int TopK { get; set; } = 40;
        public float RepetitionPenalty { get; set; } = 1.1f;
        public int MaxTokens { get; set; } = 256;
        public HashSet<int> StopTokens { get; set; } = new HashSet<int>();
        public LogitProcessor? LogitProcessor { get; set; }
        public ContextAwareLogitProcessor? ContextLogitProcessor { get; set; }

        public static SamplingConfig Greedy => new SamplingConfig
        {
            Temperature = 0.0f,
            TopP = 1.0f,
            TopK = 1,
            RepetitionPenalty = 1.0f
        };
    }
}
