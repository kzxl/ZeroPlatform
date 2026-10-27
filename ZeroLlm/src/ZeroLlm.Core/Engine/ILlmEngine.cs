using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ZeroLlm.Core.Sampling;

namespace ZeroLlm.Core.Engine
{
    /// <summary>
    /// High-level Small Language Model (SLM) generation and completion engine.
    /// Supports streaming, custom sampling configurations, and logit processors for grammar-constrained decoding.
    /// </summary>
    public interface ILlmEngine : IDisposable
    {
        /// <summary>
        /// Generates a complete text completion for the provided prompt using default sampling.
        /// </summary>
        Task<string> CompleteAsync(string prompt, CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates a complete text completion with customized sampling parameters and logit processors.
        /// </summary>
        Task<string> CompleteAsync(
            string prompt,
            SamplingConfig? samplingConfig,
            LogitProcessor? logitProcessor = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Streams generated text tokens asynchronously token-by-token using default sampling.
        /// </summary>
        IAsyncEnumerable<string> GenerateStreamAsync(string prompt, CancellationToken cancellationToken = default);

        /// <summary>
        /// Streams generated text tokens asynchronously token-by-token with customized sampling parameters and logit processors.
        /// </summary>
        IAsyncEnumerable<string> GenerateStreamAsync(
            string prompt,
            SamplingConfig? samplingConfig,
            LogitProcessor? logitProcessor = null,
            CancellationToken cancellationToken = default);
    }
}
