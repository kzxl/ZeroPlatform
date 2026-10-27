using System;

namespace ZeroUI.Core.Security
{
    /// <summary>
    /// Represents a discrete actionable capability or privilege in the system.
    /// </summary>
    public interface IPermission
    {
        /// <summary>
        /// Gets the unique permission key (e.g., "Alarms.Acknowledge", "Parameters.Write", "Recipe.Modify").
        /// </summary>
        string Key { get; }

        /// <summary>
        /// Gets the display name for the permission.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the logical category or module group (e.g., "Alarms", "Process", "System", "Recipe").
        /// </summary>
        string Category { get; }

        /// <summary>
        /// Gets the detailed explanation of what this permission allows.
        /// </summary>
        string Description { get; }
    }

    /// <summary>
    /// Default implementation of <see cref="IPermission"/>.
    /// </summary>
    public class PermissionModel : IPermission
    {
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public PermissionModel() { }

        public PermissionModel(string key, string name, string category, string description = "")
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
            Name = name ?? key;
            Category = category ?? "General";
            Description = description ?? string.Empty;
        }
    }
}
