using System;
using System.Collections.Generic;
using ZeroUI.Core.AiMl;

namespace ZeroUI.WinForms.Documents
{
    /// <summary>
    /// Additional setup helper extensions for WinForms <see cref="ZAiChatBox"/>.
    /// </summary>
    public static class ZAiChatBoxSetupExtensions
    {
        public static ZAiChatBox SetFlowPermission(
            this ZAiChatBox chatBox,
            string featureKey,
            bool canView = true,
            bool canAdd = false,
            bool canEdit = false,
            bool canDelete = false)
        {
            if (chatBox == null) throw new ArgumentNullException(nameof(chatBox));
            if (string.IsNullOrWhiteSpace(featureKey)) return chatBox;

            if (!chatBox.SessionContext.Parameters.TryGetValue("feature_permissions", out var existing) ||
                !(existing is Dictionary<string, object?> permMap))
            {
                permMap = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                chatBox.SessionContext.Parameters["feature_permissions"] = permMap;
            }

            permMap[featureKey] = new Dictionary<string, object?>
            {
                ["can_view"] = canView,
                ["can_add"] = canAdd,
                ["can_edit"] = canEdit,
                ["can_delete"] = canDelete
            };
            return chatBox;
        }
    }
}
