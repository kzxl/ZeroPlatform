using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Theme;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Editors
{
    /// <summary>
    /// Modern anti-aliased flat RadioButton control for ZeroUI.
    /// Provides mutual exclusion across siblings or GroupName, keyboard navigation,
    /// and responsive ZeroTheme styling.
    /// Canonical drop-in replacement for standard <see cref="System.Windows.Forms.RadioButton"/>.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Editors")]
    [DefaultProperty("Checked")]
    [DefaultEvent("CheckedChanged")]
    [Description("Modern theme-aware radio button control adhering to the canonical Z-prefix standard")]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroRadioButton.bmp")]
    public class ZRadioButton : ControlBase
    {
        private bool _checked = false;
        private bool _autoCheck = true;
        private string? _groupName;
        private ContentAlignment _checkAlign = ContentAlignment.MiddleLeft;
        private bool _isHovered = false;
        private bool _isPressed = false;
        private bool _isFocused = false;

        public event EventHandler? CheckedChanged;

        public ZRadioButton()
        {
            SetStyle(ControlStyles.Selectable, true);

            Size = new Size(140, 26);
            Cursor = Cursors.Hand;
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            BackColor = Color.Transparent;

            ZeroUIConfig.FontChanged += (s, e) =>
            {
                Font = ZeroUIConfig.DefaultFont;
                Invalidate();
            };
        }

        protected override void OnThemeChanged(ZeroSkin skin)
        {
            base.OnThemeChanged(skin);
            Invalidate();
        }

        [Category("Appearance")]
        [DefaultValue("RadioButton")]
#pragma warning disable CS8765, CS8764
        public override string Text
        {
            get => base.Text;
            set
            {
                base.Text = value;
                Invalidate();
            }
        }
#pragma warning restore CS8765, CS8764

        [Category("Behavior")]
        [DefaultValue(false)]
        public bool Checked
        {
            get => _checked;
            set
            {
                if (_checked != value)
                {
                    _checked = value;
                    if (_checked && _autoCheck)
                    {
                        UncheckSiblings();
                    }
                    Invalidate();
                    CheckedChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        [Category("Behavior")]
        [DefaultValue(true)]
        public bool AutoCheck
        {
            get => _autoCheck;
            set => _autoCheck = value;
        }

        [Category("Behavior")]
        [DefaultValue(null)]
        public string? GroupName
        {
            get => _groupName;
            set
            {
                _groupName = value;
                if (_checked && _autoCheck)
                {
                    UncheckSiblings();
                }
            }
        }

        [Category("Appearance")]
        [DefaultValue(ContentAlignment.MiddleLeft)]
        public ContentAlignment CheckAlign
        {
            get => _checkAlign;
            set
            {
                _checkAlign = value;
                Invalidate();
            }
        }

        private void UncheckSiblings()
        {
            if (Parent == null) return;

            foreach (Control sibling in Parent.Controls)
            {
                if (sibling != this && sibling is ZRadioButton rb)
                {
                    bool matchGroup = string.IsNullOrEmpty(_groupName)
                        ? string.IsNullOrEmpty(rb.GroupName)
                        : string.Equals(_groupName, rb.GroupName, StringComparison.OrdinalIgnoreCase);

                    if (matchGroup && rb.Checked)
                    {
                        rb.Checked = false;
                    }
                }
            }
        }

        protected override void OnClick(EventArgs e)
        {
            if (_autoCheck && !_checked)
            {
                Checked = true;
            }
            base.OnClick(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Space)
            {
                if (_autoCheck && !_checked)
                {
                    Checked = true;
                }
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Down || e.KeyCode == Keys.Right)
            {
                SelectSibling(forward: true);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Left)
            {
                SelectSibling(forward: false);
                e.Handled = true;
            }
        }

        private void SelectSibling(bool forward)
        {
            if (Parent == null) return;

            var siblings = new List<ZRadioButton>();
            int currentIndex = -1;

            foreach (Control c in Parent.Controls)
            {
                if (c is ZRadioButton rb && rb.Enabled && rb.Visible)
                {
                    bool matchGroup = string.IsNullOrEmpty(_groupName)
                        ? string.IsNullOrEmpty(rb.GroupName)
                        : string.Equals(_groupName, rb.GroupName, StringComparison.OrdinalIgnoreCase);

                    if (matchGroup)
                    {
                        if (rb == this) currentIndex = siblings.Count;
                        siblings.Add(rb);
                    }
                }
            }

            if (siblings.Count <= 1 || currentIndex < 0) return;

            int targetIndex = forward
                ? (currentIndex + 1) % siblings.Count
                : (currentIndex - 1 + siblings.Count) % siblings.Count;

            var target = siblings[targetIndex];
            target.Focus();
            if (target.AutoCheck)
            {
                target.Checked = true;
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _isHovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isHovered = false;
            _isPressed = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                _isPressed = true;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _isPressed = false;
            Invalidate();
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            _isFocused = true;
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            _isFocused = false;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var palette = CurrentPalette;
            bool isDark = EffectiveSkin.IsDark;

            int circleSize = 18;
            int circleX = 4;
            int circleY = (Height - circleSize) / 2;

            if (_checkAlign == ContentAlignment.MiddleRight || _checkAlign == ContentAlignment.TopRight || _checkAlign == ContentAlignment.BottomRight)
            {
                circleX = Width - circleSize - 4;
            }

            var circleRect = new RectangleF(circleX, circleY, circleSize, circleSize);

            // Determine colors
            Color borderColor;
            Color fillColor;
            Color dotColor = Color.White;

            if (!Enabled)
            {
                borderColor = isDark ? Color.FromArgb(60, 65, 75) : Color.FromArgb(200, 205, 215);
                fillColor = isDark ? Color.FromArgb(35, 40, 50) : Color.FromArgb(240, 243, 246);
                dotColor = isDark ? Color.FromArgb(80, 85, 95) : Color.FromArgb(170, 175, 185);
            }
            else if (_checked)
            {
                borderColor = palette.Primary;
                fillColor = palette.Primary;
                dotColor = Color.White;
            }
            else if (_isPressed)
            {
                borderColor = palette.Primary;
                fillColor = Color.FromArgb(40, palette.Primary);
                dotColor = Color.White;
            }
            else if (_isHovered)
            {
                borderColor = palette.Primary;
                fillColor = palette.Surface;
            }
            else
            {
                borderColor = palette.Border;
                fillColor = palette.Surface;
            }

            // Focus glow
            if (_isFocused && Enabled)
            {
                using var glowPen = new Pen(Color.FromArgb(50, palette.Primary), 3f);
                g.DrawEllipse(glowPen, circleX - 1.5f, circleY - 1.5f, circleSize + 3f, circleSize + 3f);
            }

            // Background fill
            using (var fillBrush = new SolidBrush(fillColor))
            {
                g.FillEllipse(fillBrush, circleRect);
            }

            // Border ring
            float penWidth = _checked ? 1.5f : 1.25f;
            using (var borderPen = new Pen(borderColor, penWidth))
            {
                g.DrawEllipse(borderPen, circleRect);
            }

            // Inner check dot
            if (_checked)
            {
                float dotSize = circleSize * 0.42f;
                float dotX = circleX + (circleSize - dotSize) / 2f;
                float dotY = circleY + (circleSize - dotSize) / 2f;

                using var dotBrush = new SolidBrush(dotColor);
                g.FillEllipse(dotBrush, dotX, dotY, dotSize, dotSize);
            }

            // Text rendering
            if (!string.IsNullOrEmpty(Text))
            {
                int textX;
                int textW;

                if (circleX <= 4)
                {
                    textX = circleX + circleSize + 8;
                    textW = Width - textX - 2;
                }
                else
                {
                    textX = 2;
                    textW = circleX - 8;
                }

                var textRect = new Rectangle(textX, 0, Math.Max(10, textW), Height);
                Color textColor = Enabled ? palette.TextPrimary : palette.TextSecondary;

                var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.WordEllipsis | TextFormatFlags.NoPrefix;
                TextRenderer.DrawText(g, Text, Font, textRect, textColor, flags);
            }
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZRadioButton"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("RadioButtonControl is deprecated and will be removed in 5 release cycles. Please migrate to ZRadioButton instead.")]
    [ToolboxItem(false)]
    public class RadioButtonControl : ZRadioButton
    {
    }

    /// <summary>
    /// Legacy alias for <see cref="ZRadioButton"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroRadioButton is deprecated and will be removed in 5 release cycles. Please migrate to ZRadioButton instead.")]
    [ToolboxItem(false)]
    public class ZeroRadioButton : ZRadioButton
    {
    }

    #endregion
}
