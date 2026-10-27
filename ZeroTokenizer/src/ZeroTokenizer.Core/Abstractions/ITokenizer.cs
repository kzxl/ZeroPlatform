using System;

namespace ZeroTokenizer.Core.Abstractions
{
    /// <summary>
    /// Common abstraction for LLM tokenizers (BPE, WordPiece, Tiktoken).
    /// </summary>
    public interface ITokenizer
    {
        /// <summary>
        /// Total number of unique tokens in the tokenizer's vocabulary.
        /// </summary>
        int VocabularySize { get; }

        /// <summary>
        /// Calculates the number of tokens in the given text without allocating integer arrays.
        /// </summary>
        int CountTokens(ReadOnlySpan<char> text);

        /// <summary>
        /// Encodes the input text into the destination token ID span.
        /// Returns the number of tokens written.
        /// </summary>
        int Encode(ReadOnlySpan<char> text, Span<int> destinationTokenIds);

        /// <summary>
        /// Convenience method to encode text into a newly allocated token array.
        /// </summary>
        int[] Encode(string text);

        /// <summary>
        /// Decodes a sequence of token IDs back into string format.
        /// </summary>
        string Decode(ReadOnlySpan<int> tokenIds);

        /// <summary>
        /// Decodes a sequence of token IDs into a destination character span.
        /// Returns the number of characters written.
        /// </summary>
        int Decode(ReadOnlySpan<int> tokenIds, Span<char> destinationText);
    }
}
