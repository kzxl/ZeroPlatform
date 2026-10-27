using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Xunit;
using ZeroUI.Core.Security;

namespace ZeroUI.Desktop.Tests
{
    public class RoleMatrixAndUserManagerTests
    {
        #region WinForms Tests

        [Fact]
        public async Task ZRoleMatrix_WinForms_LoadToggleAndSaveFlow()
        {
            var store = new InMemoryUserStore(seedDefaultIndustrialData: true);
            using var matrix = new ZeroUI.WinForms.Security.ZRoleMatrix();

            await matrix.LoadAsync(store);

            Assert.NotEmpty(matrix.CurrentRoles);
            Assert.NotEmpty(matrix.CurrentPermissions);

            // Initially Operator doesn't have Parameters.Write
            Assert.False(matrix.IsGranted("Operator", "Parameters.Write"));

            // Grant permission
            bool eventFired = false;
            matrix.PermissionToggled += (s, e) =>
            {
                if (e.RoleId == "Operator" && e.PermissionKey == "Parameters.Write")
                    eventFired = true;
            };

            matrix.SetGranted("Operator", "Parameters.Write", true);
            Assert.True(eventFired);
            Assert.True(matrix.IsGranted("Operator", "Parameters.Write"));

            // Save to store
            await matrix.SaveAsync(store);

            // Verify in store
            var opPerms = await store.GetPermissionsForRolesAsync(new[] { "Operator" });
            Assert.Contains("Parameters.Write", opPerms);
        }

        [Fact]
        public async Task ZUserManager_WinForms_CrudAndLockFlow()
        {
            var store = new InMemoryUserStore(seedDefaultIndustrialData: true);
            using var mgr = new ZeroUI.WinForms.Security.ZUserManager();

            await mgr.LoadAsync(store);
            Assert.NotNull(mgr.SelectedUser);

            // Create new user
            var newUser = new UserModel("usr-new", "tech2", "Shift Technician 2", badgeId: "BADGE-T2", roles: new[] { "Maintenance" });
            bool saved = await mgr.AddOrUpdateUserAsync(newUser, password: "pw2", pin: "2222");
            Assert.True(saved);

            var found = await store.FindByUsernameAsync("tech2");
            Assert.NotNull(found);
            Assert.Equal("Shift Technician 2", found!.DisplayName);

            // Lock toggle
            var loadedUser = await store.FindByIdAsync("usr-new");
            Assert.False(loadedUser!.IsLocked);

            var lockedModel = new UserModel(loadedUser.Id, loadedUser.Username, loadedUser.DisplayName, isLocked: true, roles: loadedUser.Roles);
            await mgr.AddOrUpdateUserAsync(lockedModel);

            var afterLock = await store.FindByIdAsync("usr-new");
            Assert.True(afterLock!.IsLocked);
        }

        [Fact]
        public void ZAuthorizeExtender_WinForms_SecurityEvaluation()
        {
            var session = new SessionContext();
            using var btnAdminOnly = new Button();
            using var btnTuning = new Button();

            using var extender = new ZeroUI.WinForms.Security.ZAuthorizeExtender();
            extender.Session = session;
            extender.SetRequiredRole(btnAdminOnly, "Administrator");
            extender.SetRequiredPermission(btnTuning, "Parameters.Write");

            // Before sign in
            session.SetCurrentUser(null);
            extender.EvaluateAll();
            Assert.False(btnAdminOnly.Enabled);
            Assert.False(btnTuning.Enabled);

            // Sign in as Operator (has no admin, no tuning)
            var opUser = new UserModel("op", "op", "Op", roles: new[] { "Operator" });
            session.SetCurrentUser(opUser, new[] { "Alarms.View" });
            extender.EvaluateAll();
            Assert.False(btnAdminOnly.Enabled);
            Assert.False(btnTuning.Enabled);

            // Sign in as Admin (has both)
            var adminUser = new UserModel("adm", "adm", "Admin", roles: new[] { "Administrator" });
            session.SetCurrentUser(adminUser, new[] { "Parameters.Write" });
            extender.EvaluateAll();
            Assert.True(btnAdminOnly.Enabled);
            Assert.True(btnTuning.Enabled);

            session.Dispose();
        }

        [Fact]
        public async Task ZOperationLogViewer_WinForms_LoadAndExportCsv()
        {
            var store = new InMemoryUserStore(seedDefaultIndustrialData: true);
            store.Log(new OperationLogEntry("Test.Action", "operator1", "Motor1", "0", "1", OperationLogLevel.Info, "Manual", "Jog motor"));

            using var viewer = new ZeroUI.WinForms.Security.ZOperationLogViewer();
            await viewer.LoadLogsAsync(store);

            Assert.NotEmpty(viewer.Entries);
            Assert.Contains(viewer.Entries, e => e.Action == "Test.Action");

            // Export to CSV
            string tmpFile = Path.Combine(Path.GetTempPath(), $"audit_test_{Guid.NewGuid():N}.csv");
            try
            {
                viewer.ExportToCsv(tmpFile);
                Assert.True(File.Exists(tmpFile));
                string text = File.ReadAllText(tmpFile);
                Assert.Contains("Test.Action", text);
                Assert.Contains("operator1", text);
            }
            finally
            {
                if (File.Exists(tmpFile)) File.Delete(tmpFile);
            }
        }

