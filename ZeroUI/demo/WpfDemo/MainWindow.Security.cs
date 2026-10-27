using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using ZeroUI.Core.Input;
using ZeroUI.Core.Security;
using ZeroUI.Wpf.Overlays;
using ZeroUI.Wpf.Security;

namespace ZeroUI.Samples.WpfDemo
{
    public partial class MainWindow
    {
        private readonly InMemoryUserStore _wpfSecurityStore = new InMemoryUserStore(seedDefaultIndustrialData: true);
        private readonly SessionContext _wpfSession = SessionContext.Current;

        private void SetupSecurityDemo()
        {
            if (_wpfSession.CurrentUser == null)
            {
                var admin = _wpfSecurityStore.FindByIdAsync("usr-admin").GetAwaiter().GetResult();
                if (admin != null)
                {
                    var perms = _wpfSecurityStore.GetPermissionsForRolesAsync(admin.Roles).GetAwaiter().GetResult();
                    _wpfSession.SetCurrentUser(admin, perms);
                }
            }

            WpfUserBadge.Session = _wpfSession;
            _ = WpfRoleMatrix.LoadAsync(_wpfSecurityStore);
            _ = WpfUserManager.LoadAsync(_wpfSecurityStore, _wpfSecurityStore);
            _ = WpfLogViewer.LoadLogsAsync(_wpfSecurityStore);

            WpfEmbeddedKbd.KeyPressed += (s, e) =>
            {
                if (e.KeyType == VirtualKeyType.Character)
                {
                    TxtWpfKbdPreview.AppendText(e.Text);
                }
                else if (e.KeyType == VirtualKeyType.Backspace && TxtWpfKbdPreview.Text.Length > 7)
                {
                    TxtWpfKbdPreview.Text = TxtWpfKbdPreview.Text.Substring(0, TxtWpfKbdPreview.Text.Length - 1);
                }
            };

            EvaluateWpfAuthorizations();
        }

        private void EvaluateWpfAuthorizations()
        {
            ZAuthorize.EvaluateElement(BtnWpfAdminAction);
            ZAuthorize.EvaluateElement(BtnWpfEngAction);
            ZAuthorize.EvaluateElement(BtnWpfOpAction);
            ZAuthorize.EvaluateElement(BtnWpfExportAction);
        }

        private void BtnWpfSwitchAdmin_Click(object sender, RoutedEventArgs e) => SwitchWpfUser("usr-admin");
        private void BtnWpfSwitchEng_Click(object sender, RoutedEventArgs e) => SwitchWpfUser("usr-engineer");
        private void BtnWpfSwitchOp_Click(object sender, RoutedEventArgs e) => SwitchWpfUser("usr-operator");

        private void SwitchWpfUser(string userId)
        {
            Task.Run(async () =>
            {
                var user = await _wpfSecurityStore.FindByIdAsync(userId);
                if (user != null)
                {
                    var perms = await _wpfSecurityStore.GetPermissionsForRolesAsync(user.Roles);
                    _wpfSession.SetCurrentUser(user, perms);
                    _wpfSecurityStore.Log(new OperationLogEntry("Security.FastSwitch", user.Username, "UserSession", null, user.Roles.FirstOrDefault(), OperationLogLevel.Info, "Auth", $"Fast-switched session to {user.Username} ({string.Join(",", user.Roles)})"));
                    Dispatcher.Invoke(() =>
                    {
                        EvaluateWpfAuthorizations();
                        _ = WpfLogViewer.LoadLogsAsync(_wpfSecurityStore);
                    });
                }
            });
        }

        private void BtnWpfSwipeBadge_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(async () =>
            {
                var user = await _wpfSecurityStore.FindByBadgeIdAsync("BADGE-TECH");
                if (user != null)
                {
                    var perms = await _wpfSecurityStore.GetPermissionsForRolesAsync(user.Roles);
                    _wpfSession.SetCurrentUser(user, perms);
                    _wpfSecurityStore.Log(new OperationLogEntry("Security.BadgeSwipe", user.Username, "RFID-Reader", null, "BADGE-TECH", OperationLogLevel.Info, "Hardware", "Operator scanned RFID badge BADGE-TECH"));
                    Dispatcher.Invoke(() =>
                    {
                        EvaluateWpfAuthorizations();
                        _ = WpfLogViewer.LoadLogsAsync(_wpfSecurityStore);
                        MessageBox.Show($"RFID Badge 'BADGE-TECH' accepted! Logged in as: {user.DisplayName} ({string.Join(", ", user.Roles)})", "Badge Scanned", MessageBoxButton.OK, MessageBoxImage.Information);
                    });
                }
            });
        }

