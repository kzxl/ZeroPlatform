using System;
using System.Threading.Tasks;
using Xunit;
using ZeroUI.Core.Security;

namespace ZeroUI.Desktop.Tests
{
    public class LoginAndBadgeAuthenticationTests
    {
        #region WinForms Tests

        [Fact]
        public async Task ZLoginDialog_WinForms_PasswordAndBadgeAuthenticationFlow()
        {
            var store = new InMemoryUserStore(seedDefaultIndustrialData: true);
            var session = new SessionContext();

            using var dlg = new ZeroUI.WinForms.Overlays.ZLoginDialog(store, session);

            // Valid password authentication
            bool success = await dlg.ExecuteLoginAsync();
            Assert.True(success);
            Assert.NotNull(dlg.AuthenticatedUser);
            Assert.Equal("admin", dlg.AuthenticatedUser!.Username);
            Assert.True(session.IsAuthenticated);

            // Badge authentication
            bool badgeOk = await dlg.ExecuteBadgeLoginAsync("BADGE-ENG");
            Assert.True(badgeOk);
            Assert.Equal("engineer", dlg.AuthenticatedUser!.Username);

            session.Dispose();
        }

        [Fact]
        public void ZUserStatusBadge_WinForms_ReflectsSessionIdentityChanges()
        {
            var session = new SessionContext();
            var user = new UserModel("usr-test", "operator1", "Line Operator", roles: new[] { "Operator" });

            using var badge = new ZeroUI.WinForms.Feedback.ZUserStatusBadge
            {
                Session = session,
                ShowAvatar = true,
                ShowCountdown = true
            };

            Assert.False(session.IsAuthenticated);

            session.SetCurrentUser(user);
            Assert.True(session.IsAuthenticated);
            Assert.Equal("operator1", session.CurrentUser?.Username);

            session.Logout();
            Assert.False(session.IsAuthenticated);
            session.Dispose();
        }

        #endregion

        #region WPF Tests

        [Fact]
        public async Task ZLoginDialog_Wpf_AuthenticationFlowOnStaThread()
        {
            await Task.Run(() =>
            {
                StaTestRunner.Run(async () =>
                {
                    var store = new InMemoryUserStore(seedDefaultIndustrialData: true);
                    var session = new SessionContext();

                    var dlg = new ZeroUI.Wpf.Overlays.ZLoginDialog(store, session);

                    bool success = await dlg.ExecuteLoginAsync(0);
                    Assert.True(success);
                    Assert.NotNull(dlg.AuthenticatedUser);
                    Assert.Equal("admin", dlg.AuthenticatedUser!.Username);
                    Assert.True(session.IsAuthenticated);

                    bool badgeOk = await dlg.ExecuteBadgeLoginAsync("BADGE-TECH");
                    Assert.True(badgeOk);
                    Assert.Equal("tech", dlg.AuthenticatedUser!.Username);

                    session.Dispose();
                });
            });
        }

        [Fact]
        public void ZUserStatusBadge_Wpf_InstantiatesAndRenders()
        {
            StaTestRunner.Run(() =>
            {
                var session = new SessionContext();
                var user = new UserModel("usr-wpf", "wpf_operator", "WPF Operator", roles: new[] { "Engineer" });
                session.SetCurrentUser(user);

                var badge = new ZeroUI.Wpf.Feedback.ZUserStatusBadge
                {
                    Session = session,
                    ShowAvatar = true,
                    ShowCountdown = true
                };

                Assert.True(badge.ShowAvatar);
                Assert.True(badge.ShowCountdown);
                Assert.Equal("wpf_operator", session.CurrentUser?.Username);

                session.Dispose();
            });
        }

        #endregion
    }
}
