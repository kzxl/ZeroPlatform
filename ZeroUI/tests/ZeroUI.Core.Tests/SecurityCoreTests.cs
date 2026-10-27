using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using ZeroUI.Core.Security;

namespace ZeroUI.Core.Tests
{
    public class SecurityCoreTests : IDisposable
    {
        private readonly InMemoryUserStore _store;
        private readonly SessionContext _session;

        public SecurityCoreTests()
        {
            _store = new InMemoryUserStore(seedDefaultIndustrialData: true);
            _session = new SessionContext
            {
                IdleTimeout = TimeSpan.FromMinutes(10),
                WarningThreshold = TimeSpan.FromSeconds(30)
            };
        }

        public void Dispose()
        {
            _session.Dispose();
        }

        [Fact]
        public async Task DefaultSeed_InitializesExpectedIndustrialRolesAndUsers()
        {
            var users = await _store.GetUsersAsync();
            Assert.NotEmpty(users);
            Assert.Contains(users, u => u.Username == "admin");
            Assert.Contains(users, u => u.Username == "engineer");
            Assert.Contains(users, u => u.Username == "tech");
            Assert.Contains(users, u => u.Username == "operator1");

            var roles = await _store.GetRolesAsync();
            Assert.NotEmpty(roles);
            Assert.Contains(roles, r => r.Id == "Administrator");
            Assert.Contains(roles, r => r.Id == "Engineer");
            Assert.Contains(roles, r => r.Id == "Maintenance");
            Assert.Contains(roles, r => r.Id == "Operator");
            Assert.Contains(roles, r => r.Id == "Guest");

            var permissions = await _store.GetAllPermissionsAsync();
            Assert.NotEmpty(permissions);
            Assert.Contains(permissions, p => p.Key == "Alarms.Acknowledge");
            Assert.Contains(permissions, p => p.Key == "Parameters.Write");
            Assert.Contains(permissions, p => p.Key == "Manual.Override");
        }

        [Fact]
        public async Task AuthenticatePassword_ValidCredentials_Succeeds()
        {
            var result = await _store.AuthenticatePasswordAsync("admin", "admin");

            Assert.True(result.Succeeded);
            Assert.NotNull(result.User);
            Assert.Equal("admin", result.User!.Username);
            Assert.Equal(AuthFailureReason.None, result.FailureReason);
            Assert.Null(result.ErrorMessage);
        }

        [Fact]
        public async Task AuthenticatePassword_InvalidPassword_Fails()
        {
            var result = await _store.AuthenticatePasswordAsync("admin", "wrong_pass");

            Assert.False(result.Succeeded);
            Assert.Null(result.User);
            Assert.Equal(AuthFailureReason.InvalidCredentials, result.FailureReason);
            Assert.False(string.IsNullOrEmpty(result.ErrorMessage));
        }

        [Fact]
        public async Task AuthenticatePassword_NonExistentUser_FailsWithUserNotFound()
        {
            var result = await _store.AuthenticatePasswordAsync("unknown_operator", "pass");

            Assert.False(result.Succeeded);
            Assert.Equal(AuthFailureReason.UserNotFound, result.FailureReason);
        }

        [Fact]
        public async Task AuthenticatePassword_LockedUser_FailsWithAccountLocked()
        {
            var user = await _store.FindByUsernameAsync("operator1");
            Assert.NotNull(user);

            var lockedUser = new UserModel(user!.Id, user.Username, user.DisplayName, isLocked: true, roles: user.Roles);
            await _store.SaveUserAsync(lockedUser);

            var result = await _store.AuthenticatePasswordAsync("operator1", "op");

            Assert.False(result.Succeeded);
            Assert.Equal(AuthFailureReason.AccountLocked, result.FailureReason);
        }

        [Fact]
        public async Task AuthenticateBadge_ValidRfidCard_Succeeds()
        {
            var result = await _store.AuthenticateBadgeAsync("BADGE-ADMIN");

            Assert.True(result.Succeeded);
            Assert.NotNull(result.User);
            Assert.Equal("admin", result.User!.Username);
        }

        [Fact]
        public async Task AuthenticateBadge_UnregisteredCard_FailsWithBadgeNotRecognized()
        {
            var result = await _store.AuthenticateBadgeAsync("UNKNOWN-RFID-999");

            Assert.False(result.Succeeded);
            Assert.Equal(AuthFailureReason.BadgeNotRecognized, result.FailureReason);
        }

        [Fact]
        public async Task AuthenticatePin_ValidPin_Succeeds()
        {
            var result = await _store.AuthenticatePinAsync("engineer", "1234");

            Assert.True(result.Succeeded);
            Assert.NotNull(result.User);
            Assert.Equal("engineer", result.User!.Username);
        }

        [Fact]
        public async Task AuthenticatePin_InvalidPin_Fails()
        {
            var result = await _store.AuthenticatePinAsync("engineer", "9999");

            Assert.False(result.Succeeded);
            Assert.Equal(AuthFailureReason.PinIncorrect, result.FailureReason);
        }

