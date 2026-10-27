using System;
using System.Collections.Generic;

namespace ZeroUI.Core.AiMl
{
    /// <summary>
    /// Separate configuration builder for wiring an <see cref="IChatFlowProvider"/> (such as <see cref="ZFlowChatClient"/>),
    /// session context parameters, toolbar items, and prompt suggestions without hardcoding domain logic into UI controls.
    /// </summary>
    public sealed class ZFlowChatOptions
    {
        public string Endpoint { get; set; } = "http://127.0.0.1:8000";
        public string WorkflowId { get; set; } = string.Empty;
        public string? SessionId { get; set; }
        public bool UseStreaming { get; set; } = true;
        public bool ShowContextBar { get; set; } = true;
        public bool ShowReasoning { get; set; } = true;
        public string ContextBarPrefix { get; set; } = "📌 Context:";
        public string ContextBarEmptyText { get; set; } = "Global";
        public bool ReplaceDefaultToolbarItems { get; set; }

        public Dictionary<string, object?> InitialParameters { get; } =
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, string> InitialDisplayTags { get; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public List<ChatToolbarItem> ToolbarItems { get; } = new List<ChatToolbarItem>();
        public List<string> PromptSuggestions { get; } = new List<string>();

        public ZFlowChatOptions WithEndpoint(string endpoint, string workflowId = "")
        {
            if (!string.IsNullOrWhiteSpace(endpoint))
                Endpoint = endpoint.TrimEnd('/');
            if (!string.IsNullOrWhiteSpace(workflowId))
                WorkflowId = workflowId.Trim();
            return this;
        }

        public ZFlowChatOptions WithIdentity(string? userId, string? userRole = null)
        {
            if (!string.IsNullOrWhiteSpace(userId))
            {
                InitialParameters["user_id"] = userId;
                InitialDisplayTags["User"] = !string.IsNullOrWhiteSpace(userRole)
                    ? $"{userId} ({userRole})"
                    : userId!;
            }
            if (!string.IsNullOrWhiteSpace(userRole))
            {
                InitialParameters["user_role"] = userRole;
            }
            return this;
        }

        public ZFlowChatOptions WithContextParameter(string key, object? value, string? displayTagLabel = null)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                InitialParameters[key] = value;
                if (!string.IsNullOrWhiteSpace(displayTagLabel) && value != null)
                {
                    string strVal = Convert.ToString(value) ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(strVal))
                    {
                        InitialDisplayTags[displayTagLabel!] = strVal;
                    }
                }
            }
            return this;
        }

        public ZFlowChatOptions WithFeaturePermission(
            string featureKey,
            bool canView = true,
            bool canAdd = false,
            bool canEdit = false,
            bool canDelete = false)
        {
            if (string.IsNullOrWhiteSpace(featureKey)) return this;

            if (!InitialParameters.TryGetValue("feature_permissions", out var existing) ||
                !(existing is Dictionary<string, object?> permMap))
            {
                permMap = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                InitialParameters["feature_permissions"] = permMap;
            }

            permMap[featureKey] = new Dictionary<string, object?>
            {
                ["can_view"] = canView,
                ["can_add"] = canAdd,
                ["can_edit"] = canEdit,
                ["can_delete"] = canDelete
            };
            return this;
        }

        public ZFlowChatOptions WithToolbarItem(ChatToolbarItem item)
        {
            if (item != null)
            {
                ToolbarItems.Add(item);
            }
            return this;
        }

        public ZFlowChatOptions WithContinuousLearningTools(
            string teachButtonText = "💡 Teach (/learn)",
            string unlearnButtonText = "🧹 Unlearn (/forget)",
            string learnPrefix = "/learn: ",
            string forgetCommand = "/forget")
        {
            ToolbarItems.Add(new ChatToolbarItem(
                id: "flow_teach",
                text: teachButtonText,
                commandType: ChatToolbarCommandType.PrefillInput,
                commandValue: learnPrefix,
                accentColorHex: "#10B981"));

            ToolbarItems.Add(new ChatToolbarItem(
                id: "flow_unlearn",
                text: unlearnButtonText,
                commandType: ChatToolbarCommandType.SendPrompt,
                commandValue: forgetCommand,
                accentColorHex: "#F59E0B"));

            return this;
        }

        public ZFlowChatOptions WithSuggestions(params string[] suggestions)
        {
            if (suggestions != null)
            {
                foreach (var s in suggestions)
                {
                    if (!string.IsNullOrWhiteSpace(s))
                        PromptSuggestions.Add(s);
                }
            }
            return this;
        }

        public IChatFlowProvider BuildProvider()
        {
            return new ZFlowChatClient(Endpoint, WorkflowId);
        }

        public ChatContextState BuildContextState()
        {
            var state = new ChatContextState();
            if (!string.IsNullOrWhiteSpace(SessionId))
            {
                state.SessionId = SessionId!;
            }
            foreach (var kv in InitialParameters)
            {
                state.Parameters[kv.Key] = kv.Value;
            }
            foreach (var kv in InitialDisplayTags)
            {
                state.DisplayTags[kv.Key] = kv.Value;
            }
            return state;
        }
    }
}
