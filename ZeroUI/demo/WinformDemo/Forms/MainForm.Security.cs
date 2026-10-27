using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using ZeroUI.Core.Input;
using ZeroUI.Core.Security;
using ZeroUI.WinForms.Containers;
using ZeroUI.WinForms.Feedback;
using ZeroUI.WinForms.Input;
using ZeroUI.WinForms.Navigation;
using ZeroUI.WinForms.Overlays;
using ZeroUI.WinForms.Security;
using ZeroUI.WinForms.Theme;
using ZeroTabPage = ZeroUI.WinForms.Navigation.ZeroTabPage;
using ZeroTabControl = ZeroUI.WinForms.Navigation.ZeroTabControl;

namespace ZeroUI.Samples.WinformDemo.Forms
{
    public sealed partial class MainForm
    {
        private InMemoryUserStore? _securityStore;
        private ZAuthorizeExtender? _authorizeExtender;
        private ZRoleMatrix? _demoRoleMatrix;
        private ZUserManager? _demoUserManager;
        private ZOperationLogViewer? _demoLogViewer;
        private ZUserStatusBadge? _demoStatusBadge;
        private ZTouchKeyboardProvider? _touchKeyboardProvider;
        private TextBox? _demoVirtualKeyPreview;

        private void InitializeSecuritySuiteCluster(ZeroTabPage page)
        {
            var colors = ZeroTheme.Colors;
            page.BackColor = colors.Background;

            _securityStore = new InMemoryUserStore(seedDefaultIndustrialData: true);
            var session = SessionContext.Current;

            // Ensure an initial operator is logged in for the demo
            if (session.CurrentUser == null)
            {
                var adminUser = _securityStore.FindByIdAsync("usr-admin").GetAwaiter().GetResult();
                if (adminUser != null)
                {
                    var perms = _securityStore.GetPermissionsForRolesAsync(adminUser.Roles).GetAwaiter().GetResult();
                    session.SetCurrentUser(adminUser, perms);
                }
            }

            var mainContainer = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = colors.Background,
                Padding = new Padding(12)
            };

            // 1. Alert Banner
            var banner = new AlertBanner
            {
                Dock = DockStyle.Top,
                Height = 64,
                Severity = ZeroAlertSeverity.Info,
                Title = "🛡️ PHASE 7: INDUSTRIAL SECURITY, TOUCH HMI & RBAC AUDIT SUITE",
                Message = "Multi-modal authentication, on-screen touch virtual keyboards, 64-bit monotonic clock watchdog, RFID hardware badge support, and 21 CFR Part 11 audit trails."
            };
            mainContainer.Controls.Add(banner);

            var spacerBanner = new Panel { Dock = DockStyle.Top, Height = 8, BackColor = Color.Transparent };
            mainContainer.Controls.Add(spacerBanner);

            // 2. Session Header Toolbar (User Badge + Fast Switchers + Actions)
            var sessionBar = CreateSecurityToolbar(session, _securityStore);
            sessionBar.Dock = DockStyle.Top;
            mainContainer.Controls.Add(sessionBar);

            var spacerToolbar = new Panel { Dock = DockStyle.Top, Height = 8, BackColor = Color.Transparent };
            mainContainer.Controls.Add(spacerToolbar);

            // 3. Tabbed Sub-Workspace for Security Suite
            var subTabs = new ZeroTabControl
            {
                Dock = DockStyle.Fill,
                Orientation = ZeroTabOrientation.Horizontal,
                TabHeight = 36,
                TabStyle = ZeroTabStyle.Pill
            };

            var tabTouchAndRbac = new ZeroTabPage("Touch HMI & Declarative RBAC", "🔤", p => BuildTouchAndRbacTab(p, session, _securityStore));
            var tabUserManagement = new ZeroTabPage("User & Role Matrix Admin", "👥", p => BuildUserAndRoleMatrixTab(p, _securityStore));
            var tabAuditLogs = new ZeroTabPage("21 CFR Part 11 Audit Trail", "📜", p => BuildAuditLogsTab(p, _securityStore));

            subTabs.AddTab(tabTouchAndRbac);
            subTabs.AddTab(tabUserManagement);
            subTabs.AddTab(tabAuditLogs);

            mainContainer.Controls.Add(subTabs);

            // Setup Touch Keyboard Provider for automatic popup on inputs
            _touchKeyboardProvider = new ZTouchKeyboardProvider
            {
                ParentContainer = mainContainer,
                AutoDetectNumeric = true
            };

