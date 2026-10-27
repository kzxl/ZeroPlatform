using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Windows.Forms;
using ZeroUI.Core.Input;
using ZeroUI.Core.Security;
using ZeroUI.Core.Theme;
using ZeroUI.WinForms.Input;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Overlays
{
    public enum LoginMode
    {
        Password = 0,
        Pin = 1,
        Badge = 2
    }

    public enum AuthStatusState
    {
        Normal,
        InProgress,
        Error
    }

    /// <summary>
    /// Multi-modal enterprise industrial login dialog supporting Username/Password,
    /// touch PIN Numpad, and hardware RFID badge authentication.
    /// </summary>
    public class ZLoginDialog : Form
    {
        private readonly IAuthenticationProvider _authProvider;
        private readonly ISessionManager _session;

        private LoginMode _currentMode = LoginMode.Password;
        private TextBox _txtUsername = null!;
        private TextBox _txtPassword = null!;
        private TextBox _txtPin = null!;
        private Label _lblUser = null!;
        private Label _lblPass = null!;
        private Button _btnSubmit = null!;
        private Button _btnCancel = null!;
        private Label _lblBadgeIcon = null!;
        private Label _lblStatus = null!;
        private Panel _tabPasswordPanel = null!;
        private Panel _tabPinPanel = null!;
        private Panel _tabBadgePanel = null!;
        private ZVirtualKeyboard _pinNumpad = null!;
        private Rectangle _cardRect;
        private Rectangle _tabPassRect;
        private Rectangle _tabPinRect;
        private Rectangle _tabBadgeRect;
        private readonly EventHandler _themeChangedHandler;

        private AuthStatusState _statusState = AuthStatusState.Normal;

        public IUser? AuthenticatedUser { get; private set; }
        public Button CloseButton { get; private set; } = null!;

        public ZLoginDialog(IAuthenticationProvider? authProvider = null, ISessionManager? session = null)
        {
            _authProvider = authProvider ?? new InMemoryUserStore();
            _session = session ?? SessionContext.Current;

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = false;
            DoubleBuffered = true;
            KeyPreview = true;
            Size = new Size(460, 520);
            BackColor = ZeroTheme.Colors.CardBackground;

            // Ensure window handle is created so IsHandleCreated is true
            _ = Handle;

            InitializeComponents();

            _themeChangedHandler = (s, e) =>
            {
                if (IsDisposed || !IsHandleCreated) return;
                if (InvokeRequired)
                {
                    try { BeginInvoke(new Action(ApplyTheme)); }
                    catch (ObjectDisposedException) { }
                }
                else
                {
                    ApplyTheme();
                }
            };
            ZeroTheme.ThemeChanged += _themeChangedHandler;
            ApplyTheme();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ZeroTheme.ThemeChanged -= _themeChangedHandler;
                _pinNumpad?.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponents()
        {
            _cardRect = new Rectangle(0, 0, Width, Height);
            int contentW = Width - 60;

            // Header close button
            CloseButton = new Button
            {
                Text = "✕",
                Size = new Size(30, 30),
                Location = new Point(Width - 42, 12),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = ZeroTheme.Colors.TextSecondary,
                Font = new Font("Segoe UI", 11f, FontStyle.Regular),
                Cursor = Cursors.Hand,
                TabStop = false,
                DialogResult = DialogResult.Cancel
            };
            CloseButton.FlatAppearance.BorderSize = 0;
            CloseButton.FlatAppearance.MouseOverBackColor = ZeroTheme.Colors.Hover;
            CloseButton.FlatAppearance.MouseDownBackColor = ZeroTheme.Colors.Border;
            CloseButton.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };
            CancelButton = CloseButton;
            Controls.Add(CloseButton);
            CloseButton.BringToFront();

            // Tabs definition
            int tabY = 60;
            int tabH = 34;
            int tabW = contentW / 3;
            _tabPassRect = new Rectangle(30, tabY, tabW, tabH);
            _tabPinRect = new Rectangle(30 + tabW, tabY, tabW, tabH);
            _tabBadgeRect = new Rectangle(30 + tabW * 2, tabY, tabW, tabH);

            int panelY = 110;
            int panelH = 330;

            // Password Panel
            _tabPasswordPanel = new Panel
            {
                Location = new Point(30, panelY),
                Size = new Size(contentW, panelH),
                BackColor = Color.Transparent
            };

            _lblUser = new Label
            {
                Text = "Operator ID / Username",
                ForeColor = ZeroTheme.Colors.TextSecondary,
                Font = new Font(Font.FontFamily, 9f),
                Location = new Point(0, 10),
                AutoSize = true
            };
            _txtUsername = new TextBox
            {
                Font = new Font(Font.FontFamily, 12f),
                Location = new Point(0, 32),
                Size = new Size(contentW, 32),
                BackColor = ZeroTheme.Colors.Background,
                ForeColor = ZeroTheme.Colors.TextPrimary,
                BorderStyle = BorderStyle.FixedSingle,
                Text = "admin"
            };

            _lblPass = new Label
            {
                Text = "Password",
                ForeColor = ZeroTheme.Colors.TextSecondary,
                Font = new Font(Font.FontFamily, 9f),
                Location = new Point(0, 80),
                AutoSize = true
            };
            _txtPassword = new TextBox
            {
                Font = new Font(Font.FontFamily, 12f),
                PasswordChar = '●',
                Location = new Point(0, 102),
                Size = new Size(contentW, 32),
                BackColor = ZeroTheme.Colors.Background,
                ForeColor = ZeroTheme.Colors.TextPrimary,
                BorderStyle = BorderStyle.FixedSingle,
                Text = "admin"
            };
            _txtPassword.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; _ = ExecuteLoginAsync(); }
            };

            _btnSubmit = CreateActionButton("Sign In", ZeroTheme.Colors.Primary, GetAccentTextColor(ZeroTheme.Colors), 0, 160, contentW, 42);
            _btnSubmit.FlatAppearance.MouseOverBackColor = ZeroTheme.Colors.PrimaryHover;
            _btnSubmit.Click += (s, e) => _ = ExecuteLoginAsync();

            _btnCancel = CreateActionButton("Cancel", ZeroTheme.Colors.Hover, ZeroTheme.Colors.TextPrimary, 0, 215, contentW, 36);
            _btnCancel.DialogResult = DialogResult.Cancel;
            _btnCancel.FlatAppearance.MouseOverBackColor = ZeroTheme.Colors.Border;
            _btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            _tabPasswordPanel.Controls.Add(_lblUser);
            _tabPasswordPanel.Controls.Add(_txtUsername);
            _tabPasswordPanel.Controls.Add(_lblPass);
            _tabPasswordPanel.Controls.Add(_txtPassword);
            _tabPasswordPanel.Controls.Add(_btnSubmit);
            _tabPasswordPanel.Controls.Add(_btnCancel);

            // PIN Panel
            _tabPinPanel = new Panel
            {
                Location = new Point(30, panelY),
                Size = new Size(contentW, panelH),
                BackColor = Color.Transparent,
                Visible = false
            };

            _txtPin = new TextBox
            {
                Font = new Font(Font.FontFamily, 16f, FontStyle.Bold),
                PasswordChar = '●',
                TextAlign = HorizontalAlignment.Center,
                Location = new Point(0, 0),
                Size = new Size(contentW, 36),
                BackColor = ZeroTheme.Colors.Background,
                ForeColor = ZeroTheme.Colors.TextPrimary,
                BorderStyle = BorderStyle.FixedSingle
            };

            _pinNumpad = new ZVirtualKeyboard
            {
                LayoutMode = VirtualKeyboardLayout.Numpad,
                TargetControl = _txtPin,
                Location = new Point(0, 45),
                Size = new Size(contentW, 260)
            };
            _pinNumpad.EnterPressed += (s, e) => _ = ExecuteLoginAsync();

            _tabPinPanel.Controls.Add(_txtPin);
            _tabPinPanel.Controls.Add(_pinNumpad);

            // Badge Panel
            _tabBadgePanel = new Panel
            {
                Location = new Point(30, panelY),
                Size = new Size(contentW, panelH),
                BackColor = Color.Transparent,
                Visible = false
            };

            _lblBadgeIcon = new Label
            {
                Text = "💳\r\n\r\nPresent RFID / NFC Badge\r\nOr Insert Hardware Dongle",
                Font = new Font(Font.FontFamily, 13f, FontStyle.Regular),
                ForeColor = ZeroTheme.Colors.Primary,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill
            };
            _tabBadgePanel.Controls.Add(_lblBadgeIcon);

            // Status label
            _lblStatus = new Label
            {
                Text = "Please select authentication mode",
                ForeColor = ZeroTheme.Colors.TextSecondary,
                Font = new Font(Font.FontFamily, 9f),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(30, Height - 50),
                Size = new Size(contentW, 30),
                BackColor = Color.Transparent
            };

            Controls.Add(_tabPasswordPanel);
            Controls.Add(_tabPinPanel);
            Controls.Add(_tabBadgePanel);
            Controls.Add(_lblStatus);
        }

        private static string ColorToHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

        public static Color GetAccentTextColor(ZeroThemePalette colors)
        {
            double bgContrast = ZeroColorUtils.GetContrastRatio(ColorToHex(colors.Background), ColorToHex(colors.Primary));
            double textContrast = ZeroColorUtils.GetContrastRatio(ColorToHex(colors.TextPrimary), ColorToHex(colors.Primary));
            if (bgContrast >= 4.5 && bgContrast >= textContrast) return colors.Background;
            if (textContrast >= 4.5) return colors.TextPrimary;

            var skin = ZeroSkinManager.CurrentSkin;
            if (skin != null && !string.IsNullOrEmpty(skin.Tokens.SelectionForeground))
            {
                double selContrast = ZeroColorUtils.GetContrastRatio(skin.Tokens.SelectionForeground, ColorToHex(colors.Primary));
                if (selContrast >= 4.5)
                {
                    return ColorTranslator.FromHtml(skin.Tokens.SelectionForeground);
                }
            }

            return bgContrast >= textContrast ? colors.Background : colors.TextPrimary;
        }

        private void SetStatus(string message, AuthStatusState state)
        {
            _statusState = state;
            if (_lblStatus != null)
            {
                _lblStatus.Text = message;
                var colors = ZeroTheme.Colors;
                _lblStatus.ForeColor = state switch
                {
                    AuthStatusState.InProgress => colors.Primary,
                    AuthStatusState.Error => colors.Danger,
                    _ => colors.TextSecondary
                };
            }
        }

        private static Button CreateActionButton(string text, Color backColor, Color foreColor, int x, int y, int w, int h)
        {
            var btn = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(w, h),
                BackColor = backColor,
                ForeColor = foreColor,
                FlatStyle = FlatStyle.Flat,
                Font = new Font(Control.DefaultFont.FontFamily, 10f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        private void ApplyTheme()
        {
            var colors = ZeroTheme.Colors;
            BackColor = colors.CardBackground;

            if (_txtUsername != null)
            {
                _txtUsername.BackColor = colors.Background;
                _txtUsername.ForeColor = colors.TextPrimary;
            }
            if (_txtPassword != null)
            {
                _txtPassword.BackColor = colors.Background;
                _txtPassword.ForeColor = colors.TextPrimary;
            }
            if (_txtPin != null)
            {
                _txtPin.BackColor = colors.Background;
                _txtPin.ForeColor = colors.TextPrimary;
            }
            if (_lblUser != null)
            {
                _lblUser.ForeColor = colors.TextSecondary;
            }
            if (_lblPass != null)
            {
                _lblPass.ForeColor = colors.TextSecondary;
            }

            if (_btnSubmit != null)
            {
                _btnSubmit.BackColor = colors.Primary;
                _btnSubmit.ForeColor = GetAccentTextColor(colors);
                _btnSubmit.FlatAppearance.MouseOverBackColor = colors.PrimaryHover;
            }
            if (_btnCancel != null)
            {
                _btnCancel.BackColor = colors.Hover;
                _btnCancel.ForeColor = colors.TextPrimary;
                _btnCancel.FlatAppearance.MouseOverBackColor = colors.Border;
            }
            if (CloseButton != null)
            {
                CloseButton.ForeColor = colors.TextSecondary;
                CloseButton.FlatAppearance.MouseOverBackColor = colors.Hover;
                CloseButton.FlatAppearance.MouseDownBackColor = colors.Border;
            }
            if (_lblBadgeIcon != null)
            {
                _lblBadgeIcon.ForeColor = colors.Primary;
            }
            if (_lblStatus != null)
            {
                _lblStatus.ForeColor = _statusState switch
                {
                    AuthStatusState.InProgress => colors.Primary,
                    AuthStatusState.Error => colors.Danger,
                    _ => colors.TextSecondary
                };
            }

            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (_tabPassRect.Contains(e.Location)) SetMode(LoginMode.Password);
            else if (_tabPinRect.Contains(e.Location)) SetMode(LoginMode.Pin);
            else if (_tabBadgeRect.Contains(e.Location)) SetMode(LoginMode.Badge);
        }

        private void SetMode(LoginMode mode)
        {
            _currentMode = mode;
            _tabPasswordPanel.Visible = (mode == LoginMode.Password);
            _tabPinPanel.Visible = (mode == LoginMode.Pin);
            _tabBadgePanel.Visible = (mode == LoginMode.Badge);
            SetStatus(mode == LoginMode.Badge ? "Listening for badge swipe..." : "Ready", AuthStatusState.Normal);
            Invalidate();
        }

        public async Task<bool> ExecuteLoginAsync()
        {
            SetStatus("Authenticating...", AuthStatusState.InProgress);

            AuthResult result;
            if (_currentMode == LoginMode.Password)
            {
                result = await _authProvider.AuthenticatePasswordAsync(_txtUsername.Text.Trim(), _txtPassword.Text);
            }
            else
            {
                result = await _authProvider.AuthenticatePinAsync("admin", _txtPin.Text);
            }

            if (result.Succeeded && result.User != null)
            {
                AuthenticatedUser = result.User;
                _session.SetCurrentUser(result.User);
                DialogResult = DialogResult.OK;
                Close();
                return true;
            }
            else
            {
                SetStatus(result.ErrorMessage ?? "Authentication failed", AuthStatusState.Error);
                return false;
            }
        }

        public async Task<bool> ExecuteBadgeLoginAsync(string badgeId)
        {
            SetStatus($"Validating Badge '{badgeId}'...", AuthStatusState.InProgress);
            var result = await _authProvider.AuthenticateBadgeAsync(badgeId);
            if (result.Succeeded && result.User != null)
            {
                AuthenticatedUser = result.User;
                _session.SetCurrentUser(result.User);
                DialogResult = DialogResult.OK;
                Close();
                return true;
            }
            else
            {
                SetStatus(result.ErrorMessage ?? "Badge not recognized", AuthStatusState.Error);
                return false;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var colors = ZeroTheme.Colors;

            // Outer border
            using (var borderPen = new Pen(colors.Border, 1.5f))
            {
                g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
            }

            // Header Title
            using (var titleFont = new Font(Font.FontFamily, 14f, FontStyle.Bold))
            using (var subFont = new Font(Font.FontFamily, 8.5f))
            using (var titleBrush = new SolidBrush(colors.TextPrimary))
            using (var subBrush = new SolidBrush(colors.TextSecondary))
            {
                g.DrawString("🔐 ZeroUI Operator Sign-In", titleFont, titleBrush, 30, 16);
                g.DrawString("Role-Based Industrial Access Control", subFont, subBrush, 32, 38);
            }

            // Draw Tabs
            DrawTab(g, "Password", _tabPassRect, _currentMode == LoginMode.Password);
            DrawTab(g, "PIN Code", _tabPinRect, _currentMode == LoginMode.Pin);
            DrawTab(g, "RFID Badge", _tabBadgeRect, _currentMode == LoginMode.Badge);
        }

        private static void DrawTab(Graphics g, string label, Rectangle rect, bool isActive)
        {
            var colors = ZeroTheme.Colors;
            Color back = isActive ? colors.Primary : colors.Surface;
            Color text = isActive ? GetAccentTextColor(colors) : colors.TextSecondary;

            using (var brush = new SolidBrush(back))
            using (var pen = new Pen(colors.Border, 1f))
            {
                g.FillRectangle(brush, rect);
                g.DrawRectangle(pen, rect);
            }

            using (var font = new Font(Control.DefaultFont.FontFamily, 8.5f, isActive ? FontStyle.Bold : FontStyle.Regular))
            using (var brush = new SolidBrush(text))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString(label, font, brush, rect, sf);
            }
        }

        public static bool ShowLogin(IAuthenticationProvider? authProvider = null, Form? owner = null)
        {
            using var dlg = new ZLoginDialog(authProvider);
            return dlg.ShowDialog(owner) == DialogResult.OK;
        }
    }
}
