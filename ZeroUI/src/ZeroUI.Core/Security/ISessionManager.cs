using System;
using System.Collections.Generic;

namespace ZeroUI.Core.Security
{
    /// <summary>
    /// Event arguments for changes to the active authenticated user.
    /// </summary>
    public class UserChangedEventArgs : EventArgs
    {
        public IUser? PreviousUser { get; }
        public IUser? NewUser { get; }

        public UserChangedEventArgs(IUser? previousUser, IUser? newUser)
        {
            PreviousUser = previousUser;
            NewUser = newUser;
        }
    }

    /// <summary>
    /// Manages the application-wide security session, current user identity,
    /// effective permissions, and idle timeout tracking.
    /// </summary>
    public interface ISessionManager : IDisposable
    {
        /// <summary>
        /// Gets the currently authenticated user, or null if no user is signed in.
        /// </summary>
        IUser? CurrentUser { get; }

        /// <summary>
        /// Gets a value indicating whether a user is currently signed in.
        /// </summary>
        bool IsAuthenticated { get; }

        /// <summary>
        /// Gets a value indicating whether the session is temporarily locked due to inactivity or operator request.
        /// </summary>
        bool IsLockedOut { get; }

        /// <summary>
        /// Gets or sets the idle inactivity duration before triggering an automatic logout or session lock.
        /// A value of <see cref="TimeSpan.Zero"/> or negative disables the idle monitor.
        /// </summary>
        TimeSpan IdleTimeout { get; set; }

        /// <summary>
        /// Gets or sets the warning interval prior to idle expiration (e.g. 15 seconds before auto-logout).
        /// </summary>
        TimeSpan WarningThreshold { get; set; }

        /// <summary>
        /// Gets the elapsed duration since the last detected user interaction.
        /// </summary>
        TimeSpan ElapsedIdleTime { get; }

        /// <summary>
        /// Gets the cached set of resolved permission keys for the currently active user.
        /// </summary>
        IReadOnlyCollection<string> CurrentPermissions { get; }

        /// <summary>
        /// Checks if the current user possesses the specified role.
        /// </summary>
        bool HasRole(string roleId);

        /// <summary>
        /// Checks if the current user possesses the specified permission key.
        /// </summary>
        bool HasPermission(string permissionKey);

        /// <summary>
        /// Registers a user interaction (keystroke, touch tap, mouse move) to reset the idle timer.
        /// </summary>
        void NotifyActivity();

        /// <summary>
        /// Updates the currently active authenticated user and their resolved permissions.
        /// </summary>
        void SetCurrentUser(IUser? user, IEnumerable<string>? permissions = null);

        /// <summary>
        /// Temporarily locks the session interface while preserving user context.
        /// </summary>
        void LockSession();

        /// <summary>
        /// Unlocks the session interface after valid credential re-verification.
        /// </summary>
        void UnlockSession();

        /// <summary>
        /// Signs out the current user and clears active permissions.
        /// </summary>
        void Logout();

        /// <summary>
        /// Fired when the active user changes (sign in, sign out, switch operator).
        /// </summary>
        event EventHandler<UserChangedEventArgs>? UserChanged;

        /// <summary>
        /// Fired when the session is locked.
        /// </summary>
        event EventHandler? SessionLocked;

        /// <summary>
        /// Fired when the session is unlocked.
        /// </summary>
        event EventHandler? SessionUnlocked;

        /// <summary>
        /// Fired when the idle timeout expires and the session automatically signs out or locks.
        /// </summary>
        event EventHandler? SessionTimedOut;

        /// <summary>
        /// Fired when inactivity reaches the warning threshold before timing out.
        /// </summary>
        event EventHandler<TimeSpan>? IdleWarning;
    }
}
