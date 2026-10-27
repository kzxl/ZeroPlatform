using System;
using System.Collections.Generic;

namespace ZeroAgent.Core.Reasoning.Reflexion
{
    /// <summary>
    /// Represents an episodic self-reflection distilled from an execution failure or sub-optimal step.
    /// </summary>
    public sealed class ReflexionEpisode
    {
        public string Goal { get; }
        public string FailureReason { get; }
        public string SelfReflection { get; }
        public DateTime TimestampUtc { get; }

        public ReflexionEpisode(string goal, string failureReason, string selfReflection)
        {
            Goal = goal ?? string.Empty;
            FailureReason = failureReason ?? string.Empty;
            SelfReflection = selfReflection ?? string.Empty;
            TimestampUtc = DateTime.UtcNow;
        }

        public string ToDirectivePrompt()
        {
            return $"[PAST FAILURE REFLECTION] For similar goal: \"{Goal}\" - Previous pitfall: {FailureReason}. Correction lesson: {SelfReflection}";
        }
    }

    /// <summary>
    /// Long-term memory store for self-reflective learning and experience playback.
    /// </summary>
    public interface IReflexionMemory
    {
        void Remember(ReflexionEpisode episode);
        IReadOnlyList<ReflexionEpisode> Recall(string goal, int topK = 2);
    }

    /// <summary>
    /// In-memory thread-safe implementation of <see cref="IReflexionMemory"/>.
    /// </summary>
    public sealed class InMemoryReflexionMemory : IReflexionMemory
    {
        private readonly List<ReflexionEpisode> _episodes = new List<ReflexionEpisode>();
        private readonly object _lock = new object();

        public int Count
        {
            get { lock (_lock) return _episodes.Count; }
        }

        public void Remember(ReflexionEpisode episode)
        {
            if (episode == null) throw new ArgumentNullException(nameof(episode));
            lock (_lock)
            {
                _episodes.Add(episode);
            }
        }

        public IReadOnlyList<ReflexionEpisode> Recall(string goal, int topK = 2)
        {
            if (string.IsNullOrWhiteSpace(goal) || topK <= 0) return Array.Empty<ReflexionEpisode>();

            lock (_lock)
            {
                var matches = new List<(ReflexionEpisode Ep, int Score)>();
                var words = goal.Split(new[] { ' ', ',', '-', '_', '.' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var ep in _episodes)
                {
                    int matchScore = 0;
                    foreach (var w in words)
                    {
                        if (ep.Goal.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            matchScore++;
                        }
                    }

                    if (matchScore > 0)
                    {
                        matches.Add((ep, matchScore));
                    }
                }

                matches.Sort((a, b) => b.Score.CompareTo(a.Score));
                int take = Math.Min(topK, matches.Count);
                var result = new List<ReflexionEpisode>(take);
                for (int i = 0; i < take; i++)
                {
                    result.Add(matches[i].Ep);
                }

                return result;
            }
        }
    }
}
