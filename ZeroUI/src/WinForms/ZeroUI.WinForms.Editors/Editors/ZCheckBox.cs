using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Editors;
using ZeroUI.Core.Theme;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Editors
{
    /// <summary>
    /// Modern anti-aliased flat CheckBox control for ZeroUI.
    /// Supports two-state and three-state (Checked, Unchecked, Indeterminate),
    /// keyboard spacebar toggling, custom check alignment, and theme synchronization.
    /// Canonical drop-in replacement for standard <see cref="System.Windows.Forms.CheckBox"/>.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Editors")]
    [DefaultProperty("Checked")]
    [DefaultEvent("CheckedChanged")]
    [Description("Modern anti-aliased flat CheckBox control with tri-state support")]
    [ToolboxBitmap(typeof(ZeroIcons), "CheckEdit.bmp")]
    public class ZCheckBox : ControlBase, IZeroEditor
    {
        private CheckState _checkState = CheckState.Unchecked;
        private bool _threeState = false;
        private ContentAlignment _checkAlign = ContentAlignment.MiddleLeft;
        private bool _isHovered = false;
        private bool _isPressed = false;
        private bool _isFocused = false;

        public event EventHandler? CheckedChanged;
        public event EventHandler? CheckStateChanged;
        public event EventHandler? EditValueChanged;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public object? EditValue
        {
            get => _threeState ? (object?)CheckState : Checked;
            set
            {
                if (value == null)
                {
                    if (_threeState) CheckState = CheckState.Indeterminate;
                    else Checked = false;
                }
                else if (value is bool b)
                {
                    Checked = b;
                }
                else if (value is CheckState cs)
                {
                    CheckState = cs;
                }
                else if (bool.TryParse(value.ToString(), out bool parsedBool))
                {
                    Checked = parsedBool;
                }
            }
        }

        [Category("Behavior")]
        [DefaultValue(false)]
        public bool IsModified { get; set; } = false;

        [Category("Behavior")]
        [DefaultValue(false)]
        public bool ReadOnly { get; set; } = false;

        public void Reset()
        {
            CheckState = CheckState.Unchecked;
            IsModified = false;
        }

        public void Clear() => Reset();

        public ZCheckBox()
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
            BackColor = Color.Transparent;
            Invalidate();
        }

        [Category("Appearance")]
        [DefaultValue("CheckBox")]
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
            get => _checkState != CheckState.Unchecked;
            set
            {
                CheckState targetState = value ? CheckState.Checked : CheckState.Unchecked;
                if (_checkState != targetState)
                {
                    _checkState = targetState;
                    IsModified = true;
                    Invalidate();
                    CheckedChanged?.Invoke(this, EventArgs.Empty);
                    CheckStateChanged?.Invoke(this, EventArgs.Empty);
                    EditValueChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        [Category("Behavior")]
        [DefaultValue(CheckState.Unchecked)]
        public CheckState CheckState
        {
            get => _checkState;
            set
            {
                if (_checkState != value)
                {
                    bool wasChecked = Checked;
                    _checkState = value;
                    IsModified = true;
                    Invalidate();
                    if (wasChecked != Checked)
                    {
                        CheckedChanged?.Invoke(this, EventArgs.Empty);
                    }
                    CheckStateChanged?.Invoke(this, EventArgs.Empty);
                    EditValueChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        [Category("Behavior")]
        [DefaultValue(false)]
        public bool ThreeState
        {
            get => _threeState;
            set
            {
                _threeState = value;
                if (!value && _checkState == CheckState.Indeterminate)
                {
                    CheckState = CheckState.Unchecked;
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

        private bool _autoCheck = true;

        [Category("Behavior")]
        [DefaultValue(true)]
        [Description("Gets or sets a value indicating whether the Checked or CheckState value and the check box's appearance are automatically changed when the check box is clicked.")]
        public bool AutoCheck
        {
            get => _autoCheck;
            set => _autoCheck = value;
        }

        public void Toggle()
        {
            if (ReadOnly || !Enabled || !_autoCheck) return;

            if (_threeState)
            {
                switch (_checkState)
                {
                    case CheckState.Unchecked:
                        CheckState = CheckState.Checked;
                        break;
                    case CheckState.Checked:
                        CheckState = CheckState.Indeterminate;
                        break;
                    case CheckState.Indeterminate:
                        CheckState = CheckState.Unchecked;
                        break;
                }
            }
            else
            {
                Checked = !Checked;
            }
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            Focus();
            Toggle();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Space)
            {
                Toggle();
                e.Handled = true;
                e.SuppressKeyPress = true;
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
            if (e.Button == MouseButtons.Left)
            {
                _isPressed = false;
                Invalidate();
            }
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
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            var p = CurrentPalette;
            int boxSize = 18;
            int boxY = (Height - boxSize) / 2;
            int boxX = (_checkAlign == ContentAlignment.MiddleRight) ? Width - boxSize - 2 : 2;

            Rectangle boxRect = new Rectangle(boxX, boxY, boxSize, boxSize);

            // 1. Draw Checkbox Box
            bool isCheckedOrIndeterminate = _checkState != CheckState.Unchecked;
            Color boxBg = isCheckedOrIndeterminate
                ? (_isPressed ? Darken(p.Primary, 0.15f) : p.Primary)
                : (_isHovered ? p.Hover : p.Surface);

            Color borderColor = isCheckedOrIndeterminate
                ? p.Primary
                : (_isHovered ? p.Primary : p.Border);

            using (var path = CreateRoundedRectangle(boxRect, 4))
            {
                using (var brush = new SolidBrush(boxBg))
                {
                    g.FillPath(brush, path);
                }
                using (var pen = new Pen(borderColor, 1.5f))
                {
                    g.DrawPath(pen, path);
                }
            }

            // 2. Draw Checkmark or Indeterminate Bar
            if (_checkState == CheckState.Checked)
            {
                using (var pen = new Pen(Color.White, 2.0f))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    pen.LineJoin = LineJoin.Round;

                    PointF p1 = new PointF(boxRect.X + 4.5f, boxRect.Y + 9.5f);
                    PointF p2 = new PointF(boxRect.X + 7.5f, boxRect.Y + 13.0f);
                    PointF p3 = new PointF(boxRect.X + 13.5f, boxRect.Y + 5.5f);

                    g.DrawLines(pen, new[] { p1, p2, p3 });
                }
            }
            else if (_checkState == CheckState.Indeterminate)
            {
                Rectangle barRect = new Rectangle(boxRect.X + 4, boxRect.Y + 8, boxRect.Width - 8, 2);
                using (var brush = new SolidBrush(Color.White))
                {
                    g.FillRectangle(brush, barRect);
                }
            }

            // 3. Draw Label Text
            if (!string.IsNullOrEmpty(Text))
            {
                Rectangle textRect;
                if (_checkAlign == ContentAlignment.MiddleRight)
                {
                    textRect = new Rectangle(2, 0, Width - boxSize - 8, Height);
                }
                else
                {
                    textRect = new Rectangle(boxSize + 8, 0, Width - boxSize - 10, Height);
                }

                Color textColor = Enabled ? p.TextPrimary : p.TextSecondary;
                TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.WordEllipsis;
                TextRenderer.DrawText(g, Text, Font, textRect, textColor, flags);
            }

            // 4. Focus border
            if (_isFocused && ShowFocusCues)
            {
                Rectangle focusRect = new Rectangle(boxX - 2, boxY - 2, boxSize + 4, boxSize + 4);
                using (var focusPen = new Pen(Color.FromArgb(120, p.Primary), 1f) { DashStyle = DashStyle.Dot })
                {
                    g.DrawRectangle(focusPen, focusRect);
                }
            }
        }

        private static GraphicsPath CreateRoundedRectangle(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int diameter = radius * 2;
            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static Color Darken(Color c, float amount)
        {
            int r = Math.Max(0, (int)(c.R * (1f - amount)));
            int g = Math.Max(0, (int)(c.G * (1f - amount)));
            int b = Math.Max(0, (int)(c.B * (1f - amount)));
            return Color.FromArgb(c.A, r, g, b);
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Convenience alias for <see cref="ZCheckBox"/>.
    /// </summary>
    public class ZCheckEdit : ZCheckBox { }

    /// <summary>
    /// Legacy alias for <see cref="ZCheckBox"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("CheckEdit is deprecated and will be removed in 5 release cycles. Please migrate to ZCheckBox instead.")]
    [ToolboxItem(false)]
    public class CheckEdit : ZCheckBox
    {
    }

    /// <summary>
    /// Legacy alias for <see cref="ZCheckBox"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroCheckBox is deprecated and will be removed in 5 release cycles. Please migrate to ZCheckBox instead.")]
    [ToolboxItem(false)]
    public class ZeroCheckBox : ZCheckBox
    {
    }

    #endregion
}
