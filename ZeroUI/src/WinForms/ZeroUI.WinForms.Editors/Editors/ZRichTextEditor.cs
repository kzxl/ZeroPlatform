using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using ZeroUI.Core.Editors;
using ZeroUI.Core.Theme;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Editors
{
    /// <summary>
    /// Modern rich text editor for ZeroUI.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Editors")]
    [DefaultProperty("HtmlContent")]
    [DefaultEvent("ContentChanged")]
    public class ZRichTextEditor : ControlBase, IZeroEditor
    {
        private readonly Panel _toolbarPanel;
        private readonly RichTextBox _richTextBox;
        private bool _showToolbar = true;
        private RichTextFeatures _enabledFeatures = RichTextFeatures.All;

        /// <summary>
        /// Gets or sets the HTML content.
        /// </summary>
        [Category("Appearance")]
        [Browsable(true)]
        public string HtmlContent
        {
            get => ConvertRtfToHtml(_richTextBox.Rtf);
            set => _richTextBox.Rtf = ConvertHtmlToRtf(value ?? string.Empty);
        }

        /// <summary>
        /// Gets the plain text content.
        /// </summary>
        [Category("Appearance")]
        [Browsable(false)]
        public string PlainText => _richTextBox.Text;

        /// <summary>
        /// Gets or sets whether the toolbar is shown.
        /// </summary>
        [Category("Appearance")]
        [DefaultValue(true)]
        public bool ShowToolbar
        {
            get => _showToolbar;
            set
            {
                _showToolbar = value;
                _toolbarPanel.Visible = value;
                UpdateInnerBounds();
            }
        }

        /// <summary>
        /// Gets or sets enabled features.
        /// </summary>
        [Category("Behavior")]
        [DefaultValue(RichTextFeatures.All)]
        public RichTextFeatures EnabledFeatures
        {
            get => _enabledFeatures;
            set => _enabledFeatures = value;
        }

        /// <summary>
        /// Gets or sets whether the editor is read only.
        /// </summary>
        [Category("Behavior")]
        [DefaultValue(false)]
        public bool ReadOnly
        {
            get => _richTextBox.ReadOnly;
            set => _richTextBox.ReadOnly = value;
        }

        /// <summary>
        /// Gets or sets max length.
        /// </summary>
        [Category("Behavior")]
        [DefaultValue(0)]
        public int MaxLength
        {
            get => _richTextBox.MaxLength;
            set => _richTextBox.MaxLength = value;
        }

        /// <summary>
        /// Event fired when content changes.
        /// </summary>
        public event EventHandler? ContentChanged;

        /// <summary>
        /// Event fired when a link is clicked.
        /// </summary>
        public event EventHandler<ZeroUI.Core.Editors.LinkClickedEventArgs>? LinkClicked;

        /// <summary>
        /// Gets or sets the edit value.
        /// </summary>
        [Browsable(false)]
        public object? EditValue
        {
            get => HtmlContent;
            set => HtmlContent = value?.ToString() ?? string.Empty;
        }
        
        [Browsable(false)]
        public bool IsModified { get; set; }

        public event EventHandler? EditValueChanged;

        public void Reset()
        {
            HtmlContent = string.Empty;
            IsModified = false;
        }

        public void Clear() => Reset();

        /// <summary>
        /// Gets whether current selection is bold.
        /// </summary>
        [Browsable(false)]
        public bool IsBold => _richTextBox.SelectionFont?.Bold ?? false;

        /// <summary>
        /// Gets whether current selection is italic.
        /// </summary>
        [Browsable(false)]
        public bool IsItalic => _richTextBox.SelectionFont?.Italic ?? false;

        /// <summary>
        /// Gets whether current selection is underline.
        /// </summary>
        [Browsable(false)]
        public bool IsUnderline => _richTextBox.SelectionFont?.Underline ?? false;

        /// <summary>
        /// Initializes a new instance of the <see cref="ZRichTextEditor"/> class.
        /// </summary>
        public ZRichTextEditor()
        {
            BackColor = Color.Transparent;
            Font = ZeroUIConfig.DefaultFont ?? new Font("Segoe UI", 9.5f, FontStyle.Regular);

            _toolbarPanel = new Panel
            {
                Height = 32,
                Dock = DockStyle.Top
            };
            _toolbarPanel.Paint += ToolbarPanel_Paint;

            _richTextBox = new RichTextBox
            {
                BorderStyle = BorderStyle.None,
                ScrollBars = RichTextBoxScrollBars.Both,
                Font = Font,
                AcceptsTab = true,
                HideSelection = false
            };

            _richTextBox.TextChanged += (s, e) => 
            {
                IsModified = true;
                ContentChanged?.Invoke(this, EventArgs.Empty);
                EditValueChanged?.Invoke(this, EventArgs.Empty);
            };
            _richTextBox.LinkClicked += (s, e) => LinkClicked?.Invoke(this, new ZeroUI.Core.Editors.LinkClickedEventArgs(e.LinkText ?? string.Empty));

            Controls.Add(_richTextBox);
            Controls.Add(_toolbarPanel);

            Size = new Size(400, 300);

            ZeroUIConfig.FontChanged += (s, e) =>
            {
                Font = ZeroUIConfig.DefaultFont;
                _richTextBox.Font = ZeroUIConfig.DefaultFont;
            };

            UpdateTheme();
            UpdateInnerBounds();
        }

        /// <summary>
        /// Raises the Resize event.
        /// </summary>
        /// <param name="e">An EventArgs that contains the event data.</param>
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateInnerBounds();
        }

        private void UpdateInnerBounds()
        {
            int top = _showToolbar ? _toolbarPanel.Height : 0;
            _richTextBox.SetBounds(1, top + 1, Width - 2, Height - top - 2);
        }

        /// <summary>
        /// Called when the theme changes.
        /// </summary>
        /// <param name="skin">The new skin.</param>
        protected override void OnThemeChanged(ZeroSkin skin)
        {
            base.OnThemeChanged(skin);
            UpdateTheme();
        }

        private void UpdateTheme()
        {
            if (IsDisposed || Disposing) return;
            var p = CurrentPalette;
            if (_richTextBox != null && !_richTextBox.IsDisposed)
            {
                _richTextBox.BackColor = ReadOnly ? p.HeaderBackground : p.Surface;
                _richTextBox.ForeColor = Enabled ? p.TextPrimary : p.TextSecondary;
            }
            if (_toolbarPanel != null && !_toolbarPanel.IsDisposed)
            {
                _toolbarPanel.BackColor = p.HeaderBackground;
            }
            Invalidate();
        }

        private void ToolbarPanel_Paint(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(CurrentPalette.HeaderBackground);
            using var brush = new SolidBrush(CurrentPalette.TextPrimary);
            g.DrawString("B I U S | List | H1 H2 H3", Font, brush, 5, 8);
        }

        /// <summary>Toggles bold formatting.</summary>
        public void Bold() => ToggleFontStyle(FontStyle.Bold);

        /// <summary>Toggles italic formatting.</summary>
        public void Italic() => ToggleFontStyle(FontStyle.Italic);

        /// <summary>Toggles underline formatting.</summary>
        public void Underline() => ToggleFontStyle(FontStyle.Underline);

        /// <summary>Toggles strikethrough formatting.</summary>
        public void Strikethrough() => ToggleFontStyle(FontStyle.Strikeout);

        private void ToggleFontStyle(FontStyle style)
        {
            if (_richTextBox.SelectionFont != null)
            {
                var current = _richTextBox.SelectionFont.Style;
                var newStyle = current ^ style;
                _richTextBox.SelectionFont = new Font(_richTextBox.SelectionFont, newStyle);
            }
        }

        /// <summary>Inserts ordered list.</summary>
        public void InsertOrderedList() => _richTextBox.SelectionBullet = true;

        /// <summary>Inserts unordered list.</summary>
        public void InsertUnorderedList() => _richTextBox.SelectionBullet = true;

        /// <summary>Sets heading level.</summary>
        /// <param name="level">Heading level.</param>
        public void SetHeading(int level)
        {
            float size = level == 1 ? 24f : level == 2 ? 18f : level == 3 ? 14f : 9.5f;
            if (_richTextBox.SelectionFont != null)
            {
                _richTextBox.SelectionFont = new Font(_richTextBox.SelectionFont.FontFamily, size, FontStyle.Bold);
            }
        }

        /// <summary>Inserts link.</summary>
        /// <param name="url">The URL.</param>
        /// <param name="text">The display text.</param>
        public void InsertLink(string url, string? text = null)
        {
            _richTextBox.SelectedText = text ?? url;
        }

        /// <summary>Clears formatting.</summary>
        public void ClearFormatting()
        {
            _richTextBox.SelectionFont = Font;
            _richTextBox.SelectionBullet = false;
        }

        private string ConvertHtmlToRtf(string html)
        {
            if (string.IsNullOrEmpty(html)) return @"{\rtf1\ansi\ansicpg1252\deff0\nouicompat\deflang1033{\fonttbl{\f0\fnil\fcharset0 Segoe UI;}}\pard\f0\fs19\par}";
            string rtf = html;
            rtf = Regex.Replace(rtf, @"<b>(.*?)</b>", @"\b $1\b0 ", RegexOptions.IgnoreCase);
            rtf = Regex.Replace(rtf, @"<i>(.*?)</i>", @"\i $1\i0 ", RegexOptions.IgnoreCase);
            rtf = Regex.Replace(rtf, @"<u>(.*?)</u>", @"\ul $1\ulnone ", RegexOptions.IgnoreCase);
            rtf = Regex.Replace(rtf, @"<s>(.*?)</s>", @"\strike $1\strike0 ", RegexOptions.IgnoreCase);
            return @"{\rtf1\ansi\ansicpg1252\deff0\nouicompat\deflang1033{\fonttbl{\f0\fnil\fcharset0 Segoe UI;}}\pard\f0\fs19 " + rtf + @"\par}";
        }

        private string ConvertRtfToHtml(string rtf)
        {
            if (string.IsNullOrEmpty(rtf)) return "";
            string text = _richTextBox.Text;
            return "<div>" + System.Net.WebUtility.HtmlEncode(text) + "</div>";
        }
    }

    /// <summary>
    /// Backward compatibility shim.
    /// </summary>
    [Obsolete("Use ZRichTextEditor instead.")]
    [ToolboxItem(false)]
    public class ZeroRichTextEditor : ZRichTextEditor
    {
    }
}