        [Fact]
        public async Task UserCrud_Operations_Succeed()
        {
            var newUser = new UserModel("usr-qa", "qa_lead", "QA Lead", "qa@zeroui.internal", "BADGE-QA", "8888", false, null, new[] { "Operator" });
            bool saved = await _store.SaveUserAsync(newUser);
            Assert.True(saved);

            var found = await _store.FindByIdAsync("usr-qa");
            Assert.NotNull(found);
            Assert.Equal("qa_lead", found!.Username);

            await _store.SetPasswordAsync("usr-qa", "qapass");
            var auth = await _store.AuthenticatePasswordAsync("qa_lead", "qapass");
            Assert.True(auth.Succeeded);

            bool deleted = await _store.DeleteUserAsync("usr-qa");
            Assert.True(deleted);

            var afterDelete = await _store.FindByIdAsync("usr-qa");
            Assert.Null(afterDelete);
        }

        [Fact]
        public async Task RoleStore_PermissionsAggregation_Succeeds()
        {
            var roles = await _store.GetRolesAsync();
            var opRole = roles.First(r => r.Id == "Operator");
            Assert.NotNull(opRole);

            var permissions = await _store.GetPermissionsForRolesAsync(new[] { "Operator" });
            Assert.Contains("Alarms.Acknowledge", permissions);
            Assert.DoesNotContain("Parameters.Write", permissions);

            var adminPerms = await _store.GetPermissionsForRolesAsync(new[] { "Administrator" });
            Assert.Contains("Parameters.Write", adminPerms);
            Assert.Contains("Manual.Override", adminPerms);
        }

        [Fact]
        public void SessionContext_UserLifecycleAndPermissions_WorkCorrectly()
        {
            var userChanges = new System.Collections.Generic.List<UserChangedEventArgs>();
            _session.UserChanged += (s, e) => userChanges.Add(e);

            var user = new UserModel("usr-admin", "admin", "Admin", roles: new[] { "Administrator" });
            var perms = new[] { "Alarms.View", "Alarms.Acknowledge", "Parameters.Write" };

            _session.SetCurrentUser(user, perms);

            Assert.Single(userChanges);
            Assert.Equal("admin", userChanges[0].NewUser?.Username);
            Assert.Null(userChanges[0].PreviousUser);
            Assert.True(_session.IsAuthenticated);
            Assert.False(_session.IsLockedOut);
            Assert.Equal("admin", _session.CurrentUser?.Username);
            Assert.True(_session.HasRole("Administrator"));
            Assert.False(_session.HasRole("Operator"));
            Assert.True(_session.HasPermission("Parameters.Write"));
            Assert.False(_session.HasPermission("Recipe.Edit"));

            // Lock session
            bool lockedFired = false;
            _session.SessionLocked += (s, e) => lockedFired = true;
            _session.LockSession();

            Assert.True(lockedFired);
            Assert.True(_session.IsLockedOut);
            // While locked, permissions should be suspended
            Assert.False(_session.HasPermission("Parameters.Write"));

            // Unlock session
            bool unlockedFired = false;
            _session.SessionUnlocked += (s, e) => unlockedFired = true;
            _session.UnlockSession();

            Assert.True(unlockedFired);
            Assert.False(_session.IsLockedOut);
            Assert.True(_session.HasPermission("Parameters.Write"));

            // Logout
            _session.Logout();
            Assert.False(_session.IsAuthenticated);
            Assert.Null(_session.CurrentUser);
            Assert.Empty(_session.CurrentPermissions);
            Assert.Equal(2, userChanges.Count);
            Assert.Null(userChanges[1].NewUser);
            Assert.Equal("admin", userChanges[1].PreviousUser?.Username);
        }

        [Fact]
        public async Task OperationLogger_AuditTrail_RecordsAndQueriesCorrectly()
        {
            _store.Log(new OperationLogEntry("Setpoint.Change", "engineer", "Reactor.Temp", "75.0", "80.0", OperationLogLevel.Warning, "Process", "Manual batch adjustment"));
            _store.Log(new OperationLogEntry("Alarm.Acknowledge", "operator1", "Alarm.PressureHigh", null, null, OperationLogLevel.Info, "Alarms", "Silenced buzzer"));

            var allLogs = await _store.QueryLogsAsync();
            Assert.True(allLogs.Count >= 2);

            var filter = new OperationLogFilter
            {
                Category = "Process"
            };
            var processLogs = await _store.QueryLogsAsync(filter);
            Assert.Contains(processLogs, l => l.Action == "Setpoint.Change");
            Assert.DoesNotContain(processLogs, l => l.Category == "Alarms");

            var filterUser = new OperationLogFilter
            {
                Username = "operator1"
            };
            var userLogs = await _store.QueryLogsAsync(filterUser);
            Assert.All(userLogs, l => Assert.Equal("operator1", l.Username));
        }
    }
}
