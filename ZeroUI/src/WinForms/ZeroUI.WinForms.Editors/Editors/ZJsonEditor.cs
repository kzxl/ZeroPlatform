using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Editors
{
    /// <summary>
    /// Professional JSON editor with syntax formatting, minification, real-time validation, and line numbering.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Editors")]
    [Description("Full-featured JSON editor with line numbering, format, minify, and validation")]
    public class ZJsonEditor : Control
    {
        private readonly TextBox _textBox;
        private readonly Panel _gutter;
        private readonly Label _statusLabel;
        private readonly Panel _statusPanel;

        private bool _showLineNumbers = true;
        private int _indentSize = 2;
        private bool _isValid = true;
        private string? _errorMessage;

        public event EventHandler? JsonChanged;
        public event EventHandler? ValidationChanged;

        public ZJsonEditor()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Size = new Size(400, 300);

            // Gutter for line numbers
            _gutter = new LineNumberGutter(this)
            {
                Dock = DockStyle.Left,
                Width = 38,
                Visible = _showLineNumbers
            };

            // Status bar at bottom
            _statusPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 22
            };

            _statusLabel = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(6, 0, 0, 0),
                Font = new Font("Segoe UI", 8.25f),
                Text = "Valid JSON"
            };
            _statusPanel.Controls.Add(_statusLabel);

            // Editor TextBox
            _textBox = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                AcceptsReturn = true,
                AcceptsTab = true,
                WordWrap = false,
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 10f),
                BorderStyle = BorderStyle.None
            };

            _textBox.TextChanged += OnTextBoxTextChanged;

            Controls.Add(_textBox);
            Controls.Add(_gutter);
            Controls.Add(_statusPanel);

            ApplyTheme();
            ZeroTheme.ThemeChanged += OnThemeChanged;
        }

        [Category("Appearance")]
        [DefaultValue(true)]
        public bool ShowLineNumbers
        {
            get => _showLineNumbers;
            set
            {
                _showLineNumbers = value;
                _gutter.Visible = value;
            }
        }

        [Category("Behavior")]
        [DefaultValue(false)]
        public bool ReadOnly
        {
            get => _textBox.ReadOnly;
            set => _textBox.ReadOnly = value;
        }

        [Category("Behavior")]
        [DefaultValue(2)]
        public int IndentSize
        {
            get => _indentSize;
            set => _indentSize = Math.Max(1, Math.Min(8, value));
        }

        [Category("Data")]
        [DefaultValue("")]
        public string JsonText
        {
            get => _textBox.Text;
            set
            {
                if (_textBox.Text != value)
                {
                    _textBox.Text = value ?? string.Empty;
                }
            }
        }

        [Browsable(false)]
        public bool IsValid => _isValid;

        [Browsable(false)]
        public string? ErrorMessage => _errorMessage;

        [Browsable(false)]
        public int LineCount => _textBox.Lines.Length;

        public void FormatJson()
        {
            string formatted = JsonHelper.Prettify(_textBox.Text, _indentSize);
            _textBox.Text = formatted;
        }

        public void MinifyJson()
        {
            string minified = JsonHelper.Minify(_textBox.Text);
            _textBox.Text = minified;
        }

        public bool ValidateJson()
        {
            var (valid, err) = JsonHelper.Validate(_textBox.Text);
            bool changed = (_isValid != valid || _errorMessage != err);
            _isValid = valid;
            _errorMessage = err;

            UpdateStatusDisplay();

            if (changed)
            {
                ValidationChanged?.Invoke(this, EventArgs.Empty);
            }

            return _isValid;
        }

        private void OnTextBoxTextChanged(object? sender, EventArgs e)
        {
            ValidateJson();
            _gutter.Invalidate();
            JsonChanged?.Invoke(this, EventArgs.Empty);
        }

        private void UpdateStatusDisplay()
        {
            if (_isValid)
            {
                _statusLabel.Text = $"✔ Valid JSON  |  Lines: {LineCount}  |  Chars: {_textBox.TextLength}";
                _statusLabel.ForeColor = Color.FromArgb(16, 185, 129); // Emerald
            }
            else
            {
                _statusLabel.Text = $"✖ Invalid JSON: {_errorMessage}  |  Lines: {LineCount}";
                _statusLabel.ForeColor = Color.FromArgb(239, 68, 68); // Red
            }
        }

        private void ApplyTheme()
        {
            var palette = ZeroTheme.Colors;
            BackColor = palette.BgInput;
            ForeColor = palette.TextPrimary;

            _textBox.BackColor = palette.BgInput;
            _textBox.ForeColor = palette.TextPrimary;

            _gutter.BackColor = palette.BgCard;
            _gutter.ForeColor = palette.TextSecondary;

            _statusPanel.BackColor = palette.BgCard;
            UpdateStatusDisplay();

            Invalidate();
        }

        private void OnThemeChanged(object? sender, EventArgs e)
        {
            if (IsDisposed) return;
            ApplyTheme();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ZeroTheme.ThemeChanged -= OnThemeChanged;
            }
            base.Dispose(disposing);
        }

        private sealed class LineNumberGutter : Panel
        {
            private readonly ZJsonEditor _parent;

            public LineNumberGutter(ZJsonEditor parent)
            {
                _parent = parent;
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                var g = e.Graphics;
                var palette = ZeroTheme.Colors;

                using var brush = new SolidBrush(palette.BgCard);
                g.FillRectangle(brush, ClientRectangle);

                using var linePen = new Pen(palette.Border, 1f);
                g.DrawLine(linePen, Width - 1, 0, Width - 1, Height);

                int count = Math.Max(1, _parent.LineCount);
                int fontHeight = _parent._textBox.Font.Height;
                if (fontHeight <= 0) fontHeight = 16;

                using var textFont = new Font("Consolas", 8.5f);
                using var textBrush = new SolidBrush(palette.TextSecondary);

                int visibleLines = Math.Min(count, (Height / fontHeight) + 2);
                for (int i = 1; i <= visibleLines; i++)
                {
                    int y = (i - 1) * fontHeight + 3;
                    var rect = new Rectangle(0, y, Width - 6, fontHeight);
                    TextRenderer.DrawText(g, i.ToString(), textFont, rect, palette.TextSecondary,
                        TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
                }
            }
        }

        #region Internal JSON Helper

        internal static class JsonHelper
        {
            public static (bool isValid, string? error) Validate(string json)
            {
                if (string.IsNullOrWhiteSpace(json)) return (true, null);
                var stack = new Stack<char>();
                bool inString = false;
                bool isEscaped = false;
                var word = new StringBuilder();

                for (int i = 0; i < json.Length; i++)
                {
                    char c = json[i];
                    if (inString)
                    {
                        if (isEscaped) isEscaped = false;
                        else if (c == '\\') isEscaped = true;
                        else if (c == '"') inString = false;
                        continue;
                    }

                    if (char.IsLetter(c))
                    {
                        word.Append(c);
                        continue;
                    }

                    if (word.Length > 0)
                    {
                        string w = word.ToString();
                        word.Clear();
                        if (w != "true" && w != "false" && w != "null")
                        {
                            return (false, $"Invalid token '{w}' outside string");
                        }
                    }

                    if (c == '"')
                    {
                        inString = true;
                    }
                    else if (c == '{' || c == '[')
                    {
                        stack.Push(c);
                    }
                    else if (c == '}')
                    {
                        if (stack.Count == 0 || stack.Pop() != '{')
                            return (false, $"Mismatched '}}' at {i}");
                    }
                    else if (c == ']')
                    {
                        if (stack.Count == 0 || stack.Pop() != '[')
                            return (false, $"Mismatched ']' at {i}");
                    }
                }

                if (inString) return (false, "Unterminated string literal");
                if (word.Length > 0)
                {
                    string w = word.ToString();
                    if (w != "true" && w != "false" && w != "null")
                        return (false, $"Invalid token '{w}'");
                }
                if (stack.Count > 0) return (false, $"Unclosed '{stack.Peek()}'");
                return (true, null);
            }

            public static string Prettify(string json, int indentSize = 2)
            {
                if (string.IsNullOrWhiteSpace(json)) return json;
                var sb = new StringBuilder();
                int indent = 0;
                bool inString = false;
                bool isEscaped = false;
                string indentStr = new string(' ', Math.Max(1, indentSize));

                for (int i = 0; i < json.Length; i++)
                {
                    char c = json[i];
                    if (inString)
                    {
                        sb.Append(c);
                        if (isEscaped) isEscaped = false;
                        else if (c == '\\') isEscaped = true;
                        else if (c == '"') inString = false;
                        continue;
                    }

                    if (char.IsWhiteSpace(c)) continue;

                    if (c == '"')
                    {
                        inString = true;
                        sb.Append(c);
                    }
                    else if (c == '{' || c == '[')
                    {
                        sb.Append(c);
                        sb.AppendLine();
                        indent++;
                        for (int s = 0; s < indent; s++) sb.Append(indentStr);
                    }
                    else if (c == '}' || c == ']')
                    {
                        sb.AppendLine();
                        indent = Math.Max(0, indent - 1);
                        for (int s = 0; s < indent; s++) sb.Append(indentStr);
                        sb.Append(c);
                    }
                    else if (c == ',')
                    {
                        sb.Append(c);
                        sb.AppendLine();
                        for (int s = 0; s < indent; s++) sb.Append(indentStr);
                    }
                    else if (c == ':')
                    {
                        sb.Append(": ");
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }

                return sb.ToString();
            }

            public static string Minify(string json)
            {
                if (string.IsNullOrWhiteSpace(json)) return json;
                var sb = new StringBuilder();
                bool inString = false;
                bool isEscaped = false;

                for (int i = 0; i < json.Length; i++)
                {
                    char c = json[i];
                    if (inString)
                    {
                        sb.Append(c);
                        if (isEscaped) isEscaped = false;
                        else if (c == '\\') isEscaped = true;
                        else if (c == '"') inString = false;
                        continue;
                    }

                    if (char.IsWhiteSpace(c)) continue;

                    if (c == '"') inString = true;
                    sb.Append(c);
                }

                return sb.ToString();
            }
        }

        #endregion
    }

    #region Backward Compatibility Shims

    [Obsolete("JsonEditor is deprecated and will be removed in 5 release cycles. Please migrate to ZJsonEditor instead.")]
    [ToolboxItem(false)]
    public class JsonEditor : ZJsonEditor { }

    [Obsolete("ZeroJsonEditor is deprecated and will be removed in 5 release cycles. Please migrate to ZJsonEditor instead.")]
    [ToolboxItem(false)]
    public class ZeroJsonEditor : ZJsonEditor { }

    #endregion
}
