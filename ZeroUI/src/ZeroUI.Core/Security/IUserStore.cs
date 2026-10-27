using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroUI.Core.Security
{
    /// <summary>
    /// Decoupled storage repository for user accounts and credential verification.
    /// Implementations may bridge to SQLite, EF Core, Active Directory, REST APIs, or local encrypted files.
    /// </summary>
    public interface IUserStore
    {
        /// <summary>
        /// Retrieves all registered users in the system.
        /// </summary>
        Task<IReadOnlyList<IUser>> GetUsersAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Finds a user by their unique identifier.
        /// </summary>
        Task<IUser?> FindByIdAsync(string userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Finds a user by login username.
        /// </summary>
        Task<IUser?> FindByUsernameAsync(string username, CancellationToken cancellationToken = default);

        /// <summary>
        /// Finds a user associated with a physical RFID/NFC badge identifier.
        /// </summary>
        Task<IUser?> FindByBadgeIdAsync(string badgeId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates or updates a user profile.
        /// </summary>
        Task<bool> SaveUserAsync(IUser user, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a user by identifier.
        /// </summary>
        Task<bool> DeleteUserAsync(string userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Verifies whether the provided raw password matches the user's stored credential.
        /// </summary>
        Task<bool> VerifyPasswordAsync(string userId, string rawPassword, CancellationToken cancellationToken = default);

        /// <summary>
        /// Verifies whether the provided PIN code matches the user's stored PIN.
        /// </summary>
        Task<bool> VerifyPinAsync(string userId, string pinCode, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets a new password for the specified user.
        /// </summary>
        Task<bool> SetPasswordAsync(string userId, string newPassword, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets a new PIN code for the specified user.
        /// </summary>
        Task<bool> SetPinAsync(string userId, string newPinCode, CancellationToken cancellationToken = default);
    }
}
