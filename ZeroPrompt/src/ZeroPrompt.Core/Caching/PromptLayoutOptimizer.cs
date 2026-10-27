using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace ZeroPrompt.Core.Caching
{
    /// <summary>
    /// Represents the optimized layout of a prompt, partitioned into a Cacheable Static Prefix
    /// and a Dynamic Suffix.
    /// </summary>
    public sealed class OptimizedPromptLayout
    {
        public string StaticPrefix { get; }
        public string DynamicSuffix { get; }
        public string FullPrompt { get; }
        public ulong PrefixHash { get; }
        public int StaticSectionCount { get; }

        public OptimizedPromptLayout(string staticPrefix, string dynamicSuffix, ulong prefixHash, int staticSectionCount)
        {
            StaticPrefix = staticPrefix;
            DynamicSuffix = dynamicSuffix;
            FullPrompt = staticPrefix.Length > 0 && dynamicSuffix.Length > 0
                ? staticPrefix + "\n\n" + dynamicSuffix
                : staticPrefix + dynamicSuffix;
            PrefixHash = prefixHash;
            StaticSectionCount = staticSectionCount;
        }
    }

    /// <summary>
    /// Reorders, normalizes, and partitions prompt components to maximize Transformer KV-cache hit rates.
    /// Ensures static instructions and tool definitions form an immutable prefix (Left-to-Right property).
    /// </summary>
    public sealed class PromptLayoutOptimizer
    {
        // Simple fast 64-bit FNV-1a hash
        private const ulong FnvOffsetBasis = 14695981039346656037UL;
        private const ulong FnvPrime = 1099511628211UL;

        private readonly List<PromptSection> _sections = new List<PromptSection>();

        public int SectionCount => _sections.Count;

        public PromptLayoutOptimizer AddSection(PromptSection section)
        {
            if (section != null && !string.IsNullOrWhiteSpace(section.Content))
            {
                _sections.Add(section);
            }
            return this;
        }

        public PromptLayoutOptimizer AddSystem(string content, string? identifier = null)
            => AddSection(new PromptSection(content, PromptSectionKind.StaticSystem, identifier));

        public PromptLayoutOptimizer AddTools(string toolSchemas, string? identifier = null)
            => AddSection(new PromptSection(toolSchemas, PromptSectionKind.ToolDefinitions, identifier));

        public PromptLayoutOptimizer AddFewShot(string exemplars, string? identifier = null)
            => AddSection(new PromptSection(exemplars, PromptSectionKind.FewShot, identifier));

        public PromptLayoutOptimizer AddRAGContext(string context, string? identifier = null)
            => AddSection(new PromptSection(context, PromptSectionKind.ContextRAG, identifier));

        public PromptLayoutOptimizer AddHistory(string history, string? identifier = null)
            => AddSection(new PromptSection(history, PromptSectionKind.History, identifier));

        public PromptLayoutOptimizer AddUserQuery(string query, string? identifier = null)
            => AddSection(new PromptSection(query, PromptSectionKind.DynamicSuffix, identifier));

        /// <summary>
        /// Optimizes the prompt layout by ordering sections strictly from static to dynamic,
        /// segregating cacheable prefixes from dynamic suffixes.
        /// </summary>
        public OptimizedPromptLayout Optimize()
        {
            if (_sections.Count == 0)
            {
                return new OptimizedPromptLayout(string.Empty, string.Empty, 0UL, 0);
            }

            // Sort sections by Kind (0: StaticSystem, 1: ToolDefinitions, ..., 5: DynamicSuffix)
            var sorted = new List<PromptSection>(_sections);
            sorted.Sort((a, b) => a.Kind.CompareTo(b.Kind));

            var prefixSb = new StringBuilder();
            var suffixSb = new StringBuilder();
            int staticCount = 0;

            foreach (var section in sorted)
            {
                // Sections from StaticSystem up to ContextRAG form the immutable cacheable prefix
                if (section.Kind <= PromptSectionKind.ContextRAG)
                {
                    if (prefixSb.Length > 0) prefixSb.Append("\n\n");
                    prefixSb.Append(section.Content.Trim());
                    staticCount++;
                }
                else
                {
                    // History and Dynamic Suffix form the dynamic append-only / request-specific payload
                    if (suffixSb.Length > 0) suffixSb.Append("\n\n");
                    suffixSb.Append(section.Content.Trim());
                }
            }

            string prefix = prefixSb.ToString();
            string suffix = suffixSb.ToString();
            ulong prefixHash = ComputeFnv1a(prefix);

            return new OptimizedPromptLayout(prefix, suffix, prefixHash, staticCount);
        }

        public static ulong ComputeFnv1a(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0UL;

            ulong hash = FnvOffsetBasis;
            for (int i = 0; i < text.Length; i++)
            {
                hash ^= text[i];
                hash *= FnvPrime;
            }
            return hash;
        }
    }
}
