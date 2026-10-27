using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroUI.Core.Security
{
    /// <summary>
    /// Decoupled repository for security roles, groups, and system permissions.
    /// </summary>
    public interface IRoleStore
    {
        /// <summary>
        /// Retrieves all configured security roles.
        /// </summary>
        Task<IReadOnlyList<IRole>> GetRolesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Finds a role by its unique role identifier.
        /// </summary>
        Task<IRole?> FindRoleByIdAsync(string roleId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates or updates a role definition including its assigned permissions.
        /// </summary>
        Task<bool> SaveRoleAsync(IRole role, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a role by identifier.
        /// </summary>
        Task<bool> DeleteRoleAsync(string roleId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves the complete catalog of all declared system permissions.
        /// </summary>
        Task<IReadOnlyList<IPermission>> GetAllPermissionsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves aggregated permission keys for a given collection of role IDs.
        /// </summary>
        Task<IReadOnlyList<string>> GetPermissionsForRolesAsync(IEnumerable<string> roleIds, CancellationToken cancellationToken = default);
    }
}
