using System;
using System.Collections.Generic;
using ZeroTokenizer.Core.Abstractions;

namespace ZeroLlm.Core.Sampling
{
    public enum ThinkingState
    {
        AwaitingThought,
        InsideThought,
        ThoughtClosed,
        InResponse,
        Completed
    }

    /// <summary>
    /// Enforces rigorous Chain-of-Thought (CoT) reasoning grammar and token-level transitions.
    /// Mathematically guarantees that:
    /// 1. The model always enters thinking phase (<thought>) before responding.
    /// 2. Thinking adheres to minimum and maximum token budgets.
    /// 3. Transitions cleanly from </thought> to either <tool_call> or <response>.
    /// 4. Cannot prematurely emit EOS or response tokens while inside thought.
    /// </summary>
    public sealed class ThinkingConstraintLogitProcessor
    {
        private readonly int _thoughtTokenId;
        private readonly int _endThoughtTokenId;
        private readonly int _responseTokenId;
        private readonly int _endResponseTokenId;
        private readonly int _toolCallTokenId;
        private readonly int _endToolCallTokenId;
        private readonly int _eosTokenId;

        private readonly int _minThinkingTokens;
        private readonly int _maxThinkingTokens;

        private int _promptTokensCount = -1;
        private int _thoughtStartPos = -1;
        private ThinkingState _state = ThinkingState.AwaitingThought;

        public ThinkingState State => _state;
        public int ThoughtTokensCount => _thoughtStartPos >= 0 ? Math.Max(0, (_tokensSeen - _thoughtStartPos)) : 0;
        private int _tokensSeen = 0;

        public ThinkingConstraintLogitProcessor(
            int thoughtTokenId,
            int endThoughtTokenId,
            int responseTokenId,
            int endResponseTokenId,
            int toolCallTokenId = -1,
            int endToolCallTokenId = -1,
            int minThinkingTokens = 5,
            int maxThinkingTokens = 250,
            int eosTokenId = 2)
        {
            _thoughtTokenId = thoughtTokenId;
            _endThoughtTokenId = endThoughtTokenId;
            _responseTokenId = responseTokenId;
            _endResponseTokenId = endResponseTokenId;
            _toolCallTokenId = toolCallTokenId;
            _endToolCallTokenId = endToolCallTokenId;
            _minThinkingTokens = minThinkingTokens;
            _maxThinkingTokens = maxThinkingTokens;
            _eosTokenId = eosTokenId;
        }

        public ThinkingConstraintLogitProcessor(
            ITokenizer tokenizer,
            int minThinkingTokens = 5,
            int maxThinkingTokens = 250,
            int eosTokenId = 2)
            : this(
                ResolveTokenId(tokenizer, "<thought>"),
                ResolveTokenId(tokenizer, "</thought>"),
                ResolveTokenId(tokenizer, "<response>"),
                ResolveTokenId(tokenizer, "</response>"),
                ResolveTokenId(tokenizer, "<tool_call>"),
                ResolveTokenId(tokenizer, "</tool_call>"),
                minThinkingTokens,
                maxThinkingTokens,
                eosTokenId)
        {
        }

        private static int ResolveTokenId(ITokenizer tokenizer, string tokenStr)
        {
            var tokens = tokenizer.Encode(tokenStr);
            return (tokens.Length > 0) ? tokens[0] : -1;
        }

        public void Reset(int promptTokensCount = -1)
        {
            _promptTokensCount = promptTokensCount;
            _thoughtStartPos = -1;
            _tokensSeen = promptTokensCount >= 0 ? promptTokensCount : 0;
            _state = ThinkingState.AwaitingThought;
        }

