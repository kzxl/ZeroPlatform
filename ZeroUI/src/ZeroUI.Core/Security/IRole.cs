using System;
using System.Collections.Generic;

namespace ZeroUI.Core.Security
{
    /// <summary>
    /// Represents a security role or group within the industrial authorization model.
    /// </summary>
    public interface IRole
    {
        /// <summary>
        /// Gets the unique role identifier (e.g., "Administrator", "Engineer", "Operator").
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Gets the localized or descriptive role name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the role description explaining granted privileges.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Gets the security level hierarchy (higher number denotes higher authority).
        /// </summary>
        int SecurityLevel { get; }

        /// <summary>
        /// Gets the permission keys associated with this role.
        /// </summary>
        IReadOnlyList<string> Permissions { get; }
    }

    /// <summary>
    /// Default implementation of <see cref="IRole"/>.
    /// </summary>
    public class RoleModel : IRole
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int SecurityLevel { get; set; }
        public IReadOnlyList<string> Permissions { get; set; } = Array.Empty<string>();

        public RoleModel() { }

        public RoleModel(
            string id,
            string name,
            string description = "",
            int securityLevel = 0,
            IReadOnlyList<string>? permissions = null)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Name = name ?? id;
            Description = description ?? string.Empty;
            SecurityLevel = securityLevel;
            Permissions = permissions ?? Array.Empty<string>();
        }
    }
}
