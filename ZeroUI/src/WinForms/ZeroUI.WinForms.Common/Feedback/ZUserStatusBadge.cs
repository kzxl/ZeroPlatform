using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Security;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Feedback
{
    /// <summary>
    /// Statusbar or header badge displaying active user identity, role,
    /// live idle timeout countdown, and quick-switch / logout actions.
    /// </summary>
    [ToolboxItem(true)]
    public class ZUserStatusBadge : ControlBase
    {
        private ISessionManager? _session;
        private readonly Timer _countdownTimer;
        private Rectangle _switchRect;
        private Rectangle _logoutRect;
        private bool _isSwitchHovered;
        private bool _isLogoutHovered;

        [Category("Behavior")]
        [DefaultValue(true)]
        [Description("Shows or hides the idle timeout countdown.")]
        public bool ShowCountdown { get; set; } = true;

        [Category("Behavior")]
        [DefaultValue(true)]
        [Description("Shows or hides the user avatar circle.")]
        public bool ShowAvatar { get; set; } = true;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ISessionManager Session
        {
            get => _session ?? SessionContext.Current;
            set
            {
                if (_session != value)
                {
                    if (_session != null) _session.UserChanged -= OnUserChanged;
                    _session = value;
                    if (_session != null) _session.UserChanged += OnUserChanged;
                    Invalidate();
                }
            }
        }

        public event EventHandler? SwitchUserClicked;
        public event EventHandler? LogoutClicked;

        public ZUserStatusBadge()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);

            Size = new Size(240, 36);
            _countdownTimer = new Timer { Interval = 1000 };
            _countdownTimer.Tick += (s, e) =>
            {
                if (ShowCountdown && Session.IsAuthenticated)
                {
                    Invalidate();
                }
            };
            _countdownTimer.Start();

            SessionContext.Current.UserChanged += OnUserChanged;
        }

        private void OnUserChanged(object? sender, UserChangedEventArgs e)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(Invalidate));
            }
            else
            {
                Invalidate();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool prevSwitch = _isSwitchHovered;
            bool prevLogout = _isLogoutHovered;

            _isSwitchHovered = _switchRect.Contains(e.Location);
            _isLogoutHovered = _logoutRect.Contains(e.Location);

            if (prevSwitch != _isSwitchHovered || prevLogout != _isLogoutHovered)
            {
                Cursor = (_isSwitchHovered || _isLogoutHovered) ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isSwitchHovered = false;
            _isLogoutHovered = false;
            Cursor = Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (_switchRect.Contains(e.Location))
            {
                SwitchUserClicked?.Invoke(this, EventArgs.Empty);
            }
            else if (_logoutRect.Contains(e.Location))
            {
                LogoutClicked?.Invoke(this, EventArgs.Empty);
                Session.Logout();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var skin = EffectiveSkin;
            bool isDark = skin.IsDark;

            Color backColor = isDark ? Color.FromArgb(26, 32, 44) : Color.FromArgb(243, 244, 246);
            Color borderColor = isDark ? Color.FromArgb(51, 65, 85) : Color.FromArgb(209, 213, 219);
            Color textColor = isDark ? Color.FromArgb(241, 245, 249) : Color.FromArgb(15, 23, 42);
            Color subColor = isDark ? Color.FromArgb(148, 163, 184) : Color.FromArgb(100, 116, 139);

            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = CreateRoundedRectanglePath(bounds, 6))
            using (var brush = new SolidBrush(backColor))
            using (var pen = new Pen(borderColor, 1f))
            {
                g.FillPath(brush, path);
                g.DrawPath(pen, path);
            }

            var session = Session;
            var user = session.CurrentUser;
            bool auth = session.IsAuthenticated;

            int curX = 6;
            int cy = Height / 2;

            if (ShowAvatar)
            {
                int avSize = Height - 10;
                var avRect = new Rectangle(curX, (Height - avSize) / 2, avSize, avSize);

                Color avColor = auth ? Color.FromArgb(14, 165, 233) : Color.FromArgb(100, 116, 139);
                using (var avBrush = new SolidBrush(avColor))
                {
                    g.FillEllipse(avBrush, avRect);
                }

                string initials = auth && user != null
                    ? (user.DisplayName.Length > 0 ? user.DisplayName.Substring(0, Math.Min(2, user.DisplayName.Length)).ToUpperInvariant() : "U")
                    : "?";

                using (var avFont = new Font(Font.FontFamily, 8f, FontStyle.Bold))
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString(initials, avFont, Brushes.White, avRect, sf);
                }

                curX += avSize + 6;
            }

            // User Name and Role
            string nameText = auth && user != null ? user.DisplayName : "Not Signed In";
            string roleText = auth && user != null && user.Roles.Count > 0 ? user.Roles[0] : "Guest";

            using (var nameFont = new Font(Font.FontFamily, 8.5f, FontStyle.Bold))
            using (var roleFont = new Font(Font.FontFamily, 7.5f, FontStyle.Regular))
            using (var textBrush = new SolidBrush(textColor))
            using (var subBrush = new SolidBrush(subColor))
            {
                g.DrawString(nameText, nameFont, textBrush, curX, 4);
                g.DrawString(roleText, roleFont, subBrush, curX, 18);
            }

            // Right-aligned actions & countdown
            int rightX = Width - 8;

            if (auth)
            {
                // Logout button [X]
                int btnSize = 18;
                _logoutRect = new Rectangle(rightX - btnSize, (Height - btnSize) / 2, btnSize, btnSize);
                rightX -= btnSize + 4;

                using (var btnBrush = new SolidBrush(_isLogoutHovered ? Color.FromArgb(239, 68, 68) : Color.FromArgb(71, 85, 105)))
                {
                    g.FillEllipse(btnBrush, _logoutRect);
                }
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                using (var iconFont = new Font(Font.FontFamily, 7.5f, FontStyle.Bold))
                {
                    g.DrawString("✕", iconFont, Brushes.White, _logoutRect, sf);
                }

                // Switch button [⇄]
                _switchRect = new Rectangle(rightX - btnSize, (Height - btnSize) / 2, btnSize, btnSize);
                rightX -= btnSize + 6;

                using (var btnBrush = new SolidBrush(_isSwitchHovered ? Color.FromArgb(14, 165, 233) : Color.FromArgb(71, 85, 105)))
                {
                    g.FillEllipse(btnBrush, _switchRect);
                }
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                using (var iconFont = new Font(Font.FontFamily, 8f, FontStyle.Bold))
                {
                    g.DrawString("⇄", iconFont, Brushes.White, _switchRect, sf);
                }

                // Countdown
                if (ShowCountdown && session.IdleTimeout > TimeSpan.Zero)
                {
                    var remain = session.IdleTimeout - session.ElapsedIdleTime;
                    if (remain < TimeSpan.Zero) remain = TimeSpan.Zero;
                    string timeStr = $"{(int)remain.TotalMinutes}:{remain.Seconds:D2}";

                    using (var timeFont = new Font(Font.FontFamily, 7.5f, FontStyle.Regular))
                    using (var timeBrush = new SolidBrush(remain.TotalSeconds < 60 ? Color.FromArgb(239, 68, 68) : subColor))
                    {
                        var size = g.MeasureString(timeStr, timeFont);
                        rightX -= (int)size.Width + 4;
                        g.DrawString(timeStr, timeFont, timeBrush, rightX, (Height - size.Height) / 2);
                    }
                }
            }
            else
            {
                _logoutRect = Rectangle.Empty;
                _switchRect = Rectangle.Empty;
            }
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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _countdownTimer.Dispose();
                SessionContext.Current.UserChanged -= OnUserChanged;
            }
            base.Dispose(disposing);
        }
    }
}
