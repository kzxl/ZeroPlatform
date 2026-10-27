using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using ZeroUI.WinForms.Rendering;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Editors
{
    public enum PasswordStrength
    {
        None,
        Weak,
        Fair,
        Good,
        Strong
    }

    /// <summary>
    /// Modern password input control with embedded eye toggle button and real-time password strength meter.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Editors")]
    [Description("Secure password input with show/hide eye toggle and visual strength meter")]
    public class ZPasswordBox : Control
    {
        private TextBox _innerTextBox;
        private bool _showToggleVisibility = true;
        private bool _showStrengthMeter = true;
        private bool _isPasswordVisible = false;
        private PasswordStrength _strength = PasswordStrength.None;
        private string _placeholderText = "Enter password...";
        private bool _isFocused = false;

        public event EventHandler? PasswordChanged;

        [Category("Appearance")]
        [DefaultValue("Enter password...")]
        public string PlaceholderText
        {
            get => _placeholderText;
            set { _placeholderText = value ?? string.Empty; Invalidate(); }
        }

        [Category("Behavior")]
        [DefaultValue(true)]
        public bool ShowToggleVisibility
        {
            get => _showToggleVisibility;
            set { _showToggleVisibility = value; UpdateLayout(); Invalidate(); }
        }

        [Category("Behavior")]
        [DefaultValue(true)]
        public bool ShowStrengthMeter
        {
            get => _showStrengthMeter;
            set { _showStrengthMeter = value; Invalidate(); }
        }

        [Category("Data")]
        [Browsable(false)]
        public string Password
        {
            get => _innerTextBox.Text;
            set => _innerTextBox.Text = value ?? string.Empty;
        }

        [Category("Data")]
        [Browsable(false)]
        public PasswordStrength Strength => _strength;

        public ZPasswordBox()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);

            Size = new Size(260, 42);
            BackColor = Color.Transparent;

            _innerTextBox = new TextBox
            {
                BorderStyle = BorderStyle.None,
                UseSystemPasswordChar = true,
                Font = ZeroFontCache.Get("Segoe UI", 9.5f, FontStyle.Regular)
            };

            _innerTextBox.TextChanged += (s, e) =>
            {
                CalculateStrength();
                PasswordChanged?.Invoke(this, EventArgs.Empty);
                Invalidate();
            };

            _innerTextBox.GotFocus += (s, e) => { _isFocused = true; Invalidate(); };
            _innerTextBox.LostFocus += (s, e) => { _isFocused = false; Invalidate(); };

            Controls.Add(_innerTextBox);
            UpdateLayout();
            UpdateTheme();

            ZeroTheme.ThemeChanged += (s, e) => UpdateTheme();
        }

        private void UpdateTheme()
        {
            var colors = ZeroTheme.Colors;
            _innerTextBox.BackColor = colors.Surface;
            _innerTextBox.ForeColor = colors.TextPrimary;
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateLayout();
        }

        private void UpdateLayout()
        {
            if (_innerTextBox == null) return;
            int eyeWidth = _showToggleVisibility ? 28 : 6;
            int meterHeight = _showStrengthMeter ? 4 : 0;
            int textHeight = _innerTextBox.PreferredHeight;

            int top = (Height - meterHeight - textHeight) / 2;
            _innerTextBox.SetBounds(10, Math.Max(2, top), Width - 16 - eyeWidth, textHeight);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (_showToggleVisibility && e.X >= Width - 32 && e.X <= Width - 4 && e.Y <= Height - 8)
            {
                _isPasswordVisible = !_isPasswordVisible;
                _innerTextBox.UseSystemPasswordChar = !_isPasswordVisible;
                Invalidate();
            }
            else
            {
                _innerTextBox.Focus();
            }
        }

        private void CalculateStrength()
        {
            string pwd = _innerTextBox.Text;
            if (string.IsNullOrEmpty(pwd))
            {
                _strength = PasswordStrength.None;
                return;
            }

            int score = 0;
            if (pwd.Length >= 8) score++;
            if (pwd.Length >= 12) score++;
            if (Regex.IsMatch(pwd, @"[A-Z]")) score++;
            if (Regex.IsMatch(pwd, @"[a-z]")) score++;
            if (Regex.IsMatch(pwd, @"[0-9]")) score++;
            if (Regex.IsMatch(pwd, @"[^A-Za-z0-9]")) score++;

            if (score <= 2) _strength = PasswordStrength.Weak;
            else if (score == 3) _strength = PasswordStrength.Fair;
            else if (score <= 5) _strength = PasswordStrength.Good;
            else _strength = PasswordStrength.Strong;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var colors = ZeroTheme.Colors;
            int meterH = _showStrengthMeter ? 4 : 0;
            var boxRect = new Rectangle(1, 1, Width - 3, Height - meterH - 4);

            // 1. Background & Border
            using (var bgBrush = new SolidBrush(colors.Surface))
            {
                g.FillRectangle(bgBrush, boxRect);
            }

            Color borderColor = _isFocused ? colors.Primary : colors.Border;
            using (var borderPen = new Pen(borderColor, _isFocused ? 1.5f : 1f))
            {
                g.DrawRectangle(borderPen, boxRect);
            }

            // 2. Placeholder Text (when empty and not focused)
            if (string.IsNullOrEmpty(_innerTextBox.Text) && !_isFocused && !string.IsNullOrEmpty(_placeholderText))
            {
                var phFont = ZeroFontCache.Get("Segoe UI", 9f, FontStyle.Italic);
                using var phBrush = new SolidBrush(colors.TextSecondary);
                g.DrawString(_placeholderText, phFont, phBrush, 12, _innerTextBox.Top);
            }

            // 3. Eye Icon (Show/Hide Password)
            if (_showToggleVisibility)
            {
                int eyeX = Width - 24;
                int eyeY = (boxRect.Height - 12) / 2 + 1;

                Color iconColor = _isPasswordVisible ? colors.Primary : colors.TextSecondary;
                using var iconPen = new Pen(iconColor, 1.4f);

                // Draw stylized eye shape
                g.DrawEllipse(iconPen, eyeX - 6, eyeY - 2, 14, 8);
                using var dotBrush = new SolidBrush(iconColor);
                g.FillEllipse(dotBrush, eyeX - 1, eyeY + 1, 4, 4);

                if (_isPasswordVisible)
                {
                    // Strikethrough line when visible
                    g.DrawLine(iconPen, eyeX - 6, eyeY + 7, eyeX + 8, eyeY - 3);
                }
            }

            // 4. Strength Meter Bar
            if (_showStrengthMeter && _strength != PasswordStrength.None)
            {
                int barY = Height - 4;
                int totalW = Width - 4;

                Color meterColor = _strength switch
                {
                    PasswordStrength.Weak => Color.FromArgb(231, 76, 60),
                    PasswordStrength.Fair => Color.FromArgb(243, 156, 18),
                    PasswordStrength.Good => Color.FromArgb(241, 196, 15),
                    PasswordStrength.Strong => Color.FromArgb(46, 204, 113),
                    _ => Color.Transparent
                };

                float percent = (int)_strength / 4.0f;
                int activeW = (int)(totalW * percent);

                using (var fillBrush = new SolidBrush(meterColor))
                {
                    g.FillRectangle(fillBrush, 2, barY, activeW, 3);
                }
                using (var remBrush = new SolidBrush(colors.Border))
                {
                    if (totalW - activeW > 0)
                        g.FillRectangle(remBrush, 2 + activeW, barY, totalW - activeW, 3);
                }
            }
        }
    }

    [Obsolete("ZeroPasswordBox is deprecated. Use ZPasswordBox instead.")]
    [ToolboxItem(false)]
    public class ZeroPasswordBox : ZPasswordBox { }
}
