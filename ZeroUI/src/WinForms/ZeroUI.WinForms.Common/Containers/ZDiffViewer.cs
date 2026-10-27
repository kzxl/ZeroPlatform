using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Theme;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Containers
{
    public enum DiffViewMode
    {
        SideBySide,
        Inline
    }

    public enum DiffLineKind
    {
        Unchanged,
        Added,
        Deleted,
        Modified
    }

    public class DiffLine
    {
        public DiffLineKind Kind { get; set; } = DiffLineKind.Unchanged;
        public int? OldLineNumber { get; set; }
        public int? NewLineNumber { get; set; }
        public string Text { get; set; } = string.Empty;
    }

    /// <summary>
    /// Visual side-by-side and inline document / text diff viewer control.
    /// Provides line-by-line comparison, color-coded additions/deletions,
    /// synchronized scrolling, and change statistics for ERP audit trails and versioning.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Containers")]
    [DefaultProperty("DiffMode")]
    [Description("Visual side-by-side and inline diff viewer for audit trails and text comparison")]
    public class ZDiffViewer : ControlBase
    {
        private readonly Panel _headerPanel;
        private readonly Button _btnSideBySide;
        private readonly Button _btnInline;
        private readonly Label _lblStats;
        private readonly Button _btnCopy;
        private readonly Panel _viewportPanel;
        private readonly VScrollBar _vScrollBar;

        private DiffViewMode _diffMode = DiffViewMode.SideBySide;
        private string _oldText = string.Empty;
        private string _newText = string.Empty;

        private readonly List<DiffLine> _inlineLines = new List<DiffLine>();
        private readonly List<(DiffLine? Left, DiffLine? Right)> _sideBySideLines = new List<(DiffLine?, DiffLine?)>();

        private int _additionsCount = 0;
        private int _deletionsCount = 0;
        private int _lineHeight = 22;

        public ZDiffViewer()
        {
            Size = new Size(640, 420);
            DoubleBuffered = true;

            // 1. Top Header Toolbar
            _headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 40,
                Padding = new Padding(8, 6, 8, 6)
            };

            _btnSideBySide = new Button
            {
                Text = "Side-by-Side",
                FlatStyle = FlatStyle.Flat,
                Size = new Size(100, 28),
                Location = new Point(8, 6)
            };
            _btnSideBySide.FlatAppearance.BorderSize = 0;
            _btnSideBySide.Click += (s, e) => SetDiffMode(DiffViewMode.SideBySide);

            _btnInline = new Button
            {
                Text = "Inline",
                FlatStyle = FlatStyle.Flat,
                Size = new Size(80, 28),
                Location = new Point(112, 6)
            };
            _btnInline.FlatAppearance.BorderSize = 0;
            _btnInline.Click += (s, e) => SetDiffMode(DiffViewMode.Inline);

            _lblStats = new Label
            {
                AutoSize = true,
                Location = new Point(204, 11),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };

            _btnCopy = new Button
            {
                Text = "Copy New",
                FlatStyle = FlatStyle.Flat,
                Size = new Size(85, 28),
                Dock = DockStyle.Right
            };
            _btnCopy.Click += (s, e) =>
            {
                if (!string.IsNullOrEmpty(_newText))
                {
                    Clipboard.SetText(_newText);
                }
            };

            _headerPanel.Controls.Add(_btnSideBySide);
            _headerPanel.Controls.Add(_btnInline);
            _headerPanel.Controls.Add(_lblStats);
            _headerPanel.Controls.Add(_btnCopy);

            // 2. Viewport Panel (Initialized first to eliminate lambda capture warning)
            _viewportPanel = new DiffViewport(this)
            {
                Dock = DockStyle.Fill
            };

            // 3. ScrollBar
            _vScrollBar = new VScrollBar
            {
                Dock = DockStyle.Right,
                SmallChange = 1,
                LargeChange = 5
            };
            _vScrollBar.ValueChanged += (s, e) => _viewportPanel.Invalidate();

            Controls.Add(_viewportPanel);
            Controls.Add(_vScrollBar);
            Controls.Add(_headerPanel);

            // Set sample texts for design time
            SetTexts(
                "Document: PO-2026-001\r\nVendor: Global Polymer Co., Ltd.\r\nAmount: $15,000.00\r\nStatus: Pending Approval",
                "Document: PO-2026-001\r\nVendor: Global Polymer Co., Ltd.\r\nAmount: $18,500.00\r\nNotes: Added 10% VAT & Freight\r\nStatus: Approved"
            );

            ApplyThemeStyles();
        }

        #region Properties

        [Category("Appearance")]
        [DefaultValue(DiffViewMode.SideBySide)]
        public DiffViewMode DiffMode
        {
            get => _diffMode;
            set => SetDiffMode(value);
        }

        [Browsable(false)]
        public string OldText => _oldText;

        [Browsable(false)]
        public string NewText => _newText;

        [Browsable(false)]
        public int AdditionsCount => _additionsCount;

        [Browsable(false)]
        public int DeletionsCount => _deletionsCount;

        #endregion

        public void SetTexts(string oldText, string newText)
        {
            _oldText = oldText ?? string.Empty;
            _newText = newText ?? string.Empty;
            ComputeDiff();
            UpdateScrollBar();
            UpdateStatsUI();
            _viewportPanel.Invalidate();
        }

        private void SetDiffMode(DiffViewMode mode)
        {
            _diffMode = mode;
            UpdateScrollBar();
            ApplyThemeStyles();
            _viewportPanel.Invalidate();
        }

        private void ComputeDiff()
        {
            _inlineLines.Clear();
            _sideBySideLines.Clear();
            _additionsCount = 0;
            _deletionsCount = 0;

            string[] oldLines = _oldText.Replace("\r\n", "\n").Split('\n');
            string[] newLines = _newText.Replace("\r\n", "\n").Split('\n');

            int maxLines = Math.Max(oldLines.Length, newLines.Length);

            // Simple line-level comparison
            int oldIdx = 0;
            int newIdx = 0;

            while (oldIdx < oldLines.Length || newIdx < newLines.Length)
            {
                if (oldIdx < oldLines.Length && newIdx < newLines.Length)
                {
                    if (oldLines[oldIdx] == newLines[newIdx])
                    {
                        var line = new DiffLine
                        {
                            Kind = DiffLineKind.Unchanged,
                            OldLineNumber = oldIdx + 1,
                            NewLineNumber = newIdx + 1,
                            Text = oldLines[oldIdx]
                        };
                        _inlineLines.Add(line);
                        _sideBySideLines.Add((line, line));
                        oldIdx++;
                        newIdx++;
                    }
                    else
                    {
                        // Difference detected
                        var delLine = new DiffLine
                        {
                            Kind = DiffLineKind.Deleted,
                            OldLineNumber = oldIdx + 1,
                            Text = oldLines[oldIdx]
                        };
                        var addLine = new DiffLine
                        {
                            Kind = DiffLineKind.Added,
                            NewLineNumber = newIdx + 1,
                            Text = newLines[newIdx]
                        };

                        _inlineLines.Add(delLine);
                        _inlineLines.Add(addLine);
                        _sideBySideLines.Add((delLine, addLine));

                        _deletionsCount++;
                        _additionsCount++;
                        oldIdx++;
                        newIdx++;
                    }
                }
                else if (oldIdx < oldLines.Length)
                {
                    var delLine = new DiffLine
                    {
                        Kind = DiffLineKind.Deleted,
                        OldLineNumber = oldIdx + 1,
                        Text = oldLines[oldIdx]
                    };
                    _inlineLines.Add(delLine);
                    _sideBySideLines.Add((delLine, null));
                    _deletionsCount++;
                    oldIdx++;
                }
                else
                {
                    var addLine = new DiffLine
                    {
                        Kind = DiffLineKind.Added,
                        NewLineNumber = newIdx + 1,
                        Text = newLines[newIdx]
                    };
                    _inlineLines.Add(addLine);
                    _sideBySideLines.Add((null, addLine));
                    _additionsCount++;
                    newIdx++;
                }
            }
        }

        private void UpdateScrollBar()
        {
            int totalLines = (_diffMode == DiffViewMode.SideBySide) ? _sideBySideLines.Count : _inlineLines.Count;
            int visibleLines = Math.Max(1, _viewportPanel.Height / _lineHeight);

            if (totalLines > visibleLines)
            {
                _vScrollBar.Visible = true;
                _vScrollBar.Maximum = totalLines - visibleLines + _vScrollBar.LargeChange - 1;
            }
            else
            {
                _vScrollBar.Visible = false;
                _vScrollBar.Value = 0;
            }
        }

        private void UpdateStatsUI()
        {
            _lblStats.Text = $"+{_additionsCount} additions, -{_deletionsCount} deletions";
            _lblStats.ForeColor = CurrentPalette.TextSecondary;
        }

        protected override void OnThemeChanged(ZeroSkin skin)
        {
            base.OnThemeChanged(skin);
            ApplyThemeStyles();
        }

        private void ApplyThemeStyles()
        {
            var pal = CurrentPalette;
            _headerPanel.BackColor = pal.HeaderBackground;
            _btnCopy.BackColor = pal.Surface;
            _btnCopy.ForeColor = pal.TextPrimary;
            _btnCopy.FlatAppearance.BorderColor = pal.Border;

            if (_diffMode == DiffViewMode.SideBySide)
            {
                _btnSideBySide.BackColor = pal.Primary;
                _btnSideBySide.ForeColor = Color.White;
                _btnInline.BackColor = pal.Surface;
                _btnInline.ForeColor = pal.TextSecondary;
            }
            else
            {
                _btnInline.BackColor = pal.Primary;
                _btnInline.ForeColor = Color.White;
                _btnSideBySide.BackColor = pal.Surface;
                _btnSideBySide.ForeColor = pal.TextSecondary;
            }

            UpdateStatsUI();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateScrollBar();
        }

        internal void RenderViewport(Graphics g, Rectangle clientRect)
        {
            g.SmoothingMode = SmoothingMode.None;
            var pal = CurrentPalette;

            int scrollIndex = _vScrollBar.Visible ? _vScrollBar.Value : 0;
            int visibleCount = (clientRect.Height / _lineHeight) + 2;

            Color addedBg = Color.FromArgb(220, 252, 231);    // Light green
            Color deletedBg = Color.FromArgb(254, 226, 226);  // Light red
            Color addedFg = Color.FromArgb(22, 101, 52);
            Color deletedFg = Color.FromArgb(153, 27, 27);

            if (EffectiveSkin.IsDark)
            {
                addedBg = Color.FromArgb(6, 78, 59);
                deletedBg = Color.FromArgb(69, 26, 26);
                addedFg = Color.FromArgb(167, 243, 208);
                deletedFg = Color.FromArgb(254, 202, 202);
            }

            using (var lineFont = new Font("Consolas", 9.5f))
            using (var numFont = new Font("Consolas", 8.5f))
            {
                if (_diffMode == DiffViewMode.SideBySide)
                {
                    int halfW = clientRect.Width / 2;
                    int numW = 40;

                    // Draw center separator
                    using (var sepPen = new Pen(pal.Border, 1f))
                    {
                        g.DrawLine(sepPen, halfW, 0, halfW, clientRect.Height);
                    }

                    for (int i = 0; i < visibleCount; i++)
                    {
                        int dataIdx = scrollIndex + i;
                        if (dataIdx >= _sideBySideLines.Count) break;

                        int y = i * _lineHeight;
                        var (left, right) = _sideBySideLines[dataIdx];

                        // Left Column (Old)
                        if (left != null)
                        {
                            Color rowBg = left.Kind == DiffLineKind.Deleted ? deletedBg : pal.Surface;
                            Color rowFg = left.Kind == DiffLineKind.Deleted ? deletedFg : pal.TextPrimary;

                            using (var b = new SolidBrush(rowBg))
                            {
                                g.FillRectangle(b, 0, y, halfW, _lineHeight);
                            }

                            // Line number
                            string numStr = left.OldLineNumber.HasValue ? left.OldLineNumber.Value.ToString() : "";
                            TextRenderer.DrawText(g, numStr, numFont, new Rectangle(0, y, numW, _lineHeight), pal.TextSecondary,
                                TextFormatFlags.VerticalCenter | TextFormatFlags.Right);

                            // Text
                            TextRenderer.DrawText(g, left.Text, lineFont, new Rectangle(numW + 8, y, halfW - numW - 12, _lineHeight), rowFg,
                                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);
                        }

                        // Right Column (New)
                        if (right != null)
                        {
                            Color rowBg = right.Kind == DiffLineKind.Added ? addedBg : pal.Surface;
                            Color rowFg = right.Kind == DiffLineKind.Added ? addedFg : pal.TextPrimary;

                            using (var b = new SolidBrush(rowBg))
                            {
                                g.FillRectangle(b, halfW, y, clientRect.Width - halfW, _lineHeight);
                            }

                            // Line number
                            string numStr = right.NewLineNumber.HasValue ? right.NewLineNumber.Value.ToString() : "";
                            TextRenderer.DrawText(g, numStr, numFont, new Rectangle(halfW, y, numW, _lineHeight), pal.TextSecondary,
                                TextFormatFlags.VerticalCenter | TextFormatFlags.Right);

                            // Text
                            TextRenderer.DrawText(g, right.Text, lineFont, new Rectangle(halfW + numW + 8, y, clientRect.Width - halfW - numW - 12, _lineHeight), rowFg,
                                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);
                        }
                    }
                }
                else
                {
                    // Inline Mode
                    int numW = 40;
                    int tagW = 20;

                    for (int i = 0; i < visibleCount; i++)
                    {
                        int dataIdx = scrollIndex + i;
                        if (dataIdx >= _inlineLines.Count) break;

                        int y = i * _lineHeight;
                        var item = _inlineLines[dataIdx];

                        Color rowBg = item.Kind == DiffLineKind.Added ? addedBg
                            : item.Kind == DiffLineKind.Deleted ? deletedBg
                            : pal.Surface;

                        Color rowFg = item.Kind == DiffLineKind.Added ? addedFg
                            : item.Kind == DiffLineKind.Deleted ? deletedFg
                            : pal.TextPrimary;

                        using (var b = new SolidBrush(rowBg))
                        {
                            g.FillRectangle(b, 0, y, clientRect.Width, _lineHeight);
                        }

                        // Line numbers
                        string oldNum = item.OldLineNumber.HasValue ? item.OldLineNumber.Value.ToString() : "";
                        string newNum = item.NewLineNumber.HasValue ? item.NewLineNumber.Value.ToString() : "";

                        TextRenderer.DrawText(g, oldNum, numFont, new Rectangle(0, y, numW, _lineHeight), pal.TextSecondary,
                            TextFormatFlags.VerticalCenter | TextFormatFlags.Right);

                        TextRenderer.DrawText(g, newNum, numFont, new Rectangle(numW, y, numW, _lineHeight), pal.TextSecondary,
                            TextFormatFlags.VerticalCenter | TextFormatFlags.Right);

                        // Tag (+ / -)
                        string tag = item.Kind == DiffLineKind.Added ? "+" : item.Kind == DiffLineKind.Deleted ? "-" : " ";
                        TextRenderer.DrawText(g, tag, lineFont, new Rectangle(numW * 2 + 4, y, tagW, _lineHeight), rowFg,
                            TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);

                        // Content
                        TextRenderer.DrawText(g, item.Text, lineFont, new Rectangle(numW * 2 + tagW + 8, y, clientRect.Width - (numW * 2 + tagW + 12), _lineHeight), rowFg,
                            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);
                    }
                }
            }
        }

        private class DiffViewport : Panel
        {
            private readonly ZDiffViewer _parent;

            public DiffViewport(ZDiffViewer parent)
            {
                _parent = parent;
                DoubleBuffered = true;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                _parent.RenderViewport(e.Graphics, ClientRectangle);
            }
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZDiffViewer"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("DiffViewerControl is deprecated and will be removed in 5 release cycles. Please migrate to ZDiffViewer instead.")]
    [ToolboxItem(false)]
    public class DiffViewerControl : ZDiffViewer
    {
    }

    #endregion
}