        private void BtnWpfLoginDialog_Click(object sender, RoutedEventArgs e)
        {
            if (ZLoginDialog.ShowLogin(_wpfSecurityStore, this) == true)
            {
                _wpfSecurityStore.Log(new OperationLogEntry("Security.LoginDialog", _wpfSession.CurrentUser?.Username ?? "Unknown", "HMI", null, null, OperationLogLevel.Info, "Auth", "Authenticated via WPF Login Dialog"));
                EvaluateWpfAuthorizations();
                _ = WpfLogViewer.LoadLogsAsync(_wpfSecurityStore);
            }
        }

        private void BtnWpfLockHmi_Click(object sender, RoutedEventArgs e)
        {
            _wpfSession.LockSession();
            ZLockScreenOverlay.ShowLock(_wpfSecurityStore, _wpfSession);
        }

        private void BtnWpfAdminAction_Click(object sender, RoutedEventArgs e)
        {
            _wpfSecurityStore.Log(new OperationLogEntry("SCADA.PlantOverhaul", _wpfSession.CurrentUser?.Username ?? "Unknown", "MainTurbine", "Operational", "Maintenance", OperationLogLevel.Critical, "SCADA", "Executed emergency factory overhaul procedure (WPF)"));
            MessageBox.Show("Emergency plant overhaul parameters written successfully!", "Authorized (Admin)", MessageBoxButton.OK, MessageBoxImage.Information);
            _ = WpfLogViewer.LoadLogsAsync(_wpfSecurityStore);
        }

        private void BtnWpfEngAction_Click(object sender, RoutedEventArgs e)
        {
            _wpfSecurityStore.Log(new OperationLogEntry("PID.Calibrate", _wpfSession.CurrentUser?.Username ?? "Unknown", "Loop-01", "Kp=2.1", "Kp=2.4", OperationLogLevel.Warning, "Tuning", "Updated closed-loop PID proportional gain (WPF)"));
            MessageBox.Show("PID loop parameters calibrated and persisted!", "Authorized (Engineer)", MessageBoxButton.OK, MessageBoxImage.Information);
            _ = WpfLogViewer.LoadLogsAsync(_wpfSecurityStore);
        }

        private void BtnWpfOpAction_Click(object sender, RoutedEventArgs e)
        {
            _wpfSecurityStore.Log(new OperationLogEntry("Alarms.Acknowledge", _wpfSession.CurrentUser?.Username ?? "Unknown", "Tank-A", "Unack", "Acked", OperationLogLevel.Info, "Safety", "Operator acknowledged active pressure threshold (WPF)"));
            MessageBox.Show("SCADA high-pressure alarm acknowledged by operator.", "Alarm Silenced (Operator)", MessageBoxButton.OK, MessageBoxImage.Information);
            _ = WpfLogViewer.LoadLogsAsync(_wpfSecurityStore);
        }

        private void BtnWpfExportAction_Click(object sender, RoutedEventArgs e)
        {
            _wpfSecurityStore.Log(new OperationLogEntry("Audit.Export", _wpfSession.CurrentUser?.Username ?? "Unknown", "AuditEngine", null, "audit_wpf_export.csv", OperationLogLevel.Info, "Compliance", "Generated 21 CFR Part 11 encrypted audit file (WPF)"));
            MessageBox.Show("Audit logs exported successfully for external FDA/ISO inspection.", "Compliance Report", MessageBoxButton.OK, MessageBoxImage.Information);
            _ = WpfLogViewer.LoadLogsAsync(_wpfSecurityStore);
        }

        private void BtnWpfRefreshLogs_Click(object sender, RoutedEventArgs e)
        {
            _ = WpfLogViewer.LoadLogsAsync(_wpfSecurityStore);
        }

        private void BtnWpfExportCsv_Click(object sender, RoutedEventArgs e)
        {
            var sfd = new SaveFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv",
                FileName = $"zeroui_wpf_audit_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };
            if (sfd.ShowDialog() == true)
            {
                WpfLogViewer.ExportToCsv(sfd.FileName);
                MessageBox.Show($"Audit logs saved to {sfd.FileName}", "Export Completed", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnWpfSimulateLog_Click(object sender, RoutedEventArgs e)
        {
            var rand = new Random();
            _wpfSecurityStore.Log(new OperationLogEntry("Setpoint.Change", _wpfSession.CurrentUser?.Username ?? "operator1", $"Heater-{rand.Next(1, 4)}", $"{rand.Next(50, 70)}°C", $"{rand.Next(71, 95)}°C", OperationLogLevel.Warning, "Process", "Manual recipe tweak by operator (WPF)"));
            _ = WpfLogViewer.LoadLogsAsync(_wpfSecurityStore);
        }
    }
}
