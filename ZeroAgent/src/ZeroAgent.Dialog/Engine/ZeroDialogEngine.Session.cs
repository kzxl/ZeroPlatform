using System;
using ZeroAgent.Dialog.DST;
using ZeroAgent.Dialog.Memory;

namespace ZeroAgent.Dialog.Engine
{
    public sealed partial class ZeroDialogEngine
    {
        public DialogueSession GetOrCreateSession(string sessionId)
        {
            return Sessions.GetOrCreate(sessionId);
        }

        /// <summary>
        /// Resolves an appropriate user profile for the session:
        /// 1. Uses explicit profile if supplied.
        /// 2. Auto-detects anonymous guest session prefixes ("guest_", "anon_").
        /// 3. Fallbacks to default operator profile for backward compatibility.
        /// </summary>
        public UserProfile ResolveProfile(string sessionId, UserProfile? explicitProfile = null)
        {
            if (explicitProfile != null)
            {
                Memory.Profiles.SaveProfile(explicitProfile);
                return explicitProfile;
            }

            if (Memory.Profiles.TryGetProfile(sessionId, out var existing) && existing != null)
            {
                return existing;
            }

            if (sessionId.StartsWith("guest_", StringComparison.OrdinalIgnoreCase) ||
                sessionId.StartsWith("anon_", StringComparison.OrdinalIgnoreCase))
            {
                return Memory.Profiles.GetOrCreateGuest(sessionId);
            }

            return Memory.Profiles.GetOrCreate(sessionId, "DefaultOperator", UserRole.Operator);
        }

        /// <summary>
        /// Seamlessly upgrades an active guest session to an authenticated user profile,
        /// preserving conversation history turns, collected dialogue slots, and unblocking
        /// actions previously halted by permission gates.
        /// </summary>
        public DialogueSession UpgradeGuestSession(string guestSessionId, UserProfile authenticatedProfile)
        {
            if (string.IsNullOrWhiteSpace(guestSessionId)) throw new ArgumentNullException(nameof(guestSessionId));
            if (authenticatedProfile == null) throw new ArgumentNullException(nameof(authenticatedProfile));

            Memory.Profiles.UpgradeGuestProfile(guestSessionId, authenticatedProfile);

            var session = GetOrCreateSession(guestSessionId);

            // If the guest was previously blocked by permission requirement, unblock to ready state
            if (session.State == SessionState.ActionBlockedByPermission)
            {
                session.State = SessionState.ReadyToExecute;
            }

            return session;
        }

        /// <summary>
        /// Clears conversational state and working memory for the specified session.
        /// </summary>
        public void ClearSession(string sessionId)
        {
            Sessions.Remove(sessionId);
            Memory.GetWorkingMemory(sessionId).Clear();
        }
    }
}
