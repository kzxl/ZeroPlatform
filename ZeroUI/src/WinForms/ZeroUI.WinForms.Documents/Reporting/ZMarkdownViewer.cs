using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using ZeroUI.Core.Theme;
using ZeroUI.WinForms.Rendering;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Documents
{
    public enum MarkdownBlockType
    {
        Heading1,
        Heading2,
        Heading3,
        Paragraph,
        CodeBlock,
        BulletItem,
        Blockquote,
        HorizontalRule
    }

    public class MarkdownBlock
    {
        public MarkdownBlockType Type { get; set; }
        public string Text { get; set; } = string.Empty;
        public List<string> Lines { get; } = new List<string>();
    }

    /// <summary>
    /// Interactive Markdown document renderer for WinForms supporting headings,
    /// fenced code blocks, bullet points, blockquotes, horizontal rules, and raw source toggle.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Documents")]
    [DefaultProperty("MarkdownText")]
    [DefaultEvent("LinkClicked")]
    [Description("Live Markdown preview and documentation renderer")]
    public class ZMarkdownViewer : Control
    {
        private string _markdownText = "# Welcome to ZeroUI Markdown\n\nZeroUI is a high-performance design system.\n\n- Zero allocations in hot paths\n- Unified tokens\n- SCADA ready\n\n```csharp\nvar grid = new ZGridControl();\n```\n\n> Clean, reliable architecture.";
        private bool _showSourceToggle = true;
        private bool _isRawView = false;
        private int _scrollY = 0;
        private int _totalContentHeight = 0;

        private readonly VScrollBar _vScrollBar;
        private readonly List<MarkdownBlock> _blocks = new List<MarkdownBlock>();
        private Rectangle _toggleButtonRect = Rectangle.Empty;
        private bool _isToggleHovered = false;

        public event EventHandler? LinkClicked;
        public event EventHandler? MarkdownChanged;

        [Browsable(false)]
        public IReadOnlyList<MarkdownBlock> Blocks => _blocks;

        [Category("Appearance")]
        [Description("The Markdown-formatted document text.")]
        public string MarkdownText
        {
            get => _markdownText;
            set
            {
                _markdownText = value ?? string.Empty;
                ParseMarkdown();
                Invalidate();
                MarkdownChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        [Category("Behavior")]
        [DefaultValue(false)]
        public bool IsRawView
        {
            get => _isRawView;
            set { _isRawView = value; Invalidate(); }
        }

        [Category("Behavior")]
        [DefaultValue(true)]
        [Description("Whether to show the Raw/Preview toggle button in the top right corner.")]
        public bool ShowSourceToggle
        {
            get => _showSourceToggle;
            set { _showSourceToggle = value; Invalidate(); }
        }

        public ZMarkdownViewer()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.Selectable |
                ControlStyles.SupportsTransparentBackColor, true);

            Size = new Size(480, 360);
            BackColor = Color.Transparent;

            _vScrollBar = new VScrollBar
            {
                Dock = DockStyle.Right,
                Visible = false
            };
            _vScrollBar.Scroll += (s, e) =>
            {
                _scrollY = _vScrollBar.Value;
                Invalidate();
            };
            Controls.Add(_vScrollBar);

            ZeroTheme.ThemeChanged += OnThemeChanged;
            ParseMarkdown();
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
            Invalidate();
        }

        private void ParseMarkdown()
        {
            _blocks.Clear();
            if (string.IsNullOrWhiteSpace(_markdownText)) return;

            using var reader = new StringReader(_markdownText);
            string? line;
            bool inCodeBlock = false;
            MarkdownBlock? currentCodeBlock = null;

            while ((line = reader.ReadLine()) != null)
            {
                string trimmed = line.Trim();

                if (trimmed.StartsWith("```"))
                {
                    if (inCodeBlock)
                    {
                        inCodeBlock = false;
                        currentCodeBlock = null;
                    }
                    else
                    {
                        inCodeBlock = true;
                        currentCodeBlock = new MarkdownBlock { Type = MarkdownBlockType.CodeBlock };
                        _blocks.Add(currentCodeBlock);
                    }
                    continue;
                }

                if (inCodeBlock && currentCodeBlock != null)
                {
                    currentCodeBlock.Lines.Add(line);
                    continue;
                }

                if (trimmed.StartsWith("# "))
                {
                    _blocks.Add(new MarkdownBlock { Type = MarkdownBlockType.Heading1, Text = trimmed.Substring(2) });
                }
                else if (trimmed.StartsWith("## "))
                {
                    _blocks.Add(new MarkdownBlock { Type = MarkdownBlockType.Heading2, Text = trimmed.Substring(3) });
                }
                else if (trimmed.StartsWith("### "))
                {
                    _blocks.Add(new MarkdownBlock { Type = MarkdownBlockType.Heading3, Text = trimmed.Substring(4) });
                }
                else if (trimmed.StartsWith("> "))
                {
                    _blocks.Add(new MarkdownBlock { Type = MarkdownBlockType.Blockquote, Text = trimmed.Substring(2) });
                }
                else if (trimmed.StartsWith("- ") || trimmed.StartsWith("* ") || trimmed.StartsWith("+ "))
                {
                    _blocks.Add(new MarkdownBlock { Type = MarkdownBlockType.BulletItem, Text = trimmed.Substring(2) });
                }
                else if (trimmed == "---" || trimmed == "***" || trimmed == "___")
                {
                    _blocks.Add(new MarkdownBlock { Type = MarkdownBlockType.HorizontalRule });
                }
                else if (!string.IsNullOrEmpty(trimmed))
                {
                    _blocks.Add(new MarkdownBlock { Type = MarkdownBlockType.Paragraph, Text = trimmed });
                }
            }

            UpdateScrollbar();
        }

        private void UpdateScrollbar()
        {
            if (_totalContentHeight > Height)
            {
                _vScrollBar.Visible = true;
                _vScrollBar.Maximum = Math.Max(0, _totalContentHeight - Height + 40);
                _vScrollBar.LargeChange = Math.Max(10, Height / 2);
            }
            else
            {
                _vScrollBar.Visible = false;
                _scrollY = 0;
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (_vScrollBar.Visible)
            {
                int newScroll = _scrollY - (e.Delta / 120) * 30;
                newScroll = Math.Max(0, Math.Min(_vScrollBar.Maximum, newScroll));
                _scrollY = newScroll;
                _vScrollBar.Value = _scrollY;
                Invalidate();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool wasHovered = _isToggleHovered;
            _isToggleHovered = _showSourceToggle && _toggleButtonRect.Contains(e.Location);
            if (wasHovered != _isToggleHovered) Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (_showSourceToggle && _toggleButtonRect.Contains(e.Location))
            {
                _isRawView = !_isRawView;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var bounds = ClientRectangle;
            var c = ZeroTheme.Colors;

            // Background
            using (var bgBrush = new SolidBrush(c.Surface))
            {
                g.FillRectangle(bgBrush, bounds);
            }

            // Border
            using (var borderPen = new Pen(c.BorderSubtle, 1f))
            {
                g.DrawRectangle(borderPen, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
            }

            int contentW = bounds.Width - (_vScrollBar.Visible ? _vScrollBar.Width : 0) - 32;
            int curY = 16 - _scrollY;

            // Toggle Raw/Preview button at top-right
            if (_showSourceToggle)
            {
                string toggleText = _isRawView ? "Preview" : "Raw";
                var btnFont = ZeroFontCache.Get(8f, FontStyle.Bold);
                var textSize = g.MeasureString(toggleText, btnFont);
                int btnW = (int)textSize.Width + 16;
                int btnH = 22;

                int btnRight = bounds.Right - (_vScrollBar.Visible ? _vScrollBar.Width : 0) - 12;
                _toggleButtonRect = new Rectangle(btnRight - btnW, 8, btnW, btnH);

                var btnBg = _isToggleHovered ? Color.FromArgb(40, c.Primary) : Color.FromArgb(20, c.Primary);
                using (var btnBrush = new SolidBrush(btnBg))
                using (var btnPen = new Pen(Color.FromArgb(100, c.Primary)))
                {
                    g.FillRectangle(btnBrush, _toggleButtonRect);
                    g.DrawRectangle(btnPen, _toggleButtonRect);
                }

                using var textBrush = new SolidBrush(c.Primary);
                g.DrawString(toggleText, btnFont, textBrush, _toggleButtonRect, new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                });
            }

            if (_isRawView)
            {
                var monoFont = ZeroFontCache.Get(9f);
                using var rawBrush = new SolidBrush(c.TextPrimary);
                var rawRect = new Rectangle(16, curY, contentW, bounds.Height * 4);
                g.DrawString(_markdownText, monoFont, rawBrush, rawRect);
                return;
            }

            // Render Markdown Blocks
            foreach (var block in _blocks)
            {
                switch (block.Type)
                {
                    case MarkdownBlockType.Heading1:
                    {
                        var font = ZeroFontCache.Get(16f, FontStyle.Bold);
                        using var brush = new SolidBrush(c.TextPrimary);
                        g.DrawString(block.Text, font, brush, 16, curY);
                        curY += 32;

                        // Underline divider for H1
                        using var linePen = new Pen(c.BorderSubtle, 1f);
                        g.DrawLine(linePen, 16, curY - 4, 16 + contentW, curY - 4);
                        curY += 6;
                        break;
                    }
                    case MarkdownBlockType.Heading2:
                    {
                        var font = ZeroFontCache.Get(13f, FontStyle.Bold);
                        using var brush = new SolidBrush(c.TextPrimary);
                        g.DrawString(block.Text, font, brush, 16, curY);
                        curY += 26;
                        break;
                    }
                    case MarkdownBlockType.Heading3:
                    {
                        var font = ZeroFontCache.Get(10.5f, FontStyle.Bold);
                        using var brush = new SolidBrush(c.TextPrimary);
                        g.DrawString(block.Text, font, brush, 16, curY);
                        curY += 22;
                        break;
                    }
                    case MarkdownBlockType.Paragraph:
                    {
                        var font = ZeroFontCache.Get(9f);
                        using var brush = new SolidBrush(c.TextPrimary);
                        var size = g.MeasureString(block.Text, font, contentW);
                        g.DrawString(block.Text, font, brush, new RectangleF(16, curY, contentW, size.Height));
                        curY += (int)size.Height + 10;
                        break;
                    }
                    case MarkdownBlockType.BulletItem:
                    {
                        var font = ZeroFontCache.Get(9f);
                        using var dotBrush = new SolidBrush(c.Primary);
                        g.FillEllipse(dotBrush, 22, curY + 6, 5, 5);

                        using var textBrush = new SolidBrush(c.TextPrimary);
                        var size = g.MeasureString(block.Text, font, contentW - 20);
                        g.DrawString(block.Text, font, textBrush, new RectangleF(34, curY, contentW - 20, size.Height));
                        curY += (int)size.Height + 6;
                        break;
                    }
                    case MarkdownBlockType.Blockquote:
                    {
                        var font = ZeroFontCache.Get(9f, FontStyle.Italic);
                        var size = g.MeasureString(block.Text, font, contentW - 24);
                        int quoteH = (int)size.Height + 8;

                        // Left border bar
                        using var barBrush = new SolidBrush(c.Primary);
                        g.FillRectangle(barBrush, 16, curY, 3, quoteH);

                        using var bg = new SolidBrush(Color.FromArgb(15, c.Primary));
                        g.FillRectangle(bg, 19, curY, contentW - 3, quoteH);

                        using var quoteTextBrush = new SolidBrush(c.TextSecondary);
                        g.DrawString(block.Text, font, quoteTextBrush, new RectangleF(26, curY + 4, contentW - 24, size.Height));
                        curY += quoteH + 10;
                        break;
                    }
                    case MarkdownBlockType.CodeBlock:
                    {
                        var font = ZeroFontCache.Get(8.5f);
                        int lineH = 18;
                        int blockH = (block.Lines.Count * lineH) + 16;
                        var blockRect = new Rectangle(16, curY, contentW, blockH);

                        using var codeBg = new SolidBrush(Color.FromArgb(20, c.TextPrimary));
                        using var codeBorder = new Pen(c.BorderSubtle, 1f);
                        g.FillRectangle(codeBg, blockRect);
                        g.DrawRectangle(codeBorder, blockRect);

                        using var codeBrush = new SolidBrush(c.TextPrimary);
                        int lineY = curY + 8;
                        foreach (var cl in block.Lines)
                        {
                            g.DrawString(cl, font, codeBrush, 24, lineY);
                            lineY += lineH;
                        }
                        curY += blockH + 12;
                        break;
                    }
                    case MarkdownBlockType.HorizontalRule:
                    {
                        using var linePen = new Pen(c.BorderSubtle, 1f);
                        g.DrawLine(linePen, 16, curY + 8, 16 + contentW, curY + 8);
                        curY += 18;
                        break;
                    }
                }
            }

            _totalContentHeight = curY + _scrollY + 20;
            UpdateScrollbar();
        }
    }

    [Obsolete("MarkdownViewer is deprecated and will be removed in 5 release cycles. Please migrate to ZMarkdownViewer instead.")]
    [ToolboxItem(false)]
    public class MarkdownViewer : ZMarkdownViewer { }

    [Obsolete("ZeroMarkdownViewer is deprecated and will be removed in 5 release cycles. Please migrate to ZMarkdownViewer instead.")]
    [ToolboxItem(false)]
    public class ZeroMarkdownViewer : ZMarkdownViewer { }
}
