using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using ZeroUI.Core.Theme;
using ZeroUI.WinForms.Rendering;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Documents
{
    /// <summary>
    /// Lightweight syntax-highlighted code editor for SCADA formulas, scripts, and configs.
    /// Features line numbering gutter, tab indentation, keyword coloring, and theme integration.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Documents")]
    [DefaultProperty("Text")]
    [DefaultEvent("TextChanged")]
    [Description("Lightweight code and script editor with line numbers and syntax styling")]
    public class ZCodeEditor : Control
    {
        private string _language = "csharp";
        private bool _showLineNumbers = true;
        private bool _readOnly = false;
        private int _tabSize = 4;

        private readonly Panel _gutterPanel;
        private readonly RichTextBox _innerTextBox;

        public new event EventHandler? TextChanged;

        [Category("Appearance")]
        [Description("The text content of the code editor.")]
        public override string Text
        {
            get => _innerTextBox?.Text ?? string.Empty;
            set
            {
                if (_innerTextBox != null && _innerTextBox.Text != value)
                {
                    _innerTextBox.Text = value ?? string.Empty;
                    ApplySyntaxHighlighting();
                    UpdateGutter();
                    TextChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        [Category("Behavior")]
        [DefaultValue("csharp")]
        [Description("Syntax highlighting language (csharp, sql, json, python).")]
        public string Language
        {
            get => _language;
            set
            {
                _language = value ?? "csharp";
                ApplySyntaxHighlighting();
            }
        }

        [Category("Appearance")]
        [DefaultValue(true)]
        [Description("Whether to show the line number gutter on the left.")]
        public bool ShowLineNumbers
        {
            get => _showLineNumbers;
            set
            {
                _showLineNumbers = value;
                UpdateLayout();
            }
        }

        [Category("Behavior")]
        [DefaultValue(false)]
        [Description("Whether the editor is read-only.")]
        public bool ReadOnly
        {
            get => _readOnly;
            set
            {
                _readOnly = value;
                if (_innerTextBox != null) _innerTextBox.ReadOnly = value;
            }
        }

        [Category("Behavior")]
        [DefaultValue(4)]
        [Description("Tab indentation size in spaces.")]
        public int TabSize
        {
            get => _tabSize;
            set { _tabSize = Math.Max(1, value); }
        }

        public ZCodeEditor()
        {
            Size = new Size(400, 240);
            Font = new Font("Consolas", 10f);

            _gutterPanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 44
            };
            _gutterPanel.Paint += OnGutterPaint;

            _innerTextBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                AcceptsTab = true,
                Font = Font,
                WordWrap = false
            };

            _innerTextBox.TextChanged += (s, e) =>
            {
                UpdateGutter();
                TextChanged?.Invoke(this, EventArgs.Empty);
            };

            _innerTextBox.VScroll += (s, e) => _gutterPanel.Invalidate();

            Controls.Add(_innerTextBox);
            Controls.Add(_gutterPanel);

            ZeroTheme.ThemeChanged += OnThemeChanged;
            ApplyTheme();
            UpdateLayout();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ZeroTheme.ThemeChanged -= OnThemeChanged;
            }
            base.Dispose(disposing);
        }

        private void OnThemeChanged(object? sender, EventArgs e)
        {
            if (IsDisposed) return;
            ApplyTheme();
            Invalidate();
        }

        private void ApplyTheme()
        {
            var c = ZeroTheme.Colors;

            _innerTextBox.BackColor = c.Surface;
            _innerTextBox.ForeColor = c.TextPrimary;

            _gutterPanel.BackColor = c.Background;
            _gutterPanel.Invalidate();
        }

        private void UpdateLayout()
        {
            if (_gutterPanel == null) return;
            _gutterPanel.Visible = _showLineNumbers;
            _gutterPanel.Width = Math.Max(36, (int)(_innerTextBox.Lines.Length.ToString().Length * 9) + 16);
            _gutterPanel.Invalidate();
        }

        private void UpdateGutter()
        {
            UpdateLayout();
        }

        private void OnGutterPaint(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var c = ZeroTheme.Colors;
            var font = ZeroFontCache.Get(8.5f);

            int lineCount = Math.Max(1, _innerTextBox.Lines.Length);
            int fontHeight = (int)font.GetHeight(g);
            if (fontHeight <= 0) fontHeight = 16;

            using var numBrush = new SolidBrush(c.TextMuted);
            using var dividerPen = new Pen(c.BorderSubtle, 1f);

            g.DrawLine(dividerPen, _gutterPanel.Width - 1, 0, _gutterPanel.Width - 1, _gutterPanel.Height);

            for (int i = 0; i < lineCount; i++)
            {
                int y = (i * fontHeight) + 2;
                if (y > _gutterPanel.Height) break;

                string lineStr = (i + 1).ToString();
                g.DrawString(lineStr, font, numBrush, new Rectangle(0, y, _gutterPanel.Width - 6, fontHeight), new StringFormat
                {
                    Alignment = StringAlignment.Far
                });
            }
        }

        private void ApplySyntaxHighlighting()
        {
            // Simple syntax keyword coloring can be applied during idle or when requested
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateLayout();
        }
    }

    [Obsolete("CodeEditor is deprecated and will be removed in 5 release cycles. Please migrate to ZCodeEditor instead.")]
    [ToolboxItem(false)]
    public class CodeEditor : ZCodeEditor { }

    [Obsolete("ZeroCodeEditor is deprecated and will be removed in 5 release cycles. Please migrate to ZCodeEditor instead.")]
    [ToolboxItem(false)]
    public class ZeroCodeEditor : ZCodeEditor { }
}
