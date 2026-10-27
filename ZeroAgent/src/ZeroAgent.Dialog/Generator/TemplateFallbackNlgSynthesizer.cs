using System;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroAgent.Dialog.Generator
{
    /// <summary>
    /// Default deterministic NLG synthesizer relying on PromptTemplate pattern matching.
    /// Acts as high-speed sub-millisecond baseline and fail-safe fallback when no SLM is available.
    /// </summary>
    public sealed class TemplateFallbackNlgSynthesizer : INlgSynthesizer
    {
        private readonly DialogueResponseGenerator _generator;

        public DialogueResponseGenerator Generator => _generator;

        public TemplateFallbackNlgSynthesizer(DialogueResponseGenerator? generator = null)
        {
            _generator = generator ?? new DialogueResponseGenerator();
        }

        public Task<string> SynthesizeAsync(NlgContext context, CancellationToken cancellationToken = default)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            string result = _generator.FormatResponse(
                context.FallbackTemplates,
                context.Facts,
                context.FallbackDefault);

            return Task.FromResult(result);
        }
    }
}
