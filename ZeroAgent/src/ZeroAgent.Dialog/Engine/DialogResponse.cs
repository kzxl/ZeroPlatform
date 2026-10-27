using System;
using System.Collections.Generic;
using ZeroAgent.Dialog.DST;

namespace ZeroAgent.Dialog.Engine
{
    public sealed class DialogResponse
    {
        public string Text { get; }
        public SessionState State { get; }
        public string? IntentName { get; }
        public IReadOnlyDictionary<string, string> ActiveSlots { get; }
        public bool IsActionExecuted { get; }
        public float Confidence { get; }

        public DialogResponse(
            string text,
            SessionState state,
            string? intentName = null,
            IReadOnlyDictionary<string, string>? activeSlots = null,
            bool isActionExecuted = false,
            float confidence = 1.0f)
        {
            Text = text ?? string.Empty;
            State = state;
            IntentName = intentName;
            ActiveSlots = activeSlots ?? new Dictionary<string, string>();
            IsActionExecuted = isActionExecuted;
            Confidence = confidence;
        }

        public override string ToString() => Text;
    }
}
