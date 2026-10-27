using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using ZeroUI.Core.Editors;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Editors
{
    /// <summary>
    /// Modern multi-tag token editor for WinForms supporting interactive badge chips,
    /// inline typing, comma/enter separation, backspace deletion, and dark theme styling.
    /// </summary>
    [DefaultEvent(nameof(TokensChanged))]
    public class ZTokenEdit : Control, IZeroEditor
    {
        private readonly TokenModel _model = new TokenModel();
        private readonly List<(Rectangle chipRect, Rectangle closeRect, TokenItem item)> _chipLayouts =
            new List<(Rectangle, Rectangle, TokenItem)>();
        private readonly TextBox _inputBox = new TextBox
        {
            BorderStyle = BorderStyle.None,
            Height = 20,
            Width = 70
        };
        private bool _readOnly;
        private bool _isModified;
        private bool _isHovered;
        private string _placeholder = "Add tag...";
        private IEnumerable<string>? _autocompleteSource;

        [Category("Appearance")]
        [DefaultValue("Add tag...")]
        public string Placeholder
        {
            get => _placeholder;
            set
            {
                _placeholder = value;
                Invalidate();
            }
        }

        [Category("Behavior")]
        [DefaultValue(null)]
        public IEnumerable<string>? AutocompleteSource
        {
            get => _autocompleteSource;
            set
            {
                _autocompleteSource = value;
                UpdateAutocomplete();
            }
        }

        [Category("Behavior")]
        [DefaultValue(false)]
        public bool AllowDuplicates
        {
            get => _model.AllowDuplicates;
            set => _model.AllowDuplicates = value;
        }

        [Category("Behavior")]
        [DefaultValue(0)]
        public int MaxTokens
        {
            get => _model.MaxTokens;
            set => _model.MaxTokens = value;
        }

        [Category("Behavior")]
        [DefaultValue(false)]
        public bool ReadOnly
        {
            get => _readOnly;
            set
            {
                _readOnly = value;
                if (_inputBox != null)
                {
                    _inputBox.ReadOnly = value;
                }
            }
        }

        [Browsable(false)]
        public bool IsModified
        {
            get => _isModified;
            set => _isModified = value;
        }

        [Browsable(false)]
        public IReadOnlyList<TokenItem> Tokens => _model.Tokens;

        [Browsable(false)]
        public object? EditValue
        {
            get => _model.ToDelimitedString();
            set
            {
                if (!_readOnly)
                {
                    _model.SetFromDelimitedString(value as string);
                }
            }
        }

        public event EventHandler<TokenItem>? TokenAdded;
        public event EventHandler<TokenItem>? TokenRemoved;
        public event EventHandler? TokensChanged;
        public event EventHandler? EditValueChanged;

        public ZTokenEdit()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);

            _inputBox.BackColor = ZeroTheme.Colors.BgInput;
            _inputBox.ForeColor = ZeroTheme.Colors.TextPrimary;
            _inputBox.Font = new Font("Segoe UI", 9f);
            _inputBox.KeyDown += OnInputKeyDown;
            _inputBox.TextChanged += OnInputTextChanged;
            _inputBox.GotFocus += (s, e) => Invalidate();
            _inputBox.LostFocus += (s, e) => Invalidate();
            Controls.Add(_inputBox);

            Height = 34;
            Width = 240;

            _model.TokenAdded += (s, item) =>
            {
                _isModified = true;
                UpdateLayoutPositions();
                TokenAdded?.Invoke(this, item);
                TokensChanged?.Invoke(this, EventArgs.Empty);
                EditValueChanged?.Invoke(this, EventArgs.Empty);
                Invalidate();
            };

            _model.TokenRemoved += (s, item) =>
            {
                _isModified = true;
                UpdateLayoutPositions();
                TokenRemoved?.Invoke(this, item);
                TokensChanged?.Invoke(this, EventArgs.Empty);
                EditValueChanged?.Invoke(this, EventArgs.Empty);
                Invalidate();
            };

            ZeroTheme.ThemeChanged += OnThemeChanged;

            UpdateLayoutPositions();
        }

        private void OnThemeChanged(object? sender, EventArgs e)
        {
            if (_inputBox != null)
            {
                _inputBox.BackColor = ZeroTheme.Colors.BgInput;
                _inputBox.ForeColor = ZeroTheme.Colors.TextPrimary;
            }
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ZeroTheme.ThemeChanged -= OnThemeChanged;
            }
            base.Dispose(disposing);
        }

        public void Reset()
        {
            ClearTokens();
            _isModified = false;
        }

        public void Clear()
        {
            ClearTokens();
        }

        public bool AddToken(string text)
        {
            if (_readOnly) return false;
            return _model.Add(text);
        }

        public bool AddToken(TokenItem token)
        {
            if (_readOnly) return false;
            return _model.Add(token);
        }

        public bool RemoveToken(TokenItem token)
        {
            if (_readOnly) return false;
            return _model.Remove(token);
        }

        public void ClearTokens()
        {
            if (_readOnly) return;
            _model.Clear();
            _isModified = true;
            UpdateLayoutPositions();
            TokensChanged?.Invoke(this, EventArgs.Empty);
            EditValueChanged?.Invoke(this, EventArgs.Empty);
            Invalidate();
        }

        private void OnInputKeyDown(object? sender, KeyEventArgs e)
        {
            if (_readOnly) return;

            if (e.KeyCode == Keys.Enter)
            {
                CommitInput();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Back && _inputBox.Text.Length == 0 && _model.Count > 0)
            {
                _model.RemoveLast();
                e.Handled = true;
            }
        }

        private void OnInputTextChanged(object? sender, EventArgs e)
        {
            if (_readOnly || _inputBox == null) return;

            string text = _inputBox.Text;
            if (text.Contains(",") || text.Contains(";"))
            {
                var parts = text.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts)
                {
                    AddToken(part.Trim());
                }
                _inputBox.Text = string.Empty;
            }
        }

        private void CommitInput()
        {
            if (_inputBox != null && !string.IsNullOrWhiteSpace(_inputBox.Text))
            {
                AddToken(_inputBox.Text.Trim());
                _inputBox.Text = string.Empty;
            }
        }

        private void UpdateAutocomplete()
        {
            if (_inputBox == null) return;

            if (_autocompleteSource == null)
            {
                _inputBox.AutoCompleteMode = AutoCompleteMode.None;
                _inputBox.AutoCompleteSource = AutoCompleteSource.None;
                _inputBox.AutoCompleteCustomSource = null;
            }
            else
            {
                var col = new AutoCompleteStringCollection();
                foreach (var item in _autocompleteSource)
                {
                    if (!string.IsNullOrEmpty(item))
                    {
                        col.Add(item);
                    }
                }
                _inputBox.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
                _inputBox.AutoCompleteSource = AutoCompleteSource.CustomSource;
                _inputBox.AutoCompleteCustomSource = col;
            }
        }

        private void UpdateLayoutPositions()
        {
            if (_inputBox == null) return;

            _chipLayouts.Clear();

            int x = 6;
            int y = 5;
            int rowHeight = 22;

            using var font = new Font("Segoe UI", 8.5f);

            foreach (var token in _model.Tokens)
            {
                var size = TextRenderer.MeasureText(token.Text, font);
                int chipWidth = size.Width + 24; // text + padding + 'x' button

                if (x + chipWidth > Width - 10 && x > 6)
                {
                    x = 6;
                    y += rowHeight + 4;
                }

                var chipRect = new Rectangle(x, y, chipWidth, rowHeight);
                var closeRect = new Rectangle(chipRect.Right - 16, y + 3, 14, 14);
                _chipLayouts.Add((chipRect, closeRect, token));

                x += chipWidth + 4;
            }

            int inputWidth = Math.Max(60, Width - x - 8);
            if (x + 60 > Width - 8 && x > 6)
            {
                x = 6;
                y += rowHeight + 4;
                inputWidth = Width - 16;
            }

            _inputBox.Location = new Point(x, y + 2);
            _inputBox.Width = inputWidth;

            int requiredHeight = Math.Max(34, y + rowHeight + 6);
            if (Height != requiredHeight)
            {
                Height = requiredHeight;
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateLayoutPositions();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);

            // Container Background
            using (var bgBrush = new SolidBrush(ZeroTheme.Colors.BgInput))
            using (var path = CreateRoundedRectanglePath(bounds, 5))
            {
                g.FillPath(bgBrush, path);
            }

            // Container Border
            Color borderColor = _isHovered || (_inputBox != null && _inputBox.Focused) ? ZeroTheme.Colors.PrimaryAccent : ZeroTheme.Colors.BorderDefault;
            using (var borderPen = new Pen(borderColor, 1f))
            using (var path = CreateRoundedRectanglePath(bounds, 5))
            {
                g.DrawPath(borderPen, path);
            }

            // Draw Chips
            using var font = new Font("Segoe UI", 8.5f);
            using var textBrush = new SolidBrush(ZeroTheme.Colors.TextPrimary);
            using var closeBrush = new SolidBrush(ZeroTheme.Colors.TextSecondary);
            using var chipBgBrush = new SolidBrush(ZeroTheme.Colors.BgCard);
            using var chipBorderPen = new Pen(ZeroTheme.Colors.BorderDefault, 1f);

            foreach (var (chipRect, closeRect, item) in _chipLayouts)
            {
                using var chipPath = CreateRoundedRectanglePath(chipRect, 10);
                g.FillPath(chipBgBrush, chipPath);
                g.DrawPath(chipBorderPen, chipPath);

                g.DrawString(item.Text, font, textBrush, chipRect.X + 8, chipRect.Y + 2);

                // 'x' icon
                using var closeFont = new Font("Segoe UI", 7.5f, FontStyle.Bold);
                g.DrawString("×", closeFont, closeBrush, closeRect.X + 1, closeRect.Y - 1);
            }

            // Draw Placeholder
            if (_model.Count == 0 && (_inputBox == null || string.IsNullOrEmpty(_inputBox.Text)) && !string.IsNullOrEmpty(_placeholder) && (_inputBox == null || !_inputBox.Focused))
            {
                using var placeholderBrush = new SolidBrush(ZeroTheme.Colors.TextSecondary);
                g.DrawString(_placeholder, font, placeholderBrush, 8, 7);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (_readOnly) return;

            foreach (var (_, closeRect, item) in _chipLayouts)
            {
                if (closeRect.Contains(e.Location))
                {
                    RemoveToken(item);
                    return;
                }
            }

            _inputBox?.Focus();
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

        private static GraphicsPath CreateRoundedRectanglePath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            if (rect.Width <= 0 || rect.Height <= 0) return path;

            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZTokenEdit"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("TokenEdit is deprecated and will be removed in 5 release cycles. Please migrate to ZTokenEdit instead.")]
    [ToolboxItem(false)]
    public class TokenEdit : ZTokenEdit
    {
    }

    #endregion
}
