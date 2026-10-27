using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace ZeroAgent.Dialog.Memory
{
    public enum UserRole
    {
        Guest,
        Operator,
        Technician,
        Engineer,
        Supervisor,
        Admin
    }

    public sealed class UserProfile
    {
        public string UserId { get; }
        public string Name { get; }
        public UserRole Role { get; }
        public bool IsGuest => Role == UserRole.Guest;
        public HashSet<string> Permissions { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public List<string> AssignedAreas { get; } = new List<string>();
        public UserPersona Persona { get; } = new UserPersona();
        public DateTime CreatedAtUtc { get; } = DateTime.UtcNow;

        public UserProfile(string userId, string name, UserRole role)
        {
            UserId = userId ?? throw new ArgumentNullException(nameof(userId));
            Name = name ?? string.Empty;
            Role = role;

            // Default role permissions
            switch (role)
            {
                case UserRole.Guest:
                    Permissions.Add("PUBLIC_INFO");
                    Permissions.Add("GUEST_FAQ");
                    Permissions.Add("PRODUCT_CATALOG");
                    Permissions.Add("COMPANY_INFO");
                    break;
                case UserRole.Operator:
                    Permissions.Add("READ_STATUS");
                    Permissions.Add("QUERY_TELEMETRY");
                    break;
                case UserRole.Technician:
                    Permissions.Add("READ_STATUS");
                    Permissions.Add("QUERY_TELEMETRY");
                    Permissions.Add("DIAGNOSE_FAULTS");
                    Permissions.Add("RESET_FAULTS");
                    break;
                case UserRole.Engineer:
                case UserRole.Supervisor:
                case UserRole.Admin:
                    Permissions.Add("READ_STATUS");
                    Permissions.Add("QUERY_TELEMETRY");
                    Permissions.Add("DIAGNOSE_FAULTS");
                    Permissions.Add("RESET_FAULTS");
                    Permissions.Add("WRITE_PLC");
                    Permissions.Add("ACTUATE_RELAY");
                    Permissions.Add("STOP_MACHINE");
                    break;
            }
        }

        public static UserProfile CreateGuest(string? guestSessionId = null, string name = "Khách")
        {
            string id = string.IsNullOrWhiteSpace(guestSessionId)
                ? $"guest_{Guid.NewGuid():N}"
                : (guestSessionId!.StartsWith("guest_", StringComparison.OrdinalIgnoreCase) ? guestSessionId : $"guest_{guestSessionId}");
            return new UserProfile(id, name, UserRole.Guest);
        }

        public bool CanExecute(string actionPermission)
        {
            if (string.IsNullOrEmpty(actionPermission)) return true;
            if (Role == UserRole.Admin) return true;
            return Permissions.Contains(actionPermission);
        }
    }

    /// <summary>
    /// Profile Memory (User & Role Context Memory).
    /// Enforces Role-Based Access Control (RBAC), guest isolation, and safety boundaries for conversational actions.
    /// </summary>
    public sealed class ProfileMemory
    {
        private readonly ConcurrentDictionary<string, UserProfile> _profiles = new ConcurrentDictionary<string, UserProfile>(StringComparer.OrdinalIgnoreCase);

        public void SaveProfile(UserProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            _profiles[profile.UserId] = profile;
        }

        public UserProfile GetOrCreate(string userId, string name = "Operator", UserRole defaultRole = UserRole.Operator)
        {
            return _profiles.GetOrAdd(userId, id =>
            {
                if (id.StartsWith("guest_", StringComparison.OrdinalIgnoreCase) || id.StartsWith("anon_", StringComparison.OrdinalIgnoreCase))
                {
                    return UserProfile.CreateGuest(id, string.IsNullOrWhiteSpace(name) || name == "Operator" ? "Khách" : name);
                }
                return new UserProfile(id, name, defaultRole);
            });
        }

        public UserProfile GetOrCreateGuest(string guestSessionId, string name = "Khách")
        {
            if (string.IsNullOrWhiteSpace(guestSessionId))
            {
                guestSessionId = $"guest_{Guid.NewGuid():N}";
            }
            return _profiles.GetOrAdd(guestSessionId, id => UserProfile.CreateGuest(id, name));
        }

        public bool TryGetProfile(string userId, out UserProfile? profile)
        {
            return _profiles.TryGetValue(userId, out profile);
        }

        public bool UpgradeGuestProfile(string guestId, UserProfile authenticatedProfile)
        {
            if (string.IsNullOrWhiteSpace(guestId) || authenticatedProfile == null) return false;

            // Carry over any learned communication pronouns from guest persona if authenticated persona is default
            if (_profiles.TryGetValue(guestId, out var existingGuest) && existingGuest.IsGuest)
            {
                if (authenticatedProfile.Persona.UserPronoun == "bạn" && existingGuest.Persona.UserPronoun != "bạn")
                {
                    authenticatedProfile.Persona.UserPronoun = existingGuest.Persona.UserPronoun;
                    authenticatedProfile.Persona.BotPronoun = existingGuest.Persona.BotPronoun;
                    authenticatedProfile.Persona.Tone = existingGuest.Persona.Tone;
                }
            }

            _profiles[authenticatedProfile.UserId] = authenticatedProfile;
            _profiles[guestId] = authenticatedProfile;
            return true;
        }

        public int Count => _profiles.Count;

        /// <summary>
        /// Evicts inactive guest profiles to prevent memory exhaustion (DoS mitigation).
        /// Registered enterprise users are permanently preserved.
        /// </summary>
        public int PruneStaleGuestProfiles(TimeSpan maxInactiveAge)
        {
            var cutoff = DateTime.UtcNow - maxInactiveAge;
            int evicted = 0;
            foreach (var kvp in _profiles)
            {
                if (kvp.Value.IsGuest && kvp.Value.Persona.LastUpdatedUtc < cutoff)
                {
                    if (_profiles.TryRemove(kvp.Key, out _))
                    {
                        evicted++;
                    }
                }
            }
            return evicted;
        }
    }
}
