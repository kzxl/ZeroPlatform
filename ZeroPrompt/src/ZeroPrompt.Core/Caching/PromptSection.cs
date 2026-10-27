using System;

namespace ZeroPrompt.Core.Caching
{
    /// <summary>
    /// Represents the volatility and role of a prompt segment.
    /// Ordered by caching priority (lowest volatility at the top).
    /// </summary>
    public enum PromptSectionKind
    {
        /// <summary>
        /// Highly static system instructions, persona, and safety guidelines. (Longest cache lifetime)
        /// </summary>
        StaticSystem = 0,

        /// <summary>
        /// Declarations of invokable tools and parameter schemas.
        /// </summary>
        ToolDefinitions = 1,

        /// <summary>
        /// Few-shot exemplar input/output pairs.
        /// </summary>
        FewShot = 2,

        /// <summary>
        /// Injected reference documents, SOPs, or static database schema context.
        /// </summary>
        ContextRAG = 3,

        /// <summary>
        /// Multi-turn conversation history (Append-only).
        /// </summary>
        History = 4,

        /// <summary>
        /// Current turn user utterance, real-time timestamps, or dynamic query. (Never cached across sessions)
        /// </summary>
        DynamicSuffix = 5
    }

    /// <summary>
    /// Represents a discrete section of an AI prompt with its associated volatility classification.
    /// </summary>
    public sealed class PromptSection
    {
        public string Content { get; }
        public PromptSectionKind Kind { get; }
        public string? Identifier { get; }

        public PromptSection(string content, PromptSectionKind kind, string? identifier = null)
        {
            Content = content ?? string.Empty;
            Kind = kind;
            Identifier = identifier;
        }

        public override string ToString() => $"[{Kind}] {Identifier ?? "Unnamed"}: {Content.Length} chars";
    }
}
