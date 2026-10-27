using System;

namespace ZeroUI.Core.Security
{
    /// <summary>
    /// Reason code for authentication failure.
    /// </summary>
    public enum AuthFailureReason
    {
        None = 0,
        InvalidCredentials = 1,
        UserNotFound = 2,
        AccountLocked = 3,
        BadgeNotRecognized = 4,
        PinIncorrect = 5,
        PasswordExpired = 6,
        HardwareError = 7,
        SystemError = 8
    }

    /// <summary>
    /// Encapsulates the outcome of an authentication attempt.
    /// </summary>
    public class AuthResult
    {
        /// <summary>
        /// Gets a value indicating whether authentication succeeded.
        /// </summary>
        public bool Succeeded { get; }

        /// <summary>
        /// Gets the authenticated user, or null if authentication failed.
        /// </summary>
        public IUser? User { get; }

        /// <summary>
        /// Gets the localized or descriptive error message if authentication failed.
        /// </summary>
        public string? ErrorMessage { get; }

        /// <summary>
        /// Gets the failure reason code.
        /// </summary>
        public AuthFailureReason FailureReason { get; }

        private AuthResult(bool succeeded, IUser? user, string? errorMessage, AuthFailureReason failureReason)
        {
            Succeeded = succeeded;
            User = user;
            ErrorMessage = errorMessage;
            FailureReason = failureReason;
        }

        /// <summary>
        /// Creates a successful authentication result with the specified user.
        /// </summary>
        public static AuthResult Success(IUser user)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));
            return new AuthResult(true, user, null, AuthFailureReason.None);
        }

        /// <summary>
        /// Creates a failed authentication result with a reason and error message.
        /// </summary>
        public static AuthResult Failure(string errorMessage, AuthFailureReason reason = AuthFailureReason.InvalidCredentials)
        {
            return new AuthResult(false, null, errorMessage, reason);
        }
    }
}
