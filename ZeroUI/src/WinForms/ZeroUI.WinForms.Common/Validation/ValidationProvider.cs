using System;

using ZeroUI.WinForms.Icons;using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Editors;
using ZeroUI.Core.Validation;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Validation
{
    public enum ErrorIconType : byte
    {
        Error = 0,
        Warning = 1,
        Information = 2
    }

    /// <summary>
    /// Modern, anti-aliased error and validation provider for WinForms applications.
    /// Displays crisp vector badges with hover tooltips next to target controls without screen flickering.
    /// Integrates seamlessly with IZeroEditor, IControlValidationRule, and standard forms.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Validation")]
    [ProvideProperty("Error", typeof(Control))]
    [ProvideProperty("IconAlignment", typeof(Control))]
    [ProvideProperty("IconPadding", typeof(Control))]
    [Description("Modern vector error provider with smooth hover tooltips")]
    [ToolboxBitmap(typeof(ZeroIcons), "ValidationProvider.bmp")]
    public class ValidationProvider : Component, IExtenderProvider
    {
        private class ControlRuleBinding
        {
            public List<IControlValidationRule> Rules { get; } = new List<IControlValidationRule>();
            public ValidationTrigger Trigger { get; set; } = ValidationTrigger.ValueChanged;
        }

        private readonly Dictionary<Control, ErrorEntry> _entries = new Dictionary<Control, ErrorEntry>();
        private readonly Dictionary<Control, ControlRuleBinding> _rules = new Dictionary<Control, ControlRuleBinding>();
        private readonly ToolTip _toolTip = new ToolTip();

        private int _defaultPadding = 4;
        private ErrorIconAlignment _defaultAlignment = ErrorIconAlignment.MiddleRight;

        public ValidationProvider()
        {
            _toolTip.AutoPopDelay = 5000;
            _toolTip.InitialDelay = 200;
            _toolTip.ReshowDelay = 100;
        }

        public ValidationProvider(IContainer container) : this()
        {
            container.Add(this);
        }

        public bool CanExtend(object extendee) => extendee is Control;

        [Category("Appearance")]
        [DefaultValue(4)]
        [Description("Default spacing in pixels between the target control and the error badge.")]
        public int IconPadding
        {
            get => _defaultPadding;
            set => _defaultPadding = Math.Max(0, value);
        }

        [Category("Appearance")]
        [DefaultValue(ErrorIconAlignment.MiddleRight)]
        [Description("Default positioning alignment of the error badge relative to the target control.")]
        public ErrorIconAlignment IconAlignment
        {
            get => _defaultAlignment;
            set => _defaultAlignment = value;
        }

        [Browsable(false)]
        public bool HasErrors
        {
            get
            {
                foreach (var kvp in _entries)
                {
                    if (!string.IsNullOrEmpty(kvp.Value.Message)) return true;
                }
                return false;
            }
        }

        [DefaultValue("")]
        [DisplayName("Error")]
        [Category("Validation")]
        public string GetError(Control control)
        {
            if (control != null && _entries.TryGetValue(control, out var entry))
            {
                return entry.Message;
            }
            return string.Empty;
        }

        public void SetError(Control control, string? value)
        {
            SetError(control, value, ErrorIconType.Error);
        }

        public void SetError(Control control, string? value, ErrorIconType iconType)
        {
            if (control == null) return;

            string msg = value ?? string.Empty;

            if (string.IsNullOrEmpty(msg))
            {
                if (_entries.TryGetValue(control, out var existing))
                {
                    existing.Dispose();
                    _entries.Remove(control);
                }
                return;
            }

            if (_entries.TryGetValue(control, out var entry))
            {
                entry.Update(msg, iconType);
            }
            else
            {
                var newEntry = new ErrorEntry(control, msg, iconType, _defaultAlignment, _defaultPadding, _toolTip);
                _entries[control] = newEntry;
            }
        }

        /// <summary>
        /// Applies the outcome of a Core ValidationResult to the specified control.
        /// Automatically picks the highest severity notification and maps to vector badges.
        /// </summary>
        public void SetResult(Control control, ValidationResult? result)
        {
            if (control == null) return;

            if (result == null || (result.IsValid && result.Messages.Count == 0))
            {
                SetError(control, null);
                return;
            }

            var messages = result.Messages;
            if (messages.Count == 0)
            {
                SetError(control, null);
                return;
            }

            // Find highest severity (Error < Warning < Information)
            var highest = messages[0];
            for (int i = 1; i < messages.Count; i++)
            {
                if ((byte)messages[i].Severity < (byte)highest.Severity)
                {
                    highest = messages[i];
                }
            }

            ErrorIconType iconType = highest.Severity switch
            {
                ValidationSeverity.Warning => ErrorIconType.Warning,
                ValidationSeverity.Information => ErrorIconType.Information,
                _ => ErrorIconType.Error
            };

            SetError(control, highest.Text, iconType);
        }

        /// <summary>
        /// Registers a validation rule for a specific control or editor.
        /// </summary>
        public void SetRule(Control control, IControlValidationRule rule, ValidationTrigger trigger = ValidationTrigger.ValueChanged)
        {
            if (control == null || rule == null) return;

            if (!_rules.TryGetValue(control, out var binding))
            {
                binding = new ControlRuleBinding { Trigger = trigger };
                _rules[control] = binding;

                if (control is IZeroEditor editor)
                {
                    editor.EditValueChanged += (s, e) =>
                    {
                        if (binding.Trigger == ValidationTrigger.ValueChanged)
                        {
                            Validate(control);
                        }
                    };
                }
                else
                {
                    control.TextChanged += (s, e) =>
                    {
                        if (binding.Trigger == ValidationTrigger.ValueChanged)
                        {
                            Validate(control);
                        }
                    };
                }

                control.Leave += (s, e) =>
                {
                    if (binding.Trigger == ValidationTrigger.FocusLost)
                    {
                        Validate(control);
                    }
                };
            }

            binding.Trigger = trigger;
            if (!binding.Rules.Contains(rule))
            {
                binding.Rules.Add(rule);
            }
        }

        /// <summary>
        /// Validates a specific control against its registered rules.
        /// </summary>
        public bool Validate(Control control)
        {
            if (control == null) return true;

            if (!_rules.TryGetValue(control, out var binding) || binding.Rules.Count == 0)
            {
                SetError(control, null);
                return true;
            }

            object? val = control is IZeroEditor editor ? editor.EditValue : control.Text;
            foreach (var rule in binding.Rules)
            {
                var res = rule.Validate(val);
                if (!res.IsValid)
                {
                    SetResult(control, res);
                    return false;
                }
            }

            SetError(control, null);
            return true;
        }

        /// <summary>
        /// Evaluates validation rules for all registered controls across the form.
        /// Returns true if all controls are valid; false if any validation failed.
        /// </summary>
        public bool Validate()
        {
            bool allValid = true;
            foreach (var control in _rules.Keys)
            {
                if (!Validate(control))
                {
                    allValid = false;
                }
            }
            return allValid;
        }

        /// <summary>
        /// Retrieves the first control currently displaying an error.
        /// </summary>
        public Control? GetFirstInvalidControl()
        {
            foreach (var kvp in _entries)
            {
                if (!string.IsNullOrEmpty(kvp.Value.Message) && kvp.Key.CanFocus)
                {
                    return kvp.Key;
                }
            }
            return null;
        }

        /// <summary>
        /// Automatically shifts keyboard input focus to the first invalid control.
        /// </summary>
        public bool FocusFirstInvalidControl()
        {
            var ctl = GetFirstInvalidControl();
            if (ctl != null)
            {
                return ctl.Focus();
            }
            return false;
        }

        /// <summary>
        /// Clears all error states and hides all badges.
        /// </summary>
        public void ClearErrors()
        {
            var controls = new List<Control>(_entries.Keys);
            foreach (var ctl in controls)
            {
                SetError(ctl, null);
            }
        }

        [DefaultValue(ErrorIconAlignment.MiddleRight)]
        [DisplayName("IconAlignment")]
        [Category("Validation")]
        public ErrorIconAlignment GetIconAlignment(Control control)
        {
            if (control != null && _entries.TryGetValue(control, out var entry))
            {
                return entry.Alignment;
            }
            return _defaultAlignment;
        }

        public void SetIconAlignment(Control control, ErrorIconAlignment value)
        {
            if (control != null && _entries.TryGetValue(control, out var entry))
            {
                entry.Alignment = value;
                entry.Reposition();
            }
        }

        [DefaultValue(4)]
        [DisplayName("IconPadding")]
        [Category("Validation")]
        public int GetIconPadding(Control control)
        {
            if (control != null && _entries.TryGetValue(control, out var entry))
            {
                return entry.Padding;
            }
            return _defaultPadding;
        }

        public void SetIconPadding(Control control, int value)
        {
            if (control != null && _entries.TryGetValue(control, out var entry))
            {
                entry.Padding = Math.Max(0, value);
                entry.Reposition();
            }
        }

        public void Clear()
        {
            foreach (var entry in _entries.Values)
            {
                entry.Dispose();
            }
            _entries.Clear();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Clear();
                _toolTip.Dispose();
            }
            base.Dispose(disposing);
        }

        // --- INTERNAL HELPER CLASSES ---

        private sealed class ErrorEntry : IDisposable
        {
            private readonly Control _target;
            private readonly ToolTip _toolTip;
            private ErrorBadgeControl? _badge;

            public string Message { get; private set; }
            public ErrorIconType IconType { get; private set; }
            public ErrorIconAlignment Alignment { get; set; }
            public int Padding { get; set; }

            public ErrorEntry(Control target, string message, ErrorIconType iconType, ErrorIconAlignment alignment, int padding, ToolTip toolTip)
            {
                _target = target;
                _toolTip = toolTip;
                Message = message;
                IconType = iconType;
                Alignment = alignment;
                Padding = padding;

                CreateBadge();
                HookTarget();
                Reposition();
            }

            public void Update(string message, ErrorIconType iconType)
            {
                Message = message;
                IconType = iconType;

                if (_badge != null)
                {
                    _badge.IconType = iconType;
                    _badge.Invalidate();
                    _toolTip.SetToolTip(_badge, Message);
                    _badge.Visible = _target.Visible && !string.IsNullOrEmpty(Message);
                }
                Reposition();
            }

            private void CreateBadge()
            {
                _badge = new ErrorBadgeControl(IconType)
                {
                    Size = new Size(16, 16),
                    Cursor = Cursors.Help
                };

                _toolTip.SetToolTip(_badge, Message);

                if (_target.Parent != null)
                {
                    _target.Parent.Controls.Add(_badge);
                    _badge.BringToFront();
                }
            }

            private void HookTarget()
            {
                _target.LocationChanged += OnTargetLayoutChanged;
                _target.SizeChanged += OnTargetLayoutChanged;
                _target.VisibleChanged += OnTargetVisibleChanged;
                _target.ParentChanged += OnTargetParentChanged;
            }

            private void UnhookTarget()
            {
                _target.LocationChanged -= OnTargetLayoutChanged;
                _target.SizeChanged -= OnTargetLayoutChanged;
                _target.VisibleChanged -= OnTargetVisibleChanged;
                _target.ParentChanged -= OnTargetParentChanged;
            }

            private void OnTargetLayoutChanged(object? sender, EventArgs e) => Reposition();

            private void OnTargetVisibleChanged(object? sender, EventArgs e)
            {
                if (_badge != null)
                {
                    _badge.Visible = _target.Visible && !string.IsNullOrEmpty(Message);
                }
            }

            private void OnTargetParentChanged(object? sender, EventArgs e)
            {
                if (_badge != null && _target.Parent != null)
                {
                    _target.Parent.Controls.Add(_badge);
                    _badge.BringToFront();
                    Reposition();
                }
            }

            public void Reposition()
            {
                if (_badge == null || _target.Parent == null) return;

                int bw = _badge.Width;
                int bh = _badge.Height;

                int x = _target.Right + Padding;
                int y = _target.Top + (_target.Height - bh) / 2;

                switch (Alignment)
                {
                    case ErrorIconAlignment.TopLeft:
                        x = _target.Left - bw - Padding;
                        y = _target.Top;
                        break;
                    case ErrorIconAlignment.MiddleLeft:
                        x = _target.Left - bw - Padding;
                        y = _target.Top + (_target.Height - bh) / 2;
                        break;
                    case ErrorIconAlignment.BottomLeft:
                        x = _target.Left - bw - Padding;
                        y = _target.Bottom - bh;
                        break;
                    default:
                        x = _target.Right + Padding;
                        y = _target.Top + (_target.Height - bh) / 2;
                        break;
                }

                _badge.Location = new Point(x, y);
                _badge.Visible = _target.Visible && !string.IsNullOrEmpty(Message);
            }

            public void Dispose()
            {
                UnhookTarget();
                if (_badge != null)
                {
                    _toolTip.SetToolTip(_badge, null);
                    _badge.Parent?.Controls.Remove(_badge);
                    _badge.Dispose();
                    _badge = null;
                }
            }
        }

        private sealed class ErrorBadgeControl : Control
        {
            public ErrorIconType IconType { get; set; }

            public ErrorBadgeControl(ErrorIconType iconType)
            {
                IconType = iconType;
                SetStyle(
                    ControlStyles.UserPaint |
                    ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer |
                    ControlStyles.ResizeRedraw |
                    ControlStyles.SupportsTransparentBackColor, true);

                BackColor = Color.Transparent;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                Color badgeColor = IconType switch
                {
                    ErrorIconType.Warning => Color.FromArgb(245, 158, 11),   // Amber 500
                    ErrorIconType.Information => Color.FromArgb(59, 130, 246), // Blue 500
                    _ => Color.FromArgb(239, 68, 68)                         // Red 500
                };

                int sz = Math.Min(Width, Height) - 2;
                var circleRect = new Rectangle(1, 1, sz, sz);

                using (var brush = new SolidBrush(badgeColor))
                {
                    g.FillEllipse(brush, circleRect);
                }

                using (var pen = new Pen(Color.White, 1.6f))
                {
                    if (IconType == ErrorIconType.Error)
                    {
                        // Exclamation mark: vertical line + dot
                        int cx = circleRect.Left + circleRect.Width / 2;
                        int topY = circleRect.Top + 4;
                        int bottomY = circleRect.Top + circleRect.Height - 7;
                        g.DrawLine(pen, cx, topY, cx, bottomY);

                        using var dotBrush = new SolidBrush(Color.White);
                        g.FillEllipse(dotBrush, cx - 1.1f, circleRect.Bottom - 5, 2.2f, 2.2f);
                    }
                    else if (IconType == ErrorIconType.Warning)
                    {
                        // Exclamation mark for warning
                        int cx = circleRect.Left + circleRect.Width / 2;
                        int topY = circleRect.Top + 4;
                        int bottomY = circleRect.Top + circleRect.Height - 7;
                        g.DrawLine(pen, cx, topY, cx, bottomY);

                        using var dotBrush = new SolidBrush(Color.White);
                        g.FillEllipse(dotBrush, cx - 1.1f, circleRect.Bottom - 5, 2.2f, 2.2f);
                    }
                    else
                    {
                        // 'i' glyph: dot on top + line below
                        int cx = circleRect.Left + circleRect.Width / 2;
                        using var dotBrush = new SolidBrush(Color.White);
                        g.FillEllipse(dotBrush, cx - 1.1f, circleRect.Top + 4, 2.2f, 2.2f);

                        int lineTop = circleRect.Top + 8;
                        int lineBottom = circleRect.Bottom - 4;
                        g.DrawLine(pen, cx, lineTop, cx, lineBottom);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Backward-compatibility alias for <see cref="ValidationProvider"/>.
    /// </summary>
    [Obsolete("ZeroErrorProvider is deprecated. Please use ValidationProvider instead.")]
    [ToolboxItem(false)]
    public class ZeroErrorProvider : ValidationProvider
    {
        public ZeroErrorProvider() { }
        public ZeroErrorProvider(IContainer container) : base(container) { }
    }

    /// <summary>
    /// Backward-compatibility alias for <see cref="ValidationProvider"/>.
    /// </summary>
    [Obsolete("ZeroValidationProvider is deprecated. Please use ValidationProvider instead.")]
    [ToolboxItem(false)]
    public class ZeroValidationProvider : ValidationProvider
    {
        public ZeroValidationProvider() { }
        public ZeroValidationProvider(IContainer container) : base(container) { }
    }
}
