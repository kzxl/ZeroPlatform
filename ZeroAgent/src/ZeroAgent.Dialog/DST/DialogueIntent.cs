using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ZeroAgent.Dialog.DST
{
    public sealed class DialogueIntent
    {
        public string Name { get; }
        public string Id => Name;
        public string Description { get; }
        public List<string> SampleUtterances { get; } = new List<string>();
        public List<string> RequiredSlots { get; } = new List<string>();
        public List<string> OptionalSlots { get; } = new List<string>();
        public string RequiredPermission { get; set; } = string.Empty;
        public Dictionary<string, string> SlotClarificationPrompts { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public List<string> ResponseTemplates { get; } = new List<string>();
        public Func<DialogueSession, Task<string>>? ActionHandler { get; set; }
        public string? Domain { get; set; }

        public DialogueIntent(string name, string description)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Description = description ?? string.Empty;
        }

        public DialogueIntent AddSamples(params string[] samples)
        {
            SampleUtterances.AddRange(samples);
            return this;
        }

        public DialogueIntent RequireSlot(string slotName, string clarificationPrompt)
        {
            RequiredSlots.Add(slotName);
            SlotClarificationPrompts[slotName] = clarificationPrompt;
            return this;
        }

        public DialogueIntent AddOptionalSlot(string slotName)
        {
            OptionalSlots.Add(slotName);
            return this;
        }

        public DialogueIntent AddTemplates(params string[] templates)
        {
            ResponseTemplates.AddRange(templates);
            return this;
        }

        public DialogueIntent WithDomain(string domain)
        {
            Domain = domain;
            return this;
        }
    }
}
