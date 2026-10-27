using System;
using System.Collections.Generic;

namespace ZeroUI.Core.Security
{
    /// <summary>
    /// Represents an operator or system user in the industrial runtime.
    /// </summary>
    public interface IUser
    {
        /// <summary>
        /// Gets the unique identifier for the user.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Gets the login username or operator ID.
        /// </summary>
        string Username { get; }

        /// <summary>
        /// Gets the human-readable display name.
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Gets the optional contact email.
        /// </summary>
        string? Email { get; }

        /// <summary>
        /// Gets the physical badge identifier (RFID / NFC / Barcode).
        /// </summary>
        string? BadgeId { get; }

        /// <summary>
        /// Gets the hashed PIN code for rapid touchscreen sign-in.
        /// </summary>
        string? PinCodeHash { get; }

        /// <summary>
        /// Gets a value indicating whether the user account is disabled or locked.
        /// </summary>
        bool IsLocked { get; }

        /// <summary>
        /// Gets the timestamp of the last successful authentication.
        /// </summary>
        DateTime? LastLoginUtc { get; }

        /// <summary>
        /// Gets the assigned role identifiers.
        /// </summary>
        IReadOnlyList<string> Roles { get; }

        /// <summary>
        /// Gets custom metadata or application-specific properties.
        /// </summary>
        IReadOnlyDictionary<string, object?> CustomAttributes { get; }
    }

    /// <summary>
    /// Default immutable implementation of <see cref="IUser"/>.
    /// </summary>
    public class UserModel : IUser
    {
        public string Id { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? BadgeId { get; set; }
        public string? PinCodeHash { get; set; }
        public bool IsLocked { get; set; }
        public DateTime? LastLoginUtc { get; set; }
        public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
        public IReadOnlyDictionary<string, object?> CustomAttributes { get; set; } = new Dictionary<string, object?>();

        public UserModel() { }

        public UserModel(
            string id,
            string username,
            string displayName,
            string? email = null,
            string? badgeId = null,
            string? pinCodeHash = null,
            bool isLocked = false,
            DateTime? lastLoginUtc = null,
            IReadOnlyList<string>? roles = null,
            IReadOnlyDictionary<string, object?>? customAttributes = null)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Username = username ?? throw new ArgumentNullException(nameof(username));
            DisplayName = displayName ?? username;
            Email = email;
            BadgeId = badgeId;
            PinCodeHash = pinCodeHash;
            IsLocked = isLocked;
            LastLoginUtc = lastLoginUtc;
            Roles = roles ?? Array.Empty<string>();
            CustomAttributes = customAttributes ?? new Dictionary<string, object?>();
        }
    }
}
