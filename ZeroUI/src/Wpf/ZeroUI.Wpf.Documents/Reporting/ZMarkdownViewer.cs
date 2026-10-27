using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Documents
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
    /// Interactive Markdown document renderer for WPF supporting headings,
    /// fenced code blocks, bullet points, blockquotes, horizontal rules, and raw source toggle.
    /// </summary>
    public class ZMarkdownViewer : Control
    {
        private string _markdownText = "# Welcome to ZeroUI Markdown\n\nZeroUI is a high-performance design system.\n\n- Zero allocations in hot paths\n- Unified tokens\n- SCADA ready\n\n```csharp\nvar grid = new ZGridControl();\n```\n\n> Clean, reliable architecture.";
        private bool _showSourceToggle = true;
        private bool _isRawView = false;
        private double _scrollY = 0;
        private double _totalContentHeight = 0;

        private readonly List<MarkdownBlock> _blocks = new List<MarkdownBlock>();
        private Rect _toggleButtonRect = Rect.Empty;
        private bool _isToggleHovered = false;

        public event EventHandler? LinkClicked;
        public event EventHandler? MarkdownChanged;

        public IReadOnlyList<MarkdownBlock> Blocks => _blocks;

        public string MarkdownText
        {
            get => _markdownText;
            set
            {
                _markdownText = value ?? string.Empty;
                ParseMarkdown();
                InvalidateVisual();
                MarkdownChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public bool IsRawView
        {
            get => _isRawView;
            set { _isRawView = value; InvalidateVisual(); }
        }

        public bool ShowSourceToggle
        {
            get => _showSourceToggle;
            set { _showSourceToggle = value; InvalidateVisual(); }
        }

        static ZMarkdownViewer()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ZMarkdownViewer), new FrameworkPropertyMetadata(typeof(ZMarkdownViewer)));
        }

        public ZMarkdownViewer()
        {
            Width = 480;
            Height = 360;
            Focusable = true;

            Loaded += (s, e) => ZeroWpfTheme.ThemeChanged += OnThemeChanged;
            Unloaded += (s, e) => ZeroWpfTheme.ThemeChanged -= OnThemeChanged;

            ParseMarkdown();
        }

        private void OnThemeChanged()
        {
            if (!Dispatcher.CheckAccess())
            {
                if (!Dispatcher.HasShutdownStarted && !Dispatcher.HasShutdownFinished)
                    Dispatcher.BeginInvoke((Action)OnThemeChanged);
                return;
            }
            InvalidateVisual();
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
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            double maxScroll = Math.Max(0, _totalContentHeight - ActualHeight + 40);
            if (maxScroll > 0)
            {
                _scrollY = Math.Max(0, Math.Min(maxScroll, _scrollY - (e.Delta / 120.0) * 30));
                InvalidateVisual();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var pos = e.GetPosition(this);
            bool wasHovered = _isToggleHovered;
            _isToggleHovered = _showSourceToggle && _toggleButtonRect.Contains(pos);
            Cursor = _isToggleHovered ? Cursors.Hand : Cursors.Arrow;

            if (wasHovered != _isToggleHovered) InvalidateVisual();
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            var pos = e.GetPosition(this);
            if (_showSourceToggle && _toggleButtonRect.Contains(pos))
            {
                _isRawView = !_isRawView;
                InvalidateVisual();
                e.Handled = true;
            }
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
            if (bounds.Width <= 0 || bounds.Height <= 0) return;

            // Background & border
            dc.DrawRectangle(ZeroWpfTheme.BgInput, ZeroWpfTheme.GridLinePen, bounds);

            double contentW = bounds.Width - 32;
            double curY = 16 - _scrollY;
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var regularTypeface = ZeroWpfTheme.RegularTypeface;
            var boldTypeface = ZeroWpfTheme.BoldTypeface;

            // Toggle button
            if (_showSourceToggle)
            {
                string toggleText = _isRawView ? "Preview" : "Raw";
                var btnFormatted = new FormattedText(
                    toggleText,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    boldTypeface,
                    11.0,
                    ZeroWpfTheme.PrimaryAccent,
                    dpi);

                double btnW = btnFormatted.Width + 16;
                double btnH = 22;
                _toggleButtonRect = new Rect(bounds.Right - btnW - 12, 8, btnW, btnH);

                var btnBg = _isToggleHovered ? new SolidColorBrush(Color.FromArgb(40, 59, 130, 246)) : new SolidColorBrush(Color.FromArgb(20, 59, 130, 246));
                dc.DrawRoundedRectangle(btnBg, ZeroWpfTheme.AccentPen, _toggleButtonRect, 3, 3);
                dc.DrawText(btnFormatted, new Point(_toggleButtonRect.X + 8, _toggleButtonRect.Y + (btnH - btnFormatted.Height) / 2));
            }

            if (_isRawView)
            {
                var rawFormatted = new FormattedText(
                    _markdownText,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    regularTypeface,
                    12.0,
                    ZeroWpfTheme.TextPrimary,
                    dpi)
                {
                    MaxTextWidth = contentW
                };
                dc.DrawText(rawFormatted, new Point(16, curY));
                return;
            }

            // Render Markdown Blocks
            foreach (var block in _blocks)
            {
                switch (block.Type)
                {
                    case MarkdownBlockType.Heading1:
                    {
                        var text = new FormattedText(
                            block.Text,
                            CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight,
                            boldTypeface,
                            20.0,
                            ZeroWpfTheme.TextPrimary,
                            dpi);

                        dc.DrawText(text, new Point(16, curY));
                        curY += text.Height + 6;

                        dc.DrawLine(ZeroWpfTheme.GridLinePen, new Point(16, curY), new Point(16 + contentW, curY));
                        curY += 10;
                        break;
                    }
                    case MarkdownBlockType.Heading2:
                    {
                        var text = new FormattedText(
                            block.Text,
                            CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight,
                            boldTypeface,
                            16.0,
                            ZeroWpfTheme.TextPrimary,
                            dpi);

                        dc.DrawText(text, new Point(16, curY));
                        curY += text.Height + 8;
                        break;
                    }
                    case MarkdownBlockType.Heading3:
                    {
                        var text = new FormattedText(
                            block.Text,
                            CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight,
                            boldTypeface,
                            13.5,
                            ZeroWpfTheme.TextPrimary,
                            dpi);

                        dc.DrawText(text, new Point(16, curY));
                        curY += text.Height + 6;
                        break;
                    }
                    case MarkdownBlockType.Paragraph:
                    {
                        var text = new FormattedText(
                            block.Text,
                            CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight,
                            regularTypeface,
                            12.0,
                            ZeroWpfTheme.TextPrimary,
                            dpi)
                        {
                            MaxTextWidth = contentW
                        };

                        dc.DrawText(text, new Point(16, curY));
                        curY += text.Height + 10;
                        break;
                    }
                    case MarkdownBlockType.BulletItem:
                    {
                        dc.DrawEllipse(ZeroWpfTheme.PrimaryAccent, null, new Point(24, curY + 8), 3, 3);

                        var text = new FormattedText(
                            block.Text,
                            CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight,
                            regularTypeface,
                            12.0,
                            ZeroWpfTheme.TextPrimary,
                            dpi)
                        {
                            MaxTextWidth = contentW - 20
                        };

                        dc.DrawText(text, new Point(34, curY));
                        curY += text.Height + 6;
                        break;
                    }
                    case MarkdownBlockType.Blockquote:
                    {
                        var text = new FormattedText(
                            block.Text,
                            CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight,
                            new Typeface(FontFamily, FontStyles.Italic, FontWeights.Normal, FontStretches.Normal),
                            12.0,
                            ZeroWpfTheme.TextSecondary,
                            dpi)
                        {
                            MaxTextWidth = contentW - 24
                        };

                        double quoteH = text.Height + 8;
                        dc.DrawRectangle(ZeroWpfTheme.PrimaryAccent, null, new Rect(16, curY, 3, quoteH));

                        var bgBrush = new SolidColorBrush(Color.FromArgb(15, 59, 130, 246));
                        dc.DrawRectangle(bgBrush, null, new Rect(19, curY, contentW - 3, quoteH));

                        dc.DrawText(text, new Point(26, curY + 4));
                        curY += quoteH + 10;
                        break;
                    }
                    case MarkdownBlockType.CodeBlock:
                    {
                        double lineH = 18;
                        double blockH = (block.Lines.Count * lineH) + 16;
                        var blockRect = new Rect(16, curY, contentW, blockH);

                        var codeBg = new SolidColorBrush(Color.FromArgb(20, 128, 128, 128));
                        dc.DrawRoundedRectangle(codeBg, ZeroWpfTheme.GridLinePen, blockRect, 4, 4);

                        double lineY = curY + 8;
                        foreach (var cl in block.Lines)
                        {
                            var lineText = new FormattedText(
                                cl,
                                CultureInfo.CurrentCulture,
                                FlowDirection.LeftToRight,
                                regularTypeface,
                                11.5,
                                ZeroWpfTheme.TextPrimary,
                                dpi);

                            dc.DrawText(lineText, new Point(24, lineY));
                            lineY += lineH;
                        }
                        curY += blockH + 12;
                        break;
                    }
                    case MarkdownBlockType.HorizontalRule:
                    {
                        dc.DrawLine(ZeroWpfTheme.GridLinePen, new Point(16, curY + 8), new Point(16 + contentW, curY + 8));
                        curY += 18;
                        break;
                    }
                }
            }

            _totalContentHeight = curY + _scrollY + 20;
        }
    }

    [Obsolete("MarkdownViewer is deprecated and will be removed in 5 release cycles. Please migrate to ZMarkdownViewer instead.")]
    public class MarkdownViewer : ZMarkdownViewer { }

    [Obsolete("ZeroMarkdownViewer is deprecated and will be removed in 5 release cycles. Please migrate to ZMarkdownViewer instead.")]
    public class ZeroMarkdownViewer : ZMarkdownViewer { }
}
