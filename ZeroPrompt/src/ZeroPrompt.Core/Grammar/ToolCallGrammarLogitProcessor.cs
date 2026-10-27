using System;
using System.Collections.Generic;
using ZeroTokenizer.Core.Abstractions;

namespace ZeroPrompt.Core.Grammar
{
    /// <summary>
    /// High-performance context-aware logit processor that strictly forces token generation
    /// to adhere to schema-constrained tool calling grammar.
    /// Compatible with ZeroLlm.Core.Sampling.ContextAwareLogitProcessor.
    /// </summary>
    public sealed class ToolCallGrammarLogitProcessor
    {
        private readonly ITokenizer _tokenizer;
        private readonly IReadOnlyDictionary<string, JsonSchemaConstraint> _toolSchemas;
        private readonly Dictionary<int, string> _tokenStringCache = new Dictionary<int, string>();

        private ToolCallGrammarState? _activeGrammar;
        private int _lastConsumedTokenCount = 0;
        private bool _isInsideToolCall = false;

        public ToolCallGrammarState? ActiveGrammar => _activeGrammar;
        public bool IsInsideToolCall => _isInsideToolCall;

        public ToolCallGrammarLogitProcessor(
            ITokenizer tokenizer,
            IReadOnlyDictionary<string, JsonSchemaConstraint> toolSchemas)
        {
            _tokenizer = tokenizer ?? throw new ArgumentNullException(nameof(tokenizer));
            _toolSchemas = toolSchemas ?? throw new ArgumentNullException(nameof(toolSchemas));
        }

        private string _accumulatedText = string.Empty;

        public void Reset()
        {
            _activeGrammar = null;
            _lastConsumedTokenCount = 0;
            _isInsideToolCall = false;
            _accumulatedText = string.Empty;
        }

        public void Process(Span<float> logits, ReadOnlySpan<int> pastTokens)
        {
            if (logits.Length == 0) return;

            // 1. Advance state by consuming newly generated tokens since last step
            if (pastTokens.Length > _lastConsumedTokenCount)
            {
                for (int i = _lastConsumedTokenCount; i < pastTokens.Length; i++)
                {
                    int tok = pastTokens[i];
                    string piece = GetTokenString(tok);

                    if (!_isInsideToolCall)
                    {
                        _accumulatedText += piece;
                        int tagIdx = _accumulatedText.LastIndexOf("<tool_call>", StringComparison.Ordinal);
                        if (tagIdx >= 0)
                        {
                            _isInsideToolCall = true;
                            _activeGrammar = new ToolCallGrammarState(_toolSchemas, requireStartTag: false);

                            string afterTag = _accumulatedText.Substring(tagIdx + "<tool_call>".Length);
                            for (int c = 0; c < afterTag.Length; c++)
                            {
                                _activeGrammar.TryConsume(afterTag[c]);
                            }
                        }
                    }
                    else if (_activeGrammar != null)
                    {
                        for (int c = 0; c < piece.Length; c++)
                        {
                            _activeGrammar.TryConsume(piece[c]);
                        }

                        if (_activeGrammar.IsCompleted)
                        {
                            _isInsideToolCall = false;
                            _activeGrammar = null;
                        }
                    }
                }
                _lastConsumedTokenCount = pastTokens.Length;
            }

            // 2. If inside tool call, apply logit masking to enforce valid transitions
            if (_isInsideToolCall && _activeGrammar != null && !_activeGrammar.IsCompleted)
            {
                ApplyGrammarMask(logits, _activeGrammar);
            }
        }

        private void ApplyGrammarMask(Span<float> logits, ToolCallGrammarState grammar)
        {
            // Find max logit for candidate thresholding
            float maxLogit = float.NegativeInfinity;
            for (int i = 0; i < logits.Length; i++)
            {
                if (logits[i] > maxLogit) maxLogit = logits[i];
            }

            // Candidate threshold: evaluate tokens within 20 logit delta of best candidate
            float threshold = maxLogit - 20.0f;
            int validCount = 0;

            for (int tok = 0; tok < logits.Length; tok++)
            {
                if (logits[tok] < threshold)
                {
                    logits[tok] = -1e9f;
                    continue;
                }

                string piece = GetTokenString(tok);
                if (string.IsNullOrEmpty(piece) || !grammar.CanAcceptNext(piece.AsSpan()))
                {
                    logits[tok] = -1e9f; // Mask out invalid token
                }
                else
                {
                    validCount++;
                }
            }

            // Fallback: If no top candidates were valid, scan remaining tokens to ensure at least 1 valid token
            if (validCount == 0)
            {
                for (int tok = 0; tok < logits.Length; tok++)
                {
                    string piece = GetTokenString(tok);
                    if (!string.IsNullOrEmpty(piece) && grammar.CanAcceptNext(piece.AsSpan()))
                    {
                        logits[tok] = 0.0f; // Enable valid token
                        validCount++;
                        if (validCount >= 8) break; // Found sufficient fallback tokens
                    }
                }
            }
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
