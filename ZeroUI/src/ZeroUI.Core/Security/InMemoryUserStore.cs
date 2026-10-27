using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroUI.Core.Security
{
    /// <summary>
    /// Thread-safe in-memory provider combining <see cref="IUserStore"/>, <see cref="IRoleStore"/>,
    /// <see cref="IAuthenticationProvider"/>, and <see cref="IOperationLogger"/>.
    /// Pre-configured with standard industrial roles, permissions, and operator credentials.
    /// Ideal for offline HMI runtime, unit testing, and demonstration suites.
    /// </summary>
    public class InMemoryUserStore : IUserStore, IRoleStore, IAuthenticationProvider, IOperationLogger
    {
        private readonly ConcurrentDictionary<string, UserModel> _users = new ConcurrentDictionary<string, UserModel>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, string> _passwords = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, string> _pins = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, RoleModel> _roles = new ConcurrentDictionary<string, RoleModel>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, PermissionModel> _permissions = new ConcurrentDictionary<string, PermissionModel>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentQueue<OperationLogEntry> _auditLogs = new ConcurrentQueue<OperationLogEntry>();

        public InMemoryUserStore(bool seedDefaultIndustrialData = true)
        {
            if (seedDefaultIndustrialData)
            {
                SeedDefaults();
            }
        }

        #region Default Seeding

        private void SeedDefaults()
        {
            // Standard Industrial Permissions
            RegisterPermission(new PermissionModel("Alarms.View", "View Alarms", "Alarms", "Allow viewing active and historic alarms"));
            RegisterPermission(new PermissionModel("Alarms.Acknowledge", "Acknowledge Alarms", "Alarms", "Allow silencing and acknowledging alarms"));
            RegisterPermission(new PermissionModel("Alarms.Configure", "Configure Alarms", "Alarms", "Allow editing alarm trip points and deadbands"));

            RegisterPermission(new PermissionModel("Process.Start", "Start Production", "Process", "Allow starting automated batch sequences"));
            RegisterPermission(new PermissionModel("Process.Stop", "Stop Production", "Process", "Allow stopping automated batch sequences"));
            RegisterPermission(new PermissionModel("Parameters.Write", "Modify Parameters", "Process", "Allow writing analog tuning parameters and setpoints"));

            RegisterPermission(new PermissionModel("Recipe.Select", "Select Recipe", "Recipe", "Allow loading existing production recipes"));
            RegisterPermission(new PermissionModel("Recipe.Edit", "Edit Recipes", "Recipe", "Allow modifying and saving recipe parameters"));

            RegisterPermission(new PermissionModel("Manual.Override", "Manual Override", "Manual", "Allow forcing digital outputs and motor jog"));
            RegisterPermission(new PermissionModel("Security.UserManage", "Manage Users", "Security", "Allow creating, locking, and resetting users"));
            RegisterPermission(new PermissionModel("Security.RoleManage", "Manage Roles", "Security", "Allow modifying role permission assignments"));
            RegisterPermission(new PermissionModel("Audit.View", "View Audit Logs", "Audit", "Allow viewing operation and compliance audit trails"));

            // Standard Industrial Roles
            _roles["Administrator"] = new RoleModel(
                "Administrator", "System Administrator", "Full sovereign control over all machine and safety parameters", 100,
                _permissions.Keys.ToList());

            _roles["Engineer"] = new RoleModel(
                "Engineer", "Process Engineer", "Access to recipes, parameters, alarm trip points, and diagnostics", 80,
                new[] { "Alarms.View", "Alarms.Acknowledge", "Alarms.Configure", "Process.Start", "Process.Stop", "Parameters.Write", "Recipe.Select", "Recipe.Edit", "Audit.View" });

            _roles["Maintenance"] = new RoleModel(
                "Maintenance", "Maintenance Technician", "Access to manual motor jog, interlock override, and sensor diagnostics", 50,
                new[] { "Alarms.View", "Alarms.Acknowledge", "Manual.Override", "Audit.View" });

            _roles["Operator"] = new RoleModel(
                "Operator", "Line Operator", "Standard operation, alarm acknowledgment, and recipe selection", 20,
                new[] { "Alarms.View", "Alarms.Acknowledge", "Process.Start", "Process.Stop", "Recipe.Select" });

            _roles["Guest"] = new RoleModel(
                "Guest", "Read-Only Guest", "View-only telemetry access without control authority", 10,
                new[] { "Alarms.View" });

            // Standard Seed Users
            CreateSeedUser("usr-admin", "admin", "Administrator", "admin@zeroui.internal", "BADGE-ADMIN", "9999", "admin", new[] { "Administrator" });
            CreateSeedUser("usr-eng", "engineer", "Chief Engineer", "engineer@zeroui.internal", "BADGE-ENG", "1234", "engineer", new[] { "Engineer" });
            CreateSeedUser("usr-tech", "tech", "Line Technician", "tech@zeroui.internal", "BADGE-TECH", "5555", "tech", new[] { "Maintenance" });
            CreateSeedUser("usr-op1", "operator1", "Shift Operator 1", "op1@zeroui.internal", "BADGE-OP1", "0000", "operator", new[] { "Operator" });
        }

        private void RegisterPermission(PermissionModel permission)
        {
            _permissions[permission.Key] = permission;
        }

        private void CreateSeedUser(string id, string username, string displayName, string email, string badgeId, string pin, string password, string[] roles)
        {
            var user = new UserModel(id, username, displayName, email, badgeId, pin, false, null, roles);
            _users[id] = user;
            _passwords[id] = password;
            _pins[id] = pin;
        }

        #endregion

        #region IUserStore Implementation

        public Task<IReadOnlyList<IUser>> GetUsersAsync(CancellationToken cancellationToken = default)
        {
            var list = _users.Values.Cast<IUser>().ToList();
            return Task.FromResult<IReadOnlyList<IUser>>(list);
        }

        public Task<IUser?> FindByIdAsync(string userId, CancellationToken cancellationToken = default)
        {
            _users.TryGetValue(userId, out var user);
            return Task.FromResult<IUser?>(user);
        }

        public Task<IUser?> FindByUsernameAsync(string username, CancellationToken cancellationToken = default)
        {
            var user = _users.Values.FirstOrDefault(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult<IUser?>(user);
        }

        public Task<IUser?> FindByBadgeIdAsync(string badgeId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(badgeId)) return Task.FromResult<IUser?>(null);
            var user = _users.Values.FirstOrDefault(u => string.Equals(u.BadgeId, badgeId, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult<IUser?>(user);
        }

        public Task<bool> SaveUserAsync(IUser user, CancellationToken cancellationToken = default)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));

            var model = user as UserModel ?? new UserModel(
                user.Id, user.Username, user.DisplayName, user.Email, user.BadgeId, user.PinCodeHash,
                user.IsLocked, user.LastLoginUtc, user.Roles, user.CustomAttributes);

            _users[model.Id] = model;
            return Task.FromResult(true);
        }

        public Task<bool> DeleteUserAsync(string userId, CancellationToken cancellationToken = default)
        {
            bool removed = _users.TryRemove(userId, out _);
            _passwords.TryRemove(userId, out _);
            _pins.TryRemove(userId, out _);
            return Task.FromResult(removed);
        }

        public Task<bool> VerifyPasswordAsync(string userId, string rawPassword, CancellationToken cancellationToken = default)
        {
            if (_passwords.TryGetValue(userId, out var storedPassword))
            {
                return Task.FromResult(string.Equals(storedPassword, rawPassword, StringComparison.Ordinal));
            }
            return Task.FromResult(false);
        }

        public Task<bool> VerifyPinAsync(string userId, string pinCode, CancellationToken cancellationToken = default)
        {
            if (_pins.TryGetValue(userId, out var storedPin))
            {
                return Task.FromResult(string.Equals(storedPin, pinCode, StringComparison.Ordinal));
            }
            return Task.FromResult(false);
        }

        public Task<bool> SetPasswordAsync(string userId, string newPassword, CancellationToken cancellationToken = default)
        {
            _passwords[userId] = newPassword;
            return Task.FromResult(true);
        }

        public Task<bool> SetPinAsync(string userId, string newPinCode, CancellationToken cancellationToken = default)
        {
            _pins[userId] = newPinCode;
            return Task.FromResult(true);
        }

        #endregion

        #region IRoleStore Implementation

        public Task<IReadOnlyList<IRole>> GetRolesAsync(CancellationToken cancellationToken = default)
        {
            var list = _roles.Values.Cast<IRole>().OrderByDescending(r => r.SecurityLevel).ToList();
            return Task.FromResult<IReadOnlyList<IRole>>(list);
        }

        public Task<IRole?> FindRoleByIdAsync(string roleId, CancellationToken cancellationToken = default)
        {
            _roles.TryGetValue(roleId, out var role);
            return Task.FromResult<IRole?>(role);
        }

        public Task<bool> SaveRoleAsync(IRole role, CancellationToken cancellationToken = default)
        {
            if (role == null) throw new ArgumentNullException(nameof(role));
            var model = role as RoleModel ?? new RoleModel(role.Id, role.Name, role.Description, role.SecurityLevel, role.Permissions);
            _roles[model.Id] = model;
            return Task.FromResult(true);
        }

        public Task<bool> DeleteRoleAsync(string roleId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_roles.TryRemove(roleId, out _));
        }

        public Task<IReadOnlyList<IPermission>> GetAllPermissionsAsync(CancellationToken cancellationToken = default)
        {
            var list = _permissions.Values.Cast<IPermission>().ToList();
            return Task.FromResult<IReadOnlyList<IPermission>>(list);
        }

        public Task<IReadOnlyList<string>> GetPermissionsForRolesAsync(IEnumerable<string> roleIds, CancellationToken cancellationToken = default)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (roleIds != null)
            {
                foreach (var id in roleIds)
                {
                    if (_roles.TryGetValue(id, out var role))
                    {
                        for (int i = 0; i < role.Permissions.Count; i++)
                        {
                            set.Add(role.Permissions[i]);
                        }
                    }
                }
            }
            return Task.FromResult<IReadOnlyList<string>>(set.ToList());
        }

        #endregion

        #region IAuthenticationProvider Implementation

        public async Task<AuthResult> AuthenticatePasswordAsync(string username, string password, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return AuthResult.Failure("Username cannot be empty", AuthFailureReason.InvalidCredentials);
            }

            var user = await FindByUsernameAsync(username, cancellationToken).ConfigureAwait(false);
            if (user == null)
            {
                return AuthResult.Failure("User not found", AuthFailureReason.UserNotFound);
            }

            if (user.IsLocked)
            {
                return AuthResult.Failure("Account is locked by administrator", AuthFailureReason.AccountLocked);
            }

            bool valid = await VerifyPasswordAsync(user.Id, password, cancellationToken).ConfigureAwait(false);
            if (!valid)
            {
                return AuthResult.Failure("Invalid password", AuthFailureReason.InvalidCredentials);
            }

            UpdateLastLogin(user.Id);
            Log(new OperationLogEntry("Login.Password", user.Username, "Security", null, null, OperationLogLevel.SecurityAudit, "Security", "Successful operator login via password"));
            return AuthResult.Success(user);
        }

        public async Task<AuthResult> AuthenticateBadgeAsync(string badgeId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(badgeId))
            {
                return AuthResult.Failure("Badge ID cannot be empty", AuthFailureReason.BadgeNotRecognized);
            }

            var user = await FindByBadgeIdAsync(badgeId, cancellationToken).ConfigureAwait(false);
            if (user == null)
            {
                return AuthResult.Failure($"Badge '{badgeId}' not recognized", AuthFailureReason.BadgeNotRecognized);
            }

            if (user.IsLocked)
            {
                return AuthResult.Failure("Account is locked by administrator", AuthFailureReason.AccountLocked);
            }

            UpdateLastLogin(user.Id);
            Log(new OperationLogEntry("Login.Badge", user.Username, "Security", null, badgeId, OperationLogLevel.SecurityAudit, "Security", "Successful operator badge swipe"));
            return AuthResult.Success(user);
        }

        public async Task<AuthResult> AuthenticatePinAsync(string usernameOrUserId, string pinCode, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(usernameOrUserId))
            {
                return AuthResult.Failure("User identifier cannot be empty", AuthFailureReason.UserNotFound);
            }

            var user = await FindByIdAsync(usernameOrUserId, cancellationToken).ConfigureAwait(false)
                       ?? await FindByUsernameAsync(usernameOrUserId, cancellationToken).ConfigureAwait(false);

            if (user == null)
            {
                return AuthResult.Failure("User not found", AuthFailureReason.UserNotFound);
            }

            if (user.IsLocked)
            {
                return AuthResult.Failure("Account is locked by administrator", AuthFailureReason.AccountLocked);
            }

            bool valid = await VerifyPinAsync(user.Id, pinCode, cancellationToken).ConfigureAwait(false);
            if (!valid)
            {
                return AuthResult.Failure("Incorrect PIN code", AuthFailureReason.PinIncorrect);
            }

            UpdateLastLogin(user.Id);
            Log(new OperationLogEntry("Login.Pin", user.Username, "Security", null, null, OperationLogLevel.SecurityAudit, "Security", "Successful PIN unlock"));
            return AuthResult.Success(user);
        }

        private void UpdateLastLogin(string userId)
        {
            if (_users.TryGetValue(userId, out var user))
            {
                user.LastLoginUtc = DateTime.UtcNow;
            }
        }

        #endregion

        #region IOperationLogger Implementation

        public void Log(OperationLogEntry entry)
        {
            if (entry == null) return;
            _auditLogs.Enqueue(entry);

            // Limit memory audit logs to 10,000 entries
            while (_auditLogs.Count > 10000)
            {
                _auditLogs.TryDequeue(out _);
            }
        }

        public Task<IReadOnlyList<OperationLogEntry>> QueryLogsAsync(OperationLogFilter? filter = null, CancellationToken cancellationToken = default)
        {
            IEnumerable<OperationLogEntry> query = _auditLogs;

            if (filter != null)
            {
                if (filter.FromUtc.HasValue)
                    query = query.Where(e => e.TimestampUtc >= filter.FromUtc.Value);
                if (filter.ToUtc.HasValue)
                    query = query.Where(e => e.TimestampUtc <= filter.ToUtc.Value);
                if (!string.IsNullOrWhiteSpace(filter.Username))
                    query = query.Where(e => string.Equals(e.Username, filter.Username, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(filter.Action))
                    query = query.Where(e => string.Equals(e.Action, filter.Action, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(filter.Category))
                    query = query.Where(e => string.Equals(e.Category, filter.Category, StringComparison.OrdinalIgnoreCase));
                if (filter.MinLevel.HasValue)
                    query = query.Where(e => e.Level >= filter.MinLevel.Value);

                query = query.Take(filter.MaxResults);
            }

            var list = query.OrderByDescending(e => e.TimestampUtc).ToList();
            return Task.FromResult<IReadOnlyList<OperationLogEntry>>(list);
        }

        #endregion
    }
}