        public void Process(Span<float> logits, ReadOnlySpan<int> pastTokens)
        {
            if (logits.Length == 0) return;

            if (_promptTokensCount < 0)
            {
                _promptTokensCount = pastTokens.Length;
                _tokensSeen = pastTokens.Length;
                if (pastTokens.Length > 0 && pastTokens[^1] == _thoughtTokenId)
                {
                    _state = ThinkingState.InsideThought;
                    _thoughtStartPos = pastTokens.Length - 1;
                }
            }

            int generatedCount = pastTokens.Length - _promptTokensCount;
            int lastToken = (pastTokens.Length > 0) ? pastTokens[^1] : -1;

            // Track state transitions based on last token observed
            UpdateState(lastToken, generatedCount);

            switch (_state)
            {
                case ThinkingState.AwaitingThought:
                    // Force the very first generated token to be <thought>
                    if (_thoughtTokenId >= 0 && _thoughtTokenId < logits.Length)
                    {
                        ForceToken(logits, _thoughtTokenId);
                    }
                    break;

                case ThinkingState.InsideThought:
                    int currentThoughtTokens = pastTokens.Length - (_thoughtStartPos >= 0 ? _thoughtStartPos : _promptTokensCount);

                    // Ban early closure if under min thinking budget
                    if (currentThoughtTokens < _minThinkingTokens)
                    {
                        BanToken(logits, _endThoughtTokenId);
                    }
                    else if (currentThoughtTokens >= _maxThinkingTokens)
                    {
                        // Force closing thought if budget exceeded
                        if (_endThoughtTokenId >= 0 && _endThoughtTokenId < logits.Length)
                        {
                            ForceToken(logits, _endThoughtTokenId);
                            break;
                        }
                    }

                    // Strict bans while inside thought: cannot respond, call tool, or exit
                    BanToken(logits, _responseTokenId);
                    BanToken(logits, _endResponseTokenId);
                    BanToken(logits, _toolCallTokenId);
                    BanToken(logits, _endToolCallTokenId);
                    BanToken(logits, _thoughtTokenId); // no duplicate <thought>
                    BanToken(logits, _eosTokenId);      // no early exit
                    break;

                case ThinkingState.ThoughtClosed:
                    // Immediate step after </thought>: MUST emit either <response> or <tool_call>
                    MaskAllExcept(logits, new[] { _responseTokenId, _toolCallTokenId });
                    break;

                case ThinkingState.InResponse:
                    // Inside response: cannot reopen thought or call tools
                    BanToken(logits, _thoughtTokenId);
                    BanToken(logits, _endThoughtTokenId);
                    BanToken(logits, _toolCallTokenId);
                    BanToken(logits, _endToolCallTokenId);
                    break;

                case ThinkingState.Completed:
                    // Force EOS once response closed
                    ForceToken(logits, _eosTokenId);
                    break;
            }
        }

        private void UpdateState(int lastToken, int generatedCount)
        {
            if (generatedCount == 0)
            {
                _state = ThinkingState.AwaitingThought;
                return;
            }

            if (lastToken == _thoughtTokenId)
            {
                _state = ThinkingState.InsideThought;
                if (_thoughtStartPos < 0) _thoughtStartPos = _promptTokensCount + generatedCount - 1;
            }
            else if (lastToken == _endThoughtTokenId)
            {
                _state = ThinkingState.ThoughtClosed;
            }
            else if (lastToken == _responseTokenId || lastToken == _toolCallTokenId)
            {
                _state = ThinkingState.InResponse;
            }
            else if (lastToken == _endResponseTokenId || lastToken == _endToolCallTokenId)
            {
                _state = ThinkingState.Completed;
            }
        }

        private static void ForceToken(Span<float> logits, int tokenId)
        {
            if (tokenId < 0 || tokenId >= logits.Length) return;
            logits.Fill(-1e9f);
            logits[tokenId] = 100.0f;
        }

        private static void BanToken(Span<float> logits, int tokenId)
        {
            if (tokenId >= 0 && tokenId < logits.Length)
            {
                logits[tokenId] = -1e9f;
            }
        }

        private static void MaskAllExcept(Span<float> logits, int[] allowedTokenIds)
        {
            Span<float> saved = stackalloc float[allowedTokenIds.Length];
            for (int i = 0; i < allowedTokenIds.Length; i++)
            {
                int id = allowedTokenIds[i];
                saved[i] = (id >= 0 && id < logits.Length) ? logits[id] : -1e9f;
            }

            logits.Fill(-1e9f);

            for (int i = 0; i < allowedTokenIds.Length; i++)
            {
                int id = allowedTokenIds[i];
                if (id >= 0 && id < logits.Length)
                {
                    logits[id] = saved[i];
                }
            }
        }
    }
}
