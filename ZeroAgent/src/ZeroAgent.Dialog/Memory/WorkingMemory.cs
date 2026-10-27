using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ZeroAgent.Dialog.Memory
{
    public sealed class DialogTurn
    {
        public string UserMessage { get; }
        public string BotResponse { get; }
        public string Intent { get; }
        public DateTime TimestampUtc { get; }

        public DialogTurn(string userMessage, string botResponse, string intent)
        {
            UserMessage = userMessage;
            BotResponse = botResponse;
            Intent = intent;
            TimestampUtc = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Working Memory (Short-Term Conversational Memory).
    /// Tracks active dialogue session state, slot accumulation, and resolves anaphora/pronoun references across turns.
    /// </summary>
    public sealed partial class WorkingMemory
    {
        private readonly List<DialogTurn> _turns = new List<DialogTurn>();
        private readonly Dictionary<string, string> _activeSlots = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public string SessionId { get; }
        public IReadOnlyList<DialogTurn> Turns => _turns;
        public IReadOnlyDictionary<string, string> ActiveSlots => _activeSlots;

        public string? CurrentSubject { get; set; }
        public string? CurrentMetric { get; set; }
        public string? CurrentArea { get; set; }

        public WorkingMemory(string sessionId)
        {
            SessionId = sessionId ?? Guid.NewGuid().ToString("N");
        }

        public void AddTurn(string userMessage, string botResponse, string intent)
        {
            _turns.Add(new DialogTurn(userMessage, botResponse, intent));
            if (_turns.Count > _maxRetainedTurns)
            {
                CompactOldestTurns(_turns.Count - _maxRetainedTurns);
            }
        }

        /// <summary>
        /// Applies Knapsack token budgeting to compact history to fit within a model's context window.
        /// Preserves the most recent turns and essential state, compressing evicted older turns into SummaryContext.
        /// </summary>
        public int PruneToTokenBudget(int maxTokens, Func<string, int>? tokenEstimator = null)
        {
            if (maxTokens <= 0 || _turns.Count <= 1) return 0;

            tokenEstimator ??= _compactor.EstimateTokens;

            int totalTokens = 0;
            for (int i = 0; i < _turns.Count; i++)
            {
                totalTokens += tokenEstimator(_turns[i].UserMessage) + tokenEstimator(_turns[i].BotResponse);
            }

            var prunedBatch = new List<DialogTurn>();
            while (totalTokens > maxTokens && _turns.Count > 1)
            {
                var removed = _turns[0];
                _turns.RemoveAt(0);
                prunedBatch.Add(removed);
                totalTokens -= (tokenEstimator(removed.UserMessage) + tokenEstimator(removed.BotResponse));
            }

            if (prunedBatch.Count > 0)
            {
                SummaryContext = _compactor.CompactTurns(prunedBatch, _activeSlots, SummaryContext);
            }

            return prunedBatch.Count;
        }

        private static int DefaultTokenEstimator(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            // Fast heuristic for Vietnamese and English: ~3.5 chars per token
            return Math.Max(1, (text.Length + 2) / 3);
        }

        public void SetSlot(string key, string value)
        {
            if (string.IsNullOrEmpty(key)) return;
            _activeSlots[key] = value;

            if (key.Equals("machine_id", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("target", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("tableName", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("product", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("product_code", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("entity", StringComparison.OrdinalIgnoreCase))
            {
                CurrentSubject = value;
            }
            else if (key.Equals("metric", StringComparison.OrdinalIgnoreCase))
            {
                CurrentMetric = value;
            }
            else if (key.Equals("area", StringComparison.OrdinalIgnoreCase))
            {
                CurrentArea = value;
            }
        }

        public bool TryGetSlot(string key, out string value)
        {
            return _activeSlots.TryGetValue(key, out value!);
        }

        public void ClearSlots()
        {
            _activeSlots.Clear();
            CurrentSubject = null;
            CurrentMetric = null;
            CurrentArea = null;
        }

        public void Clear()
        {
            _turns.Clear();
            ClearSlots();
            SummaryContext = null;
        }

        /// <summary>
        /// Resolves anaphora, pronouns, and implicit subject references based on active working memory.
        /// Replaces 'nó', 'máy đó', 'con đó', 'it', 'that machine', 'bảng đó', 'sản phẩm đó' with the active subject name.
        /// </summary>
        public string ResolveAnaphora(string rawInput)
        {
            if (string.IsNullOrWhiteSpace(rawInput)) return string.Empty;
            if (string.IsNullOrEmpty(CurrentSubject)) return rawInput;

            string text = rawInput;

            // Vietnamese pronouns and anaphoric references
            text = Regex.Replace(text, @"\b(nó|máy đó|con đó|máy này|thiết bị đó|thiết bị này|bảng đó|bảng này|sản phẩm đó|món đó|cái đó)\b", CurrentSubject, RegexOptions.IgnoreCase);

            // English pronouns
            text = Regex.Replace(text, @"\b(it|that machine|this machine|that device|that table|that product)\b", CurrentSubject, RegexOptions.IgnoreCase);

            // Elliptical follow-up questions: "còn áp suất thì sao" or "còn áp suất của nó thì sao" -> "kiểm tra áp suất của CNC-01"
            var ellipticalMatch = Regex.Match(text, @"^còn\s+([a-zA-Z0-9_\u00C0-\u024F\u1E00-\u1EFF\s]+?)(?:\s+của\s+[a-zA-Z0-9_-]+)?\s+(thì sao|thế nào)\??$", RegexOptions.IgnoreCase);
            if (ellipticalMatch.Success)
            {
                string metric = ellipticalMatch.Groups[1].Value.Trim();
                text = $"kiểm tra {metric} của {CurrentSubject}";
            }

            return text;
        }
    }
}
