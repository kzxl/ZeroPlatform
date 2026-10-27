using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Input.Masking;
using ZeroUI.Core.Theme;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Editors
{
    /// <summary>
    /// Modern anti-aliased masked text box for ZeroUI.
    /// Supports formatted input for IP addresses, MAC addresses, serial numbers, phone numbers, and lot codes.
    /// Provides theme synchronization and clean vector rendering.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Editors")]
    [DefaultProperty("Mask")]
    [DefaultEvent("TextChanged")]
    [Description("Modern anti-aliased masked text box")]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroMaskedTextBox.bmp")]
    public class ZMaskBox : ControlBase
    {
        private readonly MaskedTextBox _innerBox;
        private bool _isFocused = false;
        private bool _isHovered = false;

        public ZMaskBox()
        {
            BackColor = Color.Transparent;
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

            _innerBox = new MaskedTextBox
            {
                BorderStyle = BorderStyle.None,
                Font = Font,
                BackColor = ZeroTheme.Colors.Surface,
                ForeColor = ZeroTheme.Colors.TextPrimary,
                PromptChar = '_'
            };

            _innerBox.TextChanged += (s, e) =>
            {
                OnTextChanged(e);
                Invalidate();
            };
            _innerBox.GotFocus += (s, e) =>
            {
                _isFocused = true;
                Invalidate();
            };
            _innerBox.LostFocus += (s, e) =>
            {
                _isFocused = false;
                Invalidate();
            };
            _innerBox.KeyDown += (s, e) => OnKeyDown(e);
            _innerBox.KeyPress += (s, e) => OnKeyPress(e);
            _innerBox.KeyUp += (s, e) => OnKeyUp(e);

            Controls.Add(_innerBox);
            Size = new Size(220, 36);

            ZeroUIConfig.CornerStyleChanged += (s, e) => Invalidate();
            ZeroUIConfig.FontChanged += (s, e) =>
            {
                Font = ZeroUIConfig.DefaultFont;
                _innerBox.Font = ZeroUIConfig.DefaultFont;
                UpdateInnerBounds();
                Invalidate();
            };

            UpdateTheme();
            UpdateInnerBounds();
        }

        protected override void OnThemeChanged(ZeroSkin skin)
        {
            base.OnThemeChanged(skin);
            UpdateTheme();
        }

        private void UpdateTheme()
        {
            if (_innerBox == null) return;
            var p = CurrentPalette;
            _innerBox.BackColor = ReadOnly ? p.HeaderBackground : p.Surface;
            _innerBox.ForeColor = Enabled ? p.TextPrimary : p.TextSecondary;
            Invalidate();
        }

        private MaskDefinition? _maskDefinition;

        [Browsable(false)]
        public MaskDefinition? Definition
        {
            get => _maskDefinition;
            set
            {
                _maskDefinition = value;
                if (_innerBox != null && value != null)
                {
                    _innerBox.Mask = value.Pattern;
                }
            }
        }

        public void ApplyMask(MaskDefinition maskDefinition)
        {
            Definition = maskDefinition;
        }

        [Browsable(false)]
        public string RawText
        {
            get
            {
                if (_maskDefinition != null && _innerBox != null)
                {
                    Span<char> raw = stackalloc char[_maskDefinition.EditableCount];
                    string text = _innerBox.Text ?? string.Empty;
                    if (_maskDefinition.TryExtractRaw(text.AsSpan(), raw, out int written, _innerBox.PromptChar))
                    {
                        return new string(raw.Slice(0, written).ToArray());
                    }
                }
                return _innerBox?.Text ?? string.Empty;
            }
        }

        [Browsable(false)]
        public bool IsComplete => _innerBox?.MaskCompleted ?? false;

        [Category("Behavior")]
        [DefaultValue("")]
        public string Mask
        {
            get => _innerBox?.Mask ?? "";
            set
            {
                if (_innerBox != null) _innerBox.Mask = value ?? "";
                _maskDefinition = !string.IsNullOrEmpty(value) ? new MaskDefinition(value!) : null;
                Invalidate();
            }
        }

        [Category("Appearance")]
        [DefaultValue("")]
#pragma warning disable CS8765, CS8764
        public override string Text
        {
            get => _innerBox?.Text ?? "";
            set
            {
                if (_innerBox != null && _innerBox.Text != value)
                {
                    _innerBox.Text = value ?? "";
                    Invalidate();
                }
            }
        }
#pragma warning restore CS8765, CS8764

        [Category("Behavior")]
        [DefaultValue('_')]
        public char PromptChar
        {
            get => _innerBox?.PromptChar ?? '_';
            set
            {
                if (_innerBox != null) _innerBox.PromptChar = value;
                Invalidate();
            }
        }

        [Category("Behavior")]
        [DefaultValue(false)]
        public bool ReadOnly
        {
            get => _innerBox?.ReadOnly ?? false;
            set
            {
                if (_innerBox != null)
                {
                    _innerBox.ReadOnly = value;
                    UpdateTheme();
                }
            }
        }

        [Category("Behavior")]
        [DefaultValue(true)]
        public bool BeepOnError
        {
            get => _innerBox?.BeepOnError ?? true;
            set
            {
                if (_innerBox != null) _innerBox.BeepOnError = value;
            }
        }

        public void SelectAll() => _innerBox?.SelectAll();
        public void Clear() => _innerBox?.Clear();

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateInnerBounds();
        }

        private void UpdateInnerBounds()
        {
            if (_innerBox == null) return;

            int leftPad = 12;
            int rightPad = 12;
            int innerH = _innerBox.PreferredHeight;
            int innerY = Math.Max(2, (Height - innerH) / 2);
            int innerW = Math.Max(10, Width - leftPad - rightPad);

            _innerBox.SetBounds(leftPad, innerY, innerW, innerH);
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
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var palette = CurrentPalette;
            bool isDark = EffectiveSkin.IsDark;

            int effRadius = ZeroUIConfig.GetEffectiveRadius(6);
            var borderRect = new Rectangle(1, 1, Width - 2, Height - 2);

            Color borderColor;
            Color bgColor;

            if (!Enabled)
            {
                borderColor = isDark ? Color.FromArgb(55, 60, 70) : Color.FromArgb(215, 220, 228);
                bgColor = isDark ? Color.FromArgb(28, 33, 43) : Color.FromArgb(243, 246, 249);
            }
            else if (_isFocused)
            {
                borderColor = palette.Primary;
                bgColor = ReadOnly ? palette.HeaderBackground : palette.Surface;
            }
            else if (_isHovered)
            {
                borderColor = palette.Primary;
                bgColor = ReadOnly ? palette.HeaderBackground : palette.Surface;
            }
            else
            {
                borderColor = palette.Border;
                bgColor = ReadOnly ? palette.HeaderBackground : palette.Surface;
            }

            // Draw Background and Border
            using (var path = ZeroUIConfig.CreateRoundedRectangle(borderRect, effRadius))
            {
                using (var bgBrush = new SolidBrush(bgColor))
                {
                    g.FillPath(bgBrush, path);
                }

                float penWidth = _isFocused ? 1.5f : 1.0f;
                using (var pen = new Pen(borderColor, penWidth))
                {
                    g.DrawPath(pen, path);
                }
            }
        }
    }

    /// <summary>
    /// Obsolete alias for <see cref="ZMaskBox"/>.
    /// </summary>
    [Obsolete("ZeroMaskedTextBox is deprecated and will be removed in 5 release cycles. Please migrate to ZMaskBox instead.")]
    [ToolboxItem(false)]
    public class ZeroMaskedTextBox : ZMaskBox { }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZMaskBox"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("MaskBox is deprecated and will be removed in 5 release cycles. Please migrate to ZMaskBox instead.")]
    [ToolboxItem(false)]
    public class MaskBox : ZMaskBox
    {
    }

    #endregion
}
