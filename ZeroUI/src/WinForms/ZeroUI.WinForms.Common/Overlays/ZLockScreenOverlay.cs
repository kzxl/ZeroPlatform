using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Windows.Forms;
using ZeroUI.Core.Input;
using ZeroUI.Core.Security;
using ZeroUI.WinForms.Input;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Overlays
{
    /// <summary>
    /// Industrial touch-friendly Lock Screen overlay with PIN pad, password verification,
    /// and RFID badge unlock for secured SCADA stations.
    /// </summary>
    public class ZLockScreenOverlay : Form
    {
        private static ZLockScreenOverlay? _currentInstance;
        private readonly IAuthenticationProvider _authProvider;
        private readonly ISessionManager _session;

        private TextBox _inputBox = null!;
        private Label _lblStatus = null!;
        private ZVirtualKeyboard _numpad = null!;
        private Rectangle _cardRect;

        public ZLockScreenOverlay(IAuthenticationProvider? authProvider = null, ISessionManager? session = null)
        {
            _authProvider = authProvider ?? new InMemoryUserStore();
            _session = session ?? SessionContext.Current;

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            DoubleBuffered = true;
            KeyPreview = true;

            BackColor = Color.FromArgb(10, 14, 22);

            InitializeLayout();
        }

        private void InitializeLayout()
        {
            Size = Screen.PrimaryScreen?.Bounds.Size ?? new Size(1280, 800);
            Location = Point.Empty;

            int cardW = 380;
            int cardH = 500;
            int cardX = (ClientSize.Width - cardW) / 2;
            int cardY = (ClientSize.Height - cardH) / 2;
            _cardRect = new Rectangle(cardX, cardY, cardW, cardH);

            _inputBox = new TextBox
            {
                PasswordChar = '●',
                Font = new Font(Font.FontFamily, 16f, FontStyle.Bold),
                TextAlign = HorizontalAlignment.Center,
                Size = new Size(cardW - 60, 36),
                Location = new Point(cardX + 30, cardY + 120),
                BackColor = Color.FromArgb(20, 25, 36),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            _inputBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    _ = AttemptUnlockAsync(_inputBox.Text);
                }
            };

            _lblStatus = new Label
            {
                Text = "Enter PIN or Password to Unlock",
                Font = new Font(Font.FontFamily, 9.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(148, 163, 184),
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(cardW - 40, 24),
                Location = new Point(cardX + 20, cardY + 162),
                BackColor = Color.Transparent
            };

            // Touch Numpad
            _numpad = new ZVirtualKeyboard
            {
                LayoutMode = VirtualKeyboardLayout.Numpad,
                TargetControl = _inputBox,
                Location = new Point(cardX + 25, cardY + 195),
                Size = new Size(cardW - 50, 280)
            };
            _numpad.EnterPressed += (s, e) => _ = AttemptUnlockAsync(_inputBox.Text);

            Controls.Add(_inputBox);
            Controls.Add(_lblStatus);
            Controls.Add(_numpad);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _inputBox.Focus();
        }

        public async Task<bool> AttemptUnlockAsync(string secret)
        {
            if (string.IsNullOrWhiteSpace(secret))
            {
                SetStatus("Please enter your PIN or password", isError: true);
                return false;
            }

            SetStatus("Verifying...", isError: false);

            var user = _session.CurrentUser;
            string targetUser = user?.Username ?? "admin";

            // Try PIN first if all digits
            AuthResult result;
            bool isAllDigits = true;
            for (int i = 0; i < secret.Length; i++)
            {
                if (!char.IsDigit(secret[i])) { isAllDigits = false; break; }
            }

            if (isAllDigits)
            {
                result = await _authProvider.AuthenticatePinAsync(targetUser, secret);
            }
            else
            {
                result = await _authProvider.AuthenticatePasswordAsync(targetUser, secret);
            }

            if (result.Succeeded)
            {
                _session.UnlockSession();
                Close();
                return true;
            }
            else
            {
                SetStatus(result.ErrorMessage ?? "Invalid credential", isError: true);
                _inputBox.SelectAll();
                _inputBox.Focus();
                return false;
            }
        }

        public async Task<bool> UnlockWithBadgeAsync(string badgeId)
        {
            SetStatus("Reading Badge...", isError: false);
            var result = await _authProvider.AuthenticateBadgeAsync(badgeId);
            if (result.Succeeded)
            {
                _session.UnlockSession();
                Close();
                return true;
            }
            else
            {
                SetStatus(result.ErrorMessage ?? "Badge not recognized", isError: true);
                return false;
            }
        }

        private void SetStatus(string message, bool isError)
        {
            _lblStatus.Text = message;
            _lblStatus.ForeColor = isError ? Color.FromArgb(239, 68, 68) : Color.FromArgb(56, 189, 248);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Backdrop dimming
            using (var dimBrush = new SolidBrush(Color.FromArgb(190, 8, 11, 19)))
            {
                g.FillRectangle(dimBrush, ClientRectangle);
            }

            // Card Panel
            using (var cardPath = CreateRoundedRectanglePath(_cardRect, 8))
            using (var cardBrush = new SolidBrush(Color.FromArgb(26, 32, 44)))
            using (var borderPen = new Pen(Color.FromArgb(51, 65, 85), 1.5f))
            {
                g.FillPath(cardBrush, cardPath);
                g.DrawPath(borderPen, cardPath);
            }

            // Header Lock Icon & Text
            var user = _session.CurrentUser;
            string displayName = user?.DisplayName ?? "Workstation Locked";

            using var titleFont = new Font(Font.FontFamily, 14f, FontStyle.Bold);
            using var subtitleFont = new Font(Font.FontFamily, 9f, FontStyle.Regular);
            using var textBrush = new SolidBrush(Color.White);
            using var subBrush = new SolidBrush(Color.FromArgb(148, 163, 184));

            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("🔒 " + displayName, titleFont, textBrush, new Rectangle(_cardRect.X + 10, _cardRect.Y + 30, _cardRect.Width - 20, 30), sf);

            string roleText = (user != null && user.Roles.Count > 0) ? $"Role: {string.Join(", ", user.Roles)}" : "Operator Station";
            g.DrawString(roleText, subtitleFont, subBrush, new Rectangle(_cardRect.X + 10, _cardRect.Y + 65, _cardRect.Width - 20, 24), sf);
        }

        private static GraphicsPath CreateRoundedRectanglePath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        /// <summary>
        /// Displays the global lock screen overlay.
        /// </summary>
        public static ZLockScreenOverlay ShowLock(IAuthenticationProvider? authProvider = null, ISessionManager? session = null)
        {
            if (_currentInstance != null && !_currentInstance.IsDisposed)
            {
                _currentInstance.BringToFront();
                return _currentInstance;
            }

            var lockScreen = new ZLockScreenOverlay(authProvider, session);
            _currentInstance = lockScreen;
            lockScreen.FormClosed += (s, e) => _currentInstance = null;
            lockScreen.Show();
            return lockScreen;
        }
    }
}
