using System;
using System.Collections.Generic;
using System.Text;

namespace ZeroAgent.Dialog.Memory
{
    public sealed partial class WorkingMemory
    {
        private IContextCompactor _compactor = DeterministicContextCompactor.Instance;
        private int _maxRetainedTurns = 50;

        /// <summary>
        /// Cumulative high-density &lt;CONTEXT_SUMMARY&gt; distilled from older pruned turns and historical state.
        /// Injected into deliberative ReAct execution loops to preserve 100% semantic grounding across long conversations.
        /// </summary>
        public string? SummaryContext { get; set; }

        /// <summary>
        /// Gets or sets the context compactor engine used for turn distillation and observation masking.
        /// </summary>
        public IContextCompactor Compactor
        {
            get => _compactor;
            set => _compactor = value ?? DeterministicContextCompactor.Instance;
        }

        /// <summary>
        /// Maximum number of recent turns held in memory before older turns are automatically compacted into SummaryContext.
        /// </summary>
        public int MaxRetainedTurns
        {
            get => _maxRetainedTurns;
            set => _maxRetainedTurns = Math.Max(2, value);
        }

        /// <summary>
        /// Explicitly triggers compaction on older turns beyond the retention window.
        /// </summary>
        public void CompactOldestTurns(int turnsToCompact)
        {
            if (turnsToCompact <= 0 || _turns.Count <= 1) return;
            int count = Math.Min(turnsToCompact, _turns.Count - 1);
            var batch = new List<DialogTurn>(count);
            for (int i = 0; i < count; i++)
            {
                batch.Add(_turns[i]);
            }
            _turns.RemoveRange(0, count);
            SummaryContext = _compactor.CompactTurns(batch, _activeSlots, SummaryContext);
        }

        /// <summary>
        /// Builds an enriched context history combining the persistent &lt;CONTEXT_SUMMARY&gt; and recent turns.
        /// </summary>
        public string GetEffectiveContextHistory(int maxRecentTurns = 4)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(SummaryContext))
            {
                sb.AppendLine(SummaryContext);
                sb.AppendLine();
            }
            int start = Math.Max(0, _turns.Count - maxRecentTurns);
            for (int i = start; i < _turns.Count; i++)
            {
                sb.AppendLine($"User: {_turns[i].UserMessage}");
                sb.AppendLine($"Assistant: {_turns[i].BotResponse}");
            }
            return sb.ToString().TrimEnd();
        }
    }
}
