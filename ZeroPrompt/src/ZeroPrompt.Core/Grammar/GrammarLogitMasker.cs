using System;
using System.Collections.Generic;
using ZeroTokenizer.Core.Abstractions;

namespace ZeroPrompt.Core.Grammar
{
    /// <summary>
    /// In-place logit transformation delegate for grammar-guided decoding.
    /// Compatible with ZeroLlm.Core.Sampling.LogitProcessor.
    /// </summary>
    public delegate void GrammarLogitProcessor(Span<float> logits);

    /// <summary>
    /// Logit masking engine that guarantees 100% syntactically valid JSON tool calls by setting invalid token logits to -Infinity.
    /// Interoperates directly with ZeroLlm via LogitProcessor delegates.
    /// </summary>
    public sealed class GrammarLogitMasker
    {
        private readonly ITokenizer _tokenizer;
        private readonly Dictionary<int, string> _tokenStringCache = new Dictionary<int, string>();

        public GrammarLogitMasker(ITokenizer tokenizer)
        {
            _tokenizer = tokenizer ?? throw new ArgumentNullException(nameof(tokenizer));
        }

        /// <summary>
        /// Masks candidate logits in-place. Sets invalid tokens to -1e9f.
        /// </summary>
        public void MaskLogits(
            JsonGrammarState grammar,
            Span<float> logits,
            ReadOnlySpan<int> candidateTokens)
        {
            if (grammar == null) throw new ArgumentNullException(nameof(grammar));

            for (int i = 0; i < candidateTokens.Length; i++)
            {
                int tokenId = candidateTokens[i];
                if ((uint)tokenId >= (uint)logits.Length) continue;

                string piece = GetTokenString(tokenId);
                if (!grammar.CanAcceptNext(piece.AsSpan()))
                {
                    logits[tokenId] = -1e9f; // Mask out
                }
            }
        }

        /// <summary>
        /// Masks all logits in-place across the entire vocabulary. Sets invalid tokens to -1e9f.
        /// </summary>
        public void MaskAllLogits(JsonGrammarState grammar, Span<float> logits)
        {
            if (grammar == null) throw new ArgumentNullException(nameof(grammar));

            int len = logits.Length;
            for (int tokenId = 0; tokenId < len; tokenId++)
            {
                string piece = GetTokenString(tokenId);
                if (!grammar.CanAcceptNext(piece.AsSpan()))
                {
                    logits[tokenId] = -1e9f;
                }
            }
        }

        /// <summary>
        /// Creates a delegate compatible with ZeroLlm.Core.Sampling.LogitProcessor to mask invalid grammar tokens in-place.
        /// </summary>
        public GrammarLogitProcessor CreateLogitProcessor(JsonGrammarState grammar)
        {
            if (grammar == null) throw new ArgumentNullException(nameof(grammar));
            return logits => MaskAllLogits(grammar, logits);
        }

        /// <summary>
        /// Advances the grammar automaton by consuming all characters in the decoded token.
        /// </summary>
        public bool AdvanceToken(JsonGrammarState grammar, int tokenId)
        {
            if (grammar == null) return false;
            string piece = GetTokenString(tokenId);
            for (int i = 0; i < piece.Length; i++)
            {
                if (!grammar.TryConsume(piece[i]))
                    return false;
            }
            return true;
        }

        public string GetTokenString(int tokenId)
        {
            if (!_tokenStringCache.TryGetValue(tokenId, out var str))
            {
                str = _tokenizer.Decode(new[] { tokenId });
                _tokenStringCache[tokenId] = str;
            }
            return str;
        }
    }
}