            page.Controls.Add(mainContainer);
        }

        private Panel CreateSecurityToolbar(SessionContext session, InMemoryUserStore store)
        {
            var colors = ZeroTheme.Colors;
            var bar = new Panel
            {
                Height = 52,
                BackColor = colors.Surface,
                Padding = new Padding(10, 6, 10, 6)
            };

            // User Status Badge
            _demoStatusBadge = new ZUserStatusBadge
            {
                Dock = DockStyle.Left,
                Width = 260
            };
            _demoStatusBadge.Session = session;
            bar.Controls.Add(_demoStatusBadge);

            // Right-aligned action buttons
            var pnlActions = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0, 4, 0, 0)
            };

            // Fast Switch Buttons
            pnlActions.Controls.Add(CreateSmallButton("👤 Admin", () => SwitchUser(store, session, "usr-admin")));
            pnlActions.Controls.Add(CreateSmallButton("🔧 Engineer", () => SwitchUser(store, session, "usr-engineer")));
            pnlActions.Controls.Add(CreateSmallButton("👷 Operator", () => SwitchUser(store, session, "usr-operator")));
            pnlActions.Controls.Add(CreateSmallButton("💳 Swipe Tech Badge", () => SimulateBadgeSwipe(store, session, "BADGE-TECH")));
            pnlActions.Controls.Add(CreateSmallButton("🔑 Sign-In Dialog", () =>
            {
                if (ZLoginDialog.ShowLogin(store, this))
                {
                    store.Log(new OperationLogEntry("Security.LoginDialog", session.CurrentUser?.Username ?? "Unknown", "HMI", null, null, OperationLogLevel.Info, "Auth", "Authenticated via Login Dialog"));
                    _demoLogViewer?.LoadLogsAsync(store);
                }
            }));
            pnlActions.Controls.Add(CreateSmallButton("🔒 Lock HMI", () =>
            {
                session.LockSession();
                ZLockScreenOverlay.ShowLock(store, session);
            }));

            bar.Controls.Add(pnlActions);
            return bar;
        }

        private void SwitchUser(InMemoryUserStore store, SessionContext session, string userId)
        {
            Task.Run(async () =>
            {
                var user = await store.FindByIdAsync(userId);
                if (user != null)
                {
                    var perms = await store.GetPermissionsForRolesAsync(user.Roles);
                    session.SetCurrentUser(user, perms);
                    store.Log(new OperationLogEntry("Security.FastSwitch", user.Username, "UserSession", null, user.Roles.FirstOrDefault(), OperationLogLevel.Info, "Auth", $"Fast-switched session to {user.Username} ({string.Join(",", user.Roles)})"));
                    BeginInvoke(new Action(() =>
                    {
                        _authorizeExtender?.EvaluateAll();
                        _demoLogViewer?.LoadLogsAsync(store);
                    }));
                }
            });
        }

        private void SimulateBadgeSwipe(InMemoryUserStore store, SessionContext session, string badgeId)
        {
            Task.Run(async () =>
            {
                var user = await store.FindByBadgeIdAsync(badgeId);
                if (user != null)
                {
                    var perms = await store.GetPermissionsForRolesAsync(user.Roles);
                    session.SetCurrentUser(user, perms);
                    store.Log(new OperationLogEntry("Security.BadgeSwipe", user.Username, "RFID-Reader", null, badgeId, OperationLogLevel.Info, "Hardware", $"Operator scanned RFID card {badgeId}"));
                    BeginInvoke(new Action(() =>
                    {
                        _authorizeExtender?.EvaluateAll();
                        _demoLogViewer?.LoadLogsAsync(store);
                        MessageBox.Show(this, $"RFID Badge '{badgeId}' accepted! Logged in as: {user.DisplayName} ({string.Join(", ", user.Roles)})", "Badge Scanned", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }));
                }
            });
        }

        private void BuildTouchAndRbacTab(ZeroTabPage page, SessionContext session, InMemoryUserStore store)
        {
            var colors = ZeroTheme.Colors;
            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 580,
                SplitterWidth = 8,
                BackColor = Color.Transparent
            };

            // LEFT CARD: Touch HMI & Virtual Keyboards
            var cardTouch = new Card
            {
                Dock = DockStyle.Fill,
                Title = "Touch-Screen HMI & On-Screen Virtual Keyboard",
                Subtitle = "Automatic 48px+ touch hit target popup on click/focus with QWERTY and Numpad modes",
                StepNumber = 1,
                AutoFitContent = false
            };

            var pnlTouch = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(12) };

            var lblTip = new Label
            {
                Text = "👉 Click into any input below to trigger automatic touch keyboard popup:",
                Font = new Font("Segoe UI", 9f, FontStyle.Italic),
                ForeColor = colors.TextSecondary,
                Dock = DockStyle.Top,
                Height = 24
            };
            pnlTouch.Controls.Add(lblTip);

            // Numpad input
            var lblNumpad = new Label { Text = "Setpoint Value / PIN (Auto-detected Numeric Keypad):", Dock = DockStyle.Top, Height = 22, ForeColor = colors.TextPrimary };
            var txtNumpad = new TextBox { Dock = DockStyle.Top, Height = 32, Font = new Font("Segoe UI", 11f), Text = "1250" };
            pnlTouch.Controls.Add(txtNumpad);
            pnlTouch.Controls.Add(lblNumpad);

            var sp1 = new Panel { Dock = DockStyle.Top, Height = 12 };
            pnlTouch.Controls.Add(sp1);

            // Qwerty input
            var lblQwerty = new Label { Text = "Recipe / Batch Code (Alphanumeric QWERTY):", Dock = DockStyle.Top, Height = 22, ForeColor = colors.TextPrimary };
            var txtQwerty = new TextBox { Dock = DockStyle.Top, Height = 32, Font = new Font("Segoe UI", 11f), Text = "RECIPE-TITANIUM-2026" };
            pnlTouch.Controls.Add(txtQwerty);
            pnlTouch.Controls.Add(lblQwerty);

            var sp2 = new Panel { Dock = DockStyle.Top, Height = 14 };
            pnlTouch.Controls.Add(sp2);

            // Embedded live keyboard preview
            var lblEmbedTitle = new Label { Text = "Embedded Live Keyboard Instance (Touch Preview):", Dock = DockStyle.Top, Height = 24, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = colors.Primary };
            pnlTouch.Controls.Add(lblEmbedTitle);

            _demoVirtualKeyPreview = new TextBox
            {
                Dock = DockStyle.Top,
                Height = 30,
                Font = new Font("Consolas", 10.5f),
                Text = "Typed: ",
                ReadOnly = true,
                BackColor = colors.Surface,
                ForeColor = colors.TextPrimary
            };
            pnlTouch.Controls.Add(_demoVirtualKeyPreview);

            var sp3 = new Panel { Dock = DockStyle.Top, Height = 8 };
            pnlTouch.Controls.Add(sp3);

            var embeddedKbd = new ZVirtualKeyboard
            {
                Dock = DockStyle.Top,
                Height = 220,
                LayoutMode = VirtualKeyboardLayout.AlphaNumeric
            };
            embeddedKbd.KeyPressed += (s, e) =>
            {
                if (_demoVirtualKeyPreview != null && !_demoVirtualKeyPreview.IsDisposed)
                {
                    if (e.KeyType == VirtualKeyType.Character)
                    {
                        _demoVirtualKeyPreview.AppendText(e.Text);
                    }
                    else if (e.KeyType == VirtualKeyType.Backspace && _demoVirtualKeyPreview.Text.Length > 7)
                    {
                        _demoVirtualKeyPreview.Text = _demoVirtualKeyPreview.Text.Substring(0, _demoVirtualKeyPreview.Text.Length - 1);
                    }
                }
            };
            pnlTouch.Controls.Add(embeddedKbd);

            cardTouch.Controls.Add(pnlTouch);
            split.Panel1.Controls.Add(cardTouch);

            // RIGHT CARD: Declarative RBAC Actions
            var cardRbac = new Card
            {
                Dock = DockStyle.Fill,
                Title = "Declarative RBAC Dynamic Control Authorization",
                Subtitle = "Controls automatically enable, disable, or hide based on active user roles & permissions",
                StepNumber = 2,
                AutoFitContent = false
            };

            var pnlRbac = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(12) };

            _authorizeExtender = new ZAuthorizeExtender { Session = session };

            // 1. Admin Action
            var btnAdminAction = CreateSecurityActionButton("⚙️ Overhaul Plant Parameters (Admin Only)", colors.Danger, () =>
            {
                store.Log(new OperationLogEntry("SCADA.PlantOverhaul", session.CurrentUser?.Username ?? "Unknown", "MainTurbine", "Operational", "Maintenance", OperationLogLevel.Critical, "SCADA", "Executed emergency factory overhaul procedure"));
                MessageBox.Show(this, "Emergency plant overhaul parameters written successfully!", "Authorized", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _demoLogViewer?.LoadLogsAsync(store);
            });
            _authorizeExtender.SetRequiredRole(btnAdminAction, "Administrator");

            var lblAdminReq = new Label { Text = "🔒 Requires Role: 'Administrator'", Font = new Font("Segoe UI", 8.5f, FontStyle.Italic), ForeColor = colors.TextSecondary, Dock = DockStyle.Top, Height = 20 };
            pnlRbac.Controls.Add(lblAdminReq);
            pnlRbac.Controls.Add(btnAdminAction);

            var spRbac1 = new Panel { Dock = DockStyle.Top, Height = 14 };
            pnlRbac.Controls.Add(spRbac1);

            // 2. Engineer Action
            var btnEngineerAction = CreateSecurityActionButton("🔧 Calibrate PID Loops & Setpoints (Engineer+)", Color.FromArgb(14, 165, 233), () =>
            {
                store.Log(new OperationLogEntry("PID.Calibrate", session.CurrentUser?.Username ?? "Unknown", "Loop-01", "Kp=2.1", "Kp=2.4", OperationLogLevel.Warning, "Tuning", "Updated closed-loop PID proportional gain"));
                MessageBox.Show(this, "PID loop parameters calibrated and persisted!", "Authorized", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _demoLogViewer?.LoadLogsAsync(store);
            });
            _authorizeExtender.SetRequiredPermission(btnEngineerAction, "Parameters.Write");

            var lblEngReq = new Label { Text = "🔒 Requires Permission: 'Parameters.Write' (Engineer / Admin)", Font = new Font("Segoe UI", 8.5f, FontStyle.Italic), ForeColor = colors.TextSecondary, Dock = DockStyle.Top, Height = 20 };
            pnlRbac.Controls.Add(lblEngReq);
            pnlRbac.Controls.Add(btnEngineerAction);

            var spRbac2 = new Panel { Dock = DockStyle.Top, Height = 14 };
            pnlRbac.Controls.Add(spRbac2);

            // 3. Operator Action
            var btnOperatorAction = CreateSecurityActionButton("🔔 Acknowledge Active Alarms (All Operators)", Color.FromArgb(34, 197, 94), () =>
            {
                store.Log(new OperationLogEntry("Alarms.Acknowledge", session.CurrentUser?.Username ?? "Unknown", "Tank-A", "Unack", "Acked", OperationLogLevel.Info, "Safety", "Operator acknowledged active pressure threshold"));
                MessageBox.Show(this, "SCADA high-pressure alarm acknowledged by operator.", "Alarm Silenced", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _demoLogViewer?.LoadLogsAsync(store);
            });
            _authorizeExtender.SetRequiredPermission(btnOperatorAction, "Alarms.Acknowledge");

            var lblOpReq = new Label { Text = "🔒 Requires Permission: 'Alarms.Acknowledge' (Operator / Engineer / Admin)", Font = new Font("Segoe UI", 8.5f, FontStyle.Italic), ForeColor = colors.TextSecondary, Dock = DockStyle.Top, Height = 20 };
            pnlRbac.Controls.Add(lblOpReq);
            pnlRbac.Controls.Add(btnOperatorAction);

            var spRbac3 = new Panel { Dock = DockStyle.Top, Height = 14 };
            pnlRbac.Controls.Add(spRbac3);

            // 4. Audit Export Action
            var btnExportAction = CreateSecurityActionButton("💾 Export Regulatory Compliance Audit (Admin/Supervisor)", Color.FromArgb(168, 85, 247), () =>
            {
                store.Log(new OperationLogEntry("Audit.Export", session.CurrentUser?.Username ?? "Unknown", "AuditEngine", null, "audit_export.csv", OperationLogLevel.Info, "Compliance", "Generated 21 CFR Part 11 encrypted audit file"));
                MessageBox.Show(this, "Audit logs exported successfully for external FDA/ISO inspection.", "Compliance Report", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _demoLogViewer?.LoadLogsAsync(store);
            });
            _authorizeExtender.SetRequiredPermission(btnExportAction, "Audit.Export");

            var lblExportReq = new Label { Text = "🔒 Requires Permission: 'Audit.Export' (Administrator)", Font = new Font("Segoe UI", 8.5f, FontStyle.Italic), ForeColor = colors.TextSecondary, Dock = DockStyle.Top, Height = 20 };
            pnlRbac.Controls.Add(lblExportReq);
            pnlRbac.Controls.Add(btnExportAction);

            cardRbac.Controls.Add(pnlRbac);
            split.Panel2.Controls.Add(cardRbac);

            _authorizeExtender.EvaluateAll();

            page.Controls.Add(split);
        }

        private void BuildUserAndRoleMatrixTab(ZeroTabPage page, InMemoryUserStore store)
        {
            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 580,
                SplitterWidth = 8,
                BackColor = Color.Transparent
            };

            // LEFT: Role Matrix
            var cardMatrix = new Card
            {
                Dock = DockStyle.Fill,
                Title = "Role-to-Permission Access Matrix (ZRoleMatrix)",
                Subtitle = "Interactive tri-state matrix assigning permissions across industrial roles",
                StepNumber = 1,
                AutoFitContent = false
            };

            _demoRoleMatrix = new ZRoleMatrix { Dock = DockStyle.Fill };
            _demoRoleMatrix.LoadAsync(store);
            cardMatrix.Controls.Add(_demoRoleMatrix);
            split.Panel1.Controls.Add(cardMatrix);

            // RIGHT: User Manager
            var cardUsers = new Card
            {
                Dock = DockStyle.Fill,
                Title = "Industrial Operator Accounts (ZUserManager)",
                Subtitle = "Manage operator credentials, PINs, and RFID badge card hardware assignments",
                StepNumber = 2,
                AutoFitContent = false
            };

            _demoUserManager = new ZUserManager { Dock = DockStyle.Fill };
            _demoUserManager.LoadAsync(store, store);
            cardUsers.Controls.Add(_demoUserManager);
            split.Panel2.Controls.Add(cardUsers);

            page.Controls.Add(split);
        }

        private void BuildAuditLogsTab(ZeroTabPage page, InMemoryUserStore store)
        {
            var colors = ZeroTheme.Colors;
            var pnlContainer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };

            // Audit Action Bar
            var topBar = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = colors.Surface };
            var flowBar = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Padding = new Padding(6) };

            flowBar.Controls.Add(CreateSmallButton("🔄 Refresh Logs", () => _demoLogViewer?.LoadLogsAsync(store)));
            flowBar.Controls.Add(CreateSmallButton("📤 Export CSV", () =>
            {
                using var sfd = new SaveFileDialog { Filter = "CSV Files (*.csv)|*.csv", FileName = $"zeroui_audit_{DateTime.Now:yyyyMMdd_HHmmss}.csv" };
                if (sfd.ShowDialog(this) == DialogResult.OK)
                {
                    _demoLogViewer?.ExportToCsv(sfd.FileName);
                    MessageBox.Show(this, $"Audit logs saved to {sfd.FileName}", "Export Completed", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }));
            flowBar.Controls.Add(CreateSmallButton("🧪 Simulate Parameter Change Log", () =>
            {
                var rand = new Random();
                store.Log(new OperationLogEntry("Setpoint.Change", SessionContext.Current.CurrentUser?.Username ?? "operator1", $"Heater-{rand.Next(1, 4)}", $"{rand.Next(50, 70)}°C", $"{rand.Next(71, 95)}°C", OperationLogLevel.Warning, "Process", "Manual recipe tweak by operator"));
                _demoLogViewer?.LoadLogsAsync(store);
            }));

            topBar.Controls.Add(flowBar);
            pnlContainer.Controls.Add(topBar);

            var sp = new Panel { Dock = DockStyle.Top, Height = 8 };
            pnlContainer.Controls.Add(sp);

            _demoLogViewer = new ZOperationLogViewer { Dock = DockStyle.Fill };
            _demoLogViewer.LoadLogsAsync(store);
            pnlContainer.Controls.Add(_demoLogViewer);

            page.Controls.Add(pnlContainer);
        }

        private static Button CreateSmallButton(string text, Action onClick)
        {
            var colors = ZeroTheme.Colors;
            var btn = new Button
            {
                Text = text,
                AutoSize = true,
                Height = 32,
                Margin = new Padding(4, 2, 4, 2),
                BackColor = colors.Surface,
                ForeColor = colors.TextPrimary,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(51, 65, 85);
            btn.Click += (s, e) => onClick();
            return btn;
        }

        private static Button CreateSecurityActionButton(string text, Color backColor, Action onClick)
        {
            var btn = new Button
            {
                Text = text,
                Dock = DockStyle.Top,
                Height = 44,
                BackColor = backColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += (s, e) => onClick();
            return btn;
        }
    }
}
