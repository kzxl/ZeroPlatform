using System.Threading;
using System.Threading.Tasks;

namespace ZeroUI.Core.Security
{
    /// <summary>
    /// Contract for verifying credentials across various modalities (Username/Password, RFID Badge, PIN).
    /// </summary>
    public interface IAuthenticationProvider
    {
        /// <summary>
        /// Authenticates an operator using standard username and password credentials.
        /// </summary>
        Task<AuthResult> AuthenticatePasswordAsync(string username, string password, CancellationToken cancellationToken = default);

        /// <summary>
        /// Authenticates an operator instantaneously using an RFID/NFC/Dongle hardware badge identifier.
        /// </summary>
        Task<AuthResult> AuthenticateBadgeAsync(string badgeId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Authenticates an operator using an account identifier and rapid touchscreen PIN code.
        /// </summary>
        Task<AuthResult> AuthenticatePinAsync(string usernameOrUserId, string pinCode, CancellationToken cancellationToken = default);
    }
}
