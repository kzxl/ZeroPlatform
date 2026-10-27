using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ZeroUI.Core.Common;

namespace ZeroUI.Core.Security
{
    /// <summary>
    /// Action to perform when user session inactivity reaches the configured <see cref="ISessionManager.IdleTimeout"/>.
    /// </summary>
    public enum SessionTimeoutAction
    {
        /// <summary>
        /// Automatically signs out the user and clears permissions.
        /// </summary>
        Logout = 0,

        /// <summary>
        /// Locks the user interface and requires PIN or password re-entry while retaining session context.
        /// </summary>
        Lock = 1
    }

    /// <summary>
    /// Thread-safe, high-performance runtime implementation of <see cref="ISessionManager"/>.
    /// Utilizes 64-bit monotonic clock timing with zero-allocation activity heartbeats.
    /// </summary>
    public class SessionContext : ISessionManager
    {
        private static readonly Lazy<SessionContext> _lazyInstance = new Lazy<SessionContext>(() => new SessionContext());

        /// <summary>
        /// Gets the ambient application-wide default session context singleton.
        /// </summary>
        public static SessionContext Current => _lazyInstance.Value;

        private readonly object _syncLock = new object();
        private readonly Timer? _timer;
        private long _lastActivityMs;
        private bool _disposed;
        private bool _warningFired;
        private HashSet<string> _permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public IUser? CurrentUser { get; private set; }
        public bool IsAuthenticated => CurrentUser != null && !CurrentUser.IsLocked;
        public bool IsLockedOut { get; private set; }

        public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromMinutes(15);
        public TimeSpan WarningThreshold { get; set; } = TimeSpan.FromSeconds(30);
        public SessionTimeoutAction TimeoutAction { get; set; } = SessionTimeoutAction.Lock;

        public TimeSpan ElapsedIdleTime
        {
            get
            {
                long last = Interlocked.Read(ref _lastActivityMs);
                if (last == 0) return TimeSpan.Zero;
                long elapsedMs = MonotonicClock.ElapsedMilliseconds - last;
                return TimeSpan.FromMilliseconds(Math.Max(0, elapsedMs));
            }
        }

        public IReadOnlyCollection<string> CurrentPermissions
        {
            get
            {
                lock (_syncLock)
                {
                    return _permissions.ToArray();
                }
            }
        }

        public event EventHandler<UserChangedEventArgs>? UserChanged;
        public event EventHandler? SessionLocked;
        public event EventHandler? SessionUnlocked;
        public event EventHandler? SessionTimedOut;
        public event EventHandler<TimeSpan>? IdleWarning;

        public SessionContext()
        {
            _lastActivityMs = MonotonicClock.ElapsedMilliseconds;
            _timer = new Timer(CheckIdleTick, null, 1000, 1000);
        }

        public bool HasRole(string roleId)
        {
            if (string.IsNullOrWhiteSpace(roleId)) return false;
            var user = CurrentUser;
            if (user == null || user.IsLocked) return false;

            for (int i = 0; i < user.Roles.Count; i++)
            {
                if (string.Equals(user.Roles[i], roleId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        public bool HasPermission(string permissionKey)
        {
            if (string.IsNullOrWhiteSpace(permissionKey)) return false;
            if (!IsAuthenticated || IsLockedOut) return false;

            lock (_syncLock)
            {
                return _permissions.Contains(permissionKey);
            }
        }

        public void NotifyActivity()
        {
            Interlocked.Exchange(ref _lastActivityMs, MonotonicClock.ElapsedMilliseconds);
            _warningFired = false;
        }

        public void SetCurrentUser(IUser? user, IEnumerable<string>? permissions = null)
        {
            IUser? previousUser;
            lock (_syncLock)
            {
                previousUser = CurrentUser;
                CurrentUser = user;
                IsLockedOut = false;
                _permissions.Clear();
                if (permissions != null)
                {
                    foreach (var p in permissions)
                    {
                        if (!string.IsNullOrWhiteSpace(p)) _permissions.Add(p);
                    }
                }
            }

            NotifyActivity();
            UserChanged?.Invoke(this, new UserChangedEventArgs(previousUser, user));
        }

        public void LockSession()
        {
            if (!IsAuthenticated || IsLockedOut) return;

            lock (_syncLock)
            {
                IsLockedOut = true;
            }
            SessionLocked?.Invoke(this, EventArgs.Empty);
        }

        public void UnlockSession()
        {
            if (!IsLockedOut) return;

            lock (_syncLock)
            {
                IsLockedOut = false;
            }
            NotifyActivity();
            SessionUnlocked?.Invoke(this, EventArgs.Empty);
        }

        public void Logout()
        {
            SetCurrentUser(null, null);
        }

        private void CheckIdleTick(object? state)
        {
            if (_disposed || !IsAuthenticated || IsLockedOut || IdleTimeout <= TimeSpan.Zero)
            {
                return;
            }

            var idle = ElapsedIdleTime;

            if (WarningThreshold > TimeSpan.Zero && idle >= (IdleTimeout - WarningThreshold) && idle < IdleTimeout)
            {
                if (!_warningFired)
                {
                    _warningFired = true;
                    var remaining = IdleTimeout - idle;
                    IdleWarning?.Invoke(this, remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero);
                }
            }

            if (idle >= IdleTimeout)
            {
                SessionTimedOut?.Invoke(this, EventArgs.Empty);

                if (TimeoutAction == SessionTimeoutAction.Lock)
                {
                    LockSession();
                }
                else
                {
                    Logout();
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _timer?.Dispose();
        }
    }
}
