using System;
using System.Collections.Generic;

namespace ZeroAgent.Dialog.DST
{
    public enum SessionState
    {
        Idle,
        CollectingSlots,
        ReadyToExecute,
        Completed,
        ActionBlockedByPermission
    }

    /// <summary>
    /// Represents the active state and collected slots of an ongoing dialogue conversation.
    /// </summary>
    public sealed class DialogueSession
    {
        public string SessionId { get; }
        public SessionState State { get; set; } = SessionState.Idle;
        public DialogueIntent? CurrentIntent { get; set; }
        public string? ActiveDomain { get; set; }
        public Dictionary<string, string> Slots { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public string? PendingRequiredSlot { get; set; }

        public DialogueSession(string sessionId)
        {
            SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
        }

        public void SetSlot(string key, string value)
        {
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value)) return;
            Slots[key] = value;
        }

        public bool TryGetSlot(string key, out string value)
        {
            return Slots.TryGetValue(key, out value!);
        }

        public bool HasSlot(string key)
        {
            return Slots.ContainsKey(key) && !string.IsNullOrWhiteSpace(Slots[key]);
        }

        public void Reset()
        {
            State = SessionState.Idle;
            CurrentIntent = null;
            ActiveDomain = null;
            Slots.Clear();
            PendingRequiredSlot = null;
        }
    }
}
