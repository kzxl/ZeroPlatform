using System;

namespace ZeroAgent.Core.Embedding
{
    /// <summary>
    /// Represents a text embedding contract for generating dense continuous vector representations.
    /// Used by semantic routers, episodic memory, and cognitive indexing.
    /// </summary>
    public interface ITextEmbedder
    {
        /// <summary>
        /// Gets the dimensionality of the generated vector embeddings.
        /// </summary>
        int Dimension { get; }

        /// <summary>
        /// Generates a dense, L2-normalized vector embedding from the input text.
        /// </summary>
        float[] Embed(string text);

        /// <summary>
        /// Generates a dense, L2-normalized vector embedding directly into the destination span without heap allocations.
        /// </summary>
        void Embed(string text, Span<float> destination);
    }
}
