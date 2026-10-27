// <auto-refactored> Partial class file - Auth </auto-refactored>
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Input;
using ZeroUI.Core.Security;
using ZeroUI.Core.Theme;
using ZeroUI.Wpf.Input;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Overlays
{
    public partial class ZLoginDialog
    {
        public async Task<bool> ExecuteLoginAsync(int mode = 0)
        {
            SetStatus("Authenticating...", StatusState.Authenticating);

            AuthResult result;
            if (mode == 0)
            {
                result = await _authProvider.AuthenticatePasswordAsync(_txtUsername.Text.Trim(), _txtPassword.Password);
            }
            else
            {
                result = await _authProvider.AuthenticatePinAsync("admin", _txtPin.Password);
            }

            if (result.Succeeded && result.User != null)
            {
                AuthenticatedUser = result.User;
                _session.SetCurrentUser(result.User);
                try { DialogResult = true; } catch (InvalidOperationException) { }
                Close();
                return true;
            }
            else
            {
                SetStatus(result.ErrorMessage ?? "Authentication failed", StatusState.Error);
                return false;
            }
        }

        public async Task<bool> ExecuteBadgeLoginAsync(string badgeId)
        {
            SetStatus($"Validating Badge '{badgeId}'...", StatusState.Authenticating);
            var result = await _authProvider.AuthenticateBadgeAsync(badgeId);
            if (result.Succeeded && result.User != null)
            {
                AuthenticatedUser = result.User;
                _session.SetCurrentUser(result.User);
                try { DialogResult = true; } catch (InvalidOperationException) { }
                Close();
                return true;
            }
            else
            {
                SetStatus(result.ErrorMessage ?? "Badge not recognized", StatusState.Error);
                return false;
            }
        }

        public static bool? ShowLogin(IAuthenticationProvider? authProvider = null, Window? owner = null)
        {
            var dlg = new ZLoginDialog(authProvider) { Owner = owner };
            return dlg.ShowDialog();
        }
    }
}
