using System;
using System.Collections.Generic;
using System.Text;
using ZeroTokenizer.Core.Abstractions;

namespace ZeroTokenizer.Core.Budget
{
    /// <summary>
    /// Item type in the context prompt.
    /// </summary>
    public enum PromptItemType
    {
        SystemInstruction = 0,
        UserMessage = 1,
        AssistantMessage = 2,
        KnowledgeFragment = 3,
        ToolCallResult = 4
    }

    /// <summary>
    /// An individual content item candidate for context packing.
    /// </summary>
    public sealed class PromptCandidate
    {
        public string Content { get; }
        public PromptItemType Type { get; }
        public int Priority { get; }
        public int EstimatedTokens { get; }

        public PromptCandidate(string content, PromptItemType type, int priority, int estimatedTokens)
        {
            Content = content ?? string.Empty;
            Type = type;
            Priority = priority;
            EstimatedTokens = estimatedTokens;
        }
    }

    /// <summary>
    /// Greedy Knapsack context window token budgeter.
    /// Ensures that prompts assembled for LLMs and AI Agents never exceed model context limits,
    /// while prioritizing system instructions, recent conversation turns, and high-relevance RAG facts.
    /// </summary>
    public sealed class TokenBudgeter
    {
        private readonly ITokenizer _tokenizer;
        private readonly int _maxContextTokens;
        private readonly List<PromptCandidate> _candidates = new List<PromptCandidate>();

        public int MaxContextTokens => _maxContextTokens;
        public int CandidateCount => _candidates.Count;

        public TokenBudgeter(ITokenizer tokenizer, int maxContextTokens = 8192)
        {
            _tokenizer = tokenizer ?? throw new ArgumentNullException(nameof(tokenizer));
            _maxContextTokens = Math.Max(128, maxContextTokens);
        }

        public void AddSystemPrompt(string content)
        {
            int tokens = _tokenizer.CountTokens(content.AsSpan());
            _candidates.Add(new PromptCandidate(content, PromptItemType.SystemInstruction, priority: 1000, tokens));
        }

        public void AddUserMessage(string content, int priority = 500)
        {
            int tokens = _tokenizer.CountTokens(content.AsSpan());
            _candidates.Add(new PromptCandidate(content, PromptItemType.UserMessage, priority, tokens));
        }

        public void AddAssistantMessage(string content, int priority = 400)
        {
            int tokens = _tokenizer.CountTokens(content.AsSpan());
            _candidates.Add(new PromptCandidate(content, PromptItemType.AssistantMessage, priority, tokens));
        }

        public void AddKnowledgeItem(string content, int priority = 300)
        {
            int tokens = _tokenizer.CountTokens(content.AsSpan());
            _candidates.Add(new PromptCandidate(content, PromptItemType.KnowledgeFragment, priority, tokens));
        }

        public void AddToolResult(string toolName, string content, int priority = 450)
        {
            string formatted = $"[Tool: {toolName}]\n{content}";
            int tokens = _tokenizer.CountTokens(formatted.AsSpan());
            _candidates.Add(new PromptCandidate(formatted, PromptItemType.ToolCallResult, priority, tokens));
        }

        /// <summary>
        /// Selects the optimal set of candidates that fit into the token budget and renders the packed prompt.
        /// </summary>
        public string BuildPackedPrompt(string separator = "\n\n")
        {
            if (_candidates.Count == 0) return string.Empty;

            // Sort candidates by priority (descending)
            var sorted = new List<(PromptCandidate Candidate, int OriginalIndex)>(_candidates.Count);
            for (int i = 0; i < _candidates.Count; i++)
            {
                sorted.Add((_candidates[i], i));
            }

            sorted.Sort((a, b) => b.Candidate.Priority.CompareTo(a.Candidate.Priority));

            int separatorTokens = _tokenizer.CountTokens(separator.AsSpan());
            int remainingBudget = _maxContextTokens;
            var accepted = new List<(PromptCandidate Candidate, int OriginalIndex)>();

            foreach (var item in sorted)
            {
                int cost = item.Candidate.EstimatedTokens + separatorTokens;
                if (cost <= remainingBudget)
                {
                    accepted.Add(item);
                    remainingBudget -= cost;
                }
            }

            // Restore chronological order based on original insertion index
            accepted.Sort((a, b) => a.OriginalIndex.CompareTo(b.OriginalIndex));

            var sb = new StringBuilder();
            for (int i = 0; i < accepted.Count; i++)
            {
                if (i > 0) sb.Append(separator);
                sb.Append(accepted[i].Candidate.Content);
            }

            return sb.ToString();
        }

        public void Clear()
        {
            _candidates.Clear();
        }
    }
}
