using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroUI.Core.AiMl
{
    /// <summary>
    /// Represents the role of the message author in an AI chat conversation.
    /// </summary>
    public enum ChatRole
    {
        User,
        Assistant,
        System,
        Tool
    }

    /// <summary>
    /// Represents the current runtime streaming or generation state of the AI assistant.
    /// </summary>
    public enum ChatStreamingState
    {
        Idle,
        Thinking,
        Streaming,
        Completed,
        Error
    }

    /// <summary>
    /// Defines the behavior of a customizable toolbar action item in ZAiChatBox.
    /// </summary>
    public enum ChatToolbarCommandType
    {
        /// <summary>
        /// Raises the ToolbarItemClicked event for custom handling.
        /// </summary>
        Custom,

        /// <summary>
        /// Opens a file dialog using FileFilter and raises FileAttached (and attaches to context if provider is active).
        /// </summary>
        AttachFile,

        /// <summary>
        /// Prefills the prompt input box with CommandValue and focuses the input box.
        /// </summary>
        PrefillInput,

        /// <summary>
        /// Immediately submits CommandValue as a chat message.
        /// </summary>
        SendPrompt
    }

    /// <summary>
    /// Represents a single message in an AI conversation session.
    /// </summary>
    public class ChatMessage
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public ChatRole Role { get; set; } = ChatRole.User;
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string? SenderName { get; set; }
        public string? ModelName { get; set; }
        public bool IsStreaming { get; set; }
        public string? ErrorMessage { get; set; }
        public object? Tag { get; set; }
        public bool CanUndo { get; set; }
        public string? AttachmentPath { get; set; }
        public string? AttachmentType { get; set; }

        /// <summary>
        /// Optional reasoning or execution trace associated with this message.
        /// </summary>
        public string? Reasoning { get; set; }

        /// <summary>
        /// Execution latency in milliseconds.
        /// </summary>
        public double? LatencyMs { get; set; }

        /// <summary>
        /// Generic interactive action chips / deep-links rendered inside the message bubble.
        /// </summary>
        public List<ChatActionItem> Actions { get; set; } = new List<ChatActionItem>();

        /// <summary>
        /// Generic key-value metadata or contextual entities returned with this message.
        /// </summary>
        public Dictionary<string, string> ContextData { get; set; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public ChatMessage() { }

        public ChatMessage(ChatRole role, string content, string? senderName = null)
        {
            Role = role;
            Content = content ?? string.Empty;
            SenderName = senderName;
            Timestamp = DateTime.UtcNow;
        }

        public static ChatMessage User(string content, string? senderName = "Operator") =>
            new(ChatRole.User, content, senderName);

        public static ChatMessage Assistant(string content, string? modelName = "ZeroCopilot") =>
            new(ChatRole.Assistant, content, modelName) { ModelName = modelName };

        public static ChatMessage System(string content) =>
            new(ChatRole.System, content, "System");
    }

    /// <summary>
    /// Represents an actionable quick prompt suggestion chip.
    /// </summary>
    public class ChatPromptAction
    {
        public string Title { get; set; } = string.Empty;
        public string PromptText { get; set; } = string.Empty;
        public string? IconKey { get; set; }

        public ChatPromptAction() { }

        public ChatPromptAction(string title, string promptText, string? iconKey = null)
        {
            Title = title;
            PromptText = promptText;
            IconKey = iconKey;
        }
    }

    /// <summary>
    /// Represents a generic interactive action chip rendered inside an assistant message bubble.
    /// </summary>
    public class ChatActionItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string ActionType { get; set; } = string.Empty;
        public string TargetKey { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string? Category { get; set; }
        public string? ReferenceValue { get; set; }
        public string? BadgeText { get; set; }
        public List<string> AllowedModes { get; set; } = new List<string>();
        public Dictionary<string, bool> Flags { get; set; } =
            new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> Metadata { get; set; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public object? Tag { get; set; }
    }

    /// <summary>
    /// Represents a generic configurable button in the chat control's quick-action toolbar.
    /// </summary>
    public class ChatToolbarItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Text { get; set; } = string.Empty;
        public string? Tooltip { get; set; }
        public ChatToolbarCommandType CommandType { get; set; } = ChatToolbarCommandType.Custom;
        public string? CommandValue { get; set; }
        public string? FileFilter { get; set; }
        public string? FileDialogTitle { get; set; }
        public string? AttachmentType { get; set; }
        public string? AccentColorHex { get; set; }
        public object? Tag { get; set; }

        public ChatToolbarItem() { }

        public ChatToolbarItem(
            string id,
            string text,
            ChatToolbarCommandType commandType = ChatToolbarCommandType.Custom,
            string? commandValue = null,
            string? accentColorHex = null)
        {
            Id = id;
            Text = text;
            CommandType = commandType;
            CommandValue = commandValue;
            AccentColorHex = accentColorHex;
        }
    }

    /// <summary>
    /// Holds generic session state, active context tags, and arbitrary key-value payload sent to an IChatFlowProvider.
    /// </summary>
    public class ChatContextState
    {
        public string SessionId { get; set; } = "chat_" + Guid.NewGuid().ToString("N").Substring(0, 12);

        /// <summary>
        /// Key-value tags displayed in the UI context bar (e.g. ["User"] = "admin", ["Ref"] = "1001").
        /// </summary>
        public Dictionary<string, string> DisplayTags { get; set; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Arbitrary key-value parameters passed in the provider request context.
        /// </summary>
        public Dictionary<string, object?> Parameters { get; set; } =
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Transient attachment payload (e.g. base64 data URI) for the next request.
        /// </summary>
        public string? PendingAttachmentData { get; set; }

        public ChatContextState SetParameter(string key, object? value, string? displayLabel = null)
        {
            if (string.IsNullOrWhiteSpace(key)) return this;
            Parameters[key] = value;

            if (displayLabel != null)
            {
                string strVal = Convert.ToString(value) ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(strVal))
                    DisplayTags[displayLabel] = strVal;
                else
                    DisplayTags.Remove(displayLabel);
            }
            return this;
        }

        public ChatContextState SetTag(string label, string? value)
        {
            if (string.IsNullOrWhiteSpace(label)) return this;
            if (string.IsNullOrWhiteSpace(value))
                DisplayTags.Remove(label);
            else
                DisplayTags[label] = value!;
            return this;
        }

        public void MergeContextUpdates(IDictionary<string, string>? updates)
        {
            if (updates == null) return;
            foreach (var kv in updates)
            {
                if (!string.IsNullOrWhiteSpace(kv.Value))
                {
                    Parameters[kv.Key] = kv.Value;
                    DisplayTags[kv.Key] = kv.Value;
                }
            }
        }

        public void ClearTagsAndParameters(bool preserveIdentity = true)
        {
            DisplayTags.Clear();
            PendingAttachmentData = null;

            if (!preserveIdentity)
            {
                Parameters.Clear();
                return;
            }

            var preserved = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var key in new[] { "user_id", "user_role", "feature_permissions" })
            {
                if (Parameters.TryGetValue(key, out var val))
                    preserved[key] = val;
            }
            Parameters.Clear();
            foreach (var kv in preserved)
            {
                Parameters[kv.Key] = kv.Value;
            }
        }

        public string FormatDisplaySummary()
        {
            if (DisplayTags.Count == 0) return string.Empty;
            var parts = new List<string>(DisplayTags.Count);
            foreach (var kv in DisplayTags)
            {
                if (!string.IsNullOrWhiteSpace(kv.Value))
                {
                    parts.Add($"{kv.Key}: {kv.Value}");
                }
            }
            return string.Join("  |  ", parts);
        }
    }

    /// <summary>
    /// Generic request envelope for IChatFlowProvider execution.
    /// </summary>
    public class ChatFlowRequest
    {
        public string WorkflowId { get; set; } = string.Empty;
        public string InputText { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public Dictionary<string, object?> Context { get; set; } =
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Generic response envelope returned by an IChatFlowProvider.
    /// </summary>
    public class ChatFlowResult
    {
        public string Status { get; set; } = "completed";
        public string Output { get; set; } = string.Empty;
        public string? Reasoning { get; set; }
        public double DurationMs { get; set; }
        public string? Error { get; set; }
        public List<ChatActionItem> Actions { get; set; } = new List<ChatActionItem>();
        public Dictionary<string, string> ContextUpdates { get; set; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public bool IsSuccess => string.IsNullOrEmpty(Error) &&
                                 !string.Equals(Status, "failed", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Generic real-time streaming chunk emitted during IChatFlowProvider.StreamAsync.
    /// </summary>
    public class ChatStreamChunk
    {
        /// <summary>
        /// Event type: "status", "reasoning", "token", "complete", "error".
        /// </summary>
        public string EventType { get; set; } = string.Empty;
        public string? StatusText { get; set; }
        public string? Token { get; set; }
        public string? Error { get; set; }
        public ChatFlowResult? FinalResult { get; set; }
    }

    /// <summary>
    /// Generic abstraction for connecting ZAiChatBox to any workflow or AI backend.
    /// </summary>
    public interface IChatFlowProvider
    {
        Task<ChatFlowResult> ExecuteAsync(
            ChatFlowRequest request,
            CancellationToken cancellationToken = default);

        Task<ChatFlowResult> StreamAsync(
            ChatFlowRequest request,
            Action<ChatStreamChunk>? onChunk = null,
            CancellationToken cancellationToken = default);

        Task<bool> ResetSessionAsync(
            string sessionId,
            CancellationToken cancellationToken = default);
    }
}
