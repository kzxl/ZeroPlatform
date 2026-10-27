using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Editors;
using ZeroUI.Core.Theme;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Base
{
    /// <summary>
    /// Architectural strongly-typed base class for all ZeroUI WinForms data editors and form inputs.
    /// Provides standardized EditValue/Value synchronization, dirty tracking (IsModified),
    /// validation states, focus glow rings, placeholder text, and embedded inner-box lifecycles.
    /// </summary>
    /// <typeparam name="TValue">The primary strongly-typed value managed by the editor.</typeparam>
    [ToolboxItem(false)]
    public abstract class EditorBase<TValue> : ControlBase, IZeroEditor<TValue>
    {
        private TValue _value = default!;
        private bool _isModified;
        private bool _readOnly;
        private string _placeholder = string.Empty;
        private bool _showClearButton;
        private bool _isHovered;
        private bool _isFocused;
        private bool _hasError;
        private string? _errorText;

        protected Rectangle ClearButtonRect;
        protected bool HoverOnClear;

        #region Events

        public event EventHandler? ValueChanged;
        public event EventHandler? EditValueChanged;
        public event EventHandler<ValueChangingEventArgs<TValue>>? ValueChanging;

        #endregion

        #region Properties & IZeroEditor Implementation

        [Category("Data")]
        [Description("The strongly-typed value managed by this editor.")]
        public virtual TValue Value
        {
            get => _value;
            set
            {
                if (!Equals(_value, value))
                {
                    var args = new ValueChangingEventArgs<TValue>(_value, value);
                    OnValueChanging(args);
                    if (args.Cancel)
                    {
                        return;
                    }

                    _value = args.NewValue;
                    IsModified = true;
                    OnValueChanged();
                    Invalidate();
                }
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public virtual object? EditValue
        {
            get => Value;
            set
            {
                if (value == null || value == DBNull.Value)
                {
                    Value = default!;
                }
                else if (value is TValue typedVal)
                {
                    Value = typedVal;
                }
                else if (TryConvertEditValue(value, out TValue converted))
                {
                    Value = converted;
                }
            }
        }

        [Category("Behavior")]
        [DefaultValue(false)]
        [Description("Determines whether the value has been modified by user interaction.")]
        public bool IsModified
        {
            get => _isModified;
            set
            {
                if (_isModified != value)
                {
                    _isModified = value;
                    OnModifiedChanged();
                }
            }
        }

        [Category("Behavior")]
        [DefaultValue(false)]
        [Description("Determines whether the editor value is read-only.")]
        public virtual bool ReadOnly
        {
            get => _readOnly;
            set
            {
                if (_readOnly != value)
                {
                    _readOnly = value;
                    OnReadOnlyChanged();
                    Invalidate();
                }
            }
        }

        [Browsable(false)]
        public bool IsReadOnly
        {
            get => ReadOnly;
            set => ReadOnly = value;
        }

        [Category("Appearance")]
        [DefaultValue("")]
        [Description("Placeholder text rendered when the editor is empty.")]
        public string Placeholder
        {
            get => _placeholder;
            set
            {
                if (_placeholder != value)
                {
                    _placeholder = value ?? string.Empty;
                    Invalidate();
                }
            }
        }

        [Category("Appearance")]
        [DefaultValue(false)]
        [Description("Determines whether an inline clear button is displayed when text is present.")]
        public bool ShowClearButton
        {
            get => _showClearButton;
            set
            {
                if (_showClearButton != value)
                {
                    _showClearButton = value;
                    Invalidate();
                }
            }
        }

        [Category("Validation")]
        [DefaultValue(false)]
        [Description("Indicates whether this editor currently contains a validation error.")]
        public bool HasError
        {
            get => _hasError;
            set
            {
                if (_hasError != value)
                {
                    _hasError = value;
                    Invalidate();
                }
            }
        }

        [Category("Validation")]
        [DefaultValue(null)]
        [Description("The error message associated with the current validation failure.")]
        public string? ErrorText
        {
            get => _errorText;
            set
            {
                _errorText = value;
                HasError = !string.IsNullOrEmpty(value);
            }
        }

        [Browsable(false)]
        protected bool IsHovered => _isHovered;

        [Browsable(false)]
        protected bool IsEditorFocused => _isFocused;

        #endregion

        protected EditorBase()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.Selectable |
                ControlStyles.SupportsTransparentBackColor, true);

            Size = new Size(200, 32);
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            Cursor = Cursors.IBeam;
        }

        #region Value Conversion & Lifecycle Overrides

        /// <summary>
        /// Converts an untyped EditValue assignment into the strongly-typed TValue.
        /// Override in derived editors to provide custom type parsers.
        /// </summary>
        protected virtual bool TryConvertEditValue(object? rawValue, out TValue converted)
        {
            if (rawValue == null)
            {
                converted = default!;
                return true;
            }

            try
            {
                converted = (TValue)Convert.ChangeType(rawValue, typeof(TValue));
                return true;
            }
            catch
            {
                converted = default!;
                return false;
            }
        }

        protected virtual void OnValueChanging(ValueChangingEventArgs<TValue> e)
        {
            ValueChanging?.Invoke(this, e);
        }

        protected virtual void OnValueChanged()
        {
            ValueChanged?.Invoke(this, EventArgs.Empty);
            EditValueChanged?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnModifiedChanged()
        {
        }

        protected virtual void OnReadOnlyChanged()
        {
        }

        public virtual void Reset()
        {
            _value = default!;
            _isModified = false;
            _hasError = false;
            _errorText = null;
            OnValueChanged();
            Invalidate();
        }

        public virtual void Clear()
        {
            Reset();
        }

        #endregion

        #region Mouse & Focus State Machine

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
            HoverOnClear = false;
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

        #endregion

        #region Standard Visual Rendering Helpers

        /// <summary>
        /// Renders standard anti-aliased editor borders, background fills, focus glow rings, and validation error frames.
        /// </summary>
        protected virtual void DrawStandardEditorFrame(Graphics g, Rectangle bounds, ZeroThemePalette palette)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Background Fill
            Color bgColor = !Enabled ? palette.Background : (ReadOnly ? palette.Surface : palette.CardBackground);
            using (var bgBrush = new SolidBrush(bgColor))
            {
                g.FillRectangle(bgBrush, bounds);
            }

            // Determine Border Color
            Color borderColor = palette.Border;
            if (HasError)
            {
                borderColor = palette.Danger;
            }
            else if (_isFocused)
            {
                borderColor = palette.Primary; // Active focus
            }
            else if (_isHovered)
            {
                borderColor = palette.PrimaryHover;
            }

            // Draw Border
            var borderRect = new Rectangle(bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
            using (var pen = new Pen(borderColor, 1f))
            {
                g.DrawRectangle(pen, borderRect);
            }

            // Draw Outer Focus Ring if focused and not in error
            if (_isFocused && !HasError)
            {
                using (var glowPen = new Pen(Color.FromArgb(48, palette.Primary), 2f))
                {
                    var glowRect = new Rectangle(bounds.X + 1, bounds.Y + 1, bounds.Width - 3, bounds.Height - 3);
                    g.DrawRectangle(glowPen, glowRect);
                }
            }

            // Draw Clear Button [X] if applicable
            if (ShowClearButton && !ReadOnly && HasValue())
            {
                int btnSize = 16;
                ClearButtonRect = new Rectangle(bounds.Right - btnSize - 6, bounds.Y + (bounds.Height - btnSize) / 2, btnSize, btnSize);

                Color xColor = HoverOnClear ? palette.TextPrimary : palette.TextSecondary;
                using (var xPen = new Pen(xColor, 1.4f))
                {
                    int m = 4;
                    g.DrawLine(xPen, ClearButtonRect.Left + m, ClearButtonRect.Top + m, ClearButtonRect.Right - m, ClearButtonRect.Bottom - m);
                    g.DrawLine(xPen, ClearButtonRect.Left + m, ClearButtonRect.Bottom - m, ClearButtonRect.Right - m, ClearButtonRect.Top + m);
                }
            }
            else
            {
                ClearButtonRect = Rectangle.Empty;
            }
        }

        /// <summary>
        /// Determines whether the editor currently contains a non-empty, non-default value.
        /// </summary>
        protected virtual bool HasValue()
        {
            if (Value == null) return false;
            if (Value is string s) return !string.IsNullOrEmpty(s);
            return true;
        }

        /// <summary>
        /// Synchronizes and positions an embedded inner TextBox control cleanly inside this editor.
        /// </summary>
        protected void SetupInnerTextBox(TextBox innerBox, int leftPadding = 8, int rightPadding = 8)
        {
            if (innerBox == null) return;

            innerBox.BorderStyle = BorderStyle.None;
            innerBox.Font = Font;
            innerBox.BackColor = !Enabled ? CurrentPalette.Background : (ReadOnly ? CurrentPalette.Surface : CurrentPalette.CardBackground);
            innerBox.ForeColor = CurrentPalette.TextPrimary;

            int rightReserve = ShowClearButton && !ReadOnly && HasValue() ? 24 : rightPadding;
            int boxHeight = innerBox.PreferredHeight;
            int boxY = (Height - boxHeight) / 2;
            int boxWidth = Math.Max(10, Width - leftPadding - rightReserve);

            innerBox.SetBounds(leftPadding, boxY, boxWidth, boxHeight);
        }

        #endregion
    }

    /// <summary>
    /// Obsolete alias for <see cref="EditorBase{TValue}"/> to maintain backward compatibility.
    /// </summary>
    [Obsolete("Use EditorBase<TValue> instead.")]
    public abstract class ZeroEditorBase<TValue> : EditorBase<TValue>
    {
    }
}

