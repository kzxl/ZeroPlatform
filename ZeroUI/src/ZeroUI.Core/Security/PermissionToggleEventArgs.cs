using System;

namespace ZeroUI.Core.Security
{
    /// <summary>
    /// Event arguments for permission toggles in role matrix editors.
    /// </summary>
    public class PermissionToggleEventArgs : EventArgs
    {
        public string RoleId { get; }
        public string PermissionKey { get; }
        public bool IsGranted { get; }

        public PermissionToggleEventArgs(string roleId, string permissionKey, bool isGranted)
        {
            RoleId = roleId ?? string.Empty;
            PermissionKey = permissionKey ?? string.Empty;
            IsGranted = isGranted;
        }
    }
}
