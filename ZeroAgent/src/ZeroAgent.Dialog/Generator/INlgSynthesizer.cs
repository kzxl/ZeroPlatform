using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroAgent.Dialog.Generator
{
    /// <summary>
    /// Contextual payload provided to an <see cref="INlgSynthesizer"/> for natural language generation.
    /// Bundles raw system facts, user query, intent metadata, and fallback templates.
    /// </summary>
    public sealed class NlgContext
    {
        public string UserQuery { get; }
        public string IntentName { get; }
        public IReadOnlyDictionary<string, string> Facts { get; }
        public IReadOnlyList<string> FallbackTemplates { get; }
        public string FallbackDefault { get; }
        public string PersonaStyle { get; set; } = "kỹ sư ERP chuyên nghiệp, lịch sự, chính xác";
        public string? Domain { get; set; }

        public NlgContext(
            string userQuery,
            string intentName,
            IReadOnlyDictionary<string, string> facts,
            IReadOnlyList<string>? fallbackTemplates = null,
            string fallbackDefault = "")
        {
            UserQuery = userQuery ?? string.Empty;
            IntentName = intentName ?? string.Empty;
            Facts = facts ?? new Dictionary<string, string>();
            FallbackTemplates = fallbackTemplates ?? Array.Empty<string>();
            FallbackDefault = fallbackDefault ?? string.Empty;
        }
    }

    /// <summary>
    /// Natural Language Generation (NLG) abstraction interface.
    /// Decouples deterministic tool facts from surface sentence generation.
    /// Enables switching seamlessly between static template rendering, local Micro-SLMs, or external gateway models.
    /// </summary>
    public interface INlgSynthesizer
    {
        /// <summary>
        /// Synthesizes a grammatically fluent, natural language response based on verified facts and user query.
        /// </summary>
        Task<string> SynthesizeAsync(NlgContext context, CancellationToken cancellationToken = default);
    }
}