        #endregion

        #region WPF Tests

        [Fact]
        public async Task ZRoleMatrix_Wpf_LoadAndToggleOnStaThread()
        {
            await Task.Run(() =>
            {
                StaTestRunner.Run(async () =>
                {
                    var store = new InMemoryUserStore(seedDefaultIndustrialData: true);
                    var matrix = new ZeroUI.Wpf.Security.ZRoleMatrix();

                    await matrix.LoadAsync(store);

                    Assert.NotEmpty(matrix.CurrentRoles);
                    Assert.NotEmpty(matrix.CurrentPermissions);

                    matrix.SetGranted("Operator", "Parameters.Write", true);
                    Assert.True(matrix.IsGranted("Operator", "Parameters.Write"));

                    await matrix.SaveAsync(store);
                    var perms = await store.GetPermissionsForRolesAsync(new[] { "Operator" });
                    Assert.Contains("Parameters.Write", perms);
                });
            });
        }

        [Fact]
        public async Task ZUserManager_Wpf_LoadAndSelectOnStaThread()
        {
            await Task.Run(() =>
            {
                StaTestRunner.Run(async () =>
                {
                    var store = new InMemoryUserStore(seedDefaultIndustrialData: true);
                    var mgr = new ZeroUI.Wpf.Security.ZUserManager();

                    await mgr.LoadAsync(store);
                    Assert.NotNull(mgr.SelectedUser);
                    Assert.Equal("admin", mgr.SelectedUser!.Username);

                    var newUser = new UserModel("usr-wpf", "wpf_lead", "WPF Lead", roles: new[] { "Engineer" });
                    await mgr.AddOrUpdateUserAsync(newUser);

                    var found = await store.FindByIdAsync("usr-wpf");
                    Assert.NotNull(found);
                });
            });
        }

        [Fact]
        public void ZAuthorize_Wpf_AttachedSecurityEvaluation()
        {
            StaTestRunner.Run(() =>
            {
                var session = new SessionContext();
                ZeroUI.Wpf.Security.ZAuthorize.Session = session;
                var btn = new System.Windows.Controls.Button();

                ZeroUI.Wpf.Security.ZAuthorize.SetRequiredRole(btn, "Administrator");
                ZeroUI.Wpf.Security.ZAuthorize.SetBehavior(btn, ZeroUI.Wpf.Security.UnauthorizedBehavior.Disable);

                // No user
                session.SetCurrentUser(null);
                ZeroUI.Wpf.Security.ZAuthorize.EvaluateElement(btn);
                Assert.False(btn.IsEnabled);

                // Admin signed in
                var admin = new UserModel("adm", "admin", "Admin", roles: new[] { "Administrator" });
                session.SetCurrentUser(admin);
                ZeroUI.Wpf.Security.ZAuthorize.EvaluateElement(btn);
                Assert.True(btn.IsEnabled);

                session.Dispose();
                ZeroUI.Wpf.Security.ZAuthorize.Session = null;
            });
        }

        [Fact]
        public async Task ZOperationLogViewer_Wpf_LoadAndExportCsv()
        {
            await Task.Run(() =>
            {
                StaTestRunner.Run(async () =>
                {
                    var store = new InMemoryUserStore(seedDefaultIndustrialData: true);
                    store.Log(new OperationLogEntry("Wpf.Action", "engineer", "Heater", "off", "on", OperationLogLevel.Warning, "Process"));

                    var viewer = new ZeroUI.Wpf.Security.ZOperationLogViewer();
                    await viewer.LoadLogsAsync(store);

                    Assert.NotEmpty(viewer.Entries);
                    Assert.Contains(viewer.Entries, e => e.Action == "Wpf.Action");

                    string tmpFile = Path.Combine(Path.GetTempPath(), $"audit_wpf_{Guid.NewGuid():N}.csv");
                    try
                    {
                        viewer.ExportToCsv(tmpFile);
                        Assert.True(File.Exists(tmpFile));
                        string text = File.ReadAllText(tmpFile);
                        Assert.Contains("Wpf.Action", text);
                    }
                    finally
                    {
                        if (File.Exists(tmpFile)) File.Delete(tmpFile);
                    }
                });
            });
        }

        #endregion
    }
}
