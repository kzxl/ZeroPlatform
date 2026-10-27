using System;

namespace ZeroLlm.Core.Generation
{
    /// <summary>
    /// Zero-weight N-Gram Prompt Lookup Speculative Drafter (Prompt Lookup Decoding / PLD).
    /// Proposes speculative continuation tokens by matching trailing N-gram suffixes of the current
    /// context against earlier occurrences in the prompt/context buffer without requiring a draft neural network.
    /// </summary>
    public sealed class PromptLookupDrafter
    {
        public int MaxNgramSize { get; }
        public int MinNgramSize { get; }
        public int DefaultDraftLength { get; }

        public PromptLookupDrafter(int maxNgramSize = 4, int minNgramSize = 2, int defaultDraftLength = 4)
        {
            if (minNgramSize < 1)
                throw new ArgumentOutOfRangeException(nameof(minNgramSize), "Minimum N-gram size must be at least 1.");
            if (maxNgramSize < minNgramSize)
                throw new ArgumentOutOfRangeException(nameof(maxNgramSize), "Maximum N-gram size must be >= minimum N-gram size.");
            if (defaultDraftLength < 1)
                throw new ArgumentOutOfRangeException(nameof(defaultDraftLength), "Default draft length must be at least 1.");

            MaxNgramSize = maxNgramSize;
            MinNgramSize = minNgramSize;
            DefaultDraftLength = defaultDraftLength;
        }

        /// <summary>
        /// Searches <paramref name="contextTokens"/> for the longest matching suffix N-gram (from <see cref="MaxNgramSize"/>
        /// down to <see cref="MinNgramSize"/>) and writes speculative continuation tokens into <paramref name="draftDestination"/>.
        /// </summary>
        /// <returns>The number of continuation tokens written to <paramref name="draftDestination"/>.</returns>
        public int Draft(ReadOnlySpan<int> contextTokens, Span<int> draftDestination)
        {
            if (draftDestination.IsEmpty || contextTokens.Length <= MinNgramSize)
            {
                return 0;
            }

            int len = contextTokens.Length;
            int maxN = Math.Min(MaxNgramSize, len - 1);

            for (int n = maxN; n >= MinNgramSize; n--)
            {
                ReadOnlySpan<int> suffix = contextTokens.Slice(len - n, n);
                int firstToken = suffix[0];

                // Scan backwards to prefer the most recent matching N-gram in context
                for (int pos = len - n - 1; pos >= 0; pos--)
                {
                    if (contextTokens[pos] != firstToken)
                    {
                        continue;
                    }

                    if (contextTokens.Slice(pos, n).SequenceEqual(suffix))
                    {
                        int continuationStart = pos + n;
                        int available = (len - n) - continuationStart;
                        if (available <= 0)
                        {
                            // Allow non-overlapping continuation up to the start of the suffix, or up to len if still positive
                            available = len - continuationStart;
                        }

                        int count = Math.Min(draftDestination.Length, len - continuationStart);
                        if (count > 0)
                        {
                            contextTokens.Slice(continuationStart, count).CopyTo(draftDestination);
                            return count;
                        }
                    }
                }
            }

            return 0;
        }

        /// <summary>
        /// Alias for <see cref="Draft(ReadOnlySpan{int}, Span{int})"/>.
        /// </summary>
        public int ProposeDraftTokens(ReadOnlySpan<int> contextTokens, Span<int> draftDestination)
            => Draft(contextTokens, draftDestination);

        /// <summary>
        /// Proposes up to <paramref name="maxDraftTokens"/> continuation tokens from matching N-grams in <paramref name="contextTokens"/>.
        /// </summary>
        public int[] Draft(ReadOnlySpan<int> contextTokens, int maxDraftTokens = 0)
        {
            int cap = maxDraftTokens > 0 ? maxDraftTokens : DefaultDraftLength;
            Span<int> scratch = cap <= 64 ? stackalloc int[cap] : new int[cap];
            int count = Draft(contextTokens, scratch);
            if (count == 0)
            {
                return Array.Empty<int>();
            }

            return scratch.Slice(0, count).ToArray();
        }

        /// <summary>
        /// Alias for <see cref="Draft(ReadOnlySpan{int}, int)"/>.
        /// </summary>
        public int[] ProposeDraftTokens(ReadOnlySpan<int> contextTokens, int maxDraftTokens = 0)
            => Draft(contextTokens, maxDraftTokens);
    }
}
